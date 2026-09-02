internal static class RebuildPatchApplier
{
    private static readonly HashSet<string> ObjectPropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "caption",
        "value",
        "listIndex",
        "style",
        "tabStyle",
        "groupName",
        "fontName",
        "fontSize",
        "fontWeight",
        "fontEffects",
        "fontBold",
        "fontItalic",
        "fontUnderline",
        "fontStrikethrough",
        "fontCharSet",
        "fontPitchAndFamily",
        "backColor",
        "foreColor",
        "borderColor",
        "enabled",
        "locked",
        "backStyle",
        "wordWrap",
        "autoSize",
        "imeMode",
        "picturePosition",
        "mousePointer",
        "accelerator",
        "alignment",
        "takeFocusOnClick",
        "borderStyle",
        "specialEffect",
        "formSpecialEffect",
        "textAlign",
        "paragraphAlign",
        "maxLength",
        "passwordChar",
        "scrollBars",
        "displayStyle",
        "listWidth",
        "boundColumn",
        "textColumn",
        "columnCount",
        "listRows",
        "matchEntry",
        "listStyle",
        "showDropButtonWhen",
        "dropButtonStyle",
        "multiSelect",
        "dragBehavior",
        "enterFieldBehavior",
        "enterKeyBehavior",
        "tabKeyBehavior",
        "selectionMargin",
        "autoWordSelect",
        "hideSelection",
        "autoTab",
        "multiLine",
        "integralHeight",
        "columnHeads",
        "matchRequired",
        "editable",
        "picture",
        "mouseIcon",
        "pictureSizeMode",
        "pictureAlignment",
        "pictureTiling",
        "keepScrollBarsVisible",
        "rightToLeft",
        "min",
        "max",
        "position",
        "smallChange",
        "largeChange",
        "orientation",
        "delay",
        "proportionalThumb",
        "logicalWidth",
        "logicalHeight",
        "scrollLeft",
        "scrollTop",
        "logicalWidthPt",
        "logicalHeightPt",
        "scrollLeftPt",
        "scrollTopPt",
        "formBooleanProperties",
        "formDrawBuffer",
        "drawBuffer",
        "tabCaptions",
        "tabTooltips",
        "tabNames",
        "tabTags",
        "tabAccelerators",
        "tabFlags",
        "pageNames",
        "pageCaptions"
    };

    private static readonly HashSet<string> FormSitePropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "siteBitFlags",
        "tabIndex",
        "controlTipText",
        "controlSource",
        "rowSource",
        "tag",
        "helpContextId",
        "groupId",
        "tabStop",
        "visible",
        "default",
        "cancel",
        "siteAutoSize",
        "preserveHeight",
        "fitToParent",
        "selectChild"
    };

    private static readonly HashSet<string> RootFormPropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "formBackColor",
        "formForeColor",
        "formBorderColor",
        "formCaption",
        "formBorderStyle",
        "formMousePointer",
        "formScrollBars",
        "formCycle",
        "formSpecialEffect",
        "formPictureAlignment",
        "formPictureSizeMode",
        "formZoom",
        "formGroupCount",
        "backColor",
        "foreColor",
        "borderColor",
        "borderStyle",
        "mousePointer",
        "scrollBars",
        "cycle",
        "specialEffect",
        "pictureAlignment",
        "pictureSizeMode",
        "zoom",
        "nextAvailableId",
        "displayedWidth",
        "displayedHeight",
        "displayedWidthPt",
        "displayedHeightPt",
        "widthPt",
        "heightPt",
        "logicalWidth",
        "logicalHeight",
        "logicalWidthPt",
        "logicalHeightPt",
        "scrollLeft",
        "scrollTop",
        "scrollLeftPt",
        "scrollTopPt",
        "caption",
        "clientWidth",
        "clientHeight",
        "clientLeft",
        "clientTop",
        "left",
        "top",
        "width",
        "height",
        "startUpPosition",
        "showModal",
        "whatsThisButton",
        "whatsThisHelp",
        "tag",
        "drawBuffer",
        "formDrawBuffer",
        "formBooleanProperties",
        "enabled",
        "pictureTiling",
        "keepScrollBarsVisible",
        "rightToLeft"
    };

    private static readonly string[] FontPropertyNames =
    [
        "fontName", "fontSize", "fontWeight", "fontEffects", "fontBold", "fontItalic",
        "fontUnderline", "fontStrikethrough", "fontCharSet", "fontPitchAndFamily",
        "textAlign", "paragraphAlign"
    ];

    private static readonly string[] MorphVariousPropertyNames =
    [
        "enabled", "locked", "backStyle", "alignment", "wordWrap", "autoSize", "autoTab",
        "autoWordSelect", "hideSelection", "integralHeight", "multiLine", "selectionMargin",
        "enterKeyBehavior", "tabKeyBehavior", "enterFieldBehavior", "dragBehavior", "imeMode",
        "columnHeads", "matchRequired", "editable"
    ];

    private static readonly string[] ContainerPropertyNames =
    [
        "enabled", "pictureTiling", "keepScrollBarsVisible", "rightToLeft",
        "logicalWidth", "logicalHeight", "scrollLeft", "scrollTop",
        "logicalWidthPt", "logicalHeightPt", "scrollLeftPt", "scrollTopPt",
        "formBooleanProperties", "formDrawBuffer", "drawBuffer"
    ];

    private static readonly string[] TabArrayPropertyNames =
    [
        "tabCaptions", "tabTooltips", "tabNames", "tabTags", "tabAccelerators", "tabFlags",
        "pageNames", "pageCaptions"
    ];

    public static LayoutInspection ApplyObjectPropertyPatch(LayoutInspection source, PatchDocument patch, bool allowFormSitePatch = false, string? formName = null, string? patchDir = null, WriterProvenanceAuditCollector? writerAudit = null)
    {
        ValidateObjectPatch(patch, allowFormSitePatch, formName);

        if ((patch.Properties is null || patch.Properties.Count == 0) &&
            (!allowFormSitePatch || patch.Layout is null || patch.Layout.Count == 0) &&
            (!allowFormSitePatch || patch.Renames is null || patch.Renames.Count == 0) &&
            (!allowFormSitePatch || patch.Move is null || patch.Move.Count == 0) &&
            (!allowFormSitePatch || patch.Add is null || patch.Add.Count == 0) &&
            (!allowFormSitePatch || patch.Remove is null || patch.Remove.Count == 0))
        {
            return source;
        }

        var patchedByName = patch.Properties?.ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, Dictionary<string, JsonElement>>(StringComparer.OrdinalIgnoreCase);

        var frxFormControl = source.FrxFormControl;
        if (frxFormControl is not null)
        {
            var formKeys = new[] { formName, "UserForm", "Form", "root" };
            foreach (var key in formKeys)
            {
                if (key is not null && patchedByName.TryGetValue(key, out var formPatches))
                {
                    var originalFrxFormControl = frxFormControl;
                    var newFrxFormControl = new Dictionary<string, object?>(frxFormControl, StringComparer.OrdinalIgnoreCase);
                    foreach (var (propName, propVal) in OrderPropertyApplications(formPatches))
                    {
                        ApplyFormPropertyToDictionary(key, newFrxFormControl, propName, propVal, patchDir);
                    }
                    foreach (var propertyName in formPatches.Keys)
                    {
                        if (!ReconstructionIntentBuilder.RootPropertyEqual(originalFrxFormControl, newFrxFormControl, propertyName))
                        {
                            ValidateExistingRootMutation(key, originalFrxFormControl, propertyName);
                        }
                    }
                    frxFormControl = newFrxFormControl;
                    break;
                }
            }
        }

        var layoutByName = allowFormSitePatch
            ? patch.Layout?.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, LayoutPatch>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, LayoutPatch>(StringComparer.OrdinalIgnoreCase);

        var renameByName = allowFormSitePatch
            ? patch.Renames?.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var moveByName = allowFormSitePatch
            ? patch.Move?.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        var removeRequests = allowFormSitePatch
            ? (patch.Remove ?? []).Concat(moveByName.Keys).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var removalPlan = allowFormSitePatch
            ? BuildRemovalPlan(source.Controls, removeRequests)
            : new RemovalPlan(new HashSet<string>(StringComparer.OrdinalIgnoreCase), []);

        var removedControls = new List<ControlInfo>();
        var controls = new List<ControlInfo>(source.Controls.Count + (patch.Add?.Count ?? 0));
        foreach (var control in source.Controls)
        {
            if (removalPlan.ControlNames.Contains(control.Name))
            {
                if (removeRequests.Contains(control.Name))
                {
                    ValidateRemovedControl(source.Controls, control);
                }

                removedControls.Add(control);
                continue;
            }

            renameByName.TryGetValue(control.Name, out var newName);
            patchedByName.TryGetValue(control.Name, out var requested);
            layoutByName.TryGetValue(control.Name, out var layout);

            if (!string.IsNullOrWhiteSpace(newName))
            {
                if (requested is null)
                {
                    patchedByName.TryGetValue(newName, out requested);
                }

                if (layout is null)
                {
                    layoutByName.TryGetValue(newName, out layout);
                }
            }

            if (requested is null && layout is null && string.IsNullOrWhiteSpace(newName))
            {
                controls.Add(control);
                continue;
            }

            controls.Add(ApplyToControl(control, requested, layout, newName, patchDir));
        }

        if (renameByName.Count > 0)
        {
            controls = controls
                .Select(control => control.Parent is not null && renameByName.TryGetValue(control.Parent, out var renamedParent)
                    ? control with { Parent = renamedParent }
                    : control)
                .ToList();
        }

        if (allowFormSitePatch && moveByName.Count > 0)
        {
            controls.AddRange(BuildMovedControls(source.Controls, controls, moveByName, patchedByName, layoutByName, patchDir, writerAudit));
        }

        if (allowFormSitePatch && patch.Add is { Count: > 0 })
        {
            controls.AddRange(BuildAddedControls(source.Controls, controls, patch.Add, patchDir, patchedByName, layoutByName, writerAudit));
        }

        controls = SynchronizeMultiPageMetadata(source.Controls, controls, patchedByName, renameByName);

        var hasMaterializedAdditions = controls.Any(control =>
            control.Properties is not null && IsAddedControlMetadata(control.Properties));
        if (allowFormSitePatch &&
            (hasMaterializedAdditions || removedControls.Count > 0 || patch.Move is { Count: > 0 }) &&
            !PatchRequestsRootProperty(patch, formName, "nextAvailableId"))
        {
            frxFormControl = SynchronizeRootNextAvailableId(frxFormControl, controls);
        }

        return source with { Controls = controls, RemovedControls = removedControls, RemovedStoragePaths = removalPlan.StoragePaths, FrxFormControl = frxFormControl };
    }

    private static bool PatchRequestsRootProperty(PatchDocument patch, string? formName, string propertyName)
    {
        if (patch.Properties is null)
        {
            return false;
        }

        foreach (var key in new[] { formName, "UserForm", "Form", "root" })
        {
            if (!string.IsNullOrWhiteSpace(key) &&
                patch.Properties.TryGetValue(key, out var properties) &&
                properties.ContainsKey(propertyName))
            {
                return true;
            }
        }

        return false;
    }

    private static Dictionary<string, object?>? SynchronizeRootNextAvailableId(
        Dictionary<string, object?>? formProperties,
        IReadOnlyList<ControlInfo> controls)
    {
        if (formProperties is null ||
            (!formProperties.ContainsKey("nextAvailableId") && !formProperties.ContainsKey("nextAvailableIdOffset")))
        {
            return formProperties;
        }

        var maxDirectSiteId = controls
            .Where(control => string.IsNullOrWhiteSpace(control.Parent))
            .Select(control => control.Properties is not null && TryGetInt(control.Properties, "siteId", out var siteId)
                ? siteId
                : control.Properties is not null && TryGetInt(control.Properties, "id", out var id)
                    ? id
                    : 0)
            .DefaultIfEmpty(0)
            .Max();
        if (maxDirectSiteId <= 0)
        {
            return formProperties;
        }

        var synchronized = new Dictionary<string, object?>(formProperties, StringComparer.OrdinalIgnoreCase)
        {
            ["nextAvailableId"] = checked((uint)(maxDirectSiteId + 1))
        };
        return synchronized;
    }

    private static List<ControlInfo> SynchronizeMultiPageMetadata(
        IReadOnlyList<ControlInfo> sourceControls,
        List<ControlInfo> targetControls,
        IReadOnlyDictionary<string, Dictionary<string, JsonElement>> patchedByName,
        IReadOnlyDictionary<string, string> renameByName)
    {
        var sourceByName = sourceControls.ToDictionary(control => control.Name, StringComparer.OrdinalIgnoreCase);
        var reverseRenames = renameByName.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.OrdinalIgnoreCase);
        var result = targetControls.ToList();

        for (var controlIndex = 0; controlIndex < result.Count; controlIndex++)
        {
            var multiPage = result[controlIndex];
            if (!multiPage.Type.Equals("MultiPage", StringComparison.OrdinalIgnoreCase) || multiPage.Properties is null)
            {
                continue;
            }

            var sourceName = reverseRenames.TryGetValue(multiPage.Name, out var renamedSourceName)
                ? renamedSourceName
                : multiPage.Name;
            if (!sourceByName.TryGetValue(sourceName, out var sourceMultiPage) || sourceMultiPage.Properties is null)
            {
                // Generated MultiPages already receive metadata from the exact TabStrip schema
                // request that produced their internal object stream.
                continue;
            }

            patchedByName.TryGetValue(multiPage.Name, out var requestedMultiPageProperties);
            if (requestedMultiPageProperties is null && !sourceName.Equals(multiPage.Name, StringComparison.OrdinalIgnoreCase))
            {
                patchedByName.TryGetValue(sourceName, out requestedMultiPageProperties);
            }

            var pages = result
                .Where(control => control.Type.Equals("Page", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(control.Parent, multiPage.Name, StringComparison.OrdinalIgnoreCase))
                .OrderBy(control => control.Properties is not null && TryGetInt(control.Properties, "multiPagePageIndex", out var pageIndex)
                    ? pageIndex
                    : int.MaxValue)
                .ThenBy(control => control.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (pages.Count == 0)
            {
                continue;
            }

            var properties = new Dictionary<string, object?>(multiPage.Properties, StringComparer.OrdinalIgnoreCase);
            properties["tabNames"] = BuildEffectiveMultiPageStrings(
                "tabNames", pages, sourceMultiPage.Properties, requestedMultiPageProperties,
                page => page.Name);
            properties["tabCaptions"] = BuildEffectiveMultiPageStrings(
                "tabCaptions", pages, sourceMultiPage.Properties, requestedMultiPageProperties,
                GetPageCaptionForMetadata, useExplicitPageCaption: true);
            properties["tabTooltips"] = BuildEffectiveMultiPageStrings(
                "tabTooltips", pages, sourceMultiPage.Properties, requestedMultiPageProperties, _ => string.Empty);
            properties["tabTags"] = BuildEffectiveMultiPageStrings(
                "tabTags", pages, sourceMultiPage.Properties, requestedMultiPageProperties, _ => string.Empty);
            properties["tabAccelerators"] = BuildEffectiveMultiPageStrings(
                "tabAccelerators", pages, sourceMultiPage.Properties, requestedMultiPageProperties, _ => string.Empty);
            properties["tabFlags"] = BuildEffectiveMultiPageFlags(
                pages, sourceMultiPage.Properties, requestedMultiPageProperties);

            result[controlIndex] = multiPage with { Properties = properties };
        }

        return result;
    }

    private static IReadOnlyList<string> BuildEffectiveMultiPageStrings(
        string propertyName,
        IReadOnlyList<ControlInfo> pages,
        Dictionary<string, object?> sourceProperties,
        IReadOnlyDictionary<string, JsonElement>? requestedProperties,
        Func<ControlInfo, string> fallback,
        bool alwaysUseFallback = false,
        bool useExplicitPageCaption = false)
    {
        if (requestedProperties is not null &&
            requestedProperties.ContainsKey(propertyName) &&
            MsFormsFactoryBinary.GetStringList(
                new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    [propertyName] = requestedProperties[propertyName]
                },
                propertyName) is { } explicitlyRequested)
        {
            if (explicitlyRequested.Count != pages.Count)
            {
                throw new CliException($"MultiPage property '{propertyName}' contains {explicitlyRequested.Count} entries; expected {pages.Count}.");
            }

            return explicitlyRequested.ToArray();
        }

        var originalValues = MsFormsFactoryBinary.GetStringList(sourceProperties, propertyName);
        var values = new List<string>(pages.Count);
        foreach (var page in pages)
        {
            var value = fallback(page);
            var pageProperties = page.Properties;
            var hasExplicitPageCaption = useExplicitPageCaption &&
                pageProperties is not null &&
                (pageProperties.ContainsKey("caption") ||
                 pageProperties.ContainsKey("tabCaption") ||
                 pageProperties.ContainsKey("formCaption"));
            if (!alwaysUseFallback &&
                !hasExplicitPageCaption &&
                pageProperties is not null &&
                !IsAddedControlMetadata(pageProperties) &&
                TryGetInt(pageProperties, "multiPagePageIndex", out var originalIndex) &&
                originalValues is not null &&
                originalIndex >= 0 && originalIndex < originalValues.Count)
            {
                value = originalValues[originalIndex];
            }

            values.Add(value);
        }

        return values;
    }

    private static IReadOnlyList<Dictionary<string, object?>> BuildEffectiveMultiPageFlags(
        IReadOnlyList<ControlInfo> pages,
        Dictionary<string, object?> sourceProperties,
        IReadOnlyDictionary<string, JsonElement>? requestedProperties)
    {
        IReadOnlyList<uint>? requestedFlags = null;
        if (requestedProperties is not null && requestedProperties.TryGetValue("tabFlags", out var requestedValue))
        {
            requestedFlags = MsFormsFactoryBinary.GetUInt32List(
                new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["tabFlags"] = requestedValue },
                "tabFlags");
            if (requestedFlags is null || requestedFlags.Count != pages.Count)
            {
                throw new CliException($"MultiPage property 'tabFlags' contains {requestedFlags?.Count ?? 0} entries; expected {pages.Count}.");
            }
        }

        var sourceFlags = GetMultiPageFlagValues(sourceProperties, "tabFlags") ?? [];
        var defaultFlag = sourceFlags.Count > 0 ? sourceFlags[0] : 3u;
        var effective = new List<uint>(pages.Count);
        for (var index = 0; index < pages.Count; index++)
        {
            if (requestedFlags is not null)
            {
                effective.Add(requestedFlags[index]);
                continue;
            }

            var page = pages[index];
            var pageProperties = page.Properties;
            var flag = defaultFlag;
            if (pageProperties is not null &&
                !IsAddedControlMetadata(pageProperties) &&
                TryGetInt(pageProperties, "multiPagePageIndex", out var originalIndex) &&
                originalIndex >= 0 && originalIndex < sourceFlags.Count)
            {
                flag = sourceFlags[originalIndex];
            }
            effective.Add(flag);
        }

        return effective.Select(flag => new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["raw"] = flag,
            ["visible"] = (flag & 0x0000_0001u) != 0,
            ["enabled"] = (flag & 0x0000_0002u) != 0
        }).ToList();
    }

    private static IReadOnlyList<uint>? GetMultiPageFlagValues(
        Dictionary<string, object?> properties,
        string propertyName)
    {
        if (MsFormsFactoryBinary.GetUInt32List(properties, propertyName) is { } directValues)
        {
            return directValues;
        }

        if (!properties.TryGetValue(propertyName, out var raw) ||
            raw is not IEnumerable<Dictionary<string, object?>> dictionaries)
        {
            return null;
        }

        var values = new List<uint>();
        foreach (var dictionary in dictionaries)
        {
            if (MsFormsFactoryBinary.GetUInt32(dictionary, "raw") is not uint value)
            {
                return null;
            }
            values.Add(value);
        }

        return values;
    }

    private static bool IsAddedControlMetadata(Dictionary<string, object?> properties) =>
        MsFormsFactoryBinary.GetBool(properties, "isAddedControl") == true;

    private static string GetPageCaptionForMetadata(ControlInfo page)
    {
        if (page.Properties is not null)
        {
            if (TryGetString(page.Properties, "tabCaption", out var tabCaption)) return tabCaption;
            if (TryGetString(page.Properties, "caption", out var caption)) return caption;
            if (TryGetString(page.Properties, "formCaption", out var formCaption)) return formCaption;
        }

        return page.Name;
    }

    public static void ValidateObjectPatch(PatchDocument patch, bool allowFormSitePatch = false, string? formName = null)
    {
        if (patch.Add is { Count: > 0 } && !allowFormSitePatch)
        {
            throw new CliException("Rebuild object-patch does not support 'add' because new controls require FormSiteData rebuild. Use '--stream-mode full-patch'.");
        }

        if (patch.Move is { Count: > 0 } && !allowFormSitePatch)
        {
            throw new CliException("Rebuild object-patch does not support 'move' because moving controls requires FormSiteData rebuild. Use '--stream-mode full-patch'.");
        }

        if (patch.Remove is { Count: > 0 } && !allowFormSitePatch)
        {
            throw new CliException("Rebuild object-patch does not support 'remove' because removing controls requires FormSiteData rebuild. Use '--stream-mode full-patch'.");
        }

        if (patch.Renames is { Count: > 0 } && !allowFormSitePatch)
        {
            throw new CliException("Rebuild object-patch does not support 'renames' because control names live in FormSiteData. Use '--stream-mode full-patch' for rebuild renames.");
        }

        if (patch.Layout is { Count: > 0 } && !allowFormSitePatch)
        {
            throw new CliException("Rebuild object-patch does not support 'layout'. Use '--stream-mode full-patch' for rebuild layout edits.");
        }

        foreach (var (controlName, properties) in patch.Properties ?? new Dictionary<string, Dictionary<string, JsonElement>>(StringComparer.OrdinalIgnoreCase))
        {
            var isForm = (formName is not null && string.Equals(controlName, formName, StringComparison.OrdinalIgnoreCase)) ||
                         string.Equals(controlName, "UserForm", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(controlName, "Form", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(controlName, "root", StringComparison.OrdinalIgnoreCase);

            foreach (var propertyName in properties.Keys)
            {
                if (isForm)
                {
                    if (!RootFormPropertyNames.Contains(propertyName))
                    {
                        throw new CliException($"Property '{propertyName}' is not supported for the root UserForm.");
                    }
                }
                else
                {
                    if (!ObjectPropertyNames.Contains(propertyName) && !(allowFormSitePatch && FormSitePropertyNames.Contains(propertyName)))
                    {
                        throw new CliException($"Property '{propertyName}' is not supported for control '{controlName}'.");
                    }
                }
            }
        }

        foreach (var name in patch.Remove ?? [])
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new CliException("Each remove entry requires a non-empty control name.");
            }
        }

        foreach (var (name, _) in patch.Move ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new CliException("Each move entry requires a non-empty source control name.");
            }
        }

        foreach (var add in patch.Add ?? [])
        {
            if (string.IsNullOrWhiteSpace(add.Name))
            {
                throw new CliException("Each add entry requires a non-empty 'name'.");
            }

            if (string.IsNullOrWhiteSpace(add.FromTemplate) && string.IsNullOrWhiteSpace(add.Type))
            {
                throw new CliException($"Add entry '{add.Name}' requires either 'fromTemplate' or 'type'.");
            }

            if (string.IsNullOrWhiteSpace(add.FromTemplate) &&
                !GeneratedControlFactory.CanCreate(add.Type!) &&
                !GeneratedStorageFactory.CanCreate(add.Type!))
            {
                throw new CliException($"Add entry '{add.Name}' requested type '{add.Type}', but this build can create only: {GeneratedControlFactory.SupportedTypes}, Frame, MultiPage, Page. Use 'fromTemplate' for other types.");
            }
        }
    }

    private sealed record RemovalPlan(HashSet<string> ControlNames, IReadOnlyList<string> StoragePaths);

    private static RemovalPlan BuildRemovalPlan(IReadOnlyList<ControlInfo> controls, HashSet<string> requestedNames)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var storagePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (requestedNames.Count == 0)
        {
            return new RemovalPlan(names, []);
        }

        var byName = controls.ToDictionary(control => control.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var requested in requestedNames)
        {
            if (!byName.TryGetValue(requested, out var root))
            {
                throw new CliException($"Cannot remove '{requested}': control does not exist.");
            }

            CollectSubtree(root, controls, names);
            if (TryGetOwnedStoragePath(root, controls, out var storagePath))
            {
                storagePaths.Add(storagePath);
            }
        }

        return new RemovalPlan(names, storagePaths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList());
    }

    private static void CollectSubtree(ControlInfo root, IReadOnlyList<ControlInfo> controls, HashSet<string> names)
    {
        if (!names.Add(root.Name))
        {
            return;
        }

        foreach (var child in controls.Where(candidate => string.Equals(candidate.Parent, root.Name, StringComparison.OrdinalIgnoreCase)))
        {
            CollectSubtree(child, controls, names);
        }
    }

    private static void ValidateRemovedControl(IReadOnlyList<ControlInfo> controls, ControlInfo control)
    {
        if (control.Properties is null ||
            !TryGetString(control.Properties, "siteParser", out var siteParser) ||
            !siteParser.Equals("msOFormsOleSiteConcrete", StringComparison.OrdinalIgnoreCase))
        {
            throw new CliException($"Cannot remove '{control.Name}': the control does not expose a documented OleSiteConcrete site.");
        }

        var hasChildren = controls.Any(candidate => string.Equals(candidate.Parent, control.Name, StringComparison.OrdinalIgnoreCase));
        if (!hasChildren)
        {
            if (TryGetInt(control.Properties, "objectStreamSize", out var objectStreamSize) && objectStreamSize > 0)
            {
                return;
            }

            if (IsStorageParentType(control.Type))
            {
                if (!TryGetOwnedStoragePath(control, controls, out _))
                {
                    throw new CliException($"Cannot remove '{control.Name}': could not determine the owned storage path to remove.");
                }

                EnsurePageRemovalLeavesSibling(control, controls);
                return;
            }

            throw new CliException($"Cannot remove '{control.Name}': this pass supports leaf object-stream controls, complete Frame/MultiPage containers, and Page containers.");
        }

        if (!IsStorageParentType(control.Type))
        {
            throw new CliException($"Cannot remove '{control.Name}': removing this parent type is not supported yet. This pass supports leaf controls and complete Frame/Page/MultiPage subtree removal.");
        }

        if (!TryGetOwnedStoragePath(control, controls, out _))
        {
            throw new CliException($"Cannot remove '{control.Name}': could not determine the owned storage path to remove.");
        }

        EnsurePageRemovalLeavesSibling(control, controls);
    }


    private static void EnsurePageRemovalLeavesSibling(ControlInfo control, IReadOnlyList<ControlInfo> controls)
    {
        if (!control.Type.Equals("Page", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(control.Parent))
        {
            throw new CliException($"Cannot remove page '{control.Name}': page does not expose a MultiPage parent.");
        }

        var siblingPageCount = controls.Count(candidate =>
            candidate.Type.Equals("Page", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(candidate.Parent, control.Parent, StringComparison.OrdinalIgnoreCase) &&
            !candidate.Name.Equals(control.Name, StringComparison.OrdinalIgnoreCase));

        if (siblingPageCount <= 0)
        {
            throw new CliException($"Cannot remove page '{control.Name}': removing the last page of MultiPage '{control.Parent}' is not supported yet.");
        }
    }

    private static bool IsStorageParentType(string type) =>
        type.Equals("Frame", StringComparison.OrdinalIgnoreCase) ||
        type.Equals("MultiPage", StringComparison.OrdinalIgnoreCase) ||
        type.Equals("Page", StringComparison.OrdinalIgnoreCase);

    private static bool TryGetOwnedStoragePath(ControlInfo parent, IReadOnlyList<ControlInfo> controls, out string storagePath)
    {
        storagePath = string.Empty;

        var child = controls.FirstOrDefault(candidate => string.Equals(candidate.Parent, parent.Name, StringComparison.OrdinalIgnoreCase) &&
            candidate.Properties is not null &&
            TryGetString(candidate.Properties, "storagePath", out var childStoragePath) &&
            !string.IsNullOrWhiteSpace(childStoragePath));
        if (child?.Properties is not null && TryGetString(child.Properties, "storagePath", out storagePath) && !string.IsNullOrWhiteSpace(storagePath))
        {
            return true;
        }

        if (parent.Properties is not null &&
            TryGetString(parent.Properties, "generatedStoragePath", out var generatedStoragePath) &&
            !string.IsNullOrWhiteSpace(generatedStoragePath))
        {
            storagePath = generatedStoragePath;
            return true;
        }

        if (parent.Properties is null || !TryGetString(parent.Properties, "storagePath", out var owningStoragePath) || string.IsNullOrWhiteSpace(owningStoragePath))
        {
            return false;
        }

        if (!TryGetInt(parent.Properties, "siteId", out var id) && !TryGetInt(parent.Properties, "id", out id))
        {
            return false;
        }

        storagePath = $"{owningStoragePath}/i{FormatStorageId(id)}";
        return true;
    }

    private static string ResolveTargetStoragePath(string? parent, IReadOnlyList<ControlInfo> controls)
    {
        if (string.IsNullOrWhiteSpace(parent))
        {
            return "Root Entry";
        }

        var parentControl = controls.FirstOrDefault(c => c.Name.Equals(parent, StringComparison.OrdinalIgnoreCase))
            ?? throw new CliException($"Target parent '{parent}' does not exist.");

        if (parentControl.Type.Equals("TabStrip", StringComparison.OrdinalIgnoreCase))
        {
            throw new CliException($"Target parent '{parent}' is a TabStrip. TabStrip is a selector, not a child-control container; use sibling Frame panels plus code.tabStripPanels or use MultiPage.");
        }

        if (!IsStorageParentType(parentControl.Type) || parentControl.Type.Equals("MultiPage", StringComparison.OrdinalIgnoreCase))
        {
            throw new CliException($"Target parent '{parent}' is type '{parentControl.Type}'. This pass supports adding/moving common controls into root, Frame, or Page containers only.");
        }

        if (!TryGetOwnedStoragePath(parentControl, controls, out var storagePath))
        {
            throw new CliException($"Target parent '{parent}' does not expose an owned storage path.");
        }

        return storagePath;
    }

    private static string ResolveTargetMultiPageStoragePath(string controlName, ControlInfo? parentControl)
    {
        if (parentControl is null)
        {
            throw new CliException($"Add target '{controlName}' is type Page and requires a MultiPage parent.");
        }

        if (!parentControl.Type.Equals("MultiPage", StringComparison.OrdinalIgnoreCase))
        {
            throw new CliException($"Add target '{controlName}' is type Page, but parent '{parentControl.Name}' is type '{parentControl.Type}'. Page controls can only be added to a MultiPage.");
        }

        if (!TryGetOwnedStoragePath(parentControl, [parentControl], out var storagePath))
        {
            if (parentControl.Properties is not null &&
                TryGetString(parentControl.Properties, "storagePath", out var ownerStoragePath) &&
                TryGetInt(parentControl.Properties, "siteId", out var siteId))
            {
                storagePath = $"{ownerStoragePath}/i{FormatStorageId(siteId)}";
                return storagePath;
            }

            throw new CliException($"Add target '{controlName}' cannot determine storage path for MultiPage parent '{parentControl.Name}'.");
        }

        return storagePath;
    }

    private static string FormatStorageId(int id) => id is >= 0 and < 10 ? $"0{id}" : id.ToString(CultureInfo.InvariantCulture);

    private static IEnumerable<ControlInfo> BuildMovedControls(
        IReadOnlyList<ControlInfo> templateControls,
        IReadOnlyList<ControlInfo> existingControls,
        Dictionary<string, string?> moveByName,
        Dictionary<string, Dictionary<string, JsonElement>> patchedByName,
        Dictionary<string, LayoutPatch> layoutByName,
        string? patchDir,
        WriterProvenanceAuditCollector? writerAudit)
    {
        var additions = new List<AddControlPatch>();
        foreach (var (controlName, newParent) in moveByName)
        {
            var template = templateControls.FirstOrDefault(c => c.Name.Equals(controlName, StringComparison.OrdinalIgnoreCase))
                ?? throw new CliException($"Cannot move '{controlName}': control does not exist.");

            if (templateControls.Any(candidate => string.Equals(candidate.Parent, template.Name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new CliException($"Cannot move '{controlName}': pass 36 only supports moving leaf object-stream controls. Move/remove containers separately.");
            }

            var add = new AddControlPatch
            {
                FromTemplate = controlName,
                Name = controlName,
                // In a move map, null/empty target means move to the root container.
                Parent = string.IsNullOrWhiteSpace(newParent) ? string.Empty : newParent.Trim(),
                Properties = patchedByName.TryGetValue(controlName, out var requested) ? requested : null
            };

            if (layoutByName.TryGetValue(controlName, out var layout))
            {
                add.Left = layout.Left;
                add.Top = layout.Top;
                add.RawWidth = layout.RawWidth ?? layout.Width;
                add.RawHeight = layout.RawHeight ?? layout.Height;
                add.LeftPt = layout.LeftPt;
                add.TopPt = layout.TopPt;
                add.WidthPt = layout.WidthPt;
                add.HeightPt = layout.HeightPt;
            }

            additions.Add(add);
        }

        return BuildAddedControls(templateControls, existingControls, additions, patchDir, writerAudit: writerAudit);
    }

    private static IReadOnlyList<AdditionPlanEntry> BuildAdditionPlan(
        IReadOnlyList<ControlInfo> templateControls,
        IReadOnlyList<ControlInfo> existingControls,
        IReadOnlyList<AddControlPatch> additions,
        Dictionary<string, Dictionary<string, JsonElement>>? patchedByName)
    {
        var existingNames = existingControls
            .Select(control => control.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var entriesByName = new Dictionary<string, AdditionPlanEntry>(StringComparer.OrdinalIgnoreCase);

        var sourceOrder = 0;
        foreach (var add in additions)
        {
            var name = add.Name?.Trim();
            if (string.IsNullOrWhiteSpace(name) || existingNames.Contains(name))
            {
                continue;
            }

            if (entriesByName.ContainsKey(name))
            {
                throw new CliException($"Add target '{name}' is requested more than once.");
            }

            var template = !string.IsNullOrWhiteSpace(add.FromTemplate)
                ? templateControls.FirstOrDefault(control => control.Name.Equals(add.FromTemplate, StringComparison.OrdinalIgnoreCase))
                    ?? throw new CliException($"Add template '{add.FromTemplate}' does not exist.")
                : null;
            var type = add.Type?.Trim();
            if (template is not null)
            {
                type ??= template.Type;
                if (!template.Type.Equals(type, StringComparison.OrdinalIgnoreCase))
                {
                    throw new CliException($"Add target '{name}' requested type '{type}', but template '{template.Name}' is type '{template.Type}'. Template clones must keep the same type.");
                }
            }
            else if (string.IsNullOrWhiteSpace(type))
            {
                throw new CliException($"Add target '{name}' requires 'type' when no fromTemplate is supplied.");
            }

            var parent = add.Parent is null
                ? template?.Parent
                : string.IsNullOrWhiteSpace(add.Parent)
                    ? null
                    : add.Parent.Trim();
            var requestedTabIndex = GetRequestedTabIndex(add, name, patchedByName);
            entriesByName[name] = new AdditionPlanEntry(add, name, type!, parent, template, requestedTabIndex, sourceOrder++);
        }

        var depthByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entriesByName.Values)
        {
            GetDepth(entry);
        }

        return entriesByName.Values
            .OrderBy(entry => depthByName[entry.Name])
            .ThenBy(entry => entry.SourceOrder)
            .ToList();

        int GetDepth(AdditionPlanEntry entry)
        {
            if (depthByName.TryGetValue(entry.Name, out var cached))
            {
                return cached;
            }

            if (!visiting.Add(entry.Name))
            {
                throw new CliException($"Add graph contains a parent cycle involving '{entry.Name}'.");
            }

            var depth = 0;
            if (!string.IsNullOrWhiteSpace(entry.Parent))
            {
                if (entriesByName.TryGetValue(entry.Parent, out var parentEntry))
                {
                    depth = checked(GetDepth(parentEntry) + 1);
                }
                else if (!existingNames.Contains(entry.Parent))
                {
                    throw new CliException($"Target parent '{entry.Parent}' does not exist.");
                }
            }

            visiting.Remove(entry.Name);
            depthByName[entry.Name] = depth;
            return depth;
        }
    }

    private static int? GetRequestedTabIndex(
        AddControlPatch add,
        string name,
        Dictionary<string, Dictionary<string, JsonElement>>? patchedByName)
    {
        if (add.Properties is not null && add.Properties.TryGetValue("tabIndex", out var addTabIndex))
        {
            return RequireUInt16(name, "tabIndex", addTabIndex);
        }

        if (patchedByName is not null &&
            patchedByName.TryGetValue(name, out var requestedProperties) &&
            requestedProperties.TryGetValue("tabIndex", out var propertyTabIndex))
        {
            return RequireUInt16(name, "tabIndex", propertyTabIndex);
        }

        return null;
    }

    private sealed record AdditionPlanEntry(
        AddControlPatch Patch,
        string Name,
        string Type,
        string? Parent,
        ControlInfo? Template,
        int? RequestedTabIndex,
        int SourceOrder);

    private static GeneratedPagePlan BuildGeneratedPagePlan(
        AdditionPlanEntry entry,
        int pageIndex,
        int fallbackWidth,
        int fallbackHeight,
        string? patchDir,
        Dictionary<string, Dictionary<string, JsonElement>>? patchedByName,
        Dictionary<string, LayoutPatch>? layoutByName)
    {
        var props = entry.Template?.Properties is not null
            ? new Dictionary<string, object?>(entry.Template.Properties, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (patchedByName is not null && patchedByName.TryGetValue(entry.Name, out var requestedProperties))
        {
            foreach (var (propertyName, propertyValue) in OrderPropertyApplications(requestedProperties))
            {
                ApplyPropertyToDictionary(entry.Name, entry.Type, props, propertyName, propertyValue, patchDir);
            }
        }

        if (entry.Patch.Caption is not null)
        {
            if (!SupportsExportedObjectProperty(entry.Type, "caption"))
            {
                throw new CliException($"Property 'caption' is not supported for {entry.Type} control '{entry.Name}'.");
            }
            props["caption"] = entry.Patch.Caption;
        }

        if (entry.Patch.Value is not null)
        {
            if (!SupportsExportedObjectProperty(entry.Type, "value"))
            {
                throw new CliException($"Property 'value' is not supported for {entry.Type} control '{entry.Name}'.");
            }
            props["value"] = entry.Patch.Value;
        }

        foreach (var (propertyName, propertyValue) in OrderPropertyApplications(
                     entry.Patch.Properties ?? new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)))
        {
            ApplyAddPropertyToDictionary(entry.Name, entry.Type, props, propertyName, propertyValue, patchDir);
        }

        var left = entry.Patch.Left ?? ToRawPoints(entry.Patch.LeftPt) ?? entry.Template?.Left ?? 0;
        var top = entry.Patch.Top ?? ToRawPoints(entry.Patch.TopPt) ?? entry.Template?.Top ?? 0;
        var rawWidth = entry.Patch.RawWidth ?? entry.Patch.Width ?? ToRawPoints(entry.Patch.WidthPt) ?? entry.Template?.RawWidth ?? fallbackWidth;
        var rawHeight = entry.Patch.RawHeight ?? entry.Patch.Height ?? ToRawPoints(entry.Patch.HeightPt) ?? entry.Template?.RawHeight ?? fallbackHeight;
        if (layoutByName is not null && layoutByName.TryGetValue(entry.Name, out var layout))
        {
            left = layout.Left ?? ToRawPoints(layout.LeftPt) ?? left;
            top = layout.Top ?? ToRawPoints(layout.TopPt) ?? top;
            rawWidth = layout.RawWidth ?? layout.Width ?? ToRawPoints(layout.WidthPt) ?? rawWidth;
            rawHeight = layout.RawHeight ?? layout.Height ?? ToRawPoints(layout.HeightPt) ?? rawHeight;
        }

        var caption = props.ContainsKey("caption") && TryGetString(props, "caption", out var requestedCaption)
            ? requestedCaption
            : props.ContainsKey("tabCaption") && TryGetString(props, "tabCaption", out var templateCaption)
                ? templateCaption
                : props.ContainsKey("formCaption") && TryGetString(props, "formCaption", out var formCaption)
                    ? formCaption
                    : entry.Name;
        var tabIndex = entry.RequestedTabIndex ?? pageIndex;
        props["tabCaption"] = caption;
        props.Remove("caption");
        props.Remove("formCaption");
        props["tabIndex"] = tabIndex;

        return new GeneratedPagePlan(entry, props, tabIndex, left, top, rawWidth, rawHeight, caption);
    }

    private sealed record GeneratedPagePlan(
        AdditionPlanEntry Entry,
        Dictionary<string, object?> Properties,
        int TabIndex,
        int Left,
        int Top,
        int Width,
        int Height,
        string Caption);

    private static IEnumerable<ControlInfo> BuildAddedControls(IReadOnlyList<ControlInfo> templateControls, IReadOnlyList<ControlInfo> existingControls, IReadOnlyList<AddControlPatch> additions, string? patchDir, Dictionary<string, Dictionary<string, JsonElement>>? patchedByName = null, Dictionary<string, LayoutPatch>? layoutByName = null, WriterProvenanceAuditCollector? writerAudit = null)
    {
        var additionPlan = BuildAdditionPlan(templateControls, existingControls, additions, patchedByName);
        var explicitPagesByMultiPage = additionPlan
            .Where(entry => entry.Type.Equals("Page", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(entry.Parent))
            .GroupBy(entry => entry.Parent!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<AdditionPlanEntry>)group
                    .OrderBy(entry => entry.SourceOrder)
                    .ToList(),
                StringComparer.OrdinalIgnoreCase);
        var consumedExplicitPages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var names = existingControls.Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var reservedNames = names
            .Concat(additionPlan.Select(entry => entry.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var maxId = templateControls.Concat(existingControls)
            .Select(c => c.Properties is not null && TryGetInt(c.Properties, "siteId", out var id) ? id : 0)
            .DefaultIfEmpty(0)
            .Max();

        var result = new List<ControlInfo>();
        foreach (var planned in additionPlan)
        {
            if (consumedExplicitPages.Contains(planned.Name))
            {
                continue;
            }

            var add = planned.Patch;
            var name = planned.Name;
            if (!names.Add(name))
            {
                // [WYSIWYG / Idempotencia] Si el control ya existe, lo omitimos del ciclo de adición
                // estructural. El bucle principal de propiedades se encargará de actualizarlo.
                continue;
            }

            var template = planned.Template;
            var type = planned.Type;
            var parent = planned.Parent;
            var controlsForParent = existingControls.Concat(result).ToList();
            ControlInfo? parentControl = null;
            if (!string.IsNullOrWhiteSpace(parent))
            {
                parentControl = controlsForParent.FirstOrDefault(c => c.Name.Equals(parent, StringComparison.OrdinalIgnoreCase))
                    ?? throw new CliException($"Target parent '{parent}' does not exist.");
            }

            var targetStoragePath = type.Equals("Page", StringComparison.OrdinalIgnoreCase)
                ? ResolveTargetMultiPageStoragePath(name, parentControl)
                : ResolveTargetStoragePath(parent, controlsForParent);
            var targetStreamPath = $"{targetStoragePath}/f";

            if (template is not null && template.Properties is null)
            {
                throw new CliException($"Add template '{template.Name}' has no structured metadata.");
            }

            var props = template?.Properties is not null
                ? new Dictionary<string, object?>(template.Properties, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            maxId++;
            props["isAddedControl"] = true;
            // Property overlays need the control-specific MS-OFORMS file defaults
            // before a generated payload and its parser metadata exist.
            props["controlType"] = type;
            
            var requestedProperties = patchedByName != null && patchedByName.TryGetValue(name, out var requested)
                ? requested
                : null;
            if (requestedProperties is not null)
            {
                foreach (var kvp in OrderPropertyApplications(requestedProperties))
                {
                    if (template is null)
                    {
                        ApplyAddPropertyToDictionary(name, type, props, kvp.Key, kvp.Value, patchDir);
                    }
                    else
                    {
                        ApplyPropertyToDictionary(name, type, props, kvp.Key, kvp.Value, patchDir, allowGeneratedProperties: true);
                    }
                }
            }

            if (template is not null)
            {
                if (!TryGetString(template.Properties!, "storagePath", out var templateStoragePath) || string.IsNullOrWhiteSpace(templateStoragePath) ||
                    !TryGetString(template.Properties!, "streamPath", out var templateStreamPath) || string.IsNullOrWhiteSpace(templateStreamPath))
                {
                    throw new CliException($"Add template '{template.Name}' is missing storagePath/streamPath metadata.");
                }

                props["templateControlName"] = template.Name;
                props["templateStoragePath"] = templateStoragePath;
                props["templateStreamPath"] = templateStreamPath;
                props["templateObjectStreamPath"] = $"{templateStoragePath}/o";
            }
            props["storagePath"] = targetStoragePath;
            props["streamPath"] = targetStreamPath;
            props["name"] = name;
            props["nameRaw"] = name;
            props["siteName"] = name;
            props["siteId"] = maxId;
            props["id"] = maxId;
            int? requestedTabIndex = null;
            if (add.Properties is not null && add.Properties.TryGetValue("tabIndex", out var addTabIndex))
            {
                requestedTabIndex = RequireUInt16(name, "tabIndex", addTabIndex);
            }
            else if (requestedProperties is not null && requestedProperties.TryGetValue("tabIndex", out var propertyTabIndex))
            {
                requestedTabIndex = RequireUInt16(name, "tabIndex", propertyTabIndex);
            }

            props["tabIndex"] = requestedTabIndex
                ?? NextTabIndexForParent(existingControls.Concat(result), parent);

            if (add.Caption is not null)
            {
                if (!SupportsExportedObjectProperty(type, "caption"))
                {
                    throw new CliException($"Property 'caption' is not supported for {type} control '{name}'.");
                }
                props["caption"] = add.Caption;
            }

            if (add.Value is not null)
            {
                if (!SupportsExportedObjectProperty(type, "value"))
                {
                    throw new CliException($"Property 'value' is not supported for {type} control '{name}'.");
                }
                props["value"] = add.Value;
            }

            foreach (var (propertyName, value) in OrderPropertyApplications(
                         add.Properties ?? new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)))
            {
                ApplyAddPropertyToDictionary(name, type, props, propertyName, value, patchDir);
            }

            var left = add.Left ?? ToRawPoints(add.LeftPt) ?? template?.Left ?? 0;
            var top = add.Top ?? ToRawPoints(add.TopPt) ?? template?.Top ?? 0;
            var rawWidth = add.RawWidth ?? add.Width ?? ToRawPoints(add.WidthPt) ?? template?.RawWidth;
            var rawHeight = add.RawHeight ?? add.Height ?? ToRawPoints(add.HeightPt) ?? template?.RawHeight;
            if (layoutByName != null && layoutByName.TryGetValue(name, out var layout))
            {
                left = layout.Left ?? ToRawPoints(layout.LeftPt) ?? left;
                top = layout.Top ?? ToRawPoints(layout.TopPt) ?? top;
                rawWidth = layout.RawWidth ?? layout.Width ?? ToRawPoints(layout.WidthPt) ?? rawWidth;
                rawHeight = layout.RawHeight ?? layout.Height ?? ToRawPoints(layout.HeightPt) ?? rawHeight;
            }

            if (type.Equals("Page", StringComparison.OrdinalIgnoreCase))
            {
                if (parentControl is null || !parentControl.Type.Equals("MultiPage", StringComparison.OrdinalIgnoreCase))
                {
                    throw new CliException($"Add target '{name}' is type Page and must use an existing MultiPage as parent.");
                }

                var pageIndex = NextMultiPagePageIndex(existingControls.Concat(result), parentControl.Name);
                var selectedPageIndex = parentControl.Properties is not null &&
                    TryGetInt(parentControl.Properties, "value", out var parentValue)
                        ? parentValue
                        : 0;
                var generatedStoragePath = $"{targetStoragePath}/i{FormatStorageId(maxId)}";
                rawWidth ??= parentControl.RawWidth ?? 0;
                rawHeight ??= parentControl.RawHeight ?? 0;
                left = 0;
                top = 0;
                var pageCaption = TryGetString(props, "caption", out var captionValue) && !string.IsNullOrWhiteSpace(captionValue)
                    ? captionValue
                    : name;
                props["tabCaption"] = pageCaption;
                props.Remove("caption");
                var generated = GeneratedStorageFactory.CreatePage(
                    name,
                    maxId,
                    (int)props["tabIndex"]!,
                    rawWidth ?? 0,
                    rawHeight ?? 0,
                    generatedStoragePath,
                    BuildGeneratedPageSiteFlags(props, pageIndex == selectedPageIndex),
                    props);
                writerAudit?.RecordGeneratedStorage(
                    name,
                    "Page",
                    parent,
                    generated.StoragePath,
                    generated.SitePayload,
                    new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["f"] = generated.FStream,
                        ["o"] = generated.OStream
                    },
                    "RebuildPatchApplier.BuildAddedControls -> GeneratedStorageFactory.CreatePage");

                props["generatedFormSitePayload"] = generated.SitePayload;
                props["siteDepth"] = 0;
                props["siteType"] = 1;
                props["siteLocalOffset"] = 0;
                props["cbSite"] = generated.SitePayload.Length - 4;
                props["parser"] = "msOFormsFormSiteData";
                props["siteParser"] = "msOFormsOleSiteConcrete";
                GeneratedControlFactory.SynchronizeSiteFlagMetadata(props, generated.SiteFlags);
                props["formControlParser"] = "msOFormsFormControl";
                props["formPropMask"] = "0x0C000C48";
                props["formBooleanProperties"] = $"0x{(MsFormsFactoryBinary.GetUInt32(props, "formBooleanProperties") ?? 0x0000_C004u):X8}";
                props["formDrawBuffer"] = MsFormsFactoryBinary.GetUInt32(props, "formDrawBuffer") ?? 32_000u;
                props["sizeSource"] = "formControlDisplayedSize";
                props["displayedWidth"] = rawWidth ?? 0;
                props["displayedHeight"] = rawHeight ?? 0;
                props["logicalWidth"] = 0;
                props["logicalHeight"] = 0;
                props["generatedStoragePath"] = generated.StoragePath;
                props["generatedStorageF"] = generated.FStream;
                props["generatedStorageO"] = generated.OStream;
                props["generatedStorageCompObjKind"] = "Page";
                props["generatedPageProperties"] = generated.PageProperties;
                props["multiPageParent"] = parentControl.Name;
                props["multiPagePageIndex"] = pageIndex;
                props["multiPagePageId"] = generated.SiteId;
                props["multiPageXStreamPath"] = $"{targetStoragePath}/x";
            }
            else if (template is null && type.Equals("Frame", StringComparison.OrdinalIgnoreCase))
            {
                var ownedStoragePath = $"{targetStoragePath}/i{FormatStorageId(maxId)}";
                var generated = GeneratedStorageFactory.CreateFrame(
                    name,
                    maxId,
                    (int)props["tabIndex"]!,
                    left,
                    top,
                    rawWidth ?? 0,
                    rawHeight ?? 0,
                    add.Caption,
                    ownedStoragePath,
                    props);
                writerAudit?.RecordGeneratedStorage(
                    name,
                    "Frame",
                    parent,
                    ownedStoragePath,
                    generated.SitePayload,
                    GeneratedStreams(generated.Metadata),
                    "RebuildPatchApplier.BuildAddedControls -> GeneratedStorageFactory.CreateFrame");

                props["generatedFormSitePayload"] = generated.SitePayload;
                props.Remove("caption");
                props["siteDepth"] = 0;
                props["siteType"] = 1;
                props["siteLocalOffset"] = 0;
                props["cbSite"] = generated.SitePayload.Length - 4;
                foreach (var (propertyName, propertyValue) in generated.Metadata)
                {
                    props[propertyName] = propertyValue;
                }
            }
            else if (template is null && type.Equals("MultiPage", StringComparison.OrdinalIgnoreCase))
            {
                var ownedStoragePath = $"{targetStoragePath}/i{FormatStorageId(maxId)}";
                var selectedPageIndex = TryGetInt(props, "value", out var requestedPageIndex)
                    ? requestedPageIndex
                    : 0;
                var explicitPagePlans = explicitPagesByMultiPage.TryGetValue(name, out var requestedPages)
                    ? requestedPages.Select((entry, index) => BuildGeneratedPagePlan(
                        entry,
                        index,
                        rawWidth ?? 0,
                        rawHeight ?? 0,
                        patchDir,
                        patchedByName,
                        layoutByName)).ToList()
                    : [];
                List<GeneratedPageDefinition> pageDefinitions;
                if (explicitPagePlans.Count > 0)
                {
                    pageDefinitions = explicitPagePlans
                        .Select((page, index) => new GeneratedPageDefinition(
                            page.Entry.Name,
                            page.Caption,
                            page.TabIndex,
                            page.Left,
                            page.Top,
                            page.Width,
                            page.Height,
                            BuildGeneratedPageSiteFlags(page.Properties, index == selectedPageIndex),
                            page.Properties))
                        .ToList();
                    foreach (var page in explicitPagePlans)
                    {
                        if (!names.Add(page.Entry.Name))
                        {
                            throw new CliException($"Add target '{name}' would create duplicate page '{page.Entry.Name}'.");
                        }

                        consumedExplicitPages.Add(page.Entry.Name);
                    }
                }
                else
                {
                    var pageNames = MsFormsFactoryBinary.GetStringList(props, "pageNames")?.ToArray()
                        ?? [$"{name}Page1", $"{name}Page2"];
                    var pageCaptions = MsFormsFactoryBinary.GetStringList(props, "pageCaptions")?.ToArray()
                        ?? pageNames.Select((_, index) => $"Page{index + 1}").ToArray();
                    if (pageNames.Length != pageCaptions.Length || pageNames.Length == 0)
                    {
                        throw new CliException($"Add target '{name}' has invalid pageNames/pageCaptions.");
                    }

                    pageDefinitions = new List<GeneratedPageDefinition>(pageNames.Length);
                    for (var i = 0; i < pageNames.Length; i++)
                    {
                        var pageName = pageNames[i];
                        if (reservedNames.Contains(pageName) || !names.Add(pageName))
                        {
                            throw new CliException($"Add target '{name}' would create duplicate page '{pageName}'.");
                        }

                        pageDefinitions.Add(new GeneratedPageDefinition(
                            pageName,
                            pageCaptions[i],
                            i,
                            0,
                            0,
                            rawWidth ?? 0,
                            rawHeight ?? 0,
                            BuildGeneratedPageSiteFlags(props: null, i == selectedPageIndex),
                            null));
                    }
                }

                var generated = GeneratedStorageFactory.CreateMultiPage(
                    name,
                    maxId,
                    (int)props["tabIndex"]!,
                    left,
                    top,
                    rawWidth ?? 0,
                    rawHeight ?? 0,
                    ownedStoragePath,
                    pageDefinitions,
                    selectedPageIndex,
                    props);
                writerAudit?.RecordGeneratedStorage(
                    name,
                    "MultiPage",
                    parent,
                    ownedStoragePath,
                    generated.SitePayload,
                    GeneratedStreams(generated.Metadata),
                    "RebuildPatchApplier.BuildAddedControls -> GeneratedStorageFactory.CreateMultiPage");

                props["generatedFormSitePayload"] = generated.SitePayload;
                props.Remove("caption");
                props["siteDepth"] = 0;
                props["siteType"] = 1;
                props["siteLocalOffset"] = 0;
                props["cbSite"] = generated.SitePayload.Length - 4;
                foreach (var (propertyName, propertyValue) in generated.Metadata)
                {
                    props[propertyName] = propertyValue;
                }

                for (var i = 0; i < generated.Pages.Count; i++)
                {
                    var page = generated.Pages[i];
                    writerAudit?.RecordGeneratedStorage(
                        page.Name,
                        "Page",
                        name,
                        page.StoragePath,
                        page.SitePayload,
                        new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["f"] = page.FStream,
                            ["o"] = page.OStream
                        },
                        "GeneratedStorageFactory.CreateMultiPage page");
                    var explicitPage = explicitPagePlans.FirstOrDefault(candidate =>
                        candidate.Entry.Name.Equals(page.Name, StringComparison.OrdinalIgnoreCase));
                    var pageProps = explicitPage is not null
                        ? explicitPage.Properties
                        : new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    pageProps["isAddedControl"] = true;
                    pageProps["storagePath"] = ownedStoragePath;
                    pageProps["streamPath"] = $"{ownedStoragePath}/f";
                    pageProps["name"] = page.Name;
                    pageProps["nameRaw"] = page.Name;
                    pageProps["siteName"] = page.Name;
                    pageProps["siteId"] = page.SiteId;
                    pageProps["id"] = page.SiteId;
                    pageProps["tabIndex"] = explicitPage?.TabIndex ?? i;
                    pageProps["parser"] = "msOFormsFormSiteData";
                    pageProps["siteParser"] = "msOFormsOleSiteConcrete";
                    GeneratedControlFactory.SynchronizeSiteFlagMetadata(pageProps, page.SiteFlags);
                    pageProps["formControlParser"] = "msOFormsFormControl";
                    pageProps["formPropMask"] = "0x0C000C48";
                    pageProps["formBooleanProperties"] = $"0x{(MsFormsFactoryBinary.GetUInt32(pageProps, "formBooleanProperties") ?? 0x0000_C004u):X8}";
                    pageProps["formDrawBuffer"] = MsFormsFactoryBinary.GetUInt32(pageProps, "formDrawBuffer") ?? 32_000u;
                    pageProps["sizeSource"] = "formControlDisplayedSize";
                    pageProps["displayedWidth"] = explicitPage?.Width ?? rawWidth ?? 0;
                    pageProps["displayedHeight"] = explicitPage?.Height ?? rawHeight ?? 0;
                    pageProps["logicalWidth"] = 0;
                    pageProps["logicalHeight"] = 0;
                    pageProps["siteDepth"] = 0;
                    pageProps["siteType"] = 1;
                    pageProps["siteLocalOffset"] = 0;
                    pageProps["cbSite"] = page.SitePayload.Length - 4;
                    pageProps["generatedFormSitePayload"] = page.SitePayload;
                    pageProps["generatedFormSiteAlreadyMaterialized"] = true;
                    pageProps["generatedStoragePath"] = page.StoragePath;
                    pageProps["generatedStorageF"] = page.FStream;
                    pageProps["generatedStorageO"] = page.OStream;
                    pageProps["generatedStorageCompObjKind"] = "Page";
                    pageProps["generatedPageProperties"] = page.PageProperties;
                    pageProps["multiPageParent"] = name;
                    pageProps["multiPagePageIndex"] = i;
                    pageProps["multiPagePageId"] = page.SiteId;
                    pageProps["multiPageXStreamPath"] = $"{ownedStoragePath}/x";

                    var pageLeft = explicitPage?.Left ?? 0;
                    var pageTop = explicitPage?.Top ?? 0;
                    var pageWidth = explicitPage?.Width ?? rawWidth;
                    var pageHeight = explicitPage?.Height ?? rawHeight;
                    result.Add(new ControlInfo(
                        page.Name,
                        "Page",
                        pageLeft,
                        pageTop,
                        pageWidth,
                        pageHeight,
                        FromRawPoints(pageLeft),
                        FromRawPoints(pageTop),
                        pageWidth is int pw ? FromRawPoints(pw) : null,
                        pageHeight is int ph ? FromRawPoints(ph) : null,
                        pageProps,
                        name,
                        null,
                        null,
                        null,
                        null,
                        0,
                        null,
                        null,
                        null,
                        null));
                }

                maxId += 1 + pageDefinitions.Count;
            }
            else if (template is null)
            {
                var generated = GeneratedControlFactory.Create(
                    type!,
                    name,
                    maxId,
                    (int)props["tabIndex"]!,
                    left,
                    top,
                    rawWidth,
                    rawHeight,
                    add.Caption,
                    add.Value,
                    props);
                writerAudit?.RecordGeneratedControl(
                    name,
                    type!,
                    parent,
                    targetStoragePath,
                    generated.SitePayload,
                    generated.ObjectPayload,
                    generated.Metadata,
                    props);

                props["generatedFormSitePayload"] = generated.SitePayload;
                props["generatedObjectPayload"] = generated.ObjectPayload;
                props["siteParser"] = "msOFormsOleSiteConcrete";
                props["parser"] = type!.Equals("CommandButton", StringComparison.OrdinalIgnoreCase)
                    ? "msOFormsCommandButton"
                    : type.Equals("Label", StringComparison.OrdinalIgnoreCase)
                        ? "msOFormsLabel"
                        : "msOFormsMorphData";
                props["siteDepth"] = 0;
                props["siteType"] = 1;
                props["siteLocalOffset"] = 0;
                props["cbSite"] = generated.SitePayload.Length - 4;
                props["objectStreamLocalOffset"] = 0;
                props["objectStreamSize"] = generated.ObjectPayload.Length;
                props["siteObjectStreamSize"] = generated.ObjectPayload.Length;
                props["objectStreamSizeFromSite"] = generated.ObjectPayload.Length;

                foreach (var (propertyName, propertyValue) in generated.Metadata)
                {
                    props[propertyName] = propertyValue;
                }
            }

            var source = template ?? new ControlInfo(
                name,
                type!,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                parent,
                null,
                null,
                null,
                null,
                0,
                null,
                null,
                null,
                null);

            result.Add(source with
            {
                Name = name,
                Parent = parent,
                Left = left,
                Top = top,
                RawWidth = rawWidth,
                RawHeight = rawHeight,
                LeftPt = left is int l ? FromRawPoints(l) : source.LeftPt,
                TopPt = top is int t ? FromRawPoints(t) : source.TopPt,
                WidthPt = rawWidth is int w ? FromRawPoints(w) : source.WidthPt,
                HeightPt = rawHeight is int h ? FromRawPoints(h) : source.HeightPt,
                Properties = props
            });
        }

        return result;
    }

    private static uint BuildGeneratedPageSiteFlags(Dictionary<string, object?>? props, bool isSelectedPage)
    {
        var flags = isSelectedPage ? 0x0004_0023u : 0x0004_0021u;
        return GeneratedControlFactory.BuildSiteFlags(flags, props);
    }

    private static IReadOnlyDictionary<string, byte[]> GeneratedStreams(IReadOnlyDictionary<string, object?> metadata)
    {
        var result = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        if (metadata.TryGetValue("generatedStorageF", out var f) && f is byte[] fBytes) result["f"] = fBytes;
        if (metadata.TryGetValue("generatedStorageO", out var o) && o is byte[] oBytes) result["o"] = oBytes;
        if (metadata.TryGetValue("generatedStorageX", out var x) && x is byte[] xBytes) result["x"] = xBytes;
        return result;
    }

    private static int NextTabIndexForParent(IEnumerable<ControlInfo> controls, string? parent)
    {
        var max = controls
            .Where(c => string.Equals(c.Parent, parent, StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Properties is not null && TryGetInt(c.Properties, "tabIndex", out var tabIndex) ? tabIndex : -1)
            .DefaultIfEmpty(-1)
            .Max();
        return Math.Min(max + 1, ushort.MaxValue);
    }

    private static int NextMultiPagePageIndex(IEnumerable<ControlInfo> controls, string multiPageName)
    {
        var max = controls
            .Where(c => c.Type.Equals("Page", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(c.Parent, multiPageName, StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Properties is not null && TryGetInt(c.Properties, "multiPagePageIndex", out var index) ? index : -1)
            .DefaultIfEmpty(-1)
            .Max();
        return Math.Min(max + 1, ushort.MaxValue);
    }

    private static void ValidateExistingControlMutation(ControlInfo control, ControlInfo target, string propertyName)
    {
        var props = control.Properties
            ?? throw new CliException($"Cannot patch '{control.Name}': control has no object metadata.");
        var normalized = propertyName.ToLowerInvariant();

        switch (normalized)
        {
            case "tag":
                RequireExistingMetadata(control.Name, propertyName, props, "tagSpan");
                return;
            case "rowsource":
                RequireExistingMetadata(control.Name, propertyName, props, "rowSourceSpan");
                return;
            case "helpcontextid":
                RequireExistingMetadata(control.Name, propertyName, props, "helpContextIdOffset");
                return;
            case "groupid":
                RequireExistingMetadata(control.Name, propertyName, props, "groupIdOffset");
                return;
            case "tabindex":
                RequireExistingMetadata(control.Name, propertyName, props, "tabIndexOffset");
                return;
            case "tabstop":
            case "visible":
            case "default":
            case "cancel":
            case "siteautosize":
            case "preserveheight":
            case "fittoparent":
            case "selectchild":
                RequireExistingMetadata(control.Name, propertyName, props, "siteBitFlagsRawOffset");
                return;
            case "sitebitflags":
                RequireExistingMetadata(control.Name, propertyName, props, "siteBitFlagsRawOffset");
                var sourceFlags = MsFormsFactoryBinary.GetUInt32(props, "siteBitFlagsRaw") ??
                                  MsFormsFactoryBinary.GetUInt32(props, "siteBitFlags") ?? 0u;
                var targetProperties = target.Properties!;
                var targetFlags = MsFormsFactoryBinary.GetUInt32(targetProperties, "siteBitFlagsRaw") ??
                                  MsFormsFactoryBinary.GetUInt32(targetProperties, "siteBitFlags") ?? 0u;
                const uint structuralSiteFlagMask = (1u << 4) | (1u << 18);
                if (((sourceFlags ^ targetFlags) & structuralSiteFlagMask) != 0)
                {
                    throw new CliException(
                        $"Cannot patch '{control.Name}.siteBitFlags': changing streamed/promoteControls requires a structural object/storage conversion.");
                }
                return;
        }

        if (!IsContainerControlType(control.Type))
        {
            return;
        }

        switch (normalized)
        {
            case "caption":
                // A Page caption is carried by its parent MultiPage's internal TabStrip.
                // Pages commonly have no FormControl caption field of their own.
                if (!control.Type.Equals("Page", StringComparison.OrdinalIgnoreCase))
                {
                    RequireExistingMetadata(control.Name, propertyName, props, "formCaptionSpan");
                }
                break;
            case "formbooleanproperties":
            case "enabled":
            case "picturetiling":
            case "keepscrollbarsvisible":
            case "righttoleft":
                RequireExistingMetadata(control.Name, propertyName, props, "formBooleanPropertiesRawOffset");
                break;
            case "formdrawbuffer":
            case "drawbuffer":
                RequireExistingMetadata(control.Name, propertyName, props, "formDrawBufferOffset");
                break;
            case "specialeffect":
            case "formspecialeffect":
                RequireExistingMetadata(control.Name, propertyName, props, "formSpecialEffectOffset");
                break;
            case "logicalwidth":
            case "logicalwidthpt":
                RequireExistingMetadata(control.Name, propertyName, props, "logicalWidthOffset");
                break;
            case "logicalheight":
            case "logicalheightpt":
                RequireExistingMetadata(control.Name, propertyName, props, "logicalHeightOffset");
                break;
            case "scrollleft":
            case "scrollleftpt":
                RequireExistingMetadata(control.Name, propertyName, props, "scrollLeftOffset");
                break;
            case "scrolltop":
            case "scrolltoppt":
                RequireExistingMetadata(control.Name, propertyName, props, "scrollTopOffset");
                break;
        }
    }

    private static void ValidateExistingRootMutation(
        string formName,
        Dictionary<string, object?> props,
        string propertyName)
    {
        switch (propertyName.ToLowerInvariant())
        {
            // These are textual FRM properties, not optional FormControl fields.
            case "caption":
            case "clientwidth":
            case "clientheight":
            case "clientleft":
            case "clienttop":
            case "left":
            case "top":
            case "width":
            case "height":
            case "startupposition":
            case "showmodal":
            case "whatsthisbutton":
            case "whatsthishelp":
            case "tag":
            case "drawbuffer":
                return;
            case "formcaption":
                RequireExistingMetadata(formName, propertyName, props, "formCaptionSpan");
                return;
            case "formbackcolor":
            case "backcolor":
                RequireExistingMetadata(formName, propertyName, props, "formBackColorRawOffset", "formBackColorOffset");
                return;
            case "formforecolor":
            case "forecolor":
                RequireExistingMetadata(formName, propertyName, props, "formForeColorRawOffset", "formForeColorOffset");
                return;
            case "formbordercolor":
            case "bordercolor":
                RequireExistingMetadata(formName, propertyName, props, "formBorderColorRawOffset", "formBorderColorOffset");
                return;
            case "formbooleanproperties":
            case "enabled":
            case "picturetiling":
            case "keepscrollbarsvisible":
            case "righttoleft":
                RequireExistingMetadata(formName, propertyName, props, "formBooleanPropertiesRawOffset");
                return;
            case "formborderstyle":
            case "borderstyle":
                RequireExistingMetadata(formName, propertyName, props, "formBorderStyleOffset");
                return;
            case "formmousepointer":
            case "mousepointer":
                RequireExistingMetadata(formName, propertyName, props, "formMousePointerOffset");
                return;
            case "formscrollbars":
            case "scrollbars":
                RequireExistingMetadata(formName, propertyName, props, "formScrollBarsOffset");
                return;
            case "formcycle":
            case "cycle":
                RequireExistingMetadata(formName, propertyName, props, "formCycleOffset");
                return;
            case "formspecialeffect":
            case "specialeffect":
                RequireExistingMetadata(formName, propertyName, props, "formSpecialEffectOffset");
                return;
            case "formpicturealignment":
            case "picturealignment":
                RequireExistingMetadata(formName, propertyName, props, "formPictureAlignmentOffset");
                return;
            case "formpicturesizemode":
            case "picturesizemode":
                RequireExistingMetadata(formName, propertyName, props, "formPictureSizeModeOffset");
                return;
            case "formzoom":
            case "zoom":
                RequireExistingMetadata(formName, propertyName, props, "formZoomOffset");
                return;
            case "nextavailableid":
                RequireExistingMetadata(formName, propertyName, props, "nextAvailableIdOffset");
                return;
            case "formgroupcount":
                RequireExistingMetadata(formName, propertyName, props, "formGroupCountOffset");
                return;
            case "formdrawbuffer":
                RequireExistingMetadata(formName, propertyName, props, "formDrawBufferOffset");
                return;
            case "widthpt":
            case "displayedwidth":
            case "displayedwidthpt":
                RequireExistingMetadata(formName, propertyName, props, "displayedWidthOffset");
                return;
            case "heightpt":
            case "displayedheight":
            case "displayedheightpt":
                RequireExistingMetadata(formName, propertyName, props, "displayedHeightOffset");
                return;
            case "logicalwidth":
            case "logicalwidthpt":
                RequireExistingMetadata(formName, propertyName, props, "logicalWidthOffset");
                return;
            case "logicalheight":
            case "logicalheightpt":
                RequireExistingMetadata(formName, propertyName, props, "logicalHeightOffset");
                return;
            case "scrollleft":
            case "scrollleftpt":
                RequireExistingMetadata(formName, propertyName, props, "scrollLeftOffset");
                return;
            case "scrolltop":
            case "scrolltoppt":
                RequireExistingMetadata(formName, propertyName, props, "scrollTopOffset");
                return;
        }
    }

    private static void RequireExistingMetadata(
        string entityName,
        string propertyName,
        Dictionary<string, object?> properties,
        params string[] metadataNames)
    {
        if (metadataNames.Any(properties.ContainsKey))
        {
            return;
        }

        throw new CliException(
            $"Cannot patch '{entityName}.{propertyName}': the existing native record does not contain the optional field required to serialize this change.");
    }

    private static ControlInfo ApplyToControl(ControlInfo control, Dictionary<string, JsonElement>? requested, LayoutPatch? layout, string? newName, string? patchDir = null)
    {
        if (control.Properties is null)
        {
            throw new CliException($"Cannot patch '{control.Name}': control has no object metadata.");
        }

        var props = new Dictionary<string, object?>(control.Properties, StringComparer.OrdinalIgnoreCase);
        foreach (var (propertyName, value) in OrderPropertyApplications(
                     requested ?? new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)))
        {
            ApplyPropertyToDictionary(control.Name, control.Type, props, propertyName, value, patchDir);
        }

        var propertyTarget = control with { Properties = props };
        foreach (var propertyName in requested?.Keys ?? Enumerable.Empty<string>())
        {
            if (!ReconstructionIntentBuilder.EffectivePropertyEqual(control, propertyTarget, propertyName))
            {
                ValidateExistingControlMutation(control, propertyTarget, propertyName);
            }
        }

        var left = control.Left;
        var top = control.Top;
        var rawWidth = control.RawWidth;
        var rawHeight = control.RawHeight;

        if (layout is not null)
        {
            left = layout.Left ?? ToRawPoints(layout.LeftPt) ?? left;
            top = layout.Top ?? ToRawPoints(layout.TopPt) ?? top;
            rawWidth = layout.RawWidth ?? layout.Width ?? ToRawPoints(layout.WidthPt) ?? rawWidth;
            rawHeight = layout.RawHeight ?? layout.Height ?? ToRawPoints(layout.HeightPt) ?? rawHeight;
        }

        var effectiveName = string.IsNullOrWhiteSpace(newName) ? control.Name : newName.Trim();
        if (!effectiveName.Equals(control.Name, StringComparison.Ordinal))
        {
            if (!props.ContainsKey("nameSpan"))
            {
                throw new CliException($"Cannot rename '{control.Name}': this control does not expose a documented nameSpan in FormSiteData.");
            }

            props["name"] = effectiveName;
            props["nameRaw"] = effectiveName;
            props["siteName"] = effectiveName;
        }

        return control with
        {
            Name = effectiveName,
            Left = left,
            Top = top,
            RawWidth = rawWidth,
            RawHeight = rawHeight,
            LeftPt = left is int l ? FromRawPoints(l) : control.LeftPt,
            TopPt = top is int t ? FromRawPoints(t) : control.TopPt,
            WidthPt = rawWidth is int w ? FromRawPoints(w) : control.WidthPt,
            HeightPt = rawHeight is int h ? FromRawPoints(h) : control.HeightPt,
            Properties = props
        };
    }

    private static void ApplyPropertyToDictionary(
        string controlName,
        string controlType,
        Dictionary<string, object?> props,
        string propertyName,
        JsonElement value,
        string? patchDir,
        bool allowGeneratedProperties = false)
    {
        if (!SupportsExportedObjectProperty(controlType, propertyName) &&
            !(allowGeneratedProperties && SupportsGeneratedObjectProperty(controlType, propertyName)))
        {
            throw new CliException($"Property '{propertyName}' is not supported for {controlType} control '{controlName}'.");
        }

        switch (propertyName.ToLowerInvariant())
        {
            case "caption":
                var caption = RequireString(controlName, propertyName, value);
                if (controlType.Equals("Frame", StringComparison.OrdinalIgnoreCase))
                {
                    props["formCaption"] = caption;
                }
                else if (controlType.Equals("Page", StringComparison.OrdinalIgnoreCase))
                {
                    props["tabCaption"] = caption;
                }
                else
                {
                    props["caption"] = caption;
                }
                break;
            case "groupname":
            case "fontname":
                props[CanonicalPropertyName(propertyName)] = RequireString(controlName, propertyName, value);
                break;
            case "value":
            case "listindex":
                props["value"] = controlType.Equals("MultiPage", StringComparison.OrdinalIgnoreCase) ||
                                  controlType.Equals("TabStrip", StringComparison.OrdinalIgnoreCase) ||
                                 controlType.Equals("ScrollBar", StringComparison.OrdinalIgnoreCase) ||
                                 controlType.Equals("SpinButton", StringComparison.OrdinalIgnoreCase)
                    ? RequireInt32(controlName, propertyName, value)
                    : RequireString(controlName, propertyName, value);
                if (controlType.Equals("ScrollBar", StringComparison.OrdinalIgnoreCase) ||
                    controlType.Equals("SpinButton", StringComparison.OrdinalIgnoreCase))
                {
                    props["position"] = props["value"];
                }
                else if (controlType.Equals("TabStrip", StringComparison.OrdinalIgnoreCase))
                {
                    props["listIndex"] = props["value"];
                    props.Remove("value");
                }
                else if (controlType.Equals("MultiPage", StringComparison.OrdinalIgnoreCase))
                {
                    props["listIndex"] = props["value"];
                }
                break;
            case "style":
            case "tabstyle":
                var tabStyle = RequireInt32(controlName, propertyName, value);
                if (tabStyle is < 0 or > 2)
                {
                    throw new CliException($"Property '{propertyName}' for '{controlName}' must be between 0 and 2.");
                }
                props["tabStyle"] = tabStyle;
                if (controlType.Equals("MultiPage", StringComparison.OrdinalIgnoreCase))
                {
                    props["style"] = tabStyle;
                }
                else
                {
                    props.Remove("style");
                }
                break;
            case "formspecialeffect":
                props["formSpecialEffect"] = RequireInt32(controlName, propertyName, value);
                break;
            case "passwordchar":
                var passwordChar = RequireString(controlName, propertyName, value);
                props["passwordChar"] = passwordChar.Length == 0 ? string.Empty : passwordChar[0].ToString();
                break;
            case "tabcaptions":
            case "tabtooltips":
            case "tabnames":
            case "tabtags":
            case "tabaccelerators":
            case "pagenames":
            case "pagecaptions":
                props[CanonicalPropertyName(propertyName)] = RequireStringArray(controlName, propertyName, value);
                break;
            case "tabflags":
                if (value.ValueKind != JsonValueKind.Array)
                {
                    throw new CliException($"Property '{propertyName}' for '{controlName}' must be an array.");
                }
                props["tabFlags"] = value.Clone();
                break;
            case "fonteffects":
                var fontEffects = RequireUInt32Like(controlName, propertyName, value);
                props["fontEffects"] = fontEffects;
                props["fontItalic"] = (fontEffects & (1u << 1)) != 0;
                props["fontUnderline"] = (fontEffects & (1u << 2)) != 0;
                props["fontStrikethrough"] = (fontEffects & (1u << 3)) != 0;
                break;
            case "formbooleanproperties":
                SetFormBooleanProperties(props, RequireUInt32Like(controlName, propertyName, value));
                break;
            case "formdrawbuffer":
            case "drawbuffer":
                props["formDrawBuffer"] = RequireUInt32Like(controlName, propertyName, value);
                break;
            case "picture":
            case "mouseicon":
                props[CanonicalPropertyName(propertyName)] = RequirePicture(controlName, propertyName, value, patchDir);
                break;
            case "picturesizemode":
            case "picturealignment":
                props[CanonicalPropertyName(propertyName)] = RequireUInt16(controlName, propertyName, value);
                break;
            case "proportionalthumb":
                props[CanonicalPropertyName(propertyName)] = RequireBoolean(controlName, propertyName, value);
                break;
            case "picturetiling":
            case "keepscrollbarsvisible":
            case "righttoleft":
                var formBooleanValue = RequireBoolean(controlName, propertyName, value);
                if (IsContainerControlType(controlType))
                {
                    SetFormBooleanPropertyBit(
                        props,
                        propertyName,
                        formBooleanValue,
                        controlType.Equals("Frame", StringComparison.OrdinalIgnoreCase) ? 0x0000_8004u : 0x0000_C004u);
                }
                else
                {
                    props[CanonicalPropertyName(propertyName)] = formBooleanValue;
                }
                break;
            case "min":
            case "max":
            case "position":
            case "smallchange":
            case "largechange":
            case "orientation":
            case "delay":
            case "logicalwidth":
            case "logicalheight":
            case "scrollleft":
            case "scrolltop":
                props[CanonicalPropertyName(propertyName)] = RequireInt32(controlName, propertyName, value);
                break;
            case "logicalwidthpt":
            case "logicalheightpt":
            case "scrollleftpt":
            case "scrolltoppt":
                if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var pointValue))
                {
                    throw new CliException($"Property '{propertyName}' for '{controlName}' must be a number.");
                }
                var pointPropertyName = CanonicalPropertyName(propertyName);
                props[pointPropertyName] = pointValue;
                if (ToRawPoints(pointValue) is int rawPointValue)
                {
                    props[pointPropertyName[..^2]] = rawPointValue;
                }
                break;
            case "controltiptext":
                if (!props.ContainsKey("controlTipTextSpan"))
                {
                    throw new CliException($"Cannot patch '{controlName}.controlTipText': this control does not expose a documented controlTipTextSpan in FormSiteData.");
                }
                props["controlTipText"] = RequireString(controlName, propertyName, value);
                break;
            case "controlsource":
                if (!props.ContainsKey("controlSourceSpan"))
                {
                    throw new CliException($"Cannot patch '{controlName}.controlSource': this control does not expose a documented controlSourceSpan in FormSiteData. Emit it during add/create first.");
                }
                props["controlSource"] = RequireString(controlName, propertyName, value);
                break;
            case "backcolor":
            case "forecolor":
            case "bordercolor":
                props[CanonicalPropertyName(propertyName)] = RequireColorLikeString(controlName, propertyName, value);
                break;
            case "fontsize":
                var size = RequireFontSize(controlName, value);
                props["fontSize"] = size;
                var rawSize = checked((uint)Math.Round(size * 20.0, MidpointRounding.AwayFromZero));
                props["fontHeightRaw"] = unchecked((int)rawSize);
                props["fontSizeRaw"] = rawSize;
                break;
            case "locked":
            case "wordwrap":
            case "enterkeybehavior":
            case "tabkeybehavior":
            case "selectionmargin":
            case "autowordselect":
            case "hideselection":
            case "autotab":
            case "multiline":
            case "integralheight":
            case "columnheads":
            case "matchrequired":
            case "editable":
                SetVariousPropertyBit(props, propertyName, RequireBoolean(controlName, propertyName, value));
                break;
            case "enabled":
                var enabledValue = RequireBoolean(controlName, propertyName, value);
                if (IsContainerControlType(controlType))
                {
                    SetFormBooleanPropertyBit(
                        props,
                        propertyName,
                        enabledValue,
                        controlType.Equals("Frame", StringComparison.OrdinalIgnoreCase) ? 0x0000_8004u : 0x0000_C004u);
                }
                else
                {
                    SetVariousPropertyBit(props, propertyName, enabledValue);
                }
                break;
            case "autosize":
                var autoSizeValue = RequireBoolean(controlName, propertyName, value);
                if (controlType.Equals("Image", StringComparison.OrdinalIgnoreCase))
                {
                    props["autoSize"] = autoSizeValue;
                }
                else
                {
                    SetVariousPropertyBit(props, propertyName, autoSizeValue);
                }
                break;
            case "backstyle":
                SetVariousPropertyBit(props, propertyName, RequireUInt16(controlName, propertyName, value) != 0);
                props["backStyle"] = RequireUInt16(controlName, propertyName, value);
                break;
            case "alignment":
                var alignment = RequireUInt16(controlName, propertyName, value);
                if (alignment is > 1)
                {
                    throw new CliException($"Property '{propertyName}' for '{controlName}' must be 0 or 1.");
                }
                SetVariousPropertyBit(props, propertyName, alignment == 0);
                props["alignment"] = alignment;
                break;
            case "imemode":
                SetImeMode(props, RequireUInt16(controlName, propertyName, value));
                break;
            case "pictureposition":
                props["picturePosition"] = RequireInt32(controlName, propertyName, value);
                break;
            case "mousepointer":
                props["mousePointer"] = RequireUInt16(controlName, propertyName, value);
                break;
            case "maxlength":
                props["maxLength"] = RequireNonNegativeInt32(controlName, propertyName, value);
                break;
            case "scrollbars":
                props["scrollBars"] = RequireUInt16(controlName, propertyName, value);
                break;
            case "displaystyle":
            case "listwidth":
            case "boundcolumn":
            case "listrows":
            case "matchentry":
            case "liststyle":
            case "showdropbuttonwhen":
            case "dropbuttonstyle":
            case "multiselect":
                props[CanonicalPropertyName(propertyName)] = RequireUInt16(controlName, propertyName, value);
                break;
            case "textcolumn":
            case "columncount":
                props[CanonicalPropertyName(propertyName)] = RequireInt16(controlName, propertyName, value);
                break;
            case "dragbehavior":
            case "enterfieldbehavior":
                var behaviorValue = RequireUInt16(controlName, propertyName, value);
                SetVariousPropertyBit(props, propertyName, behaviorValue != 0);
                props[CanonicalPropertyName(propertyName)] = behaviorValue;
                break;
            case "borderstyle":
                props["borderStyle"] = RequireUInt16(controlName, propertyName, value);
                break;
            case "specialeffect":
                var specialEffect = RequireUInt16(controlName, propertyName, value);
                if (controlType.Equals("Frame", StringComparison.OrdinalIgnoreCase))
                {
                    props["formSpecialEffect"] = specialEffect;
                }
                else
                {
                    props["specialEffect"] = specialEffect;
                }
                break;
            case "textalign":
                var textAlign = RequireTextAlign(controlName, propertyName, value);
                props["textAlign"] = TextPropsFactory.TextAlignName(textAlign);
                props["paragraphAlign"] = TextPropsFactory.TextAlignToParagraphAlign(textAlign);
                break;
            case "paragraphalign":
                props["paragraphAlign"] = RequireUInt16(controlName, propertyName, value);
                props["textAlign"] = TextPropsFactory.TextAlignName(
                    TextPropsFactory.ParagraphAlignToTextAlign((int)props["paragraphAlign"]!));
                break;
            case "accelerator":
                var accelerator = RequireString(controlName, propertyName, value);
                props["accelerator"] = accelerator.Length == 0 ? string.Empty : accelerator[0].ToString();
                if (accelerator.Length > 0)
                {
                    props["acceleratorCode"] = (int)accelerator[0];
                }
                break;
            case "takefocusonclick":
                props["takeFocusOnClick"] = RequireBoolean(controlName, propertyName, value);
                break;
            case "tabindex":
                props["tabIndex"] = RequireUInt16(controlName, propertyName, value);
                break;
            case "tabstop":
            case "visible":
            case "default":
            case "cancel":
            case "siteautosize":
            case "preserveheight":
            case "fittoparent":
            case "selectchild":
                SetSiteFlag(props, propertyName, RequireBoolean(controlName, propertyName, value));
                break;
            case "sitebitflags":
                SetSiteFlags(props, RequireUInt32Like(controlName, propertyName, value));
                break;
            case "tag":
                props["tag"] = RequireString(controlName, propertyName, value);
                break;
            case "rowsource":
                props["rowSource"] = RequireString(controlName, propertyName, value);
                break;
            case "helpcontextid":
                props["helpContextId"] = RequireInt32(controlName, propertyName, value);
                break;
            case "groupid":
                props["groupId"] = RequireUInt16(controlName, propertyName, value);
                break;
            case "fontbold":
                var bold = RequireBoolean(controlName, propertyName, value);
                props["fontBold"] = bold;
                props["fontWeight"] = bold ? 700 : 400;
                break;
            case "fontitalic":
                props["fontItalic"] = RequireBoolean(controlName, propertyName, value);
                break;
            case "fontunderline":
                props["fontUnderline"] = RequireBoolean(controlName, propertyName, value);
                break;
            case "fontstrikethrough":
                props["fontStrikethrough"] = RequireBoolean(controlName, propertyName, value);
                break;
            case "fontcharset":
                props["fontCharSet"] = RequireUInt16(controlName, propertyName, value);
                break;
            case "fontpitchandfamily":
                props["fontPitchAndFamily"] = RequireUInt16(controlName, propertyName, value);
                break;
            case "fontweight":
                var fontWeight = RequireUInt16(controlName, propertyName, value);
                props["fontWeight"] = fontWeight;
                props["fontBold"] = fontWeight >= 700;
                break;
            default:
                throw new CliException($"Property '{propertyName}' is not supported for control '{controlName}'.");
        }
    }

    private static void ApplyAddPropertyToDictionary(string controlName, string controlType, Dictionary<string, object?> props, string propertyName, JsonElement value, string? patchDir)
    {
        if (!SupportsGeneratedObjectProperty(controlType, propertyName))
        {
            throw new CliException($"Property '{propertyName}' is not supported for {controlType} control '{controlName}'.");
        }

        switch (propertyName.ToLowerInvariant())
        {
            case "orientation":
                props["orientation"] = RequireInt32(controlName, propertyName, value);
                break;
            case "locked":
            case "wordwrap":
            case "enterkeybehavior":
            case "tabkeybehavior":
            case "selectionmargin":
            case "autowordselect":
            case "hideselection":
            case "autotab":
            case "multiline":
            case "integralheight":
            case "columnheads":
            case "matchrequired":
            case "editable":
                SetVariousPropertyBit(props, propertyName, RequireBoolean(controlName, propertyName, value));
                break;
            case "enabled":
                var enabled = RequireBoolean(controlName, propertyName, value);
                if (IsContainerControlType(controlType))
                {
                    SetFormBooleanPropertyBit(
                        props,
                        propertyName,
                        enabled,
                        controlType.Equals("Frame", StringComparison.OrdinalIgnoreCase) ? 0x0000_8004u : 0x0000_C004u);
                }
                else
                {
                    SetVariousPropertyBit(props, propertyName, enabled);
                }
                break;
            case "autosize":
                var autoSize = RequireBoolean(controlName, propertyName, value);
                if (controlType.Equals("Image", StringComparison.OrdinalIgnoreCase))
                {
                    props["autoSize"] = autoSize;
                }
                else
                {
                    SetVariousPropertyBit(props, propertyName, autoSize);
                }
                break;
            case "takefocusonclick":
            case "tabstop":
            case "visible":
            case "default":
            case "cancel":
            case "siteautosize":
            case "preserveheight":
            case "fittoparent":
            case "selectchild":
                props[CanonicalPropertyName(propertyName)] = RequireBoolean(controlName, propertyName, value);
                break;
            case "backstyle":
            case "pictureposition":
            case "mousepointer":
            case "borderstyle":
            case "specialeffect":
            case "maxlength":
            case "scrollbars":
            case "displaystyle":
            case "listwidth":
            case "boundcolumn":
            case "textcolumn":
            case "columncount":
            case "listrows":
            case "matchentry":
            case "liststyle":
            case "showdropbuttonwhen":
            case "dropbuttonstyle":
            case "multiselect":
            case "dragbehavior":
            case "enterfieldbehavior":
                var integerPropertyValue = RequireInt32(controlName, propertyName, value);
                if (propertyName.Equals("specialEffect", StringComparison.OrdinalIgnoreCase) &&
                    controlType.Equals("Frame", StringComparison.OrdinalIgnoreCase))
                {
                    props["formSpecialEffect"] = integerPropertyValue;
                }
                else
                {
                    props[CanonicalPropertyName(propertyName)] = integerPropertyValue;
                }
                if (propertyName.Equals("backStyle", StringComparison.OrdinalIgnoreCase))
                {
                    SetVariousPropertyBit(props, propertyName, MsFormsFactoryBinary.GetInt32(props, "backStyle") != 0);
                }
                else if (propertyName.Equals("dragBehavior", StringComparison.OrdinalIgnoreCase) ||
                         propertyName.Equals("enterFieldBehavior", StringComparison.OrdinalIgnoreCase))
                {
                    SetVariousPropertyBit(props, propertyName, MsFormsFactoryBinary.GetInt32(props, CanonicalPropertyName(propertyName)) != 0);
                }
                break;
            case "imemode":
                SetImeMode(props, RequireUInt16(controlName, propertyName, value));
                break;
            case "alignment":
                var addAlignment = RequireInt32(controlName, propertyName, value);
                if (addAlignment is < 0 or > 1)
                {
                    throw new CliException($"Property '{propertyName}' for '{controlName}' must be 0 or 1.");
                }
                props["alignment"] = addAlignment;
                SetVariousPropertyBit(props, propertyName, addAlignment == 0);
                break;
            case "textalign":
                var addTextAlign = RequireTextAlign(controlName, propertyName, value);
                props["textAlign"] = TextPropsFactory.TextAlignName(addTextAlign);
                props["paragraphAlign"] = TextPropsFactory.TextAlignToParagraphAlign(addTextAlign);
                break;
            case "paragraphalign":
                props["paragraphAlign"] = RequireInt32(controlName, propertyName, value);
                break;
            case "accelerator":
                props["accelerator"] = RequireString(controlName, propertyName, value);
                break;
            case "controlsource":
                props["controlSource"] = RequireString(controlName, propertyName, value);
                break;
            case "controltiptext":
                props["controlTipText"] = RequireString(controlName, propertyName, value);
                break;
            case "tabcaptions":
            case "tabtooltips":
            case "tabnames":
            case "tabtags":
            case "tabaccelerators":
            case "pagenames":
            case "pagecaptions":
                props[CanonicalPropertyName(propertyName)] = RequireStringArray(controlName, propertyName, value);
                break;
            case "tabflags":
                if (value.ValueKind != JsonValueKind.Array)
                {
                    throw new CliException($"Property '{propertyName}' for '{controlName}' must be an array.");
                }
                props["tabFlags"] = value.Clone();
                break;
            case "formbooleanproperties":
                SetFormBooleanProperties(props, RequireUInt32Like(controlName, propertyName, value));
                break;
            case "formdrawbuffer":
            case "drawbuffer":
                props["formDrawBuffer"] = RequireUInt32Like(controlName, propertyName, value);
                break;
            default:
                ApplyPropertyToDictionary(controlName, controlType, props, propertyName, value, patchDir, allowGeneratedProperties: true);
                break;
        }
    }

    private const double HimetricPerPoint = 2540.0 / 72.0;

    private static int? ToRawPoints(double? points) =>
        points is null ? null : (int)Math.Round(points.Value * HimetricPerPoint, MidpointRounding.AwayFromZero);

    private static double FromRawPoints(int raw) =>
        Math.Round(raw / HimetricPerPoint, 2, MidpointRounding.AwayFromZero);

    private static string CanonicalPropertyName(string propertyName) =>
        propertyName.ToLowerInvariant() switch
        {
            "groupname" => "groupName",
            "fontname" => "fontName",
            "wordwrap" => "wordWrap",
            "autosize" => "autoSize",
            "backstyle" => "backStyle",
            "alignment" => "alignment",
            "imemode" => "imeMode",
            "pictureposition" => "picturePosition",
            "picturesizemode" => "pictureSizeMode",
            "picturealignment" => "pictureAlignment",
            "picturetiling" => "pictureTiling",
            "mousepointer" => "mousePointer",
            "borderstyle" => "borderStyle",
            "specialeffect" => "specialEffect",
            "formspecialeffect" => "formSpecialEffect",
            "listindex" => "listIndex",
            "tabstyle" => "tabStyle",
            "style" => "style",
            "maxlength" => "maxLength",
            "passwordchar" => "passwordChar",
            "scrollbars" => "scrollBars",
            "displaystyle" => "displayStyle",
            "listwidth" => "listWidth",
            "boundcolumn" => "boundColumn",
            "textcolumn" => "textColumn",
            "columncount" => "columnCount",
            "listrows" => "listRows",
            "matchentry" => "matchEntry",
            "liststyle" => "listStyle",
            "showdropbuttonwhen" => "showDropButtonWhen",
            "dropbuttonstyle" => "dropButtonStyle",
            "multiselect" => "multiSelect",
            "dragbehavior" => "dragBehavior",
            "enterfieldbehavior" => "enterFieldBehavior",
            "enterkeybehavior" => "enterKeyBehavior",
            "tabkeybehavior" => "tabKeyBehavior",
            "selectionmargin" => "selectionMargin",
            "autowordselect" => "autoWordSelect",
            "hideselection" => "hideSelection",
            "autotab" => "autoTab",
            "multiline" => "multiLine",
            "integralheight" => "integralHeight",
            "columnheads" => "columnHeads",
            "matchrequired" => "matchRequired",
            "takefocusonclick" => "takeFocusOnClick",
            "textalign" => "textAlign",
            "paragraphalign" => "paragraphAlign",
            "sitebitflags" => "siteBitFlags",
            "tabstop" => "tabStop",
            "siteautosize" => "siteAutoSize",
            "preserveheight" => "preserveHeight",
            "fittoparent" => "fitToParent",
            "selectchild" => "selectChild",
            "controltiptext" => "controlTipText",
            "controlsource" => "controlSource",
            "backcolor" => "backColor",
            "forecolor" => "foreColor",
            "bordercolor" => "borderColor",
            "tabcaptions" => "tabCaptions",
            "tabtooltips" => "tabTooltips",
            "tabnames" => "tabNames",
            "tabtags" => "tabTags",
            "tabaccelerators" => "tabAccelerators",
            "tabflags" => "tabFlags",
            "pagenames" => "pageNames",
            "pagecaptions" => "pageCaptions",
            "tag" => "tag",
            "rowsource" => "rowSource",
            "helpcontextid" => "helpContextId",
            "groupid" => "groupId",
            "fontbold" => "fontBold",
            "fontitalic" => "fontItalic",
            "fontunderline" => "fontUnderline",
            "fontstrikethrough" => "fontStrikethrough",
            "fontcharset" => "fontCharSet",
            "fontpitchandfamily" => "fontPitchAndFamily",
            "fontweight" => "fontWeight",
            "fonteffects" => "fontEffects",
            "formbooleanproperties" => "formBooleanProperties",
            "formdrawbuffer" => "formDrawBuffer",
            "drawbuffer" => "DrawBuffer",
            _ => propertyName
        };

    private static void SetVariousPropertyBit(Dictionary<string, object?> props, string propertyName, bool value)
    {
        var bits = TryGetInt(props, "variousPropertyBitsRaw", out var current)
            ? unchecked((uint)current)
            : DefaultVariousPropertyBits(props);

        var bit = propertyName.ToLowerInvariant() switch
        {
            "enabled" => 1,
            "locked" => 2,
            "backstyle" => 3,
            "alignment" => 13,
            "integralheight" => 11,
            "columnheads" => 10,
            "matchrequired" => 12,
            "editable" => 14,
            "dragbehavior" => 19,
            "enterkeybehavior" => 20,
            "enterfieldbehavior" => 21,
            "tabkeybehavior" => 22,
            "wordwrap" => 23,
            "selectionmargin" => 26,
            "autowordselect" => 27,
            "autosize" => 28,
            "hideselection" => 29,
            "autotab" => 30,
            "multiline" => 31,
            _ => throw new CliException($"Property '{propertyName}' is not a supported VariousPropertyBits field.")
        };

        var mask = 1u << bit;
        bits = value ? bits | mask : bits & ~mask;
        props["variousPropertyBitsRaw"] = unchecked((int)bits);
        props[CanonicalPropertyName(propertyName)] = value;
    }

    private static void SetImeMode(Dictionary<string, object?> props, int imeMode)
    {
        if (imeMode is < 0 or > 15)
        {
            throw new CliException("Property 'imeMode' must be between 0 and 15.");
        }

        var bits = TryGetInt(props, "variousPropertyBitsRaw", out var current)
            ? unchecked((uint)current)
            : DefaultVariousPropertyBits(props);
        bits &= ~(0xFu << 15);
        bits |= ((uint)imeMode & 0xFu) << 15;
        props["variousPropertyBitsRaw"] = unchecked((int)bits);
        props["imeMode"] = imeMode;
    }

    private static uint DefaultVariousPropertyBits(Dictionary<string, object?> props) =>
        (TryGetString(props, "parser", out var parser) && parser.Equals("msOFormsLabel", StringComparison.OrdinalIgnoreCase)) ||
        IsControlType(props, "Label")
            ? 0x0080_0013u
            : (TryGetString(props, "parser", out parser) &&
               (parser.Equals("msOFormsImage", StringComparison.OrdinalIgnoreCase) ||
                parser.Equals("msOFormsScrollBar", StringComparison.OrdinalIgnoreCase) ||
                parser.Equals("msOFormsSpinButton", StringComparison.OrdinalIgnoreCase))) ||
              IsControlType(props, "Image") || IsControlType(props, "ScrollBar") || IsControlType(props, "SpinButton")
                 ? 0u
            : IsTextBox(props)
                ? 0x2C80_481Bu
            : IsControlType(props, "CheckBox") || IsControlType(props, "ToggleButton")
                ? 0x2C80_081Bu
            : IsControlType(props, "OptionButton")
                ? 0x0080_001Bu
            : IsControlType(props, "ComboBox")
                ? 0x2C80_481Bu
            : 0x0000_001Bu;

    private static bool IsTextBox(Dictionary<string, object?> props) =>
        IsControlType(props, "TextBox");

    private static bool IsControlType(Dictionary<string, object?> props, string type) =>
        TryGetString(props, "controlType", out var controlType) &&
        controlType.Equals(type, StringComparison.OrdinalIgnoreCase);

    private static void SetSiteFlag(Dictionary<string, object?> props, string propertyName, bool value)
    {
        var existingFlags = MsFormsFactoryBinary.GetUInt32(props, "siteBitFlagsRaw") ??
                            MsFormsFactoryBinary.GetUInt32(props, "siteBitFlags");
        if (existingFlags is null)
        {
            // Generated sites overlay named values on their type-specific factory
            // defaults. Existing sites without a BitFlags field are rejected by the
            // mutation capability check instead of inventing a raw word here.
            props[CanonicalPropertyName(propertyName)] = value;
            return;
        }
        var flags = existingFlags.Value;

        var bit = propertyName.ToLowerInvariant() switch
        {
            "tabstop" => 0,
            "visible" => 1,
            "default" => 2,
            "cancel" => 3,
            "siteautosize" => 5,
            "preserveheight" => 8,
            "fittoparent" => 9,
            "selectchild" => 13,
            _ => throw new CliException($"Property '{propertyName}' is not a supported SITE_FLAG field.")
        };

        var mask = 1u << bit;
        flags = value ? flags | mask : flags & ~mask;
        props["siteBitFlagsRaw"] = unchecked((int)flags);
        props["siteBitFlags"] = $"0x{flags:X8}";
        props[CanonicalPropertyName(propertyName)] = value;
    }

    private static void SetSiteFlags(Dictionary<string, object?> props, uint flags)
    {
        props["siteBitFlagsRaw"] = unchecked((int)flags);
        props["siteBitFlags"] = $"0x{flags:X8}";
        props["tabStop"] = (flags & (1u << 0)) != 0;
        props["visible"] = (flags & (1u << 1)) != 0;
        props["default"] = (flags & (1u << 2)) != 0;
        props["cancel"] = (flags & (1u << 3)) != 0;
        props["streamed"] = (flags & (1u << 4)) != 0;
        props["siteAutoSize"] = (flags & (1u << 5)) != 0;
        props["preserveHeight"] = (flags & (1u << 8)) != 0;
        props["fitToParent"] = (flags & (1u << 9)) != 0;
        props["selectChild"] = (flags & (1u << 13)) != 0;
        props["promoteControls"] = (flags & (1u << 18)) != 0;
    }

    private static string RequireString(string controlName, string propertyName, JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            throw new CliException($"Property '{propertyName}' for '{controlName}' must be a string.");
        }

        return value.GetString() ?? string.Empty;
    }

    private static string RequirePicture(string controlName, string propertyName, JsonElement value, string? patchDir)
    {
        var s = RequireString(controlName, propertyName, value);
        if (string.IsNullOrWhiteSpace(s)) return s;

        byte[] imgBytes;
        if (s.StartsWith("base64:", StringComparison.OrdinalIgnoreCase))
        {
            imgBytes = Convert.FromBase64String(s["base64:".Length..]);
            // Keep a complete native StdPicture stream intact. Partial or malformed
            // lookalikes are payload bytes and receive a fresh native envelope.
            if (MsFormsFactoryBinary.IsNativePictureStream(imgBytes))
            {
                return s; // Already contains header.
            }
        }
        else if (s.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            var path = s["file://".Length..];
            if (!Path.IsPathRooted(path) && !string.IsNullOrEmpty(patchDir))
            {
                path = Path.Combine(patchDir, path);
            }

            if (!File.Exists(path))
            {
                throw new CliException($"Picture file not found: {path} for control '{controlName}'.");
            }
            imgBytes = File.ReadAllBytes(path);
        }
        else
        {
            throw new CliException($"Picture property for '{controlName}' must be 'base64:...' or 'file://...'.");
        }

        // Attach the 24-byte header
        // GUID: {0BE35204-8F91-11CE-9DE3-00AA004BB851}
        var guidBytes = new byte[] { 0x04, 0x52, 0xE3, 0x0B, 0x91, 0x8F, 0xCE, 0x11, 0x9D, 0xE3, 0x00, 0xAA, 0x00, 0x4B, 0xB8, 0x51 };
        var length = imgBytes.Length;
        var header = new byte[24];
        Array.Copy(guidBytes, 0, header, 0, 16);
        header[16] = 0x6C; // 74 6C is 0x0000746C little-endian -> 6C 74 00 00
        header[17] = 0x74;
        header[18] = 0x00;
        header[19] = 0x00;
        header[20] = (byte)(length & 0xFF);
        header[21] = (byte)((length >> 8) & 0xFF);
        header[22] = (byte)((length >> 16) & 0xFF);
        header[23] = (byte)((length >> 24) & 0xFF);

        var finalBytes = new byte[24 + length];
        Array.Copy(header, 0, finalBytes, 0, 24);
        Array.Copy(imgBytes, 0, finalBytes, 24, length);

        return $"base64:{Convert.ToBase64String(finalBytes)}";
    }

    private static string[] RequireStringArray(string controlName, string propertyName, JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new CliException($"Property '{propertyName}' for '{controlName}' must be an array of strings.");
        }

        var result = new List<string>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                throw new CliException($"Property '{propertyName}' for '{controlName}' must be an array of strings.");
            }

            result.Add(item.GetString() ?? string.Empty);
        }

        if (result.Count == 0)
        {
            throw new CliException($"Property '{propertyName}' for '{controlName}' must contain at least one string.");
        }

        return result.ToArray();
    }

    private static string RequireColorLikeString(string controlName, string propertyName, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new CliException($"Property '{propertyName}' for '{controlName}' cannot be empty.");
            }

            return text;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetUInt32(out var raw))
        {
            return $"&H{raw:X8}&";
        }

        throw new CliException($"Property '{propertyName}' for '{controlName}' must be a VBA color string like '&H00CCCCCC&' or an unsigned integer.");
    }

    private static int RequireUInt16(string controlName, string propertyName, JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var parsed) || parsed is < 0 or > ushort.MaxValue)
        {
            throw new CliException($"Property '{propertyName}' for '{controlName}' must be an integer between 0 and 65535.");
        }

        return parsed;
    }

    private static int RequireInt32(string controlName, string propertyName, JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var parsed))
        {
            throw new CliException($"Property '{propertyName}' for '{controlName}' must be a 32-bit integer.");
        }

        return parsed;
    }

    private static int RequireNonNegativeInt32(string controlName, string propertyName, JsonElement value)
    {
        var parsed = RequireInt32(controlName, propertyName, value);
        if (parsed < 0)
        {
            throw new CliException($"Property '{propertyName}' for '{controlName}' must be a non-negative 32-bit integer.");
        }

        return parsed;
    }

    private static int RequireInt16(string controlName, string propertyName, JsonElement value)
    {
        var parsed = RequireInt32(controlName, propertyName, value);
        if (parsed is < short.MinValue or > short.MaxValue)
        {
            throw new CliException($"Property '{propertyName}' for '{controlName}' must be an integer between -32768 and 32767.");
        }

        return parsed;
    }

    private static bool RequireBoolean(string controlName, string propertyName, JsonElement value)
    {
        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return value.GetBoolean();
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var i))
        {
            return i != 0;
        }

        throw new CliException($"Property '{propertyName}' for '{controlName}' must be true, false, 1 or 0.");
    }

    private static int RequireTextAlign(string controlName, string propertyName, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String &&
            TextPropsFactory.TryParseTextAlign(value.GetString() ?? string.Empty, out var named) &&
            named is >= 1 and <= 3)
        {
            return named;
        }

        if (value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt32(out var numeric) &&
            numeric is >= 1 and <= 3)
        {
            return numeric;
        }

        throw new CliException($"Property '{propertyName}' for '{controlName}' must be 'left', 'center', 'right', or an integer from 1 to 3.");
    }

    private static double RequireFontSize(string controlName, JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var size))
        {
            throw new CliException($"Property 'fontSize' for '{controlName}' must be numeric.");
        }

        if (size is <= 0 or > 72)
        {
            throw new CliException($"Property 'fontSize' for '{controlName}' must be between 0 and 72.");
        }

        return size;
    }

    private static bool TryGetString(Dictionary<string, object?> props, string key, out string value)
    {
        value = string.Empty;
        if (!props.TryGetValue(key, out var raw) || raw is null)
        {
            return false;
        }

        if (raw is string text)
        {
            value = text;
            return true;
        }

        value = raw.ToString() ?? string.Empty;
        return !string.IsNullOrEmpty(value);
    }

    private static bool TryGetInt(Dictionary<string, object?> props, string key, out int value)
    {
        value = 0;
        if (!props.TryGetValue(key, out var raw) || raw is null)
        {
            return false;
        }

        switch (raw)
        {
            case int i:
                value = i;
                return true;
            case long l when l >= int.MinValue && l <= int.MaxValue:
                value = (int)l;
                return true;
            case uint u when u <= int.MaxValue:
                value = (int)u;
                return true;
            case ulong ul when ul <= int.MaxValue:
                value = (int)ul;
                return true;
            case short s:
                value = s;
                return true;
            case ushort us:
                value = us;
                return true;
            case byte b:
                value = b;
                return true;
            case sbyte sb:
                value = sb;
                return true;
            case JsonElement element when element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var parsed):
                value = parsed;
                return true;
            case JsonElement element when element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out var parsed64) && parsed64 >= int.MinValue && parsed64 <= int.MaxValue:
                value = (int)parsed64;
                return true;
            case JsonElement element when element.ValueKind == JsonValueKind.Number && element.TryGetUInt64(out var parsedU64) && parsedU64 <= int.MaxValue:
                value = (int)parsedU64;
                return true;
            case string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed):
                value = parsed;
                return true;
            default:
                return false;
        }
    }

    private static void SetFormBooleanPropertyBit(
        Dictionary<string, object?> props,
        string propertyName,
        bool value,
        uint defaultValue = 0x0020_0001u)
    {
        var bits = TryGetString(props, "formBooleanProperties", out var hex) && hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? Convert.ToUInt32(hex[2..], 16)
            : defaultValue;

        var bit = propertyName.ToLowerInvariant() switch
        {
            "enabled" => 0,
            "picturetiling" => 4,
            "keepscrollbarsvisible" => 21,
            "righttoleft" => 22,
            _ => throw new CliException($"Property '{propertyName}' is not a supported formBooleanProperties field.")
        };

        var mask = 1u << bit;
        bits = value ? bits | mask : bits & ~mask;
        props["formBooleanProperties"] = $"0x{bits:X8}";
    }

    private static void SetFormBooleanProperties(Dictionary<string, object?> props, uint bits)
    {
        props["formBooleanProperties"] = $"0x{bits:X8}";
        props["enabled"] = (bits & 1u) != 0;
        props["pictureTiling"] = (bits & (1u << 4)) != 0;
        props["keepScrollBarsVisible"] = (bits & (1u << 21)) != 0;
        props["rightToLeft"] = (bits & (1u << 22)) != 0;
    }

    private static void ApplyFormPropertyToDictionary(string formKey, Dictionary<string, object?> props, string propertyName, JsonElement value, string? patchDir)
    {
        var normalizedPropertyName = propertyName.ToLowerInvariant() switch
        {
            "backcolor" => "formBackColor",
            "forecolor" => "formForeColor",
            "bordercolor" => "formBorderColor",
            "caption" => "Caption",
            "borderstyle" => "formBorderStyle",
            "mousepointer" => "formMousePointer",
            "scrollbars" => "formScrollBars",
            "cycle" => "formCycle",
            "specialeffect" => "formSpecialEffect",
            "picturealignment" => "formPictureAlignment",
            "picturesizemode" => "formPictureSizeMode",
            "zoom" => "formZoom",
            "widthpt" => "displayedWidthPt",
            "heightpt" => "displayedHeightPt",
            "width" => "Width",
            "height" => "Height",
            "clientwidth" => "ClientWidth",
            "clientheight" => "ClientHeight",
            "left" => "Left",
            "top" => "Top",
            "clientleft" => "ClientLeft",
            "clienttop" => "ClientTop",
            "startupposition" => "StartUpPosition",
            "showmodal" => "ShowModal",
            "whatsthisbutton" => "WhatsThisButton",
            "whatsthishelp" => "WhatsThisHelp",
            "tag" => "Tag",
            "drawbuffer" => "DrawBuffer",
            _ => propertyName
        };

        switch (normalizedPropertyName.ToLowerInvariant())
        {
            case "formbackcolor":
            case "formforecolor":
            case "formbordercolor":
                var colorStr = RequireColorLikeString(formKey, normalizedPropertyName, value);
                props[CanonicalPropertyName(normalizedPropertyName)] = colorStr;
                props[CanonicalPropertyName(normalizedPropertyName) + "Raw"] = MsFormsFactoryBinary.ParseColor(colorStr, 0);
                break;
            case "formcaption":
            case "caption":
            case "tag":
                props[CanonicalPropertyName(normalizedPropertyName)] = RequireString(formKey, normalizedPropertyName, value);
                break;
            case "formbooleanproperties":
                SetFormBooleanProperties(props, RequireUInt32Like(formKey, normalizedPropertyName, value));
                break;
            case "enabled":
            case "picturetiling":
            case "keepscrollbarsvisible":
            case "righttoleft":
            case "showmodal":
            case "whatsthisbutton":
            case "whatsthishelp":
                var boolVal = RequireBoolean(formKey, normalizedPropertyName, value);
                if (normalizedPropertyName.Equals("showmodal", StringComparison.OrdinalIgnoreCase) ||
                    normalizedPropertyName.Equals("whatsthisbutton", StringComparison.OrdinalIgnoreCase) ||
                    normalizedPropertyName.Equals("whatsthishelp", StringComparison.OrdinalIgnoreCase))
                {
                    props[CanonicalPropertyName(normalizedPropertyName)] = boolVal;
                }
                else
                {
                    SetFormBooleanPropertyBit(props, normalizedPropertyName, boolVal);
                }
                break;
            case "formborderstyle":
            case "formmousepointer":
            case "formscrollbars":
            case "formcycle":
            case "formspecialeffect":
            case "formpicturealignment":
            case "formpicturesizemode":
                props[CanonicalPropertyName(normalizedPropertyName)] = RequireUInt16(formKey, normalizedPropertyName, value);
                break;
            case "formzoom":
            case "nextavailableid":
                props[CanonicalPropertyName(normalizedPropertyName)] = RequireUInt32(formKey, normalizedPropertyName, value);
                break;
            case "formgroupcount":
                props["formGroupCount"] = RequireNonNegativeInt32(formKey, normalizedPropertyName, value);
                break;
            case "displayedwidth":
            case "displayedheight":
            case "logicalwidth":
            case "logicalheight":
            case "scrollleft":
            case "scrolltop":
                props[CanonicalPropertyName(normalizedPropertyName)] = RequireInt32(formKey, normalizedPropertyName, value);
                break;
            case "displayedwidthpt":
            case "displayedheightpt":
            case "logicalwidthpt":
            case "logicalheightpt":
            case "scrollleftpt":
            case "scrolltoppt":
            case "left":
            case "top":
            case "width":
            case "height":
            case "clientleft":
            case "clienttop":
            case "clientwidth":
            case "clientheight":
                if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var doubleVal))
                {
                    var canonicalName = CanonicalPropertyName(normalizedPropertyName);
                    props[canonicalName] = doubleVal;
                    
                    if (canonicalName.EndsWith("Pt", StringComparison.Ordinal) && canonicalName.Length > 2)
                    {
                        var rawCanonical = canonicalName.Substring(0, canonicalName.Length - 2);
                        if (ToRawPoints(doubleVal) is int raw)
                        {
                            props[rawCanonical] = raw;
                        }
                    }
                }
                else
                {
                    throw new CliException($"Property '{normalizedPropertyName}' for '{formKey}' must be a number.");
                }
                break;
            case "startupposition":
                props[CanonicalPropertyName(normalizedPropertyName)] = RequireInt32(formKey, normalizedPropertyName, value);
                break;
            case "drawbuffer":
                props["DrawBuffer"] = RequireInt32(formKey, normalizedPropertyName, value);
                break;
            case "formdrawbuffer":
                props["formDrawBuffer"] = RequireUInt32Like(formKey, normalizedPropertyName, value);
                break;
            default:
                throw new CliException($"Property '{propertyName}' is not supported for the root UserForm '{formKey}'.");
        }
    }

    private static IEnumerable<KeyValuePair<string, JsonElement>> OrderPropertyApplications(
        IEnumerable<KeyValuePair<string, JsonElement>> properties) =>
        properties.OrderBy(pair => pair.Key.ToLowerInvariant() switch
        {
            "formbooleanproperties" or "fonteffects" or "fontweight" or "sitebitflags" => 0,
            _ => 1
        });

    private static bool IsSupportedProperty(string propertyName, params IEnumerable<string>[] groups) =>
        groups.Any(group => group.Contains(propertyName, StringComparer.OrdinalIgnoreCase));

    internal static bool SupportsExportedObjectProperty(string controlType, string propertyName)
    {
        if (!ObjectPropertyNames.Contains(propertyName) && !FormSitePropertyNames.Contains(propertyName))
        {
            return false;
        }

        if (FormSitePropertyNames.Contains(propertyName))
        {
            return true;
        }

        var normalizedType = controlType.ToLowerInvariant();
        var font = FontPropertyNames;
        var morph = MorphVariousPropertyNames;
        var container = ContainerPropertyNames;
        var tabs = TabArrayPropertyNames;
        return normalizedType switch
        {
            "commandbutton" => IsSupportedProperty(propertyName, font,
                ["caption", "backColor", "foreColor", "enabled", "locked", "backStyle", "wordWrap", "autoSize",
                 "imeMode", "picturePosition", "mousePointer", "accelerator", "takeFocusOnClick", "picture", "mouseIcon"]),
            "label" => IsSupportedProperty(propertyName, font,
                ["caption", "backColor", "foreColor", "borderColor", "enabled", "backStyle", "wordWrap", "autoSize",
                 "imeMode", "picturePosition", "mousePointer", "accelerator", "borderStyle", "specialEffect", "picture", "mouseIcon"]),
            "textbox" => IsSupportedProperty(propertyName, font, morph,
                ["value", "backColor", "foreColor", "borderColor", "maxLength", "passwordChar", "scrollBars",
                 "mousePointer", "borderStyle", "specialEffect", "picture", "mouseIcon"]),
            "checkbox" or "optionbutton" or "togglebutton" => IsSupportedProperty(propertyName, font,
                ["value", "caption", "groupName", "backColor", "foreColor", "enabled", "locked", "backStyle",
                 "alignment", "wordWrap", "autoSize", "imeMode", "mousePointer", "multiSelect", "picturePosition",
                 "specialEffect", "accelerator", "picture", "mouseIcon"]),
            "combobox" => IsSupportedProperty(propertyName, font, morph,
                ["value", "backColor", "foreColor", "borderColor", "borderStyle", "scrollBars", "displayStyle",
                 "mousePointer", "listWidth", "boundColumn", "textColumn", "columnCount", "listRows", "matchEntry",
                 "listStyle", "showDropButtonWhen", "dropButtonStyle", "maxLength", "specialEffect", "picture", "mouseIcon"]),
            "listbox" => IsSupportedProperty(propertyName, font, morph,
                ["value", "backColor", "foreColor", "borderColor", "borderStyle", "scrollBars", "displayStyle",
                 "mousePointer", "listWidth", "boundColumn", "textColumn", "columnCount", "matchEntry", "listStyle",
                 "multiSelect", "specialEffect", "picture", "mouseIcon"]),
            "scrollbar" => IsSupportedProperty(propertyName, morph,
                ["value", "backColor", "foreColor", "mousePointer", "min", "max", "position", "smallChange",
                 "largeChange", "orientation", "delay", "proportionalThumb", "mouseIcon"]),
            "spinbutton" => IsSupportedProperty(propertyName, morph,
                ["value", "backColor", "foreColor", "mousePointer", "min", "max", "position", "smallChange",
                 "orientation", "delay", "mouseIcon"]),
            "image" => IsSupportedProperty(propertyName,
                ["backColor", "borderColor", "enabled", "locked", "imeMode", "autoSize", "borderStyle", "mousePointer",
                 "pictureSizeMode", "specialEffect", "picture", "pictureAlignment", "pictureTiling", "mouseIcon"]),
            "tabstrip" => IsSupportedProperty(propertyName, font, tabs, ["caption", "value", "listIndex", "style", "tabStyle", "mouseIcon"]),
            "frame" => IsSupportedProperty(propertyName, container, ["caption", "specialEffect", "formSpecialEffect"]),
            "multipage" => IsSupportedProperty(propertyName, font, container, tabs, ["value", "listIndex", "style", "tabStyle"]),
            "page" => IsSupportedProperty(propertyName, container, ["caption"]),
            _ => false
        };
    }

    internal static bool SupportsGeneratedObjectProperty(string controlType, string propertyName) =>
        SupportsExportedObjectProperty(controlType, propertyName) ||
        controlType.Equals("Frame", StringComparison.OrdinalIgnoreCase) && FontPropertyNames.Contains(propertyName, StringComparer.OrdinalIgnoreCase);

    internal static bool SupportsExportedRootProperty(string propertyName) =>
        RootFormPropertyNames.Contains(propertyName);

    private static bool IsContainerControlType(string controlType) =>
        controlType.Equals("Frame", StringComparison.OrdinalIgnoreCase) ||
        controlType.Equals("MultiPage", StringComparison.OrdinalIgnoreCase) ||
        controlType.Equals("Page", StringComparison.OrdinalIgnoreCase);

    private static uint RequireUInt32(string controlName, string propertyName, JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetUInt32(out var parsed))
        {
            throw new CliException($"Property '{propertyName}' for '{controlName}' must be a 32-bit unsigned integer.");
        }

        return parsed;
    }

    private static uint RequireUInt32Like(string controlName, string propertyName, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetUInt32(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString()?.Trim() ?? string.Empty;
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
                uint.TryParse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex))
            {
                return hex;
            }

            if (uint.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        throw new CliException($"Property '{propertyName}' for '{controlName}' must be a 32-bit unsigned integer or 0x-prefixed hexadecimal string.");
    }
}
