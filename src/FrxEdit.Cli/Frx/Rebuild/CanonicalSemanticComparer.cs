using System.Security.Cryptography;

internal static class CanonicalSemanticComparer
{
    private static readonly string[] ExtraControlProperties =
    [
        "caption", "text", "value", "tag", "controlTipText", "controlSource", "rowSource",
        "runtimeLicKey", "helpContextId", "groupId", "groupName", "accelerator", "textAlign",
        "paragraphAlign", "backColor", "foreColor", "borderColor", "fontName", "fontSize",
        "fontWeight", "fontEffects", "fontBold", "fontItalic", "fontUnderline", "fontStrikethrough",
        "fontCharSet", "fontPitchAndFamily", "enabled", "visible", "locked", "siteBitFlags", "tabIndex", "tabStop",
        "default", "cancel", "streamed", "siteAutoSize", "preserveHeight", "fitToParent", "selectChild",
        "promoteControls", "backStyle", "alignment", "wordWrap", "autoSize", "autoTab",
        "autoWordSelect", "hideSelection", "integralHeight", "multiLine", "selectionMargin",
        "enterKeyBehavior", "tabKeyBehavior", "enterFieldBehavior", "dragBehavior", "imeMode",
        "takeFocusOnClick", "maxLength", "passwordChar", "scrollBars", "keepScrollBarsVisible",
        "rightToLeft", "specialEffect", "borderStyle", "displayStyle", "listWidth", "boundColumn",
        "textColumn", "columnCount", "listRows", "matchEntry", "listStyle", "showDropButtonWhen",
        "dropButtonStyle", "multiSelect", "columnHeads", "matchRequired", "editable", "mousePointer",
        "picturePosition", "picture", "mouseIcon", "pictureSizeMode", "pictureAlignment", "pictureTiling",
        "min", "max", "position", "smallChange", "largeChange", "orientation", "delay",
        "proportionalThumb", "logicalWidth", "logicalHeight", "scrollLeft", "scrollTop",
        "logicalWidthPt", "logicalHeightPt", "scrollLeftPt", "scrollTopPt", "tabNames", "tabCaptions",
        "tabTags", "tabTooltips", "tabAccelerators", "tabFlags", "transitionEffect", "transitionPeriod",
        "formBooleanProperties", "formDrawBuffer", "formDesignExData", "formSpecialEffect", "listIndex", "tabStyle", "style"
    ];

    private static readonly string[] RootProperties =
    [
        "Caption", "ClientHeight", "ClientLeft", "ClientTop", "ClientWidth", "StartUpPosition", "ShowModal",
        "Tag", "Left", "Top", "Width", "Height", "DrawBuffer", "WhatsThisButton", "WhatsThisHelp",
        "formCaption", "formBackColor", "formForeColor",
        "formBorderColor", "formBorderStyle", "formMousePointer", "formScrollBars", "formCycle",
        "formSpecialEffect", "formPictureAlignment", "formPictureSizeMode", "formZoom",
        "formPicture", "formMouseIcon", "formBooleanProperties", "formDrawBuffer", "formDesignExData", "displayedWidth",
        "displayedHeight", "logicalWidth", "logicalHeight", "scrollLeft", "scrollTop", "nextAvailableId",
        "formGroupCount"
    ];

