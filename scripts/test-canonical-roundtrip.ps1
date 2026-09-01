[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string]$IguanaTexRepo,

    [string]$ArtifactsRoot,

    [switch]$SkipBuild,

    [switch]$SkipWatch,

    [switch]$KeepArtifacts
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$outRoot = if ([string]::IsNullOrWhiteSpace($ArtifactsRoot)) {
    [IO.Path]::Combine($repoRoot, ".build")
}
else {
    [IO.Path]::GetFullPath($ArtifactsRoot)
}
$workRoot = [IO.Path]::Combine($outRoot, "canonical-roundtrip-tests-" + [Guid]::NewGuid().ToString("N"))
$cliPath = [IO.Path]::Combine($repoRoot, "src", "FrxEdit.Cli", "bin", $Configuration, "net8.0", "frxedit.dll")
$comparator = [IO.Path]::Combine($PSScriptRoot, "compare-canonical-form.ps1")
$canonicalFormNames = @(
    "AboutBox",
    "BatchEditForm",
    "ErrorForm",
    "ExternalEditorForm",
    "LatexForm",
    "LoadVectorGraphicsForm",
    "LogFileViewer",
    "RegenerateForm",
    "SetTempForm"
)

function Invoke-FrxEdit([string[]]$CommandArguments, [string]$WorkingDirectory) {
    $commandOutput = @()
    $exitCode = -999
    $pushedLocation = $false
    try {
        if (-not [string]::IsNullOrWhiteSpace($WorkingDirectory)) {
            New-Item -ItemType Directory -Force -Path $WorkingDirectory | Out-Null
            Push-Location -LiteralPath $WorkingDirectory
            $pushedLocation = $true
        }
        $commandOutput = @(& dotnet $cliPath @CommandArguments)
        $exitCode = $LASTEXITCODE
    }
    finally {
        if ($pushedLocation) {
            Pop-Location
        }
    }
    foreach ($line in $commandOutput) {
        Write-Host $line
    }
    if ($exitCode -ne 0) {
        throw "frxedit failed with exit code ${exitCode}: $($CommandArguments -join ' ')"
    }
}

function Get-FileUriReferences([object]$Value) {
    if ($null -eq $Value) {
        return
    }
    if ($Value -is [string]) {
        if ($Value.StartsWith("file://", [StringComparison]::OrdinalIgnoreCase)) {
            Write-Output $Value
        }
        return
    }
    if ($Value -is [Management.Automation.PSCustomObject]) {
        foreach ($property in $Value.PSObject.Properties) {
            Get-FileUriReferences $property.Value
        }
        return
    }
    if ($Value -is [Collections.IDictionary]) {
        foreach ($entryValue in $Value.Values) {
            Get-FileUriReferences $entryValue
        }
        return
    }
    if ($Value -is [Collections.IEnumerable]) {
        foreach ($item in $Value) {
            Get-FileUriReferences $item
        }
    }
}

