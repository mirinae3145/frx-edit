internal static class TextPropsFactory
{
    public const uint StandardMask = 0x0000_0035; // FontName, FontHeight, FontCharSet, FontPitchAndFamily capabilities.
    public const uint CommandButtonMask = 0x0000_0075; // Standard + ParagraphAlign capability.
    public const uint ParagraphAlignMask = 0x0000_0040;

    private const uint AllowedMask = 0x0000_00F7;
    private const uint DefaultFontHeight = 160; // 8 points, per MS-OFORMS.

    public static byte[] Build(Dictionary<string, object?> properties, uint supportedMask)
    {
        var propMask = ResolveMask(supportedMask, properties);
        var fontName = MsFormsFactoryBinary.GetString(properties, "fontName") ?? "Tahoma";
        var fontNameBytes = Encoding.Latin1.GetBytes(fontName);
        var fontHeight = GetFontHeight(properties);
        var paragraphAlign = GetParagraphAlign(properties) ?? 1;
        var fontEffects = BuildFontEffects(properties);
        var fontWeight = GetFontWeight(properties);
        var fontCharSet = MsFormsFactoryBinary.GetInt32(properties, "fontCharSet") ?? 0;
        var fontPitchAndFamily = MsFormsFactoryBinary.GetInt32(properties, "fontPitchAndFamily") ?? 2;

        using var dataBlock = new MemoryStream();
        if (HasBit(propMask, 0)) MsFormsFactoryBinary.WriteCount(dataBlock, fontNameBytes.Length);
        if (HasBit(propMask, 1)) MsFormsFactoryBinary.WriteUInt32(dataBlock, fontEffects);
        if (HasBit(propMask, 2)) MsFormsFactoryBinary.WriteUInt32(dataBlock, fontHeight);
        if (HasBit(propMask, 4)) dataBlock.WriteByte(checked((byte)fontCharSet));
        if (HasBit(propMask, 5)) dataBlock.WriteByte(checked((byte)fontPitchAndFamily));
        if (HasBit(propMask, 6)) dataBlock.WriteByte(checked((byte)paragraphAlign));
        if (HasBit(propMask, 7))
        {
            MsFormsFactoryBinary.WritePadding(dataBlock, 2);
            MsFormsFactoryBinary.WriteUInt16(dataBlock, fontWeight);
        }

        MsFormsFactoryBinary.WritePadding(dataBlock, 4);

        using var extra = new MemoryStream();
        if (HasBit(propMask, 0))
        {
            extra.Write(fontNameBytes);
            MsFormsFactoryBinary.WritePadding(extra, 4);
        }

        return MsFormsFactoryBinary.BuildVersionedControl(0, 2, propMask, dataBlock.ToArray(), extra.ToArray());
    }

    public static Dictionary<string, object?> BuildMetadata(uint supportedMask, Dictionary<string, object?> properties)
    {
        var propMask = ResolveMask(supportedMask, properties);
        var metadata = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["textPropsPropMask"] = $"0x{propMask:X8}"
        };

        if (HasBit(propMask, 0)) metadata["fontName"] = MsFormsFactoryBinary.GetString(properties, "fontName") ?? "Tahoma";
        if (HasBit(propMask, 1))
        {
            var fontEffects = BuildFontEffects(properties);
            metadata["fontEffects"] = fontEffects;
            metadata["fontEffectsHex"] = $"0x{fontEffects:X8}";
            metadata["fontItalic"] = HasBit(fontEffects, 1);
            metadata["fontUnderline"] = HasBit(fontEffects, 2);
            metadata["fontStrikethrough"] = HasBit(fontEffects, 3);
        }

        if (HasBit(propMask, 2))
        {
            var fontHeight = GetFontHeight(properties);
            metadata["fontSize"] = Math.Round(fontHeight / 20.0, 2);
            metadata["fontSizeRaw"] = unchecked((int)fontHeight);
        }

        if (HasBit(propMask, 4)) metadata["fontCharSet"] = MsFormsFactoryBinary.GetInt32(properties, "fontCharSet") ?? 0;
        if (HasBit(propMask, 5)) metadata["fontPitchAndFamily"] = MsFormsFactoryBinary.GetInt32(properties, "fontPitchAndFamily") ?? 2;
        if (HasBit(propMask, 6))
        {
            var paragraphAlign = GetParagraphAlign(properties) ?? 1;
            metadata["paragraphAlign"] = paragraphAlign;
            metadata["textAlign"] = ParagraphAlignToTextAlign(paragraphAlign);
        }

        if (HasBit(propMask, 7))
        {
            var fontWeight = GetFontWeight(properties);
            metadata["fontWeight"] = fontWeight;
            metadata["fontBold"] = fontWeight >= 700;
        }

        return metadata;
    }

    public static uint WithParagraphAlignIfNeeded(uint supportedMask, Dictionary<string, object?> properties)
    {
        var paragraphAlign = GetParagraphAlign(properties);
        return paragraphAlign is null || paragraphAlign.Value == 1
            ? supportedMask
            : supportedMask | ParagraphAlignMask;
    }

    public static int? GetParagraphAlign(Dictionary<string, object?> properties)
    {
        if (MsFormsFactoryBinary.GetInt32(properties, "paragraphAlign") is int paragraphAlign) return paragraphAlign;

        if (MsFormsFactoryBinary.GetString(properties, "textAlign") is { } textAlignText &&
            TryParseTextAlign(textAlignText, out var parsedTextAlign))
        {
            return TextAlignToParagraphAlign(parsedTextAlign);
        }

        return null;
    }

    public static bool TryParseTextAlign(string value, out int textAlign)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "left":
                textAlign = 1;
                return true;
            case "center":
            case "centre":
                textAlign = 2;
                return true;
            case "right":
                textAlign = 3;
                return true;
            default:
                return int.TryParse(value, CultureInfo.InvariantCulture, out textAlign);
        }
    }

    public static string TextAlignName(int textAlign) =>
        textAlign switch
        {
            1 => "left",
            2 => "center",
            3 => "right",
            _ => textAlign.ToString(CultureInfo.InvariantCulture)
        };

    public static int TextAlignToParagraphAlign(int textAlign) =>
        textAlign switch
        {
            1 => 1,
            2 => 3,
            3 => 2,
            _ => textAlign
        };

    public static int ParagraphAlignToTextAlign(int paragraphAlign) =>
        paragraphAlign switch
        {
            1 => 1,
            2 => 3,
            3 => 2,
            _ => paragraphAlign
        };

    private static uint ResolveMask(uint supportedMask, Dictionary<string, object?> properties)
    {
        var propMask = MsFormsFactoryBinary.GetUInt32(properties, "textPropsPropMask") is uint originalMask
            ? originalMask & AllowedMask
            : 0;
        if (HasValue(properties, "fontName") && HasBit(supportedMask, 0)) propMask |= 1u << 0;
        if (HasAnyValue(properties, "fontEffects", "fontEffectsHex", "fontItalic", "fontUnderline", "fontStrikethrough")) propMask |= 1u << 1;
        if (HasAnyValue(properties, "fontSize", "fontSizeRaw") && HasBit(supportedMask, 2)) propMask |= 1u << 2;
        if (HasValue(properties, "fontCharSet") && HasBit(supportedMask, 4)) propMask |= 1u << 4;
        if (HasValue(properties, "fontPitchAndFamily") && HasBit(supportedMask, 5)) propMask |= 1u << 5;

        var paragraphAlign = GetParagraphAlign(properties);
        if (paragraphAlign is not null && paragraphAlign.Value != 1 && HasBit(supportedMask, 6)) propMask |= 1u << 6;
        if (HasValue(properties, "fontWeight") || MsFormsFactoryBinary.GetBool(properties, "fontBold") is true) propMask |= 1u << 7;
        return propMask;
    }

    private static uint BuildFontEffects(Dictionary<string, object?> properties)
    {
        var effects = MsFormsFactoryBinary.GetUInt32(properties, "fontEffects")
            ?? MsFormsFactoryBinary.GetUInt32(properties, "fontEffectsHex")
            ?? 0;
        SetBit(ref effects, 1, MsFormsFactoryBinary.GetBool(properties, "fontItalic"));
        SetBit(ref effects, 2, MsFormsFactoryBinary.GetBool(properties, "fontUnderline"));
        SetBit(ref effects, 3, MsFormsFactoryBinary.GetBool(properties, "fontStrikethrough"));
        return effects;
    }

    private static uint GetFontHeight(Dictionary<string, object?> properties)
    {
        if (MsFormsFactoryBinary.GetUInt32(properties, "fontSizeRaw") is uint raw) return raw;
        var fontSize = MsFormsFactoryBinary.GetDouble(properties, "fontSize") ?? DefaultFontHeight / 20.0;
        return checked((uint)Math.Round(fontSize * 20.0, MidpointRounding.AwayFromZero));
    }

    private static int GetFontWeight(Dictionary<string, object?> properties)
    {
        var isBold = MsFormsFactoryBinary.GetBool(properties, "fontBold") ?? false;
        return MsFormsFactoryBinary.GetInt32(properties, "fontWeight") ?? (isBold ? 700 : 400);
    }

    private static bool HasValue(Dictionary<string, object?> properties, string name) =>
        properties.TryGetValue(name, out var value) && value is not null;

    private static bool HasAnyValue(Dictionary<string, object?> properties, params string[] names) =>
        names.Any(name => HasValue(properties, name));

    private static bool HasBit(uint value, int bit) => (value & (1u << bit)) != 0;

    private static void SetBit(ref uint bits, int bit, bool? value)
    {
        if (value is null) return;
        var mask = 1u << bit;
        bits = value.Value ? bits | mask : bits & ~mask;
    }
}
