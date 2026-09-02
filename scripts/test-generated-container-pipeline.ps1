[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string]$ArtifactsRoot,

    [switch]$SkipBuild,

    [switch]$KeepArtifacts
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$fixturePath = [IO.Path]::Combine($repoRoot, "test_data", "patches", "generated-container-graph.template.json")
$outRoot = if ([string]::IsNullOrWhiteSpace($ArtifactsRoot)) {
    [IO.Path]::Combine($repoRoot, ".build")
}
else {
    [IO.Path]::GetFullPath($ArtifactsRoot)
}
$workRoot = [IO.Path]::Combine($outRoot, "generated-container-pipeline-tests-" + [Guid]::NewGuid().ToString("N"))
$cliPath = [IO.Path]::Combine($repoRoot, "src", "FrxEdit.Cli", "bin", $Configuration, "net8.0", "frxedit.dll")

function Assert-Condition([bool]$Condition, [string]$Message) {
    if (-not $Condition) {
        throw "Assertion failed: $Message"
    }
}

function Invoke-FrxEdit([string[]]$CommandArguments) {
    & dotnet $cliPath @CommandArguments
    if ($LASTEXITCODE -ne 0) {
        throw "frxedit failed with exit code ${LASTEXITCODE}: $($CommandArguments -join ' ')"
    }
}

function Invoke-FrxEditExpectFailure([string[]]$CommandArguments) {
    $previousErrorAction = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $commandOutput = @(& dotnet $cliPath @CommandArguments 2>&1)
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorAction
    }
    Assert-Condition ($exitCode -ne 0) "frxedit command must reject unsupported input: $($CommandArguments -join ' ')"
    return ($commandOutput -join [Environment]::NewLine)
}

function Write-JsonFile([string]$Path, [object]$Value) {
    $json = $Value | ConvertTo-Json -Depth 100
    [IO.File]::WriteAllText($Path, $json, [Text.UTF8Encoding]::new($false))
}

function Get-OptionalProperty([object]$Value, [string]$Name) {
    $property = $Value.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $null
    }

    return $property.Value
}

function Get-ControlProjection([object]$Raw) {
    return @($Raw.controls | ForEach-Object {
        $rawParent = Get-OptionalProperty $_ "parent"
        $parent = if ([string]::IsNullOrWhiteSpace($rawParent)) { "<root>" } else { $rawParent }
        "$($_.name)|$($_.type)|$parent|$($_.properties.tabIndex)"
    } | Sort-Object)
}