function Assert-ExportedAssetReferences([string]$DocumentPath, [bool]$RequireAtLeastOne) {
    $document = Get-Content -Raw -LiteralPath $DocumentPath | ConvertFrom-Json
    $references = @(Get-FileUriReferences $document)
    if ($RequireAtLeastOne -and $references.Count -eq 0) {
        throw "Expected at least one exported file:// asset reference: $DocumentPath"
    }

    $documentDirectory = [IO.Path]::GetFullPath((Split-Path -Parent $DocumentPath))
    $documentPrefix = $documentDirectory.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    foreach ($reference in $references) {
        $referencePath = [Uri]::UnescapeDataString($reference.Substring("file://".Length))
        if ($reference.StartsWith("file:///", [StringComparison]::OrdinalIgnoreCase) -or
            [IO.Path]::IsPathRooted($referencePath)) {
            throw "Exporter emitted an absolute asset URI instead of a patch-document-relative URI: $reference"
        }
        $platformPath = $referencePath.Replace('/', [IO.Path]::DirectorySeparatorChar)
        $resolved = [IO.Path]::GetFullPath((Join-Path $documentDirectory $platformPath))
        if (-not $resolved.StartsWith($documentPrefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Exported asset URI escapes its patch document directory: $reference"
        }
        if (-not (Test-Path -LiteralPath $resolved)) {
            throw "Exported asset URI does not resolve beside its patch document: $reference"
        }
    }
    return $references
}

function ConvertTo-ProcessArgument([string]$Value) {
    if ($Value.Length -eq 0) {
        return '""'
    }
    if ($Value -notmatch '[\s"]') {
        return $Value
    }
    return '"' + ($Value -replace '"', '\"') + '"'
}

function Invoke-WatchRebuild(
    [string]$InputForm,
    [string]$PatchPath,
    [string]$OutputForm,
    [string]$WorkingDirectory,
    [string]$ArtifactDirectory) {
    New-Item -ItemType Directory -Force -Path $WorkingDirectory, $ArtifactDirectory | Out-Null

    $stdoutPath = Join-Path $ArtifactDirectory "watch.stdout.log"
    $stderrPath = Join-Path $ArtifactDirectory "watch.stderr.log"
    $statePath = Join-Path $ArtifactDirectory "watch-state.json"
    $outputFrx = [IO.Path]::ChangeExtension($OutputForm, ".frx")
    $arguments = @($cliPath, "watch", $InputForm, $PatchPath, "--out", $OutputForm, "--mode", "strict")

    $process = $null
    $stdoutTask = $null
    $stderrTask = $null
    $stdout = ""
    $stderr = ""
    $initialOutputReady = $false
    $runningAtTrigger = $false
    $triggered = $false
    $regenerated = $false
    $survivedTrigger = $false
    $terminated = $false
    $ownedProcessId = $null

    try {
        $startInfo = [Diagnostics.ProcessStartInfo]::new()
        $startInfo.FileName = "dotnet"
        $startInfo.WorkingDirectory = [IO.Path]::GetFullPath($WorkingDirectory)
        $startInfo.UseShellExecute = $false
        $startInfo.CreateNoWindow = $true
        $startInfo.RedirectStandardOutput = $true
        $startInfo.RedirectStandardError = $true
        if ($startInfo.PSObject.Properties.Name -contains "ArgumentList") {
            foreach ($argument in $arguments) {
                [void]$startInfo.ArgumentList.Add($argument)
            }
        }
        else {
            $startInfo.Arguments = (@($arguments | ForEach-Object { ConvertTo-ProcessArgument $_ }) -join " ")
        }

        $process = [Diagnostics.Process]::new()
        $process.StartInfo = $startInfo
        [void]$process.Start()
        $ownedProcessId = $process.Id
        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()

        $initialDeadline = [DateTime]::UtcNow.AddSeconds(30)
        while ([DateTime]::UtcNow -lt $initialDeadline) {
            $process.Refresh()
            if ($process.HasExited) {
                break
            }
            if ((Test-Path -LiteralPath $OutputForm) -and (Test-Path -LiteralPath $outputFrx)) {
                $initialOutputReady = $true
                break
            }
            Start-Sleep -Milliseconds 100
        }

        $process.Refresh()
        $runningAtTrigger = -not $process.HasExited
        if ($initialOutputReady -and $runningAtTrigger) {
            $beforeFrm = Get-Item -LiteralPath $OutputForm
            $beforeFrx = Get-Item -LiteralPath $outputFrx
            $beforeFrmWrite = $beforeFrm.LastWriteTimeUtc
            $beforeFrxWrite = $beforeFrx.LastWriteTimeUtc
            $beforeFrmHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $OutputForm).Hash
            $beforeFrxHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $outputFrx).Hash

            $patchBeforeWrite = (Get-Item -LiteralPath $PatchPath).LastWriteTimeUtc
            $patchTriggerWrite = [DateTime]::UtcNow
            if ($patchTriggerWrite -le $patchBeforeWrite) {
                $patchTriggerWrite = $patchBeforeWrite.AddSeconds(1)
            }
            [IO.File]::SetLastWriteTimeUtc($PatchPath, $patchTriggerWrite)
            $triggered = (Get-Item -LiteralPath $PatchPath).LastWriteTimeUtc -gt $patchBeforeWrite

            $regenerationDeadline = [DateTime]::UtcNow.AddSeconds(30)
            while ($triggered -and [DateTime]::UtcNow -lt $regenerationDeadline) {
                $process.Refresh()
                if ($process.HasExited) {
                    break
                }
                if ((Test-Path -LiteralPath $OutputForm) -and (Test-Path -LiteralPath $outputFrx)) {
                    try {
                        $afterFrm = Get-Item -LiteralPath $OutputForm
                        $afterFrx = Get-Item -LiteralPath $outputFrx
                        $afterFrmHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $OutputForm).Hash
                        $afterFrxHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $outputFrx).Hash
                        $regenerated =
                            $afterFrm.LastWriteTimeUtc -gt $beforeFrmWrite -or
                            $afterFrx.LastWriteTimeUtc -gt $beforeFrxWrite -or
                            $afterFrmHash -cne $beforeFrmHash -or
                            $afterFrxHash -cne $beforeFrxHash
                    }
                    catch [IO.IOException] {
                        # File.Replace makes the output pair briefly unavailable on some hosts.
                        $regenerated = $false
                    }
                    if ($regenerated) {
                        break
                    }
                }
                Start-Sleep -Milliseconds 100
            }

            if ($regenerated) {
                Start-Sleep -Milliseconds 250
                $process.Refresh()
                $survivedTrigger = -not $process.HasExited
            }
        }
    }
    catch {
        $stderr += ($_ | Out-String)
    }
    finally {
        if ($null -ne $process) {
            try {
                if (-not $process.HasExited) {
                    $process.Kill()
                    $process.WaitForExit()
                    $terminated = $true
                }
            }
            catch {
                $stderr += ($_ | Out-String)
            }
            $process.Refresh()
            $processExited = $process.HasExited
            if ($processExited -and $null -ne $stdoutTask) {
                $stdout += $stdoutTask.Result
            }
            if ($processExited -and $null -ne $stderrTask) {
                $stderr += $stderrTask.Result
            }
            $process.Dispose()
        }
        [IO.File]::WriteAllText($stdoutPath, $stdout, [Text.UTF8Encoding]::new($false))
        [IO.File]::WriteAllText($stderrPath, $stderr, [Text.UTF8Encoding]::new($false))
    }

    $ownedStillRunning = $false
    if ($null -ne $ownedProcessId) {
        $ownedStillRunning = $null -ne (Get-Process -Id $ownedProcessId -ErrorAction SilentlyContinue)
    }
    $patchOutsideWorkingDirectory =
        -not [IO.Path]::GetFullPath($PatchPath).StartsWith(
            [IO.Path]::GetFullPath($WorkingDirectory).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar,
            [StringComparison]::OrdinalIgnoreCase)
    $passed =
        $initialOutputReady -and
        $runningAtTrigger -and
        $triggered -and
        $regenerated -and
        $survivedTrigger -and
        $terminated -and
        $patchOutsideWorkingDirectory -and
        -not $ownedStillRunning
    $state = [ordered]@{
        schemaVersion = 1
        inputForm = [IO.Path]::GetFullPath($InputForm)
        patch = [IO.Path]::GetFullPath($PatchPath)
        outputForm = [IO.Path]::GetFullPath($OutputForm)
        workingDirectory = [IO.Path]::GetFullPath($WorkingDirectory)
        patchDocumentRelativeAssets = $patchOutsideWorkingDirectory
        ownedProcessId = $ownedProcessId
        initialOutputReady = $initialOutputReady
        runningAtTrigger = $runningAtTrigger
        noOpMtimeTrigger = $triggered
        outputRegeneratedAfterBaseline = $regenerated
        processSurvivedTrigger = $survivedTrigger
        boundedProcessTerminated = $terminated
        ownedProcessStillRunning = $ownedStillRunning
        passed = $passed
    }
    [IO.File]::WriteAllText($statePath, ($state | ConvertTo-Json -Depth 10), [Text.UTF8Encoding]::new($false))
    if (-not $passed) {
        throw "Bounded watch reconstruction failed; see $statePath"
    }
}