    public static IReadOnlyList<string> Compare(
        LayoutInspection expected,
        LayoutInspection actual,
        string? expectedFormName = null,
        string? actualFormName = null)
    {
        var differences = new List<string>();
        if ((expectedFormName is not null || actualFormName is not null) &&
            !string.Equals(expectedFormName, actualFormName, StringComparison.Ordinal))
        {
            differences.Add($"formName: {Display(expectedFormName)} != {Display(actualFormName)}");
        }

        var expectedByName = expected.Controls.ToDictionary(control => control.Name, StringComparer.OrdinalIgnoreCase);
        var actualByName = actual.Controls.ToDictionary(control => control.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var name in expectedByName.Keys.OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
        {
            if (!actualByName.TryGetValue(name, out var actualControl))
            {
                differences.Add($"missing rebuilt control: {name}");
                continue;
            }

            var expectedControl = expectedByName[name];
            CompareValue(name, "type", expectedControl.Type, actualControl.Type, differences);
            CompareValue(name, "parent", expectedControl.Parent ?? string.Empty, actualControl.Parent ?? string.Empty, differences);
            CompareValue(name, "left", expectedControl.Left, actualControl.Left, differences);
            CompareValue(name, "top", expectedControl.Top, actualControl.Top, differences);
            CompareValue(name, "width", expectedControl.RawWidth, actualControl.RawWidth, differences);
            CompareValue(name, "height", expectedControl.RawHeight, actualControl.RawHeight, differences);

            var expectedProperties = CanonicalProperties(expectedControl);
            var actualProperties = CanonicalProperties(actualControl);
            foreach (var propertyName in expectedProperties.Keys.Concat(actualProperties.Keys)
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
            {
                var expectedPresent = expectedProperties.TryGetValue(propertyName, out var expectedValue);
                var actualPresent = actualProperties.TryGetValue(propertyName, out var actualValue);
                if (expectedPresent != actualPresent)
                {
                    differences.Add(
                        $"{name}.{propertyName} presence: {expectedPresent} ({Display(expectedValue)}) != " +
                        $"{actualPresent} ({Display(actualValue)})");
                }
                else if (expectedPresent && !ValuesEqual(propertyName, expectedValue, actualValue))
                {
                    differences.Add($"{name}.{propertyName}: {Display(expectedValue)} != {Display(actualValue)}");
                }
            }
        }

        foreach (var name in actualByName.Keys.Except(expectedByName.Keys, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
        {
            differences.Add($"extra rebuilt control: {name}");
        }

        var expectedRootProperties = CanonicalRootProperties(expected.FrxFormControl);
        var actualRootProperties = CanonicalRootProperties(actual.FrxFormControl);
        foreach (var propertyName in RootProperties)
        {
            object? expectedValue = null;
            object? actualValue = null;
            var expectedPresent = expectedRootProperties.TryGetValue(propertyName, out expectedValue);
            var actualPresent = actualRootProperties.TryGetValue(propertyName, out actualValue);
            if (expectedPresent != actualPresent)
            {
                differences.Add(
                    $"root.{propertyName} presence: {expectedPresent} ({Display(expectedValue)}) != " +
                    $"{actualPresent} ({Display(actualValue)})");
            }
            else if (expectedPresent && !ValuesEqual(propertyName, expectedValue, actualValue))
            {
                differences.Add($"root.{propertyName}: {Display(expectedValue)} != {Display(actualValue)}");
            }
        }

        CompareMultiPagePageOrder(expected.Controls, actual.Controls, differences);
        AddParserValidationDifferences(actual, differences);

        return differences;
    }

    private static void CompareMultiPagePageOrder(
        IReadOnlyList<ControlInfo> expected,
        IReadOnlyList<ControlInfo> actual,
        List<string> differences)
    {
        var expectedOrders = GetMultiPagePageOrders(expected);
        var actualOrders = GetMultiPagePageOrders(actual);
        foreach (var parent in expectedOrders.Keys.Concat(actualOrders.Keys)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
        {
            expectedOrders.TryGetValue(parent, out var expectedOrder);
            actualOrders.TryGetValue(parent, out var actualOrder);
            expectedOrder ??= [];
            actualOrder ??= [];
            if (!expectedOrder.SequenceEqual(actualOrder, StringComparer.Ordinal))
            {
                differences.Add(
                    $"{parent}.multiPagePageOrder: {Display(expectedOrder)} != {Display(actualOrder)}");
            }
        }
    }

    private static Dictionary<string, string[]> GetMultiPagePageOrders(IReadOnlyList<ControlInfo> controls) =>
        controls
            .Where(control => control.Type.Equals("Page", StringComparison.OrdinalIgnoreCase))
            .GroupBy(control => control.Parent ?? "<root>", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(control => MultiPagePageIndex(control))
                    .ThenBy(control => control.Name, StringComparer.Ordinal)
                    .Select(control => control.Name)
                    .ToArray(),
                StringComparer.OrdinalIgnoreCase);

    private static decimal MultiPagePageIndex(ControlInfo control)
    {
        if (control.Properties?.TryGetValue("multiPagePageIndex", out var value) is true &&
            TryDecimal(value, out var index))
        {
            return index;
        }

        return decimal.MaxValue;
    }

    private static void AddParserValidationDifferences(LayoutInspection actual, List<string> differences)
    {
        foreach (var counter in new[] { "warningCount", "errorCount", "heuristicCount" })
        {
            if (actual.ParserValidation?.TryGetValue(counter, out var value) is true &&
                TryDecimal(value, out var count) && count != 0)
            {
                differences.Add($"parserValidation.{counter}: {Display(value)} != 0");
            }
        }

        AddExactStreamValidationDifferences(actual, "objectStreamValidations", differences);
        AddExactStreamValidationDifferences(actual, "multiPageXStreamValidations", differences);

        if (actual.FrxFormControl is null)
        {
            return;
        }

        foreach (var propertyName in actual.FrxFormControl.Keys
                     .Where(name => name.Contains("Warning", StringComparison.OrdinalIgnoreCase) ||
                                    name.Contains("Error", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
        {
            differences.Add($"root parser diagnostic present: {propertyName}");
        }
    }

    private static void AddExactStreamValidationDifferences(
        LayoutInspection actual,
        string groupName,
        List<string> differences)
    {
        if (actual.ParserValidation?.TryGetValue(groupName, out var groupValue) is not true ||
            groupValue is not IDictionary<string, object?> group)
        {
            return;
        }

        foreach (var (name, detailValue) in group.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            object? validation = null;
            if (detailValue is not IDictionary<string, object?> details ||
                !details.TryGetValue("validation", out validation) ||
                !string.Equals(validation?.ToString(), "exact", StringComparison.OrdinalIgnoreCase))
            {
                differences.Add(
                    $"parserValidation.{groupName}.{name}.validation: {Display(validation)} != \"exact\"");
            }
        }
    }

    private static Dictionary<string, object?> CanonicalProperties(ControlInfo control)
    {
        var document = new LayoutDocument(string.Empty, string.Empty, new Dictionary<string, object?>(), [control]);
        var human = HumanLayoutDocument.FromRaw(document).Controls[0];
        var result = human.Properties.ToDictionary(property => property.Name, property => property.Value, StringComparer.OrdinalIgnoreCase);

        foreach (var propertyName in ExtraControlProperties)
        {
            if (control.Properties?.TryGetValue(propertyName, out var value) is true)
            {
                result[propertyName] = value;
            }
        }
        if (!result.ContainsKey("caption") &&
            control.Properties?.TryGetValue("formCaption", out var formCaption) is true)
        {
            result["caption"] = formCaption;
        }
        if (MsFormsFactoryBinary.GetUInt32(result, "formBooleanProperties") is uint formBooleanProperties)
        {
            // Named container booleans are an editable projection of one persisted
            // word. Derive them on both sides so an in-memory patch target and a
            // strict native reread use the same canonical representation.
            result["enabled"] = (formBooleanProperties & 1u) != 0;
            result["pictureTiling"] = (formBooleanProperties & (1u << 4)) != 0;
            result["keepScrollBarsVisible"] = (formBooleanProperties & (1u << 21)) != 0;
            result["rightToLeft"] = (formBooleanProperties & (1u << 22)) != 0;
        }
        if (MsFormsFactoryBinary.GetInt32(result, "fontWeight") is int fontWeight)
        {
            result["fontBold"] = fontWeight >= 700;
        }

        // TextAlign and ParagraphAlign are two projections of the same TextProps
        // field. Patch application keeps both in the target model so serializers can
        // consume either spelling, while a strict reread can expose only one of them.
        if (!result.ContainsKey("paragraphAlign") &&
            result.TryGetValue("textAlign", out var textAlignValue) &&
            TextPropsFactory.TryParseTextAlign(textAlignValue?.ToString() ?? string.Empty, out var textAlign))
        {
            result["paragraphAlign"] = TextPropsFactory.TextAlignToParagraphAlign(textAlign);
        }
        if (!result.ContainsKey("textAlign") &&
            result.TryGetValue("paragraphAlign", out var paragraphAlignValue) &&
            TryDecimal(paragraphAlignValue, out var paragraphAlign))
        {
            result["textAlign"] = TextPropsFactory.TextAlignName(
                TextPropsFactory.ParagraphAlignToTextAlign((int)paragraphAlign));
        }

        AddDefault(result, "tabStop", true);
        AddDefault(result, "visible", true);
        AddDefault(result, "default", false);
        AddDefault(result, "cancel", false);
        AddDefault(result, "siteBitFlags", "0x00000033");
        AddDefault(result, "streamed", true);
        AddDefault(result, "siteAutoSize", true);
        AddDefault(result, "preserveHeight", false);
        AddDefault(result, "fitToParent", false);
        AddDefault(result, "selectChild", false);
        AddDefault(result, "promoteControls", false);
        AddDefault(result, "fontSize", 8d);
        AddDefault(result, "fontBold", false);
        AddDefault(result, "fontItalic", false);
        AddDefault(result, "fontUnderline", false);
        AddDefault(result, "fontStrikethrough", false);

        return result;
    }

    private static Dictionary<string, object?> CanonicalRootProperties(Dictionary<string, object?>? properties)
    {
        var result = properties is null
            ? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, object?>(properties, StringComparer.OrdinalIgnoreCase);
        AddDefault(result, "formGroupCount", 0);
        return result;
    }

    private static void AddDefault(Dictionary<string, object?> values, string name, object value)
    {
        if (!values.ContainsKey(name))
        {
            values[name] = value;
        }
    }

    private static bool ValuesEqual(string propertyName, object? left, object? right)
    {
        if (propertyName.Equals("siteBitFlags", StringComparison.OrdinalIgnoreCase))
        {
            var leftProperties = new Dictionary<string, object?> { ["value"] = left };
            var rightProperties = new Dictionary<string, object?> { ["value"] = right };
            return MsFormsFactoryBinary.GetUInt32(leftProperties, "value") ==
                   MsFormsFactoryBinary.GetUInt32(rightProperties, "value");
        }

        if (propertyName.Equals("tabFlags", StringComparison.OrdinalIgnoreCase))
        {
            return TabFlagsFingerprint(left).Equals(TabFlagsFingerprint(right), StringComparison.Ordinal);
        }

        if (propertyName.Equals("formDesignExData", StringComparison.OrdinalIgnoreCase))
        {
            return BinaryFingerprint(left).Equals(BinaryFingerprint(right), StringComparison.Ordinal);
        }

        if (propertyName.EndsWith("Picture", StringComparison.OrdinalIgnoreCase) ||
            propertyName.EndsWith("MouseIcon", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Equals("picture", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Equals("mouseIcon", StringComparison.OrdinalIgnoreCase))
        {
            return PictureFingerprint(left).Equals(PictureFingerprint(right), StringComparison.Ordinal);
        }

        if (propertyName.EndsWith("Color", StringComparison.OrdinalIgnoreCase))
        {
            return MsFormsFactoryBinary.ParseColor(left?.ToString(), uint.MaxValue) ==
                   MsFormsFactoryBinary.ParseColor(right?.ToString(), uint.MaxValue);
        }

        if (TryDecimal(left, out var leftNumber) && TryDecimal(right, out var rightNumber))
        {
            return leftNumber == rightNumber;
        }

        return JsonSerializer.Serialize(left, FrxEditApp.JsonOptions)
            .Equals(JsonSerializer.Serialize(right, FrxEditApp.JsonOptions), StringComparison.Ordinal);
    }

    private static string TabFlagsFingerprint(object? value)
    {
        if (value is null)
        {
            return "null";
        }

        var element = value is JsonElement jsonElement
            ? jsonElement
            : JsonSerializer.SerializeToElement(value, FrxEditApp.JsonOptions);
        if (element.ValueKind != JsonValueKind.Array)
        {
            return element.GetRawText();
        }

        var flags = new List<string>();
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                flags.Add(item.GetRawText());
                continue;
            }

            flags.Add(string.Join("|", new[] { "raw", "visible", "enabled" }.Select(name =>
            {
                foreach (var property in item.EnumerateObject())
                {
                    if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    {
                        return $"{name}:{property.Value.GetRawText()}";
                    }
                }
                return $"{name}:null";
            })));
        }

        return string.Join(",", flags);
    }

    private static string Display(object? value)
    {
        if (value is string text && text.StartsWith("base64:", StringComparison.OrdinalIgnoreCase))
        {
            return PictureFingerprint(text);
        }

        return JsonSerializer.Serialize(value, FrxEditApp.JsonOptions);
    }

    private static string PictureFingerprint(object? value)
    {
        if (value is not string text || !text.StartsWith("base64:", StringComparison.OrdinalIgnoreCase))
        {
            return JsonSerializer.Serialize(value, FrxEditApp.JsonOptions);
        }

        try
        {
            var bytes = Convert.FromBase64String(text[7..]);
            var payload = MsFormsFactoryBinary.GetPicturePayload(bytes);

            return $"picture:sha256:{Convert.ToHexString(SHA256.HashData(payload))}";
        }
        catch (FormatException)
        {
            return "picture:invalid-base64";
        }
    }

    private static string BinaryFingerprint(object? value)
    {
        if (value is not string text || !text.StartsWith("base64:", StringComparison.OrdinalIgnoreCase))
        {
            return JsonSerializer.Serialize(value, FrxEditApp.JsonOptions);
        }

        try
        {
            return $"binary:sha256:{Convert.ToHexString(SHA256.HashData(Convert.FromBase64String(text[7..])))}";
        }
        catch (FormatException)
        {
            return "binary:invalid-base64";
        }
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

    private static void CompareValue<T>(string key, string name, T? expected, T? actual, List<string> differences)
    {
        if (!EqualityComparer<T?>.Default.Equals(expected, actual))
        {
            differences.Add($"{key}.{name}: {expected} != {actual}");
        }
    }
}
