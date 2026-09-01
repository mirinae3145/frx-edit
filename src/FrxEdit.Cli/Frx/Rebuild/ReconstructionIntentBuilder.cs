internal static class ReconstructionIntentBuilder
{
    private static readonly HashSet<string> SiteProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "siteBitFlags", "tabIndex", "controlTipText", "controlSource", "rowSource", "tag", "helpContextId", "groupId",
        "tabStop", "visible", "default", "cancel", "siteAutoSize", "preserveHeight", "fitToParent",
        "selectChild"
    };

    private static readonly HashSet<string> MultiPageProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "value", "listIndex", "style", "tabStyle",
        "tabCaptions", "tabTooltips", "tabNames", "tabTags", "tabAccelerators", "tabFlags"
    };

    private static readonly HashSet<string> FrmRootProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "caption", "clientHeight", "clientLeft", "clientTop", "clientWidth", "startUpPosition",
        "showModal", "whatsThisButton", "whatsThisHelp", "tag", "drawBuffer",
        "left", "top", "width", "height"
    };

    private static readonly HashSet<string> BinaryRootProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "widthPt", "heightPt", "displayedWidth", "displayedHeight", "logicalWidth", "logicalHeight",
        "logicalWidthPt", "logicalHeightPt", "scrollLeft", "scrollTop", "scrollLeftPt", "scrollTopPt"
    };

    private static readonly HashSet<string> MetadataProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "$action", "$newName", "type", "parent", "leftPt", "topPt", "widthPt", "heightPt",
        "left", "top", "width", "height", "rawWidth", "rawHeight"
    };

    public static ReconstructionIntent Build(
        LayoutInspection source,
        LayoutInspection target,
        PatchDocument? patch,
        string? formName)
    {
        if (patch is null)
        {
            return ReconstructionIntent.Empty;
        }

        var objectControls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var siteControls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var multiPageControls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var frmRootProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rootBinaryChanged = false;
        var structuralChanged =
            patch.Add is { Count: > 0 } ||
            patch.Remove is { Count: > 0 } ||
            patch.Move is { Count: > 0 } ||
            patch.Renames is { Count: > 0 };

        var sourceByName = source.Controls.ToDictionary(control => control.Name, StringComparer.OrdinalIgnoreCase);
        var targetByName = target.Controls.ToDictionary(control => control.Name, StringComparer.OrdinalIgnoreCase);
        var reverseRenames = (patch.Renames ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase))
            .ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var (entityName, requestedProperties) in patch.Properties ??
                 new Dictionary<string, Dictionary<string, JsonElement>>(StringComparer.OrdinalIgnoreCase))
        {
            if (IsRootName(entityName, formName))
            {
                foreach (var propertyName in requestedProperties.Keys)
                {
                    if (FrmRootProperties.Contains(propertyName))
                    {
                        if (!RootPropertyEqual(source.FrxFormControl, target.FrxFormControl, propertyName))
                        {
                            frmRootProperties.Add(propertyName);
                        }
                    }
                    else if (BinaryRootProperties.Contains(propertyName))
                    {
                        if (!RootPropertyEqual(source.FrxFormControl, target.FrxFormControl, propertyName))
                        {
                            rootBinaryChanged = true;
                        }
                    }
                    else if (MetadataProperties.Contains(propertyName))
                    {
                        continue;
                    }
                    else if (!RootPropertyEqual(source.FrxFormControl, target.FrxFormControl, propertyName))
                    {
                        rootBinaryChanged = true;
                    }
                }

                continue;
            }

            var sourceName = reverseRenames.TryGetValue(entityName, out var oldName) ? oldName : entityName;
            if (!sourceByName.TryGetValue(sourceName, out var sourceControl) ||
                !targetByName.TryGetValue(entityName, out var targetControl))
            {
                continue;
            }

            foreach (var propertyName in requestedProperties.Keys)
            {
                if (MetadataProperties.Contains(propertyName) ||
                    EffectivePropertyEqual(sourceControl, targetControl, propertyName))
                {
                    continue;
                }

                if (targetControl.Type.Equals("Page", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(targetControl.Parent) &&
                    (propertyName.Equals("caption", StringComparison.OrdinalIgnoreCase) ||
                     propertyName.Equals("tabCaption", StringComparison.OrdinalIgnoreCase)))
                {
                    multiPageControls.Add(targetControl.Parent);
                }

                if (SiteProperties.Contains(propertyName))
                {
                    siteControls.Add(targetControl.Name);
                }
                else if (targetControl.Type.Equals("MultiPage", StringComparison.OrdinalIgnoreCase) &&
                         MultiPageProperties.Contains(propertyName))
                {
                    multiPageControls.Add(targetControl.Name);
                }
                else
                {
                    objectControls.Add(targetControl.Name);
                }
            }
        }

        foreach (var (name, _) in patch.Layout ?? new Dictionary<string, LayoutPatch>(StringComparer.OrdinalIgnoreCase))
        {
            var sourceName = reverseRenames.TryGetValue(name, out var oldName) ? oldName : name;
            if (sourceByName.TryGetValue(sourceName, out var sourceControl) &&
                targetByName.TryGetValue(name, out var targetControl) &&
                (sourceControl.Left != targetControl.Left ||
                 sourceControl.Top != targetControl.Top ||
                 sourceControl.RawWidth != targetControl.RawWidth ||
                 sourceControl.RawHeight != targetControl.RawHeight))
            {
                siteControls.Add(targetControl.Name);
            }
        }

        foreach (var (oldName, newName) in patch.Renames ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(newName))
            {
                siteControls.Add(newName);
                if (sourceByName.TryGetValue(oldName, out var renamedControl) &&
                    renamedControl.Type.Equals("Page", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(renamedControl.Parent))
                {
                    multiPageControls.Add(renamedControl.Parent);
                }
            }
        }

        foreach (var (name, requestedParent) in patch.Move ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase))
        {
            siteControls.Add(name);
            if (sourceByName.TryGetValue(name, out var movedSource) &&
                movedSource.Type.Equals("Page", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(movedSource.Parent))
            {
                multiPageControls.Add(movedSource.Parent);
            }
            if (!string.IsNullOrWhiteSpace(requestedParent) &&
                targetByName.TryGetValue(requestedParent, out var requestedParentControl) &&
                requestedParentControl.Type.Equals("MultiPage", StringComparison.OrdinalIgnoreCase))
            {
                multiPageControls.Add(requestedParent);
            }
        }

        foreach (var add in patch.Add ?? [])
        {
            var addedType = add.Type;
            var addedParent = add.Parent;
            if (!string.IsNullOrWhiteSpace(add.Name) && targetByName.TryGetValue(add.Name, out var addedControl))
            {
                addedType = addedControl.Type;
                addedParent ??= addedControl.Parent;
            }

            if (addedType?.Equals("Page", StringComparison.OrdinalIgnoreCase) == true &&
                !string.IsNullOrWhiteSpace(addedParent))
            {
                multiPageControls.Add(addedParent);
            }
        }

        foreach (var removedName in patch.Remove ?? [])
        {
            if (sourceByName.TryGetValue(removedName, out var removedControl) &&
                removedControl.Type.Equals("Page", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(removedControl.Parent))
            {
                multiPageControls.Add(removedControl.Parent);
            }
        }

        return new ReconstructionIntent(
            objectControls,
            siteControls,
            multiPageControls,
            frmRootProperties,
            rootBinaryChanged,
            structuralChanged);
    }

    private static bool IsRootName(string name, string? formName) =>
        (!string.IsNullOrWhiteSpace(formName) && name.Equals(formName, StringComparison.OrdinalIgnoreCase)) ||
        name.Equals("UserForm", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Form", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("root", StringComparison.OrdinalIgnoreCase);

    internal static bool RootPropertyEqual(
        Dictionary<string, object?>? source,
        Dictionary<string, object?>? target,
        string propertyName)
    {
        var key = CanonicalRootStorageKey(propertyName);
        object? sourceValue = null;
        object? targetValue = null;
        var hasSource = source is not null && source.TryGetValue(key, out sourceValue);
        var hasTarget = target is not null && target.TryGetValue(key, out targetValue);
        if (!hasSource && propertyName.Equals("formGroupCount", StringComparison.OrdinalIgnoreCase))
        {
            hasSource = true;
            sourceValue = 0;
        }
        if (!hasTarget && propertyName.Equals("formGroupCount", StringComparison.OrdinalIgnoreCase))
        {
            hasTarget = true;
            targetValue = 0;
        }
        return hasSource == hasTarget && (!hasSource || ValuesEqual(sourceValue, targetValue, propertyName));
    }

    private static string CanonicalRootStorageKey(string propertyName) =>
        propertyName.ToLowerInvariant() switch
        {
            "caption" => "Caption",
            "clientheight" => "ClientHeight",
            "clientleft" => "ClientLeft",
            "clienttop" => "ClientTop",
            "clientwidth" => "ClientWidth",
            "startupposition" => "StartUpPosition",
            "showmodal" => "ShowModal",
            "whatsthisbutton" => "WhatsThisButton",
            "whatsthishelp" => "WhatsThisHelp",
            "tag" => "Tag",
            "left" => "Left",
            "top" => "Top",
            "width" => "Width",
            "height" => "Height",
            "widthpt" => "displayedWidthPt",
            "heightpt" => "displayedHeightPt",
            "drawbuffer" => "DrawBuffer",
            "formdrawbuffer" => "formDrawBuffer",
            "backcolor" => "formBackColor",
            "forecolor" => "formForeColor",
            "bordercolor" => "formBorderColor",
            "borderstyle" => "formBorderStyle",
            "mousepointer" => "formMousePointer",
            "scrollbars" => "formScrollBars",
            "cycle" => "formCycle",
            "specialeffect" => "formSpecialEffect",
            "picturealignment" => "formPictureAlignment",
            "picturesizemode" => "formPictureSizeMode",
            "zoom" => "formZoom",
            "enabled" or "picturetiling" or "keepscrollbarsvisible" or "righttoleft" => "formBooleanProperties",
            "nextavailableid" => "nextAvailableId",
            "displayedwidth" => "displayedWidth",
            "displayedheight" => "displayedHeight",
            "displayedwidthpt" => "displayedWidthPt",
            "displayedheightpt" => "displayedHeightPt",
            "logicalwidth" => "logicalWidth",
            "logicalheight" => "logicalHeight",
            "logicalwidthpt" => "logicalWidthPt",
            "logicalheightpt" => "logicalHeightPt",
            "scrollleft" => "scrollLeft",
            "scrolltop" => "scrollTop",
            "scrollleftpt" => "scrollLeftPt",
            "scrolltoppt" => "scrollTopPt",
            "formbooleanproperties" => "formBooleanProperties",
            _ => propertyName.StartsWith("form", StringComparison.OrdinalIgnoreCase)
                ? propertyName
                : "form" + char.ToUpperInvariant(propertyName[0]) + propertyName[1..]
        };

    internal static bool EffectivePropertyEqual(ControlInfo source, ControlInfo target, string propertyName)
    {
        var canonicalName = CanonicalControlPropertyName(source.Type, propertyName);
        var sourceValue = EffectivePropertyValue(source, canonicalName, out var sourcePresent);
        var targetValue = EffectivePropertyValue(target, canonicalName, out var targetPresent);
        return sourcePresent == targetPresent && (!sourcePresent || ValuesEqual(sourceValue, targetValue, canonicalName));
    }

    private static object? EffectivePropertyValue(ControlInfo control, string propertyName, out bool present)
    {
        if (control.Properties?.TryGetValue(propertyName, out var value) is true)
        {
            present = true;
            return value;
        }

        if (TryGetFileDefault(control.Type, propertyName, out value))
        {
            present = true;
            return value;
        }

        present = false;
        return null;
    }

    private static bool TryGetFileDefault(string type, string propertyName, out object? value)
    {
        value = propertyName.ToLowerInvariant() switch
        {
            "tabstop" => true,
            "visible" => true,
            "default" => false,
            "cancel" => false,
            "siteautosize" => true,
            "preserveheight" => false,
            "fittoparent" => false,
            "selectchild" => false,
            "fontname" => "Tahoma",
            "fontsize" => 8d,
            _ => null
        };

        if (value is not null)
        {
            return true;
        }

        var key = $"{type.ToLowerInvariant()}/{propertyName.ToLowerInvariant()}";
        value = key switch
        {
            "commandbutton/enabled" => true,
            "commandbutton/locked" => false,
            "commandbutton/wordwrap" => false,
            "commandbutton/autosize" => false,
            "commandbutton/backcolor" => "&H8000000F&",
            "commandbutton/forecolor" => "&H80000012&",
            "textbox/enabled" => true,
            "textbox/locked" => false,
            "textbox/backcolor" => "&H80000005&",
            "textbox/forecolor" => "&H80000008&",
            "textbox/bordercolor" => "&H80000006&",
            "textbox/backstyle" => 1,
            "textbox/wordwrap" => true,
            "textbox/autosize" => false,
            "textbox/autotab" => false,
            "textbox/autowordselect" => true,
            "textbox/dragbehavior" => 0,
            "textbox/enterfieldbehavior" => 0,
            "textbox/enterkeybehavior" => false,
            "textbox/hideselection" => true,
            "textbox/integralheight" => true,
            "textbox/multiline" => false,
            "textbox/selectionmargin" => true,
            "textbox/tabkeybehavior" => false,
            "textbox/imemode" => 0,
            "textbox/maxlength" => 0,
            "textbox/scrollbars" => 0,
            "textbox/borderstyle" => 1,
            "textbox/specialeffect" => 2,
            "textbox/textalign" => "left",
            "combobox/backcolor" or "listbox/backcolor" => "&H80000005&",
            "combobox/forecolor" or "listbox/forecolor" => "&H80000008&",
            "combobox/bordercolor" or "listbox/bordercolor" => "&H80000006&",
            "combobox/borderstyle" or "listbox/borderstyle" => 1,
            "combobox/specialeffect" or "listbox/specialeffect" => 2,
            "combobox/boundcolumn" or "listbox/boundcolumn" => 1,
            "combobox/textcolumn" or "listbox/textcolumn" => -1,
            "combobox/columncount" or "listbox/columncount" => 1,
            "combobox/listwidth" or "listbox/listwidth" => 0,
            "combobox/liststyle" or "listbox/liststyle" => 0,
            "combobox/matchentry" or "listbox/matchentry" => 2,
            "combobox/textalign" or "listbox/textalign" => "left",
            "combobox/listrows" => 8,
            "combobox/dropbuttonstyle" => 1,
            "combobox/showdropbuttonwhen" => 0,
            "combobox/maxlength" => 0,
            "listbox/multiselect" => 0,
            "listbox/scrollbars" => 3,
            "checkbox/value" or "optionbutton/value" => "0",
            "checkbox/backcolor" or "optionbutton/backcolor" => "&H8000000F&",
            "checkbox/forecolor" or "optionbutton/forecolor" => "&H80000008&",
            "checkbox/enabled" or "optionbutton/enabled" => true,
            "checkbox/locked" or "optionbutton/locked" => false,
            "checkbox/backstyle" or "optionbutton/backstyle" => 1,
            "checkbox/alignment" or "optionbutton/alignment" => 1,
            "checkbox/wordwrap" or "optionbutton/wordwrap" => true,
            "checkbox/autosize" or "optionbutton/autosize" => false,
            "checkbox/imemode" or "optionbutton/imemode" => 0,
            "checkbox/pictureposition" or "optionbutton/pictureposition" => 458753,
            "checkbox/specialeffect" or "optionbutton/specialeffect" => 0,
            "checkbox/multiselect" or "optionbutton/multiselect" => 0,
            "checkbox/textalign" or "optionbutton/textalign" => "left",
            _ => null
        };
        return value is not null;
    }

    private static string CanonicalControlPropertyName(string controlType, string propertyName) =>
        propertyName.ToLowerInvariant() switch
        {
            "caption" when controlType.Equals("Frame", StringComparison.OrdinalIgnoreCase) => "formCaption",
            "caption" when controlType.Equals("Page", StringComparison.OrdinalIgnoreCase) => "tabCaption",
            "value" when controlType.Equals("TabStrip", StringComparison.OrdinalIgnoreCase) => "listIndex",
            "style" or "tabstyle" when
                controlType.Equals("TabStrip", StringComparison.OrdinalIgnoreCase) ||
                controlType.Equals("MultiPage", StringComparison.OrdinalIgnoreCase) => "tabStyle",
            "specialeffect" or "formspecialeffect" when
                controlType.Equals("Frame", StringComparison.OrdinalIgnoreCase) => "formSpecialEffect",
            "groupname" => "groupName",
            "fontname" => "fontName",
            "fontsize" => "fontSize",
            "fontweight" => "fontWeight",
            "fonteffects" => "fontEffects",
            "fontitalic" => "fontItalic",
            "fontunderline" => "fontUnderline",
            "fontstrikethrough" => "fontStrikethrough",
            "fontcharset" => "fontCharSet",
            "fontpitchandfamily" => "fontPitchAndFamily",
            "controltiptext" => "controlTipText",
            "controlsource" => "controlSource",
            "rowsource" => "rowSource",
            "sitebitflags" => "siteBitFlags",
            "backcolor" => "backColor",
            "forecolor" => "foreColor",
            "bordercolor" => "borderColor",
            "tabindex" => "tabIndex",
            "tabstop" => "tabStop",
            "siteautosize" => "siteAutoSize",
            "preserveheight" => "preserveHeight",
            "fittoparent" => "fitToParent",
            "selectchild" => "selectChild",
            "backstyle" => "backStyle",
            "wordwrap" => "wordWrap",
            "autosize" => "autoSize",
            "autotab" => "autoTab",
            "autowordselect" => "autoWordSelect",
            "hideselection" => "hideSelection",
            "integralheight" => "integralHeight",
            "multiline" => "multiLine",
            "selectionmargin" => "selectionMargin",
            "enterkeybehavior" => "enterKeyBehavior",
            "tabkeybehavior" => "tabKeyBehavior",
            "enterfieldbehavior" => "enterFieldBehavior",
            "dragbehavior" => "dragBehavior",
            "imemode" => "imeMode",
            "takefocusonclick" => "takeFocusOnClick",
            "maxlength" => "maxLength",
            "passwordchar" => "passwordChar",
            "scrollbars" => "scrollBars",
            "specialeffect" => "specialEffect",
            "borderstyle" => "borderStyle",
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
            "columnheads" => "columnHeads",
            "matchrequired" => "matchRequired",
            "mousepointer" => "mousePointer",
            "pictureposition" => "picturePosition",
            "picturesizemode" => "pictureSizeMode",
            "picturealignment" => "pictureAlignment",
            "picturetiling" => "pictureTiling",
            "textalign" => "textAlign",
            "paragraphalign" => "paragraphAlign",
            "tabcaptions" => "tabCaptions",
            "tabtooltips" => "tabTooltips",
            "tabnames" => "tabNames",
            "tabtags" => "tabTags",
            "tabaccelerators" => "tabAccelerators",
            "tabflags" => "tabFlags",
            _ => propertyName
        };

    private static bool ValuesEqual(object? left, object? right, string propertyName)
    {
        if (propertyName.EndsWith("Color", StringComparison.OrdinalIgnoreCase))
        {
            var leftColor = MsFormsFactoryBinary.ParseColor(left?.ToString(), uint.MaxValue);
            var rightColor = MsFormsFactoryBinary.ParseColor(right?.ToString(), uint.MaxValue);
            return leftColor == rightColor;
        }

        if (TryDecimal(left, out var leftNumber) && TryDecimal(right, out var rightNumber))
        {
            return leftNumber == rightNumber;
        }

        var leftJson = JsonSerializer.Serialize(left, FrxEditApp.JsonOptions);
        var rightJson = JsonSerializer.Serialize(right, FrxEditApp.JsonOptions);
        return leftJson.Equals(rightJson, StringComparison.Ordinal);
    }

    private static bool TryDecimal(object? value, out decimal number)
    {
        switch (value)
        {
            case byte b: number = b; return true;
            case sbyte b: number = b; return true;
            case short s: number = s; return true;
            case ushort s: number = s; return true;
            case int i: number = i; return true;
            case uint i: number = i; return true;
            case long l: number = l; return true;
            case ulong l: number = l; return true;
            case float f: number = (decimal)f; return true;
            case double d: number = (decimal)d; return true;
            case decimal d: number = d; return true;
            case JsonElement { ValueKind: JsonValueKind.Number } element when element.TryGetDecimal(out var d):
                number = d;
                return true;
            default:
                number = 0;
                return false;
        }
    }
}