function Get-SourceHashManifest([object[]]$Forms) {
    $result = [ordered]@{}
    foreach ($form in $Forms) {
        foreach ($path in @($form.Frm, $form.Frx)) {
            $fullPath = [IO.Path]::GetFullPath($path)
            $result[$fullPath] = (Get-FileHash -Algorithm SHA256 -LiteralPath $fullPath).Hash.ToLowerInvariant()
        }
    }
    return $result
}

function Assert-SourceHashes([object]$Before, [object]$After) {
    $beforeJson = $Before | ConvertTo-Json -Depth 10 -Compress
    $afterJson = $After | ConvertTo-Json -Depth 10 -Compress
    if ($beforeJson -cne $afterJson) {
        throw "Canonical source hashes changed during round-trip testing."
    }
}

function Inspect-Raw([string]$FormPath, [string]$RawPath) {
    Invoke-FrxEdit @(
        "inspect", $FormPath,
        "--mode", "strict",
        "--raw-out", $RawPath,
        "--out", ([IO.Path]::ChangeExtension($RawPath, ".inspect.json"))
    )
}

function Assert-RebuildReport([string]$ReportPath) {
    $report = Get-Content -Raw -LiteralPath $ReportPath | ConvertFrom-Json
    if (-not [bool]$report.semanticMatch) {
        throw "Internal canonical rebuild report did not match: $ReportPath"
    }
}