function Assert-Graph([object]$Patch, [object]$Raw, [object]$Storage) {
    $requested = @($Patch.add)
    $actual = @($Raw.controls)
    Assert-Condition ($actual.Count -eq $requested.Count) "observable control count must equal the requested count"
    Assert-Condition ($Raw.frxFormControl.formGroupCount -eq 2) "generated root formGroupCount must retain the requested value 2"

    foreach ($request in $requested) {
        $matches = @($actual | Where-Object name -EQ $request.name)
        Assert-Condition ($matches.Count -eq 1) "requested control '$($request.name)' must exist exactly once"
        $requestedParent = Get-OptionalProperty $request "parent"
        $observedParent = Get-OptionalProperty $matches[0] "parent"
        $expectedParent = if ([string]::IsNullOrWhiteSpace($requestedParent)) { $null } else { $requestedParent }
        $actualParent = if ([string]::IsNullOrWhiteSpace($observedParent)) { $null } else { $observedParent }
        Assert-Condition ($expectedParent -eq $actualParent) "'$($request.name)' must retain parent '$expectedParent'"
        Assert-Condition ($matches[0].type -eq $request.type) "'$($request.name)' must retain type '$($request.type)'"

        $requestedControlProperties = $Patch.properties.PSObject.Properties[$request.name].Value
        foreach ($propertyName in @("tabIndex", "visible", "caption", "value")) {
            $requestedValue = Get-OptionalProperty $requestedControlProperties $propertyName
            if ($null -ne $requestedValue) {
                $observedPropertyName = if ($request.type -eq "TabStrip" -and $propertyName -eq "value") { "listIndex" } else { $propertyName }
                $observedValue = Get-OptionalProperty $matches[0].properties $observedPropertyName
                Assert-Condition ($observedValue -eq $requestedValue) "'$($request.name)' must retain requested $propertyName '$requestedValue'"
            }
        }
    }

    Assert-Condition (-not ($actual.name -contains "TabsPage1")) "explicit Pages must suppress TabsPage1"
    Assert-Condition (-not ($actual.name -contains "TabsPage2")) "explicit Pages must suppress TabsPage2"

    $multiPage = $actual | Where-Object name -EQ "Tabs"
    $pages = @($actual | Where-Object { $_.type -eq "Page" -and $_.parent -eq "Tabs" } | Sort-Object { $_.properties.multiPagePageIndex })
    Assert-Condition ($pages.Count -eq 3) "Tabs must expose exactly three Pages"
    Assert-Condition ($multiPage.properties.multiPagePageCount -eq 3) "Tabs x metadata must report PageCount 3"
    Assert-Condition ($multiPage.properties.multiPageXStreamValidation -eq "exact") "Tabs x stream must consume exactly"
    Assert-Condition (($pages.name -join ",") -eq "PageA,PageB,PageC") "effective Pages must be PageA, PageB, PageC"
    Assert-Condition (($pages.properties.multiPagePageIndex -join ",") -eq "0,1,2") "effective Page indices must be 0,1,2"
    Assert-Condition (($pages.properties.multiPagePageId -join ",") -eq ($multiPage.properties.multiPagePageIds -join ",")) "x Page IDs must match the effective Page IDs"
    Assert-Condition (($multiPage.properties.tabNames -join ",") -eq "PageA,PageB,PageC") "generated TabStrip names must match effective Pages"
    Assert-Condition (($multiPage.properties.tabCaptions -join ",") -eq "PageA,PageB,PageC") "generated TabStrip captions must match effective Pages"
    Assert-Condition ($multiPage.properties.tabsAllocated -eq 3) "generated MultiPage TabsAllocated must equal its three inserted tabs"
    Assert-Condition ($multiPage.properties.tabData -eq 3) "generated MultiPage TabData must equal its three inserted tabs"
    Assert-Condition ($multiPage.properties.value -eq 2) "Tabs must preserve numeric MultiPage value 2"
    Assert-Condition ($multiPage.properties.listIndex -eq 2) "Tabs must project internal TabStrip listIndex 2"
    Assert-Condition (($multiPage.properties.style -eq 1) -and ($multiPage.properties.tabStyle -eq 1)) "Tabs must retain internal TabStrip style 1"

    foreach ($frameName in @("RootFrame", "PageAFrame")) {
        $frame = $actual | Where-Object name -EQ $frameName
        Assert-Condition ($frame.properties.formSpecialEffect -eq 3) "'$frameName' must retain Frame specialEffect 3"
    }

    $contractTabs = $actual | Where-Object name -EQ "ContractTabs"
    Assert-Condition ($contractTabs.properties.listIndex -eq 1) "ContractTabs must retain canonical value as listIndex 1"
    Assert-Condition ($contractTabs.properties.tabStyle -eq 1) "ContractTabs must retain canonical style as tabStyle 1"
    Assert-Condition (($contractTabs.properties.tabCaptions -join ",") -eq "First,Second") "ContractTabs must retain tab captions"
    Assert-Condition ($contractTabs.properties.tabsAllocated -eq 2) "ContractTabs TabsAllocated must equal its two inserted tabs"
    Assert-Condition ($contractTabs.properties.tabData -eq 2) "ContractTabs TabData must equal its two inserted tabs"

    $xValidation = $Raw.parserValidation.multiPageXStreamValidations.Tabs
    Assert-Condition ($xValidation.validation -eq "exact") "parser validation must report an exact Tabs x stream"
    Assert-Condition ($xValidation.pageCount -eq 3) "parser validation must report PageCount 3"
    Assert-Condition (($xValidation.pageIds -join ",") -eq ($pages.properties.multiPagePageId -join ",")) "parser x IDs must match effective Page IDs"

    $contractProperties = [ordered]@{
        ContractImage = [ordered]@{ autoSize = $true; borderStyle = 0; pictureSizeMode = 3; pictureAlignment = 4; pictureTiling = $true }
        ContractScroll = [ordered]@{ enabled = $true; mousePointer = 3; min = -5; max = 250; position = 42; smallChange = 7; largeChange = 21; orientation = 1; proportionalThumb = $true; delay = 75 }
        ContractSpin = [ordered]@{ enabled = $true; mousePointer = 2; min = 1; max = 9; position = 4; smallChange = 2; orientation = 0; delay = 65 }
    }
    foreach ($controlName in $contractProperties.Keys) {
        $control = $actual | Where-Object name -EQ $controlName
        Assert-Condition ($null -ne $control) "contract control '$controlName' must exist"
        foreach ($propertyName in $contractProperties[$controlName].Keys) {
            $observed = Get-OptionalProperty $control.properties $propertyName
            $expected = $contractProperties[$controlName][$propertyName]
            Assert-Condition ($observed -eq $expected) "'$controlName' must retain non-default $propertyName '$expected'"
        }
    }

    $defaultSite = $actual | Where-Object name -EQ "PageBText"
    Assert-Condition ($defaultSite.properties.siteBitFlags -eq "0x00000033") "an omitted leaf siteBitFlags value must use the effective 0x00000033 file default"
    Assert-Condition ($defaultSite.properties.streamed -eq $true) "a leaf control must be represented in its parent's object stream"
    Assert-Condition ($defaultSite.properties.siteAutoSize -eq $true) "the effective SITE_FLAG default must set siteAutoSize"
    Assert-Condition ($defaultSite.properties.preserveHeight -eq $false) "the effective SITE_FLAG default must clear preserveHeight"
    Assert-Condition ($defaultSite.properties.fitToParent -eq $false) "the effective SITE_FLAG default must clear fitToParent"
    Assert-Condition ($defaultSite.properties.selectChild -eq $false) "the effective SITE_FLAG default must clear selectChild"
    Assert-Condition ($defaultSite.properties.promoteControls -eq $false) "a leaf control must not promote children"

    $rawSite = $actual | Where-Object name -EQ "ContractList"
    Assert-Condition ($rawSite.properties.siteBitFlags -eq "0x80002313") "raw siteBitFlags must preserve unknown high bits while the named siteAutoSize overlay produces 0x80002313"
    Assert-Condition ($rawSite.properties.siteAutoSize -eq $false) "the named siteAutoSize overlay must clear the raw bit"
    Assert-Condition ($rawSite.properties.preserveHeight -eq $true) "raw siteBitFlags must retain preserveHeight"
    Assert-Condition ($rawSite.properties.fitToParent -eq $true) "raw siteBitFlags must retain fitToParent"
    Assert-Condition ($rawSite.properties.selectChild -eq $true) "raw siteBitFlags must retain selectChild"
    Assert-Condition (($rawSite.properties.streamed -eq $true) -and ($rawSite.properties.promoteControls -eq $false)) "raw siteBitFlags must retain the leaf storage topology"

    $containerSite = $actual | Where-Object name -EQ "RootFrame"
    Assert-Condition (($containerSite.properties.streamed -eq $false) -and ($containerSite.properties.promoteControls -eq $true)) "Frame streamed/promoteControls values must be derived from owned-storage topology"
    Assert-Condition (($rawSite.properties.groupId -eq 1) -and (($actual | Where-Object name -EQ "RootCheck").properties.groupId -eq 2)) "formGroupCount 2 must be backed by two requested Site group IDs"

    $streamPaths = @($Storage.streams.path)
    foreach ($containerName in @("Tabs", "PageA", "PageB", "PageC", "PageAFrame", "RootFrame")) {
        $container = $actual | Where-Object name -EQ $containerName
        $ownedPath = $container.properties.ownedStoragePath
        Assert-Condition (-not [string]::IsNullOrWhiteSpace($ownedPath)) "'$containerName' must own a generated storage"
        Assert-Condition ($streamPaths -contains "$ownedPath/f") "'$containerName' storage must contain f"
        Assert-Condition ($streamPaths -contains "$ownedPath/o") "'$containerName' storage must contain o"

        $children = @($actual | Where-Object { (Get-OptionalProperty $_ "parent") -eq $containerName })
        foreach ($child in $children) {
            Assert-Condition ($child.properties.storagePath -eq $ownedPath) "'$($child.name)' must be serialized in '$containerName' storage"
            Assert-Condition ($child.properties.streamPath -eq "$ownedPath/f") "'$($child.name)' must be reachable through '$ownedPath/f'"
        }

        $objectValidation = $Raw.parserValidation.objectStreamValidations.PSObject.Properties[$ownedPath].Value
        Assert-Condition ($objectValidation.validation -eq "exact") "'$containerName' o stream must consume exactly"

        if ($children.Count -gt 0) {
            Assert-Condition ($container.properties.formStreamDataValidation -eq "exact") "'$containerName' FormStreamData must parse exactly"
            Assert-Condition ($container.properties.formSiteDataBoundaryValidation -eq "exact") "'$containerName' FormSiteData must begin at the exact FormStreamData boundary"
            Assert-Condition ($container.properties.formSiteDataGapByteCount -eq 0) "'$containerName' must not contain a FormStreamData/FormSiteData gap"
            Assert-Condition ($container.properties.formStreamDataEndLocalOffset -eq $container.properties.formSiteDataStartLocalOffset) "'$containerName' FormStreamData end must equal FormSiteData start"
        }

        if ($container.type -in @("Frame", "MultiPage")) {
            Assert-Condition ($container.properties.formFontKind -eq "StdFont") "'$containerName' default font must use StdFont"
            Assert-Condition ($container.properties.formFontStreamByteCount -eq 33) "'$containerName' default GuidAndStdFont must be exactly 33 bytes"
        }
    }
}

