[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$OriginalRaw,

    [Parameter(Mandatory)]
    [string]$CandidateRaw,

    [string]$ReportOut,

    [ValidateRange(0, 1)]
    [double]$GeometryTolerancePt = 0.02
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$semanticPropertyNames = @(
    "caption", "text", "value", "tag", "controlTipText", "controlSource", "rowSource",
    "runtimeLicKey", "helpContextId", "groupId", "groupName", "accelerator", "textAlign",
    "paragraphAlign", "backColor", "foreColor", "borderColor", "fontName", "fontSize",
    "fontWeight", "fontEffects", "fontBold", "fontItalic", "fontUnderline", "fontStrikethrough",
    "fontCharSet", "fontPitchAndFamily", "enabled", "visible", "locked", "siteBitFlags", "tabIndex", "tabStop", "default", "cancel",
    "streamed", "siteAutoSize", "preserveHeight", "fitToParent", "selectChild", "promoteControls",
    "backStyle", "alignment", "wordWrap", "autoSize", "autoTab", "autoWordSelect",
    "hideSelection", "integralHeight", "multiLine", "selectionMargin", "enterKeyBehavior",
    "tabKeyBehavior", "enterFieldBehavior", "dragBehavior", "imeMode", "takeFocusOnClick",
    "maxLength", "passwordChar", "scrollBars", "keepScrollBarsVisible", "rightToLeft",
    "specialEffect", "borderStyle", "displayStyle", "listWidth", "boundColumn", "textColumn",
    "columnCount", "listRows", "matchEntry", "listStyle", "showDropButtonWhen",
    "dropButtonStyle", "multiSelect", "columnHeads", "matchRequired", "editable", "mousePointer",
    "picturePosition", "picture", "mouseIcon", "pictureSizeMode", "pictureAlignment",
    "pictureTiling", "min", "max", "position", "smallChange", "largeChange", "orientation",
    "delay", "proportionalThumb", "logicalWidth", "logicalHeight", "scrollLeft", "scrollTop",
    "logicalWidthPt", "logicalHeightPt", "scrollLeftPt", "scrollTopPt", "tabNames",
    "tabCaptions", "tabTags", "tabTooltips", "tabAccelerators", "tabFlags", "transitionEffect",
    "transitionPeriod", "formBooleanProperties", "formDrawBuffer", "formDesignExData", "formSpecialEffect",
    "listIndex", "tabStyle", "style"
)

$rootPropertyNames = @(
    "Caption", "ClientLeft", "ClientTop", "ClientWidth", "ClientHeight", "StartUpPosition",
    "ShowModal", "Tag", "Left", "Top", "Width", "Height", "DrawBuffer", "WhatsThisButton", "WhatsThisHelp", "formBackColor",
    "formForeColor", "formBorderColor", "formCaption", "formBorderStyle", "formMousePointer",
    "formScrollBars", "formCycle", "formSpecialEffect", "formPictureAlignment",
    "formPictureSizeMode", "formZoom", "formPicture", "formMouseIcon", "formBooleanProperties", "formDrawBuffer", "formDesignExData",
    "displayedWidth", "displayedHeight", "logicalWidth", "logicalHeight", "scrollLeft", "scrollTop", "nextAvailableId",
    "formGroupCount"
)

# Defaults here are limited to documented MS-OFORMS file defaults. A missing value is equivalent to
# an explicitly stored default, but no other presence difference is normalized.
$fileDefaults = @{
    visible = $true
    tabStop = $true
    default = $false
    cancel = $false
    siteBitFlags = "0x00000033"
    streamed = $true
    siteAutoSize = $true
    preserveHeight = $false
    fitToParent = $false
    selectChild = $false
    promoteControls = $false
    fontSize = 8.0
    fontBold = $false
    fontItalic = $false
    fontUnderline = $false
    fontStrikethrough = $false
    formGroupCount = 0
}

function Get-OptionalProperty([object]$Value, [string]$Name) {
    if ($null -eq $Value) {
        return $null
    }

    return $Value.PSObject.Properties[$Name]
}

function Convert-PictureValue([object]$Value) {
    if ($Value -isnot [string] -or -not $Value.StartsWith("base64:", [StringComparison]::OrdinalIgnoreCase)) {
        return $Value
    }

    $bytes = [Convert]::FromBase64String($Value.Substring(7))
    $payload = $bytes
    $stdPictureClsid = [byte[]]@(0x04, 0x52, 0xE3, 0x0B, 0x91, 0x8F, 0xCE, 0x11, 0x9D, 0xE3, 0x00, 0xAA, 0x00, 0x4B, 0xB8, 0x51)
    $hasClsid = $bytes.Length -ge 24
    if ($hasClsid) {
        for ($i = 0; $i -lt $stdPictureClsid.Length; $i++) {
            if ($bytes[$i] -ne $stdPictureClsid[$i]) {
                $hasClsid = $false
                break
            }
        }
    }
    if ($hasClsid -and [BitConverter]::ToUInt32($bytes, 16) -eq 0x0000746C) {
        $declaredLength = [BitConverter]::ToUInt32($bytes, 20)
        if ($declaredLength -eq ($bytes.Length - 24)) {
            $payload = [byte[]]::new($declaredLength)
            [Array]::Copy($bytes, 24, $payload, 0, $declaredLength)
        }
    }

    $sha256 = [Security.Cryptography.SHA256]::Create()
    try {
        $digest = $sha256.ComputeHash($payload)
    }
    finally {
        $sha256.Dispose()
    }
    return "sha256:" + ([BitConverter]::ToString($digest) -replace "-", "").ToLowerInvariant()
}

function Convert-BinaryValue([object]$Value) {
    if ($Value -isnot [string] -or -not $Value.StartsWith("base64:", [StringComparison]::OrdinalIgnoreCase)) {
        return $Value
    }

    $bytes = [Convert]::FromBase64String($Value.Substring(7))
    $sha256 = [Security.Cryptography.SHA256]::Create()
    try {
        $digest = $sha256.ComputeHash($bytes)
    }
    finally {
        $sha256.Dispose()
    }
    return "sha256:" + ([BitConverter]::ToString($digest) -replace "-", "").ToLowerInvariant()
}

function Convert-ComparableValue([string]$PropertyName, [object]$Value) {
    if ($PropertyName -eq "siteBitFlags") {
        if ($Value -is [string] -and $Value.StartsWith("0x", [StringComparison]::OrdinalIgnoreCase)) {
            return [Convert]::ToUInt32($Value.Substring(2), 16)
        }
        return [uint32]$Value
    }

    if ($PropertyName -in @("picture", "mouseIcon", "formPicture", "formMouseIcon")) {
        return Convert-PictureValue $Value
    }

    if ($PropertyName -eq "formDesignExData") {
        return Convert-BinaryValue $Value
    }

    if ($PropertyName -eq "tabFlags") {
        $semanticFlags = @($Value | ForEach-Object {
            $raw = Get-OptionalProperty $_ "raw"
            $visible = Get-OptionalProperty $_ "visible"
            $enabled = Get-OptionalProperty $_ "enabled"
            [ordered]@{
                raw = if ($null -eq $raw) { $null } else { $raw.Value }
                visible = if ($null -eq $visible) { $null } else { $visible.Value }
                enabled = if ($null -eq $enabled) { $null } else { $enabled.Value }
            }
        })
        return ConvertTo-Json -InputObject $semanticFlags -Depth 10 -Compress
    }

    if ($Value -is [Array] -or $Value -is [Collections.IList]) {
        return ($Value | ConvertTo-Json -Depth 20 -Compress)
    }

    return $Value
}

function Test-ValuesEqual([object]$Left, [object]$Right) {
    if ($null -eq $Left -or $null -eq $Right) {
        return $null -eq $Left -and $null -eq $Right
    }

    if (($Left -is [ValueType]) -and ($Right -is [ValueType]) -and
        ($Left -isnot [bool]) -and ($Right -isnot [bool])) {
        return [decimal]$Left -eq [decimal]$Right
    }

    return [string]$Left -ceq [string]$Right
}

function Add-PropertyComparison(
    [Collections.Generic.List[object]]$HardDifferences,
    [Collections.Generic.List[object]]$Normalizations,
    [string]$Entity,
    [object]$OriginalObject,
    [object]$CandidateObject,
    [string]$PropertyName,
    [bool]$AllowFileDefault
) {
    $originalProperty = Get-OptionalProperty $OriginalObject $PropertyName
    $candidateProperty = Get-OptionalProperty $CandidateObject $PropertyName
    if ($PropertyName -ceq "caption") {
        if ($null -eq $originalProperty) { $originalProperty = Get-OptionalProperty $OriginalObject "formCaption" }
        if ($null -eq $candidateProperty) { $candidateProperty = Get-OptionalProperty $CandidateObject "formCaption" }
    }
    $originalPresent = $null -ne $originalProperty
    $candidatePresent = $null -ne $candidateProperty

    if (-not $originalPresent -and -not $candidatePresent) {
        return
    }

    $defaultPresent = $AllowFileDefault -and $fileDefaults.ContainsKey($PropertyName)
    $originalValue = if ($originalPresent) { $originalProperty.Value } elseif ($defaultPresent) { $fileDefaults[$PropertyName] } else { $null }
    $candidateValue = if ($candidatePresent) { $candidateProperty.Value } elseif ($defaultPresent) { $fileDefaults[$PropertyName] } else { $null }
    $originalComparable = Convert-ComparableValue $PropertyName $originalValue
    $candidateComparable = Convert-ComparableValue $PropertyName $candidateValue

    if ($originalPresent -ne $candidatePresent) {
        if ($defaultPresent -and (Test-ValuesEqual $originalComparable $candidateComparable)) {
            $Normalizations.Add([ordered]@{
                entity = $Entity
                property = $PropertyName
                originalPresent = $originalPresent
                candidatePresent = $candidatePresent
                effectiveValue = $originalComparable
                reason = "documented file default"
            })
            return
        }

        $HardDifferences.Add([ordered]@{
            kind = "property-presence"
            entity = $Entity
            property = $PropertyName
            originalPresent = $originalPresent
            original = $originalComparable
            candidatePresent = $candidatePresent
            candidate = $candidateComparable
        })
        return
    }

    if (Test-ValuesEqual $originalComparable $candidateComparable) {
        return
    }

    $HardDifferences.Add([ordered]@{
        kind = if ($originalPresent -eq $candidatePresent) { "property-value" } else { "property-presence" }
        entity = $Entity
        property = $PropertyName
        originalPresent = $originalPresent
        original = $originalComparable
        candidatePresent = $candidatePresent
        candidate = $candidateComparable
    })
}

function Get-ControlMap([object]$Document) {
    $result = @{}
    foreach ($control in @($Document.controls)) {
        if ($result.ContainsKey($control.name)) {
            throw "Duplicate control name '$($control.name)' in '$($Document.formName)'."
        }
        $result[$control.name] = $control
    }
    return $result
}

function Get-Parent([object]$Control) {
    $property = Get-OptionalProperty $Control "parent"
    if ($null -eq $property -or [string]::IsNullOrWhiteSpace([string]$property.Value)) {
        return "<root>"
    }
    return [string]$property.Value
}

function Get-MultiPageOrders([object]$Document) {
    $result = [ordered]@{}
    $pagesByParent = @($Document.controls | Where-Object type -EQ "Page" | Group-Object { Get-Parent $_ })
    foreach ($group in $pagesByParent) {
        $orderedPages = @($group.Group | Sort-Object {
            $index = Get-OptionalProperty $_.properties "multiPagePageIndex"
            if ($null -eq $index) { [int]::MaxValue } else { [int]$index.Value }
        }, name)
        $result[$group.Name] = @($orderedPages.name)
    }
    return $result
}

function Get-NativeSiteOrders([object]$Document) {
    $result = [ordered]@{}
    $ownerByStorage = @{ "Root Entry" = "<root>" }
    foreach ($control in @($Document.controls)) {
        $ownedStorage = Get-OptionalProperty $control.properties "ownedStoragePath"
        if ($null -ne $ownedStorage -and -not [string]::IsNullOrWhiteSpace([string]$ownedStorage.Value)) {
            $ownerByStorage[[string]$ownedStorage.Value] = $control.name
        }
    }
    $groups = @($Document.controls | Group-Object {
        $storage = Get-OptionalProperty $_.properties "storagePath"
        if ($null -eq $storage) { "<unknown>" } else { [string]$storage.Value }
    })
    foreach ($group in $groups) {
        $orderedSites = @($group.Group | Sort-Object {
            $offset = Get-OptionalProperty $_.properties "siteLocalOffset"
            if ($null -eq $offset) { [long]::MaxValue } else { [long]$offset.Value }
        }, name)
        $scope = if ($ownerByStorage.ContainsKey($group.Name)) { $ownerByStorage[$group.Name] } else { $group.Name }
        $result[$scope] = @($orderedSites.name)
    }
    return $result
}

function Get-NativeObjectPayloadOrders([object]$Document) {
    $result = [ordered]@{}
    $ownerByStorage = @{ "Root Entry" = "<root>" }
    foreach ($control in @($Document.controls)) {
        $ownedStorage = Get-OptionalProperty $control.properties "ownedStoragePath"
        if ($null -ne $ownedStorage -and -not [string]::IsNullOrWhiteSpace([string]$ownedStorage.Value)) {
            $ownerByStorage[[string]$ownedStorage.Value] = $control.name
        }
    }

    $streamedControls = @($Document.controls | Where-Object {
        $offset = Get-OptionalProperty $_.properties "objectStreamLocalOffset"
        $size = Get-OptionalProperty $_.properties "objectStreamSize"
        $null -ne $offset -and $null -ne $size -and [long]$size.Value -gt 0
    })
    $groups = @($streamedControls | Group-Object {
        $storage = Get-OptionalProperty $_.properties "storagePath"
        if ($null -eq $storage) { "<unknown>" } else { [string]$storage.Value }
    })
    foreach ($group in $groups) {
        $orderedPayloads = @($group.Group | Sort-Object {
            [long](Get-OptionalProperty $_.properties "objectStreamLocalOffset").Value
        }, name)
        $scope = if ($ownerByStorage.ContainsKey($group.Name)) { $ownerByStorage[$group.Name] } else { $group.Name }
        $result[$scope] = @($orderedPayloads.name)
    }
    return $result
}

function Compare-NamedSequences([object]$Original, [object]$Candidate, [string]$Kind) {
    $differences = [Collections.Generic.List[object]]::new()
    $names = @($Original.Keys) + @($Candidate.Keys) | Sort-Object -Unique
    foreach ($name in $names) {
        $originalValue = if ($Original.Contains($name)) { @($Original[$name]) -join "," } else { $null }
        $candidateValue = if ($Candidate.Contains($name)) { @($Candidate[$name]) -join "," } else { $null }
        if ($originalValue -cne $candidateValue) {
            $differences.Add([ordered]@{
                kind = $Kind
                scope = $name
                original = $originalValue
                candidate = $candidateValue
            })
        }
    }
    return @($differences)
}

$original = Get-Content -Raw -LiteralPath $OriginalRaw | ConvertFrom-Json
$candidate = Get-Content -Raw -LiteralPath $CandidateRaw | ConvertFrom-Json
$hardDifferences = [Collections.Generic.List[object]]::new()
$normalizations = [Collections.Generic.List[object]]::new()

if ([string]$original.formName -cne [string]$candidate.formName) {
    $hardDifferences.Add([ordered]@{
        kind = "form-name"
        entity = "<form>"
        original = [string]$original.formName
        candidate = [string]$candidate.formName
    })
}

$originalControls = Get-ControlMap $original
$candidateControls = Get-ControlMap $candidate
$allControlNames = @($originalControls.Keys) + @($candidateControls.Keys) | Sort-Object -Unique
foreach ($name in $allControlNames) {
    if (-not $originalControls.ContainsKey($name)) {
        $hardDifferences.Add([ordered]@{ kind = "extra-control"; entity = $name; candidateType = $candidateControls[$name].type })
        continue
    }
    if (-not $candidateControls.ContainsKey($name)) {
        $hardDifferences.Add([ordered]@{ kind = "missing-control"; entity = $name; originalType = $originalControls[$name].type })
        continue
    }

    $before = $originalControls[$name]
    $after = $candidateControls[$name]
    if ($before.type -cne $after.type) {
        $hardDifferences.Add([ordered]@{ kind = "control-type"; entity = $name; original = $before.type; candidate = $after.type })
    }
    $beforeParent = Get-Parent $before
    $afterParent = Get-Parent $after
    if ($beforeParent -cne $afterParent) {
        $hardDifferences.Add([ordered]@{ kind = "control-parent"; entity = $name; original = $beforeParent; candidate = $afterParent })
    }

    foreach ($coordinate in @(
        @{ Raw = "left"; Points = "leftPt" },
        @{ Raw = "top"; Points = "topPt" },
        @{ Raw = "rawWidth"; Points = "widthPt" },
        @{ Raw = "rawHeight"; Points = "heightPt" }
    )) {
        $beforeRaw = Get-OptionalProperty $before $coordinate.Raw
        $afterRaw = Get-OptionalProperty $after $coordinate.Raw
        $geometryMatches = if ($null -ne $beforeRaw -and $null -ne $afterRaw) {
            [long]$beforeRaw.Value -eq [long]$afterRaw.Value
        }
        else {
            [Math]::Abs([double]$before.($coordinate.Points) - [double]$after.($coordinate.Points)) -le $GeometryTolerancePt
        }
        if (-not $geometryMatches) {
            $hardDifferences.Add([ordered]@{
                kind = "geometry"
                entity = $name
                property = $coordinate.Raw
                original = if ($null -ne $beforeRaw) { $beforeRaw.Value } else { $before.($coordinate.Points) }
                candidate = if ($null -ne $afterRaw) { $afterRaw.Value } else { $after.($coordinate.Points) }
            })
        }
    }

    foreach ($propertyName in $semanticPropertyNames) {
        Add-PropertyComparison $hardDifferences $normalizations $name $before.properties $after.properties $propertyName $true
    }
}

$originalRoot = $original.frxFormControl
$candidateRoot = $candidate.frxFormControl
foreach ($propertyName in $rootPropertyNames) {
    Add-PropertyComparison $hardDifferences $normalizations "<form>" $originalRoot $candidateRoot $propertyName ($propertyName -ceq "formGroupCount")
}

$multiPageOrderDifferences = @(Compare-NamedSequences (Get-MultiPageOrders $original) (Get-MultiPageOrders $candidate) "multipage-page-order")
foreach ($difference in $multiPageOrderDifferences) {
    $hardDifferences.Add($difference)
}

$candidateValidation = $candidate.parserValidation
foreach ($counter in @("warningCount", "errorCount", "heuristicCount")) {
    $counterProperty = Get-OptionalProperty $candidateValidation $counter
    if ($null -ne $counterProperty -and [int]$counterProperty.Value -ne 0) {
        $hardDifferences.Add([ordered]@{ kind = "parser-validation"; entity = "<form>"; property = $counter; candidate = $counterProperty.Value })
    }
}

foreach ($validationGroupName in @("objectStreamValidations", "multiPageXStreamValidations")) {
    $validationGroupProperty = Get-OptionalProperty $candidateValidation $validationGroupName
    if ($null -eq $validationGroupProperty) {
        continue
    }
    foreach ($entry in $validationGroupProperty.Value.PSObject.Properties) {
        $validationProperty = Get-OptionalProperty $entry.Value "validation"
        if ($null -eq $validationProperty -or [string]$validationProperty.Value -cne "exact") {
            $hardDifferences.Add([ordered]@{
                kind = "parser-stream-validation"
                entity = $entry.Name
                property = $validationGroupName
                candidate = if ($null -eq $validationProperty) { $null } else { $validationProperty.Value }
            })
        }
    }
}

$nativeSiteDifferences = @(Compare-NamedSequences (Get-NativeSiteOrders $original) (Get-NativeSiteOrders $candidate) "native-site-order")
$nativeObjectPayloadDifferences = @(Compare-NamedSequences (Get-NativeObjectPayloadOrders $original) (Get-NativeObjectPayloadOrders $candidate) "native-object-payload-order")
$nativeShapeCookieDifferences = @()
$originalShapeCookie = Get-OptionalProperty $originalRoot "formShapeCookie"
$candidateShapeCookie = Get-OptionalProperty $candidateRoot "formShapeCookie"
if (($null -ne $originalShapeCookie) -or ($null -ne $candidateShapeCookie)) {
    $originalShapeCookieValue = if ($null -eq $originalShapeCookie) { 0 } else { [uint32]$originalShapeCookie.Value }
    $candidateShapeCookieValue = if ($null -eq $candidateShapeCookie) { 0 } else { [uint32]$candidateShapeCookie.Value }
    if ($originalShapeCookieValue -ne $candidateShapeCookieValue) {
        $nativeShapeCookieDifferences = @([ordered]@{
            kind = "native-form-shape-cookie"
            scope = "<form>"
            original = $originalShapeCookieValue
            candidate = $candidateShapeCookieValue
        })
    }
}
$nativeStructureDifferences = @($nativeSiteDifferences) + @($nativeObjectPayloadDifferences) + @($nativeShapeCookieDifferences)
$report = [ordered]@{
    schemaVersion = 1
    original = [IO.Path]::GetFullPath($OriginalRaw)
    candidate = [IO.Path]::GetFullPath($CandidateRaw)
    semanticEqual = $hardDifferences.Count -eq 0
    hardDifferenceCount = $hardDifferences.Count
    normalizedDefaultCount = $normalizations.Count
    nativeStructureEqual = $nativeStructureDifferences.Count -eq 0
    nativeStructureDifferenceCount = $nativeStructureDifferences.Count
    nativeSiteOrderDifferenceCount = $nativeSiteDifferences.Count
    nativeObjectPayloadOrderDifferenceCount = $nativeObjectPayloadDifferences.Count
    nativeShapeCookieDifferenceCount = $nativeShapeCookieDifferences.Count
    hardDifferences = @($hardDifferences)
    normalizedDefaults = @($normalizations)
    nativeStructureDifferences = @($nativeStructureDifferences)
}

$json = $report | ConvertTo-Json -Depth 50
if (-not [string]::IsNullOrWhiteSpace($ReportOut)) {
    $parent = Split-Path -Parent ([IO.Path]::GetFullPath($ReportOut))
    if (-not [string]::IsNullOrWhiteSpace($parent)) {
        New-Item -ItemType Directory -Force -Path $parent | Out-Null
    }
    [IO.File]::WriteAllText([IO.Path]::GetFullPath($ReportOut), $json, [Text.UTF8Encoding]::new($false))
}

if ($report.semanticEqual) {
    Write-Host "PASS: canonical semantics match ($($normalizations.Count) documented-default normalization(s); $($nativeStructureDifferences.Count) native-structure difference(s))"
    return
}

throw "FAIL: $($hardDifferences.Count) hard canonical semantic difference(s)."