function Assert-LatexFormRaw([object]$Form, [string]$RawPath) {
    if ($Form.FormName -ne "LatexForm") {
        return
    }

    $raw = Get-Content -Raw -LiteralPath $RawPath | ConvertFrom-Json
    if (@($raw.controls).Count -ne 51) {
        throw "LatexForm must contain exactly 51 controls: $RawPath"
    }

    $multiPage = @($raw.controls | Where-Object name -EQ "MultiPage1")
    if ($multiPage.Count -ne 1 -or
        $multiPage[0].properties.multiPageXStreamValidation -cne "exact" -or
        [int]$multiPage[0].properties.multiPageXStreamLength -ne 60 -or
        [int]$multiPage[0].properties.multiPagePageCount -ne 3) {
        throw "LatexForm MultiPage1 must retain an exact 60-byte x stream with three Pages: $RawPath"
    }

    $pages = @($raw.controls | Where-Object {
        $_.type -ceq "Page" -and $_.parent -ceq "MultiPage1"
    } | Sort-Object { [int]$_.properties.multiPagePageIndex })
    if (($pages.name -join ",") -cne "Page1,Page2,Page3") {
        throw "LatexForm MultiPage Page order must remain Page1,Page2,Page3: $RawPath"
    }
}

function Test-Reconstruction([object]$Form) {
    $caseRoot = Join-Path $workRoot $Form.CaseName
    $sourceRoot = Join-Path $caseRoot "source-copy"
    $exportRoot = Join-Path $caseRoot "exported"
    New-Item -ItemType Directory -Force -Path $sourceRoot, $exportRoot | Out-Null

    $sourceForm = Join-Path $sourceRoot ($Form.FileStem + ".frm")
    $sourceFrx = Join-Path $sourceRoot ($Form.FileStem + ".frx")
    Copy-Item -LiteralPath $Form.Frm -Destination $sourceForm
    Copy-Item -LiteralPath $Form.Frx -Destination $sourceFrx

    $originalRaw = Join-Path $caseRoot "original.raw.json"
    Invoke-FrxEdit @("validate", $sourceForm, "--mode", "strict")
    Inspect-Raw $sourceForm $originalRaw
    Assert-LatexFormRaw $Form $originalRaw

    $noOpRoot = Join-Path $caseRoot "no-op"
    New-Item -ItemType Directory -Force -Path $noOpRoot | Out-Null
    $noOpForm = Join-Path $noOpRoot ($Form.FileStem + ".frm")
    Invoke-FrxEdit @(
        "build", $sourceForm,
        "--out", $noOpForm,
        "--mode", "strict",
        "--stream-mode", "full-patch",
        "--report-out", (Join-Path $noOpRoot "rebuild-report.json"))
    Assert-RebuildReport (Join-Path $noOpRoot "rebuild-report.json")
    Invoke-FrxEdit @("validate", $noOpForm, "--mode", "strict")
    $noOpRaw = Join-Path $noOpRoot ($Form.FileStem + ".raw.json")
    Inspect-Raw $noOpForm $noOpRaw
    Assert-LatexFormRaw $Form $noOpRaw
    & $comparator -OriginalRaw $originalRaw -CandidateRaw $noOpRaw -ReportOut (Join-Path $noOpRoot "canonical-comparison.json")

    $patchPath = Join-Path $exportRoot ($Form.FileStem + ".patch.json")
    Invoke-FrxEdit @("inspect", $sourceForm, "--mode", "strict", "--as-patch", "--extract-images", "--out", $patchPath)
    $requiresExportedAsset = $Form.CaseName -ceq "fixture-userformallcontrol"
    $patchAssetReferences = @(Assert-ExportedAssetReferences $patchPath $requiresExportedAsset)
    $patchProcessRoot = Join-Path $caseRoot "unrelated-patch-process-cwd"
    foreach ($patchStyle in @("positional", "option")) {
        $patchRoot = Join-Path $caseRoot ("patch-reapply-" + $patchStyle)
        New-Item -ItemType Directory -Force -Path $patchRoot | Out-Null
        $patchForm = Join-Path $patchRoot ($Form.FileStem + ".frm")
        $patchArguments = if ($patchStyle -eq "positional") {
            @(
                "build", $sourceForm, $patchPath,
                "--out", $patchForm,
                "--mode", "strict",
                "--stream-mode", "full-patch",
                "--report-out", (Join-Path $patchRoot "rebuild-report.json"))
        }
        else {
            @(
                "build", $sourceForm,
                "--patch", $patchPath,
                "--out", $patchForm,
                "--mode", "strict",
                "--stream-mode", "full-patch",
                "--report-out", (Join-Path $patchRoot "rebuild-report.json"))
        }
        Invoke-FrxEdit -CommandArguments $patchArguments -WorkingDirectory $patchProcessRoot
        Assert-RebuildReport (Join-Path $patchRoot "rebuild-report.json")
        Invoke-FrxEdit @("validate", $patchForm, "--mode", "strict")
        $patchRaw = Join-Path $patchRoot ($Form.FileStem + ".raw.json")
        Inspect-Raw $patchForm $patchRaw
        Assert-LatexFormRaw $Form $patchRaw
        & $comparator -OriginalRaw $originalRaw -CandidateRaw $patchRaw -ReportOut (Join-Path $patchRoot "canonical-comparison.json")
    }

    if (-not $SkipWatch) {
        $watchRoot = Join-Path $caseRoot "watch-reapply"
        $watchInputRoot = Join-Path $watchRoot "input"
        $watchOutputRoot = Join-Path $watchRoot "output"
        $watchProcessRoot = Join-Path $watchRoot "unrelated-process-cwd"
        New-Item -ItemType Directory -Force -Path $watchInputRoot, $watchOutputRoot, $watchProcessRoot | Out-Null
        $watchInputForm = Join-Path $watchInputRoot ($Form.FileStem + ".frm")
        $watchInputFrx = Join-Path $watchInputRoot ($Form.FileStem + ".frx")
        $watchOutputForm = Join-Path $watchOutputRoot ($Form.FileStem + ".frm")
        Copy-Item -LiteralPath $sourceForm -Destination $watchInputForm
        Copy-Item -LiteralPath $sourceFrx -Destination $watchInputFrx

        Invoke-WatchRebuild `
            -InputForm $watchInputForm `
            -PatchPath $patchPath `
            -OutputForm $watchOutputForm `
            -WorkingDirectory $watchProcessRoot `
            -ArtifactDirectory $watchRoot
        Invoke-FrxEdit @("validate", $watchOutputForm, "--mode", "strict")
        $watchRaw = Join-Path $watchOutputRoot ($Form.FileStem + ".raw.json")
        Inspect-Raw $watchOutputForm $watchRaw
        Assert-LatexFormRaw $Form $watchRaw
        & $comparator -OriginalRaw $originalRaw -CandidateRaw $watchRaw -ReportOut (Join-Path $watchRoot "canonical-comparison.json")
    }

    $templatePath = Join-Path $exportRoot ($Form.FileStem + ".template.json")
    Invoke-FrxEdit @("inspect", $sourceForm, "--mode", "strict", "--as-template", "--extract-images", "--out", $templatePath)
    $templateAssetReferences = @(Assert-ExportedAssetReferences $templatePath $requiresExportedAsset)
    $assetContract = [ordered]@{
        schemaVersion = 1
        patchDocumentDirectory = [IO.Path]::GetFullPath($exportRoot)
        patchAndCreateWorkingDirectory = [IO.Path]::GetFullPath($patchProcessRoot)
        workingDirectoryIsUnrelated =
            -not [IO.Path]::GetFullPath($patchProcessRoot).StartsWith(
                [IO.Path]::GetFullPath($exportRoot).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar,
                [StringComparison]::OrdinalIgnoreCase)
        patchAssetReferences = $patchAssetReferences
        templateAssetReferences = $templateAssetReferences
    }
    if (-not $assetContract.workingDirectoryIsUnrelated) {
        throw "Patch/create working directory must be unrelated to the exported document directory."
    }
    [IO.File]::WriteAllText(
        (Join-Path $caseRoot "asset-path-contract.json"),
        ($assetContract | ConvertTo-Json -Depth 10),
        [Text.UTF8Encoding]::new($false))
    $templateRoot = Join-Path $caseRoot "template-recreate"
    New-Item -ItemType Directory -Force -Path $templateRoot | Out-Null
    $templateForm = Join-Path $templateRoot ($Form.FileStem + ".frm")
    Invoke-FrxEdit -CommandArguments @("create", $templateForm, "--name", $Form.FormName, "--patch", $templatePath) -WorkingDirectory $patchProcessRoot
    Invoke-FrxEdit @("validate", $templateForm, "--mode", "strict")
    $templateRaw = Join-Path $templateRoot ($Form.FileStem + ".raw.json")
    Inspect-Raw $templateForm $templateRaw
    Assert-LatexFormRaw $Form $templateRaw
    & $comparator -OriginalRaw $originalRaw -CandidateRaw $templateRaw -ReportOut (Join-Path $templateRoot "canonical-comparison.json")

    return [ordered]@{
        form = $Form.CaseName
        originalStrict = $true
        noOpSemantic = $true
        positionalPatchSemantic = $true
        optionPatchSemantic = $true
        watchSemantic = $(if ($SkipWatch) { $null } else { $true })
        templateSemantic = $true
    }
}

$forms = [Collections.Generic.List[object]]::new()
$fixtureRoot = [IO.Path]::Combine($repoRoot, "test_data", "forms", "original")
$forms.Add([ordered]@{
    CaseName = "fixture-userformallcontrol"
    FileStem = "userformallcontrol"
    FormName = "UserForm2"
    Frm = Join-Path $fixtureRoot "userformallcontrol.frm"
    Frx = Join-Path $fixtureRoot "userformallcontrol.frx"
})

if (-not [string]::IsNullOrWhiteSpace($IguanaTexRepo)) {
    $iguanaRoot = [IO.Path]::GetFullPath($IguanaTexRepo)
    $iguanaSource = Join-Path $iguanaRoot "src"
    foreach ($name in $canonicalFormNames) {
        $frm = Join-Path $iguanaSource ($name + ".frm")
        $frx = Join-Path $iguanaSource ($name + ".frx")
        if (-not (Test-Path -LiteralPath $frm) -or -not (Test-Path -LiteralPath $frx)) {
            throw "Missing canonical IguanaTex form pair '$name' under '$iguanaSource'."
        }
        $forms.Add([ordered]@{ CaseName = "iguana-$name"; FileStem = $name; FormName = $name; Frm = $frm; Frx = $frx })
    }
}

New-Item -ItemType Directory -Force -Path $workRoot | Out-Null
$transcriptStarted = $false
$completed = $false
$hashesBefore = $null
try {
    Start-Transcript -LiteralPath (Join-Path $workRoot "run.log") | Out-Null
    $transcriptStarted = $true

    $hashesBefore = Get-SourceHashManifest @($forms)
    [IO.File]::WriteAllText(
        (Join-Path $workRoot "source-hashes.before.json"),
        ($hashesBefore | ConvertTo-Json -Depth 10),
        [Text.UTF8Encoding]::new($false))

    if (-not $SkipBuild) {
        & dotnet build (Join-Path $repoRoot "FrxEdit.sln") -c $Configuration --no-restore
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet build failed with exit code $LASTEXITCODE"
        }
    }

    $results = [Collections.Generic.List[object]]::new()
    foreach ($form in $forms) {
        Write-Host "Canonical round trip: $($form.CaseName)"
        $results.Add((Test-Reconstruction $form))
    }

    $hashesAfter = Get-SourceHashManifest @($forms)
    [IO.File]::WriteAllText(
        (Join-Path $workRoot "source-hashes.after.json"),
        ($hashesAfter | ConvertTo-Json -Depth 10),
        [Text.UTF8Encoding]::new($false))
    Assert-SourceHashes $hashesBefore $hashesAfter

    $summary = [ordered]@{
        schemaVersion = 1
        formCount = $results.Count
        originalStrict = @($results | Where-Object originalStrict).Count
        noOpSemantic = @($results | Where-Object noOpSemantic).Count
        positionalPatchSemantic = @($results | Where-Object positionalPatchSemantic).Count
        optionPatchSemantic = @($results | Where-Object optionPatchSemantic).Count
        watchSemantic = $(if ($SkipWatch) { 0 } else { @($results | Where-Object watchSemantic).Count })
        watchSkipped = [bool]$SkipWatch
        templateSemantic = @($results | Where-Object templateSemantic).Count
        canonicalSourceChanges = 0
        forms = @($results)
    }
    [IO.File]::WriteAllText(
        (Join-Path $workRoot "summary.json"),
        ($summary | ConvertTo-Json -Depth 20),
        [Text.UTF8Encoding]::new($false))

    $watchSummary = if ($SkipWatch) { "watch skipped" } else { "watch" }
    Write-Host "PASS: canonical no-op, exported patch, $watchSummary, and template reconstruction for $($results.Count) form(s)"
    $completed = $true
    Write-Host "Artifacts: $workRoot"
}
finally {
    $sourceIntegrityError = $null
    if ($null -ne $hashesBefore) {
        try {
            $finalHashes = Get-SourceHashManifest @($forms)
            [IO.File]::WriteAllText(
                (Join-Path $workRoot "source-hashes.after.json"),
                ($finalHashes | ConvertTo-Json -Depth 10),
                [Text.UTF8Encoding]::new($false))
            Assert-SourceHashes $hashesBefore $finalHashes
        }
        catch {
            $sourceIntegrityError = $_.Exception.Message
        }
    }
    if ($transcriptStarted) {
        Stop-Transcript | Out-Null
    }
    if (-not $completed -and (Test-Path -LiteralPath $workRoot)) {
        Write-Host "Failure artifacts: $workRoot"
    }
    if ($null -ne $sourceIntegrityError) {
        throw $sourceIntegrityError
    }
}