New-Item -ItemType Directory -Force -Path $workRoot | Out-Null
$completed = $false
$transcriptStarted = $false
try {
    Start-Transcript -LiteralPath (Join-Path $workRoot "run.log") | Out-Null
    $transcriptStarted = $true

    if (-not $SkipBuild) {
        & dotnet build (Join-Path $repoRoot "FrxEdit.sln") -c $Configuration --no-restore
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet build failed with exit code $LASTEXITCODE"
        }
    }

    $parentFirstPatch = Get-Content -Raw -LiteralPath $fixturePath | ConvertFrom-Json
    $shuffledPatch = Get-Content -Raw -LiteralPath $fixturePath | ConvertFrom-Json
    foreach ($patch in @($parentFirstPatch, $shuffledPatch)) {
        $patch.properties.GraphForm | Add-Member -NotePropertyName formGroupCount -NotePropertyValue 2 -Force
        $patch.properties.RootCheck | Add-Member -NotePropertyName groupId -NotePropertyValue 2 -Force
        $patch.properties | Add-Member -NotePropertyName ContractList -NotePropertyValue ([pscustomobject][ordered]@{
            type = "ListBox"
            leftPt = 235
            topPt = 80
            widthPt = 140
            heightPt = 24
            tabIndex = 6
            groupId = 1
            siteBitFlags = "0x80002333"
            siteAutoSize = $false
        }) -Force
        $patch.add += [pscustomobject][ordered]@{ type = "ListBox"; name = "ContractList"; parent = "" }
    }
    $childFirstNames = @(
        "NestedLabel", "PageALabel", "PageAFrame", "PageBText", "PageCButton", "RootCheck",
        "PageA", "PageB", "PageC", "Tabs", "RootFrame", "ContractImage", "ContractScroll", "ContractSpin", "ContractTabs", "ContractList"
    )
    $shuffledPatch.add = @($childFirstNames | ForEach-Object {
        $name = $_
        $shuffledPatch.add | Where-Object name -EQ $name
    })
    $parentFirstPatchPath = Join-Path $workRoot "parent-first.template.json"
    $shuffledPatchPath = Join-Path $workRoot "shuffled.template.json"
    Write-JsonFile $parentFirstPatchPath $parentFirstPatch
    Write-JsonFile $shuffledPatchPath $shuffledPatch

    $rawOutputs = @{}
    foreach ($case in @(
        @{ Name = "parent-first"; PatchPath = $parentFirstPatchPath; Patch = $parentFirstPatch },
        @{ Name = "child-first"; PatchPath = $shuffledPatchPath; Patch = $shuffledPatch }
    )) {
        $caseRoot = Join-Path $workRoot $case.Name
        New-Item -ItemType Directory -Force -Path $caseRoot | Out-Null
        $formPath = Join-Path $caseRoot "GraphForm.frm"
        $rawPath = Join-Path $caseRoot "GraphForm.raw.json"
        $storagePath = Join-Path $caseRoot "GraphForm.storage.json"

        Invoke-FrxEdit @("create", $formPath, "--name", "GraphForm", "--patch", $case.PatchPath)
        Invoke-FrxEdit @("validate", $formPath, "--mode", "strict")
        Invoke-FrxEdit @("inspect", $formPath, "--mode", "strict", "--raw-out", $rawPath, "--out", (Join-Path $caseRoot "GraphForm.json"))
        Invoke-FrxEdit @("dump-storage", $formPath, "--out", $storagePath)

        $raw = Get-Content -Raw -LiteralPath $rawPath | ConvertFrom-Json
        $storage = Get-Content -Raw -LiteralPath $storagePath | ConvertFrom-Json
        Assert-Graph $case.Patch $raw $storage
        Assert-Condition ($raw.frxFormControl.formSiteDataBoundaryValidation -eq "exact") "generated root FormSiteData must begin at its exact FormStreamData boundary"
        Assert-Condition ($raw.frxFormControl.formSiteDataGapByteCount -eq 0) "generated root must not contain a FormStreamData/FormSiteData gap"
        $rawOutputs[$case.Name] = $raw
    }

    $orderDifference = @(Compare-Object (Get-ControlProjection $rawOutputs["parent-first"]) (Get-ControlProjection $rawOutputs["child-first"]))
    Assert-Condition ($orderDifference.Count -eq 0) "parent-first and child-first patches must produce the same semantic graph"

    $tabMutationRoot = Join-Path $workRoot "standalone-tabstrip-mutation"
    New-Item -ItemType Directory -Force -Path $tabMutationRoot | Out-Null
    $tabMutationPatchPath = Join-Path $tabMutationRoot "tabstrip.patch.json"
    $tabMutationFormPath = Join-Path $tabMutationRoot "GraphForm.tabstrip.frm"
    $tabMutationRawPath = Join-Path $tabMutationRoot "GraphForm.tabstrip.raw.json"
    $tabMutationReportPath = Join-Path $tabMutationRoot "GraphForm.tabstrip.report.json"
    Write-JsonFile $tabMutationPatchPath ([ordered]@{
        properties = [ordered]@{
            ContractTabs = [ordered]@{ value = 0; style = 2 }
        }
    })
    Invoke-FrxEdit @(
        "build",
        (Join-Path (Join-Path $workRoot "parent-first") "GraphForm.frm"),
        $tabMutationPatchPath,
        "--out",
        $tabMutationFormPath,
        "--mode",
        "strict",
        "--stream-mode",
        "full-patch",
        "--report-out",
        $tabMutationReportPath
    )
    Invoke-FrxEdit @("validate", $tabMutationFormPath, "--mode", "strict")
    Invoke-FrxEdit @("inspect", $tabMutationFormPath, "--mode", "strict", "--raw-out", $tabMutationRawPath, "--out", (Join-Path $tabMutationRoot "GraphForm.tabstrip.json"))
    $tabMutationRaw = Get-Content -Raw -LiteralPath $tabMutationRawPath | ConvertFrom-Json
    $tabMutationReport = Get-Content -Raw -LiteralPath $tabMutationReportPath | ConvertFrom-Json
    $mutatedTabs = $tabMutationRaw.controls | Where-Object name -EQ "ContractTabs"
    Assert-Condition ($mutatedTabs.properties.listIndex -eq 0) "an existing standalone TabStrip must apply canonical value 0"
    Assert-Condition ($mutatedTabs.properties.tabStyle -eq 2) "an existing standalone TabStrip must apply canonical style 2"
    Assert-Condition ($tabMutationReport.semanticMatch -eq $true) "the standalone TabStrip mutation report must match the strict reread"

    $siteMutationRoot = Join-Path $workRoot "site-flag-root-mutation"
    New-Item -ItemType Directory -Force -Path $siteMutationRoot | Out-Null
    $siteMutationPatchPath = Join-Path $siteMutationRoot "site-flags.patch.json"
    $siteMutationFormPath = Join-Path $siteMutationRoot "GraphForm.site-flags.frm"
    $siteMutationRawPath = Join-Path $siteMutationRoot "GraphForm.site-flags.raw.json"
    $siteMutationReportPath = Join-Path $siteMutationRoot "GraphForm.site-flags.report.json"
    Write-JsonFile $siteMutationPatchPath ([ordered]@{
        properties = [ordered]@{
            GraphForm = [ordered]@{ formGroupCount = 1 }
            RootCheck = [ordered]@{ groupId = 1 }
            ContractList = [ordered]@{
                siteBitFlags = "0x80002333"
                siteAutoSize = $false
            }
        }
    })
    Invoke-FrxEdit @(
        "build",
        (Join-Path (Join-Path $workRoot "parent-first") "GraphForm.frm"),
        $siteMutationPatchPath,
        "--out",
        $siteMutationFormPath,
        "--mode",
        "strict",
        "--stream-mode",
        "full-patch",
        "--report-out",
        $siteMutationReportPath
    )
    Invoke-FrxEdit @("validate", $siteMutationFormPath, "--mode", "strict")
    Invoke-FrxEdit @("inspect", $siteMutationFormPath, "--mode", "strict", "--raw-out", $siteMutationRawPath, "--out", (Join-Path $siteMutationRoot "GraphForm.site-flags.json"))
    $siteMutationRaw = Get-Content -Raw -LiteralPath $siteMutationRawPath | ConvertFrom-Json
    $siteMutationReport = Get-Content -Raw -LiteralPath $siteMutationReportPath | ConvertFrom-Json
    $mutatedSite = $siteMutationRaw.controls | Where-Object name -EQ "ContractList"
    Assert-Condition ($siteMutationRaw.frxFormControl.formGroupCount -eq 1) "an existing root must apply formGroupCount 1"
    Assert-Condition (($siteMutationRaw.controls | Where-Object name -EQ "RootCheck").properties.groupId -eq 1) "the group-count mutation must consolidate RootCheck into group 1"
    Assert-Condition ($mutatedSite.properties.siteBitFlags -eq "0x80002313") "an existing site must preserve unknown high bits in the raw word and overlay siteAutoSize"
    Assert-Condition ($mutatedSite.properties.siteAutoSize -eq $false) "an existing site must apply siteAutoSize false"
    Assert-Condition ($mutatedSite.properties.preserveHeight -eq $true) "an existing site must retain the raw preserveHeight bit"
    Assert-Condition ($mutatedSite.properties.fitToParent -eq $true) "an existing site must retain the raw fitToParent bit"
    Assert-Condition ($mutatedSite.properties.selectChild -eq $true) "an existing site must retain the raw selectChild bit"
    Assert-Condition (($mutatedSite.properties.streamed -eq $true) -and ($mutatedSite.properties.promoteControls -eq $false)) "an existing site's structural flags must remain topology-derived"
    Assert-Condition ($siteMutationReport.semanticMatch -eq $true) "the SITE_FLAG and group-count mutation report must match the strict reread"

    $unsupportedRoot = Join-Path $workRoot "unsupported-property-contract"
    New-Item -ItemType Directory -Force -Path $unsupportedRoot | Out-Null
    $unsupportedPatchPath = Join-Path $unsupportedRoot "unsupported.patch.json"
    $unsupportedPatch = [ordered]@{
        properties = [ordered]@{
            PageCButton = [ordered]@{ pictureSizeMode = 3 }
        }
    }
    Write-JsonFile $unsupportedPatchPath $unsupportedPatch
    $unsupportedMessage = Invoke-FrxEditExpectFailure @(
        "build",
        (Join-Path (Join-Path $workRoot "parent-first") "GraphForm.frm"),
        $unsupportedPatchPath,
        "--out",
        (Join-Path $unsupportedRoot "GraphForm.unsupported.frm"),
        "--mode",
        "strict",
        "--stream-mode",
        "full-patch"
    )
    Assert-Condition ($unsupportedMessage -match "not supported for CommandButton") "type-incompatible properties must fail with a clear contract error"

    $invalidTabStylePatchPath = Join-Path $unsupportedRoot "invalid-tab-style.patch.json"
    Write-JsonFile $invalidTabStylePatchPath ([ordered]@{
        properties = [ordered]@{ ContractTabs = [ordered]@{ style = 3 } }
    })
    $invalidTabStyleMessage = Invoke-FrxEditExpectFailure @(
        "build",
        (Join-Path (Join-Path $workRoot "parent-first") "GraphForm.frm"),
        $invalidTabStylePatchPath,
        "--out",
        (Join-Path $unsupportedRoot "GraphForm.invalid-tab-style.frm"),
        "--mode",
        "strict",
        "--stream-mode",
        "full-patch"
    )
    Assert-Condition ($invalidTabStyleMessage -match "between 0 and 2") "TabStrip style values outside the schema range must be rejected"

    $structuralSiteFlagsPatchPath = Join-Path $unsupportedRoot "structural-site-flags.patch.json"
    Write-JsonFile $structuralSiteFlagsPatchPath ([ordered]@{
        properties = [ordered]@{ ContractList = [ordered]@{ siteBitFlags = "0x00000023" } }
    })
    $structuralSiteFlagsMessage = Invoke-FrxEditExpectFailure @(
        "build",
        (Join-Path (Join-Path $workRoot "parent-first") "GraphForm.frm"),
        $structuralSiteFlagsPatchPath,
        "--out",
        (Join-Path $unsupportedRoot "GraphForm.structural-site-flags.frm"),
        "--mode",
        "strict",
        "--stream-mode",
        "full-patch"
    )
    Assert-Condition ($structuralSiteFlagsMessage -match "streamed/promoteControls") "siteBitFlags must not change topology-defining streamed/promoteControls bits"

    $readOnlySiteFlagPatchPath = Join-Path $unsupportedRoot "read-only-site-flag.patch.json"
    Write-JsonFile $readOnlySiteFlagPatchPath ([ordered]@{
        properties = [ordered]@{ ContractList = [ordered]@{ streamed = $false } }
    })
    $readOnlySiteFlagMessage = Invoke-FrxEditExpectFailure @(
        "build",
        (Join-Path (Join-Path $workRoot "parent-first") "GraphForm.frm"),
        $readOnlySiteFlagPatchPath,
        "--out",
        (Join-Path $unsupportedRoot "GraphForm.read-only-site-flag.frm"),
        "--mode",
        "strict",
        "--stream-mode",
        "full-patch"
    )
    Assert-Condition ($readOnlySiteFlagMessage -match "streamed") "the structural streamed projection must not be directly editable"

    $shapeCookiePatchPath = Join-Path $unsupportedRoot "shape-cookie.patch.json"
    Write-JsonFile $shapeCookiePatchPath ([ordered]@{
        properties = [ordered]@{ GraphForm = [ordered]@{ formShapeCookie = 9 } }
    })
    $shapeCookieMessage = Invoke-FrxEditExpectFailure @(
        "build",
        (Join-Path (Join-Path $workRoot "parent-first") "GraphForm.frm"),
        $shapeCookiePatchPath,
        "--out",
        (Join-Path $unsupportedRoot "GraphForm.shape-cookie.frm"),
        "--mode",
        "strict",
        "--stream-mode",
        "full-patch"
    )
    Assert-Condition ($shapeCookieMessage -match "formShapeCookie") "formShapeCookie must remain diagnostic native structure rather than an editable root property"

    $cloneRoot = Join-Path $workRoot "template-page-clone"
    New-Item -ItemType Directory -Force -Path $cloneRoot | Out-Null
    $clonePatch = [ordered]@{
        properties = [ordered]@{
            CloneTabs = [ordered]@{
                type = "MultiPage"
                leftPt = 235
                topPt = 8
                widthPt = 180
                heightPt = 100
                tabIndex = 2
                value = 0
                fontName = "Tahoma"
                fontSize = 8.25
            }
            ClonePage = [ordered]@{
                type = "Page"
                parent = "CloneTabs"
                tabIndex = 7
                visible = $true
            }
            ExistingTabsClone = [ordered]@{
                type = "Page"
                parent = "Tabs"
                tabIndex = 8
                visible = $false
            }
        }
        add = @(
            [ordered]@{ fromTemplate = "PageA"; name = "ExistingTabsClone"; parent = "Tabs" },
            [ordered]@{ fromTemplate = "PageA"; name = "ClonePage"; parent = "CloneTabs" },
            [ordered]@{ type = "MultiPage"; name = "CloneTabs"; parent = "" }
        )
    }
    $clonePatchPath = Join-Path $cloneRoot "clone-page.patch.json"
    $cloneFormPath = Join-Path $cloneRoot "GraphForm.clone.frm"
    $cloneRawPath = Join-Path $cloneRoot "GraphForm.clone.raw.json"
    $cloneReportPath = Join-Path $cloneRoot "GraphForm.clone.report.json"
    Write-JsonFile $clonePatchPath $clonePatch
    Invoke-FrxEdit @(
        "build",
        (Join-Path (Join-Path $workRoot "parent-first") "GraphForm.frm"),
        $clonePatchPath,
        "--out",
        $cloneFormPath,
        "--mode",
        "strict",
        "--stream-mode",
        "full-patch",
        "--report-out",
        $cloneReportPath
    )
    Invoke-FrxEdit @("validate", $cloneFormPath, "--mode", "strict")
    Invoke-FrxEdit @("inspect", $cloneFormPath, "--mode", "strict", "--raw-out", $cloneRawPath, "--out", (Join-Path $cloneRoot "GraphForm.clone.json"))
    $cloneRaw = Get-Content -Raw -LiteralPath $cloneRawPath | ConvertFrom-Json
    $cloneReport = Get-Content -Raw -LiteralPath $cloneReportPath | ConvertFrom-Json
    $cloneMultiPage = $cloneRaw.controls | Where-Object name -EQ "CloneTabs"
    $clonePages = @($cloneRaw.controls | Where-Object { $_.type -eq "Page" -and $_.parent -eq "CloneTabs" })
    $existingMultiPage = $cloneRaw.controls | Where-Object name -EQ "Tabs"
    $existingPages = @($cloneRaw.controls | Where-Object { $_.type -eq "Page" -and $_.parent -eq "Tabs" } | Sort-Object { $_.properties.multiPagePageIndex })
    Assert-Condition ($clonePages.Count -eq 1) "a template-cloned explicit Page must suppress implicit default Pages"
    Assert-Condition ($clonePages[0].name -eq "ClonePage") "the effective cloned Page must be ClonePage"
    Assert-Condition ($clonePages[0].properties.tabIndex -eq 7) "the cloned Page must retain requested tabIndex 7"
    Assert-Condition ($clonePages[0].properties.visible -eq $true) "the cloned Page must retain requested visibility"
    Assert-Condition ($cloneMultiPage.properties.multiPagePageCount -eq 1) "the cloned-Page MultiPage must report PageCount 1"
    Assert-Condition ($cloneMultiPage.properties.multiPageXStreamValidation -eq "exact") "the cloned-Page MultiPage x stream must consume exactly"
    Assert-Condition (($cloneMultiPage.properties.multiPagePageIds -join ",") -eq ($clonePages[0].properties.multiPagePageId -join ",")) "the cloned-Page x ID must match the effective Page ID"
    Assert-Condition (($cloneMultiPage.properties.tabNames -join ",") -eq "ClonePage") "the cloned-Page TabStrip name must match the effective Page"
    Assert-Condition (($cloneMultiPage.properties.tabCaptions -join ",") -eq "ClonePage") "the cloned-Page TabStrip caption must match the effective Page"
    Assert-Condition (($existingPages.name -join ",") -eq "PageA,PageB,PageC,ExistingTabsClone") "a Page clone added to an existing MultiPage must join its effective Page graph"
    Assert-Condition ($existingPages[-1].properties.tabIndex -eq 8) "the existing-MultiPage Page clone must retain requested tabIndex 8"
    Assert-Condition ($existingPages[-1].properties.visible -eq $false) "the existing-MultiPage Page clone must retain requested visibility"
    Assert-Condition ($existingMultiPage.properties.multiPagePageCount -eq 4) "the existing MultiPage x stream must report the cloned Page"
    Assert-Condition (($existingMultiPage.properties.multiPagePageIds -join ",") -eq ($existingPages.properties.multiPagePageId -join ",")) "the existing MultiPage x IDs must match all effective Page IDs"
    Assert-Condition (($existingMultiPage.properties.tabNames -join ",") -eq "PageA,PageB,PageC,ExistingTabsClone") "the existing MultiPage TabStrip names must match all effective Pages"
    Assert-Condition (($existingMultiPage.properties.tabCaptions -join ",") -eq "PageA,PageB,PageC,ExistingTabsClone") "the existing MultiPage TabStrip captions must match all effective Pages"
    Assert-Condition (($existingPages.properties.visible -join ",") -eq "False,False,True,False") "existing Page visibility must survive the Page clone"
    Assert-Condition (($existingMultiPage.properties.value -eq 2) -and ($existingMultiPage.properties.listIndex -eq 2)) "the existing MultiPage selection must survive the Page clone"
    Assert-Condition ($cloneReport.semanticMatch -eq $true) "the existing-form child-first Page clone build must retain all compared semantics"

    $replacementRoot = Join-Path $workRoot "count-neutral-page-replacement"
    New-Item -ItemType Directory -Force -Path $replacementRoot | Out-Null
    $replacementPatch = [ordered]@{
        properties = [ordered]@{
            PageC = [ordered]@{
                visible = $false
            }
            ReplacementPage = [ordered]@{
                type = "Page"
                parent = "Tabs"
                tabIndex = 3
                visible = $true
            }
        }
        add = @([ordered]@{ type = "Page"; name = "ReplacementPage"; parent = "Tabs" })
        remove = @("PageA")
    }
    $replacementPatchPath = Join-Path $replacementRoot "replacement.patch.json"
    $replacementFormPath = Join-Path $replacementRoot "GraphForm.replacement.frm"
    $replacementRawPath = Join-Path $replacementRoot "GraphForm.replacement.raw.json"
    $replacementStoragePath = Join-Path $replacementRoot "GraphForm.replacement.storage.json"
    $replacementReportPath = Join-Path $replacementRoot "GraphForm.replacement.report.json"
    Write-JsonFile $replacementPatchPath $replacementPatch
    Invoke-FrxEdit @(
        "build",
        (Join-Path (Join-Path $workRoot "parent-first") "GraphForm.frm"),
        $replacementPatchPath,
        "--out",
        $replacementFormPath,
        "--mode",
        "strict",
        "--stream-mode",
        "full-patch",
        "--report-out",
        $replacementReportPath
    )
    Invoke-FrxEdit @("validate", $replacementFormPath, "--mode", "strict")
    Invoke-FrxEdit @("inspect", $replacementFormPath, "--mode", "strict", "--raw-out", $replacementRawPath, "--out", (Join-Path $replacementRoot "GraphForm.replacement.json"))
    Invoke-FrxEdit @("dump-storage", $replacementFormPath, "--out", $replacementStoragePath)
    $replacementRaw = Get-Content -Raw -LiteralPath $replacementRawPath | ConvertFrom-Json
    $replacementStorage = Get-Content -Raw -LiteralPath $replacementStoragePath | ConvertFrom-Json
    $replacementReport = Get-Content -Raw -LiteralPath $replacementReportPath | ConvertFrom-Json
    $replacementMultiPage = $replacementRaw.controls | Where-Object name -EQ "Tabs"
    $replacementPages = @($replacementRaw.controls | Where-Object { $_.type -eq "Page" -and $_.parent -eq "Tabs" } | Sort-Object { $_.properties.multiPagePageIndex })
    Assert-Condition (($replacementPages.name -join ",") -eq "PageB,PageC,ReplacementPage") "count-neutral replacement must update the effective Page identities"
    foreach ($removedName in @("PageA", "PageALabel", "PageAFrame", "NestedLabel")) {
        Assert-Condition (-not ($replacementRaw.controls.name -contains $removedName)) "removed Page subtree control '$removedName' must not remain observable"
    }
    Assert-Condition ($replacementMultiPage.properties.multiPagePageCount -eq 3) "count-neutral replacement must retain PageCount 3"
    Assert-Condition ($replacementMultiPage.properties.multiPageXStreamValidation -eq "exact") "count-neutral replacement x stream must consume exactly"
    Assert-Condition (($replacementMultiPage.properties.multiPagePageIds -join ",") -eq ($replacementPages.properties.multiPagePageId -join ",")) "count-neutral replacement x IDs must match effective Page IDs"
    Assert-Condition (($replacementMultiPage.properties.tabNames -join ",") -eq "PageB,PageC,ReplacementPage") "count-neutral replacement must update internal TabStrip names"
    Assert-Condition (($replacementMultiPage.properties.tabCaptions -join ",") -eq "PageB,PageC,ReplacementPage") "count-neutral replacement must update internal TabStrip captions"
    Assert-Condition (($replacementPages.properties.visible -join ",") -eq "False,False,True") "count-neutral replacement visibility must agree with selected Page 3"
    Assert-Condition (($replacementMultiPage.properties.value -eq 2) -and ($replacementMultiPage.properties.listIndex -eq 2)) "count-neutral replacement must retain numeric selection 2"
    $removedStoragePath = ($rawOutputs["parent-first"].controls | Where-Object name -EQ "PageA").properties.ownedStoragePath
    $replacementOwnedStoragePath = $replacementPages[-1].properties.ownedStoragePath
    $oldPrefixPaths = @($replacementStorage.streams.path | Where-Object { $_ -eq $removedStoragePath -or $_.StartsWith("$removedStoragePath/", [StringComparison]::OrdinalIgnoreCase) })
    if ($replacementOwnedStoragePath -eq $removedStoragePath) {
        $allowedReusedPaths = @(
            $replacementOwnedStoragePath,
            "$replacementOwnedStoragePath/f",
            "$replacementOwnedStoragePath/o",
            "$replacementOwnedStoragePath/$([char]1)CompObj"
        )
        $orphanedRemovedPaths = @($oldPrefixPaths | Where-Object { $allowedReusedPaths -notcontains $_ })
        Assert-Condition ($orphanedRemovedPaths.Count -eq 0) "a reused Page storage path must not retain the removed subtree"
    }
    else {
        Assert-Condition ($oldPrefixPaths.Count -eq 0) "the removed Page's unowned CFB storage subtree must be absent"
    }
    Assert-Condition (($replacementStorage.streams.path -contains "$replacementOwnedStoragePath/f") -and ($replacementStorage.streams.path -contains "$replacementOwnedStoragePath/o")) "the replacement Page must own physical f/o streams"
    Assert-Condition ($replacementReport.semanticMatch -eq $true) "count-neutral Page replacement must retain all compared semantics"

    $fallbackPatch = [ordered]@{
        properties = [ordered]@{
            FallbackForm = [ordered]@{}
            FallbackTabs = [ordered]@{
                type = "MultiPage"
                leftPt = 8
                topPt = 8
                widthPt = 180
                heightPt = 100
                tabIndex = 0
            }
        }
        add = @([ordered]@{ type = "MultiPage"; name = "FallbackTabs"; parent = "" })
    }
    $fallbackRoot = Join-Path $workRoot "implicit-pages"
    New-Item -ItemType Directory -Force -Path $fallbackRoot | Out-Null
    $fallbackPatchPath = Join-Path $fallbackRoot "FallbackForm.template.json"
    $fallbackFormPath = Join-Path $fallbackRoot "FallbackForm.frm"
    $fallbackRawPath = Join-Path $fallbackRoot "FallbackForm.raw.json"
    Write-JsonFile $fallbackPatchPath $fallbackPatch
    Invoke-FrxEdit @("create", $fallbackFormPath, "--name", "FallbackForm", "--patch", $fallbackPatchPath)
    Invoke-FrxEdit @("inspect", $fallbackFormPath, "--mode", "strict", "--raw-out", $fallbackRawPath, "--out", (Join-Path $fallbackRoot "FallbackForm.json"))
    $fallbackRaw = Get-Content -Raw -LiteralPath $fallbackRawPath | ConvertFrom-Json
    Assert-Condition ($null -eq (Get-OptionalProperty $fallbackRaw.frxFormControl "formGroupCount")) "an omitted formGroupCount must remain absent and use the effective file default zero"
    Assert-Condition ($fallbackRaw.controls.Count -eq 3) "a bare MultiPage must retain the two-page fallback"
    $fallbackMultiPage = $fallbackRaw.controls | Where-Object name -EQ "FallbackTabs"
    $fallbackPages = @($fallbackRaw.controls | Where-Object { $_.type -eq "Page" -and $_.parent -eq "FallbackTabs" } | Sort-Object { $_.properties.multiPagePageIndex })
    Assert-Condition (($fallbackPages.name -join ",") -eq "FallbackTabsPage1,FallbackTabsPage2") "fallback Pages must be effective children of FallbackTabs"
    Assert-Condition ($fallbackRaw.parserValidation.multiPageXStreamValidations.FallbackTabs.validation -eq "exact") "fallback x stream must consume exactly"
    Assert-Condition ($fallbackRaw.parserValidation.multiPageXStreamValidations.FallbackTabs.pageCount -eq 2) "fallback x stream must report PageCount 2"
    Assert-Condition (($fallbackRaw.parserValidation.multiPageXStreamValidations.FallbackTabs.pageIds -join ",") -eq ($fallbackPages.properties.multiPagePageId -join ",")) "fallback x IDs must match effective Page IDs"
    Assert-Condition (($fallbackMultiPage.properties.multiPagePageIds -join ",") -eq ($fallbackPages.properties.multiPagePageId -join ",")) "fallback MultiPage metadata must match effective Page IDs"
    Assert-Condition ($fallbackMultiPage.properties.value -eq 0) "fallback MultiPage selection must default to Page 1"
    Assert-Condition (($fallbackPages.properties.visible -join ",") -eq "True,False") "fallback visibility must agree with the selected Page"

    Write-Host "PASS: generated-container graph planning, ordering, stream participation, and fallback Pages"
    $completed = $true
    Write-Host "Artifacts: $workRoot"
}
finally {
    if ($transcriptStarted) {
        Stop-Transcript | Out-Null
    }
    if (-not $completed -and (Test-Path -LiteralPath $workRoot)) {
        Write-Host "Failure artifacts: $workRoot"
    }
}
