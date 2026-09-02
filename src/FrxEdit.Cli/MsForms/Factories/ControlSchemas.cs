internal interface IGeneratedControlSchema
{
    string Type { get; }
    uint SiteFlags { get; }
    byte[] BuildObjectPayload(GeneratedControlRequest request);
    IReadOnlyDictionary<string, object?> BuildMetadata(GeneratedControlRequest request, int objectPayloadSize);
}

internal sealed class CommandButtonControlSchema : IGeneratedControlSchema
{
    private const uint DefaultForeColor = 0x8000_0012;
    private const uint DefaultBackColor = 0x8000_000F;
    private const uint DefaultVariousPropertyBits = 0x0000_001B;
    private const uint DefaultPicturePosition = 0x0007_0001;

    public string Type => "CommandButton";
    public uint SiteFlags => 0x0000_0033; // tabStop + visible + streamed + fAutoSize file default.

    public byte[] BuildObjectPayload(GeneratedControlRequest request)
    {
        var captionProp = MsFormsFactoryBinary.GetString(request.Properties, "caption");
        var hasCaption = request.Caption is not null || request.Properties.ContainsKey("caption");
        var caption = request.Caption ?? captionProp ?? string.Empty;
        var captionBytes = Encoding.Latin1.GetBytes(caption);
        var foreColor = MsFormsFactoryBinary.ParseColor(MsFormsFactoryBinary.GetString(request.Properties, "foreColor"), DefaultForeColor);
        var backColor = MsFormsFactoryBinary.ParseColor(MsFormsFactoryBinary.GetString(request.Properties, "backColor"), DefaultBackColor);
        var variousBits = BuildVariousPropertyBits(request.Properties);
        var picturePosition = MsFormsFactoryBinary.GetInt32(request.Properties, "picturePosition");
        var mousePointer = MsFormsFactoryBinary.GetInt32(request.Properties, "mousePointer");
        var accelerator = MsFormsFactoryBinary.GetString(request.Properties, "accelerator");
        var takeFocusOnClick = MsFormsFactoryBinary.GetBool(request.Properties, "takeFocusOnClick");
        var picture = MsFormsFactoryBinary.GetNativePictureStream(request.Properties, "picture", Type, request.Name);
        var mouseIcon = MsFormsFactoryBinary.GetNativePictureStream(request.Properties, "mouseIcon", Type, request.Name);

        uint propMask = 0x0000_0020; // Size.
        if (foreColor != DefaultForeColor) propMask |= 1u << 0;
        if (backColor != DefaultBackColor) propMask |= 1u << 1;
        if (variousBits != DefaultVariousPropertyBits) propMask |= 1u << 2;
        if (picturePosition is not null && unchecked((uint)picturePosition.Value) != DefaultPicturePosition) propMask |= 1u << 4;
        if (mousePointer is not null && mousePointer.Value != 0) propMask |= 1u << 6;
        if (!string.IsNullOrEmpty(accelerator)) propMask |= 1u << 8;
        if (takeFocusOnClick is false) propMask |= 1u << 9;
        if (hasCaption) propMask |= 1u << 3;
        if (picture.Length > 0) propMask |= 1u << 7;
        if (mouseIcon.Length > 0) propMask |= 1u << 10;

        using var dataBlock = new MemoryStream();
        if ((propMask & (1u << 0)) != 0) MsFormsFactoryBinary.WriteUInt32(dataBlock, foreColor);
        if ((propMask & (1u << 1)) != 0) MsFormsFactoryBinary.WriteUInt32(dataBlock, backColor);
        if ((propMask & (1u << 2)) != 0) MsFormsFactoryBinary.WriteUInt32(dataBlock, variousBits);
        if ((propMask & (1u << 3)) != 0) MsFormsFactoryBinary.WriteCount(dataBlock, captionBytes.Length);
        if ((propMask & (1u << 4)) != 0) MsFormsFactoryBinary.WriteUInt32(dataBlock, unchecked((uint)picturePosition!.Value));
        if ((propMask & (1u << 6)) != 0)
        {
            dataBlock.WriteByte(checked((byte)mousePointer!.Value));
            MsFormsFactoryBinary.WritePadding(dataBlock, 2);
        }

        if ((propMask & (1u << 7)) != 0)
        {
            MsFormsFactoryBinary.WriteUInt16(dataBlock, 0xFFFF);
            MsFormsFactoryBinary.WritePadding(dataBlock, 4);
        }

        if ((propMask & (1u << 8)) != 0)
        {
            var code = string.IsNullOrEmpty(accelerator) ? 0 : accelerator![0];
            MsFormsFactoryBinary.WriteUInt16(dataBlock, code);
        }

        if ((propMask & (1u << 10)) != 0) MsFormsFactoryBinary.WriteUInt16(dataBlock, 0xFFFF);

        MsFormsFactoryBinary.WritePadding(dataBlock, 4);

        using var extra = new MemoryStream();
        if ((propMask & (1u << 3)) != 0)
        {
            extra.Write(captionBytes);
            MsFormsFactoryBinary.WritePadding(extra, 4);
        }
        MsFormsFactoryBinary.WriteSize(extra, request.Width, request.Height);

        var control = MsFormsFactoryBinary.BuildVersionedControl(0, 2, propMask, dataBlock.ToArray(), extra.ToArray());
        var textProps = TextPropsFactory.Build(request.Properties, TextPropsFactory.CommandButtonMask);
        return [.. control, .. picture, .. mouseIcon, .. textProps];
    }

    public IReadOnlyDictionary<string, object?> BuildMetadata(GeneratedControlRequest request, int objectPayloadSize)
    {
        var metadata = TextPropsFactory.BuildMetadata(TextPropsFactory.CommandButtonMask, request.Properties);
        var foreColor = MsFormsFactoryBinary.ParseColor(MsFormsFactoryBinary.GetString(request.Properties, "foreColor"), DefaultForeColor);
        var backColor = MsFormsFactoryBinary.ParseColor(MsFormsFactoryBinary.GetString(request.Properties, "backColor"), DefaultBackColor);
        var variousBits = BuildVariousPropertyBits(request.Properties);
        var hasCaption = request.Caption is not null || request.Properties.ContainsKey("caption");
        var picture = MsFormsFactoryBinary.GetNativePictureStream(request.Properties, "picture", Type, request.Name);
        var mouseIcon = MsFormsFactoryBinary.GetNativePictureStream(request.Properties, "mouseIcon", Type, request.Name);
        var propMask = 0x0000_0020u;
        if (foreColor != DefaultForeColor) propMask |= 1u << 0;
        if (backColor != DefaultBackColor) propMask |= 1u << 1;
        if (variousBits != DefaultVariousPropertyBits) propMask |= 1u << 2;
        if (MsFormsFactoryBinary.GetInt32(request.Properties, "picturePosition") is int picturePosition && unchecked((uint)picturePosition) != DefaultPicturePosition) propMask |= 1u << 4;
        if (MsFormsFactoryBinary.GetInt32(request.Properties, "mousePointer") is int mousePointer && mousePointer != 0) propMask |= 1u << 6;
        if (!string.IsNullOrEmpty(MsFormsFactoryBinary.GetString(request.Properties, "accelerator"))) propMask |= 1u << 8;
        if (MsFormsFactoryBinary.GetBool(request.Properties, "takeFocusOnClick") is false) propMask |= 1u << 9;
        if (hasCaption) propMask |= 1u << 3;
        if (picture.Length > 0) propMask |= 1u << 7;
        if (mouseIcon.Length > 0) propMask |= 1u << 10;

        if (hasCaption) metadata["caption"] = request.Caption ?? MsFormsFactoryBinary.GetString(request.Properties, "caption") ?? string.Empty;
        metadata["sizeSource"] = "commandButtonExtraDataBlock";
        metadata["parser"] = "msOFormsCommandButton";
        metadata["commandButtonPropMask"] = $"0x{propMask:X8}";
        metadata["variousPropertyBitsRaw"] = unchecked((int)variousBits);
        metadata["enabled"] = (variousBits & (1u << 1)) != 0;
        metadata["locked"] = (variousBits & (1u << 2)) != 0;
        metadata["backStyle"] = (variousBits & (1u << 3)) != 0 ? 1 : 0;
        metadata["wordWrap"] = (variousBits & (1u << 23)) != 0;
        metadata["autoSize"] = (variousBits & (1u << 28)) != 0;
        metadata["foreColor"] = $"&H{foreColor:X8}&";
        metadata["backColor"] = $"&H{backColor:X8}&";
        if (MsFormsFactoryBinary.GetInt32(request.Properties, "picturePosition") is int picturePositionValue) metadata["picturePosition"] = picturePositionValue;
        if (MsFormsFactoryBinary.GetInt32(request.Properties, "mousePointer") is int mousePointerValue) metadata["mousePointer"] = mousePointerValue;
        if (MsFormsFactoryBinary.GetString(request.Properties, "accelerator") is { Length: > 0 } accelerator) metadata["accelerator"] = accelerator[0].ToString();
        if (picture.Length > 0) metadata["picture"] = MsFormsFactoryBinary.GetString(request.Properties, "picture");
        if (mouseIcon.Length > 0) metadata["mouseIcon"] = MsFormsFactoryBinary.GetString(request.Properties, "mouseIcon");
        metadata["takeFocusOnClick"] = MsFormsFactoryBinary.GetBool(request.Properties, "takeFocusOnClick") ?? true;
        metadata["objectStreamSize"] = objectPayloadSize;
        metadata["siteBitFlags"] = $"0x{BuildSiteFlags(request.Properties):X8}";
        metadata["tabStop"] = MsFormsFactoryBinary.GetBool(request.Properties, "tabStop") ?? true;
        metadata["visible"] = MsFormsFactoryBinary.GetBool(request.Properties, "visible") ?? true;
        metadata["default"] = MsFormsFactoryBinary.GetBool(request.Properties, "default") ?? false;
        metadata["cancel"] = MsFormsFactoryBinary.GetBool(request.Properties, "cancel") ?? false;
        metadata["streamed"] = true;
        return metadata;
    }

    private static uint BuildVariousPropertyBits(Dictionary<string, object?> properties)
    {
        var bits = MsFormsFactoryBinary.GetUInt32(properties, "variousPropertyBitsRaw") ?? DefaultVariousPropertyBits;
        SetBit(ref bits, 1, MsFormsFactoryBinary.GetBool(properties, "enabled"));
        SetBit(ref bits, 2, MsFormsFactoryBinary.GetBool(properties, "locked"));
        if (MsFormsFactoryBinary.GetInt32(properties, "backStyle") is int backStyle)
        {
            SetBit(ref bits, 3, backStyle != 0);
        }
        SetBit(ref bits, 23, MsFormsFactoryBinary.GetBool(properties, "wordWrap"));
        SetBit(ref bits, 28, MsFormsFactoryBinary.GetBool(properties, "autoSize"));
        if (MsFormsFactoryBinary.GetInt32(properties, "imeMode") is int imeMode)
        {
            bits &= ~(0xFu << 15);
            bits |= ((uint)imeMode & 0xFu) << 15;
        }

        return bits;
    }

    private static uint BuildSiteFlags(Dictionary<string, object?> properties)
    {
        var flags = 0x0000_0033u;
        SetBit(ref flags, 0, MsFormsFactoryBinary.GetBool(properties, "tabStop"));
        SetBit(ref flags, 1, MsFormsFactoryBinary.GetBool(properties, "visible"));
        SetBit(ref flags, 2, MsFormsFactoryBinary.GetBool(properties, "default"));
        SetBit(ref flags, 3, MsFormsFactoryBinary.GetBool(properties, "cancel"));
        return flags;
    }

    private static void SetBit(ref uint bits, int bit, bool? value)
    {
        if (value is null) return;
        var mask = 1u << bit;
        bits = value.Value ? bits | mask : bits & ~mask;
    }
}

internal sealed class LabelControlSchema : IGeneratedControlSchema
{
    private const uint DefaultForeColor = 0x8000_0012;
    private const uint DefaultBackColor = 0x8000_000F;
    private const uint DefaultBorderColor = 0x8000_0006;
    private const uint DefaultVariousPropertyBits = 0x0080_0013;
    private const uint DefaultPicturePosition = 0x0007_0001;

    public string Type => "Label";
    public uint SiteFlags => 0x0000_0032; // visible + streamed + autoSize, no tabStop.

    public byte[] BuildObjectPayload(GeneratedControlRequest request)
    {
        var captionProp = MsFormsFactoryBinary.GetString(request.Properties, "caption");
        var hasCaption = request.Caption is not null || request.Properties.ContainsKey("caption");
        var caption = request.Caption ?? captionProp ?? string.Empty;
        var captionBytes = Encoding.Latin1.GetBytes(caption);
        var foreColor = MsFormsFactoryBinary.ParseColor(MsFormsFactoryBinary.GetString(request.Properties, "foreColor"), DefaultForeColor);
        var backColor = MsFormsFactoryBinary.ParseColor(MsFormsFactoryBinary.GetString(request.Properties, "backColor"), DefaultBackColor);
        var borderColor = MsFormsFactoryBinary.ParseColor(MsFormsFactoryBinary.GetString(request.Properties, "borderColor"), DefaultBorderColor);
        var variousBits = BuildVariousPropertyBits(request.Properties);
        var picturePosition = MsFormsFactoryBinary.GetInt32(request.Properties, "picturePosition");
        var mousePointer = MsFormsFactoryBinary.GetInt32(request.Properties, "mousePointer");
        var borderStyle = MsFormsFactoryBinary.GetInt32(request.Properties, "borderStyle");
        var specialEffect = MsFormsFactoryBinary.GetInt32(request.Properties, "specialEffect");
        var accelerator = MsFormsFactoryBinary.GetString(request.Properties, "accelerator");
        var picture = MsFormsFactoryBinary.GetNativePictureStream(request.Properties, "picture", Type, request.Name);
        var mouseIcon = MsFormsFactoryBinary.GetNativePictureStream(request.Properties, "mouseIcon", Type, request.Name);

        uint propMask = 0x0000_0020; // Size.
        if (foreColor != DefaultForeColor) propMask |= 1u << 0;
        if (backColor != DefaultBackColor) propMask |= 1u << 1;
        if (variousBits != DefaultVariousPropertyBits) propMask |= 1u << 2;
        if (picturePosition is not null && unchecked((uint)picturePosition.Value) != DefaultPicturePosition) propMask |= 1u << 4;
        if (mousePointer is not null && mousePointer.Value != 0) propMask |= 1u << 6;
        if (borderColor != DefaultBorderColor) propMask |= 1u << 7;
        if (borderStyle is not null && borderStyle.Value != 0) propMask |= 1u << 8;
        if (specialEffect is not null && specialEffect.Value != 0) propMask |= 1u << 9;
        if (!string.IsNullOrEmpty(accelerator)) propMask |= 1u << 11;
        if (hasCaption) propMask |= 1u << 3;
        if (picture.Length > 0) propMask |= 1u << 10;
        if (mouseIcon.Length > 0) propMask |= 1u << 12;

        using var dataBlock = new MemoryStream();
        if ((propMask & (1u << 0)) != 0) MsFormsFactoryBinary.WriteUInt32(dataBlock, foreColor);
        if ((propMask & (1u << 1)) != 0) MsFormsFactoryBinary.WriteUInt32(dataBlock, backColor);
        if ((propMask & (1u << 2)) != 0) MsFormsFactoryBinary.WriteUInt32(dataBlock, variousBits);
        if ((propMask & (1u << 3)) != 0) MsFormsFactoryBinary.WriteCount(dataBlock, captionBytes.Length);
        if ((propMask & (1u << 4)) != 0) MsFormsFactoryBinary.WriteUInt32(dataBlock, unchecked((uint)picturePosition!.Value));
        if ((propMask & (1u << 6)) != 0)
        {
            dataBlock.WriteByte(checked((byte)mousePointer!.Value));
            MsFormsFactoryBinary.WritePadding(dataBlock, 4);
        }
        if ((propMask & (1u << 7)) != 0) MsFormsFactoryBinary.WriteUInt32(dataBlock, borderColor);
        if ((propMask & (1u << 8)) != 0) MsFormsFactoryBinary.WriteUInt16(dataBlock, borderStyle!.Value);
        if ((propMask & (1u << 9)) != 0) MsFormsFactoryBinary.WriteUInt16(dataBlock, specialEffect!.Value);
        if ((propMask & (1u << 10)) != 0)
        {
            MsFormsFactoryBinary.WriteUInt16(dataBlock, 0xFFFF);
            MsFormsFactoryBinary.WritePadding(dataBlock, 2);
        }
        if ((propMask & (1u << 11)) != 0) MsFormsFactoryBinary.WriteUInt16(dataBlock, accelerator![0]);
        if ((propMask & (1u << 12)) != 0) MsFormsFactoryBinary.WriteUInt16(dataBlock, 0xFFFF);
        MsFormsFactoryBinary.WritePadding(dataBlock, 4);

        using var extra = new MemoryStream();
        if ((propMask & (1u << 3)) != 0)
        {
            extra.Write(captionBytes);
            MsFormsFactoryBinary.WritePadding(extra, 4);
        }
        MsFormsFactoryBinary.WriteSize(extra, request.Width, request.Height);

        var control = MsFormsFactoryBinary.BuildVersionedControl(0, 2, propMask, dataBlock.ToArray(), extra.ToArray());
        var textPropsMask = TextPropsFactory.WithParagraphAlignIfNeeded(TextPropsFactory.StandardMask, request.Properties);
        var textProps = TextPropsFactory.Build(request.Properties, textPropsMask);
        return [.. control, .. picture, .. mouseIcon, .. textProps];
    }

    public IReadOnlyDictionary<string, object?> BuildMetadata(GeneratedControlRequest request, int objectPayloadSize)
    {
        var textPropsMask = TextPropsFactory.WithParagraphAlignIfNeeded(TextPropsFactory.StandardMask, request.Properties);
        var metadata = TextPropsFactory.BuildMetadata(textPropsMask, request.Properties);
        var foreColor = MsFormsFactoryBinary.ParseColor(MsFormsFactoryBinary.GetString(request.Properties, "foreColor"), DefaultForeColor);
        var backColor = MsFormsFactoryBinary.ParseColor(MsFormsFactoryBinary.GetString(request.Properties, "backColor"), DefaultBackColor);
        var borderColor = MsFormsFactoryBinary.ParseColor(MsFormsFactoryBinary.GetString(request.Properties, "borderColor"), DefaultBorderColor);
        var variousBits = BuildVariousPropertyBits(request.Properties);
        var hasCaption = request.Caption is not null || request.Properties.ContainsKey("caption");
        var picture = MsFormsFactoryBinary.GetNativePictureStream(request.Properties, "picture", Type, request.Name);
        var mouseIcon = MsFormsFactoryBinary.GetNativePictureStream(request.Properties, "mouseIcon", Type, request.Name);
        uint propMask = 0x0000_0020;
        if (foreColor != DefaultForeColor) propMask |= 1u << 0;
        if (backColor != DefaultBackColor) propMask |= 1u << 1;
        if (variousBits != DefaultVariousPropertyBits) propMask |= 1u << 2;
        if (MsFormsFactoryBinary.GetInt32(request.Properties, "picturePosition") is int picturePosition && unchecked((uint)picturePosition) != DefaultPicturePosition) propMask |= 1u << 4;
        if (MsFormsFactoryBinary.GetInt32(request.Properties, "mousePointer") is int mousePointer && mousePointer != 0) propMask |= 1u << 6;
        if (borderColor != DefaultBorderColor) propMask |= 1u << 7;
        if (MsFormsFactoryBinary.GetInt32(request.Properties, "borderStyle") is int borderStyle && borderStyle != 0) propMask |= 1u << 8;
        if (MsFormsFactoryBinary.GetInt32(request.Properties, "specialEffect") is int specialEffect && specialEffect != 0) propMask |= 1u << 9;
        if (!string.IsNullOrEmpty(MsFormsFactoryBinary.GetString(request.Properties, "accelerator"))) propMask |= 1u << 11;
        if (hasCaption) propMask |= 1u << 3;
        if (picture.Length > 0) propMask |= 1u << 10;
        if (mouseIcon.Length > 0) propMask |= 1u << 12;

        if (hasCaption) metadata["caption"] = request.Caption ?? MsFormsFactoryBinary.GetString(request.Properties, "caption") ?? string.Empty;
        metadata["sizeSource"] = "labelExtraDataBlock";
        metadata["parser"] = "msOFormsLabel";
        metadata["propMask"] = $"0x{propMask:X8}";
        metadata["variousPropertyBitsRaw"] = unchecked((int)variousBits);
        metadata["enabled"] = (variousBits & (1u << 1)) != 0;
        metadata["backStyle"] = (variousBits & (1u << 3)) != 0 ? 1 : 0;
        metadata["wordWrap"] = (variousBits & (1u << 23)) != 0;
        metadata["autoSize"] = (variousBits & (1u << 28)) != 0;
        metadata["foreColor"] = $"&H{foreColor:X8}&";
        metadata["backColor"] = $"&H{backColor:X8}&";
        metadata["borderColor"] = $"&H{borderColor:X8}&";
        if (MsFormsFactoryBinary.GetInt32(request.Properties, "borderStyle") is int borderStyleValue) metadata["borderStyle"] = borderStyleValue;
        if (MsFormsFactoryBinary.GetInt32(request.Properties, "specialEffect") is int specialEffectValue) metadata["specialEffect"] = specialEffectValue;
        if (MsFormsFactoryBinary.GetInt32(request.Properties, "picturePosition") is int picturePositionValue) metadata["picturePosition"] = picturePositionValue;
        if (MsFormsFactoryBinary.GetInt32(request.Properties, "mousePointer") is int mousePointerValue) metadata["mousePointer"] = mousePointerValue;
        if (MsFormsFactoryBinary.GetString(request.Properties, "accelerator") is { Length: > 0 } accelerator) metadata["accelerator"] = accelerator[0].ToString();
        if (picture.Length > 0) metadata["picture"] = MsFormsFactoryBinary.GetString(request.Properties, "picture");
        if (mouseIcon.Length > 0) metadata["mouseIcon"] = MsFormsFactoryBinary.GetString(request.Properties, "mouseIcon");
        metadata["objectStreamSize"] = objectPayloadSize;
        metadata["siteBitFlags"] = $"0x{SiteFlags:X8}";
        metadata["tabStop"] = false;
        metadata["visible"] = true;
        metadata["streamed"] = true;
        metadata["siteAutoSize"] = true;
        return metadata;
    }

    private static uint BuildVariousPropertyBits(Dictionary<string, object?> properties)
    {
        var bits = MsFormsFactoryBinary.GetUInt32(properties, "variousPropertyBitsRaw") ?? DefaultVariousPropertyBits;
        SetBit(ref bits, 1, MsFormsFactoryBinary.GetBool(properties, "enabled"));
        if (MsFormsFactoryBinary.GetInt32(properties, "backStyle") is int backStyle)
        {
            SetBit(ref bits, 3, backStyle != 0);
        }
        SetBit(ref bits, 23, MsFormsFactoryBinary.GetBool(properties, "wordWrap"));
        SetBit(ref bits, 28, MsFormsFactoryBinary.GetBool(properties, "autoSize"));
        if (MsFormsFactoryBinary.GetInt32(properties, "imeMode") is int imeMode)
        {
            bits &= ~(0xFu << 15);
            bits |= ((uint)imeMode & 0xFu) << 15;
        }

        return bits;
    }

    private static void SetBit(ref uint bits, int bit, bool? value)
    {
        if (value is null) return;
        var mask = 1u << bit;
        bits = value.Value ? bits | mask : bits & ~mask;
    }
}

internal sealed class TextBoxControlSchema : IGeneratedControlSchema
{
    private const uint DefaultBackColor = 0x8000_0005;
    private const uint DefaultForeColor = 0x8000_0008;
    private const uint DefaultBorderColor = 0x8000_0006;
    private const uint DefaultVariousPropertyBits = 0x2C80_481B;
    private const int DefaultBorderStyle = 1;
    private const int DefaultSpecialEffect = 2;

    public string Type => "TextBox";
    public uint SiteFlags => 0x0000_0033; // tabStop + visible + streamed + fAutoSize file default.

    public byte[] BuildObjectPayload(GeneratedControlRequest request)
    {
        var value = request.Value ?? MsFormsFactoryBinary.GetString(request.Properties, "value");
        var valueBytes = string.IsNullOrEmpty(value) ? [] : Encoding.Latin1.GetBytes(value);
        var backColor = MsFormsFactoryBinary.ParseColor(MsFormsFactoryBinary.GetString(request.Properties, "backColor"), DefaultBackColor);
        var foreColor = MsFormsFactoryBinary.ParseColor(MsFormsFactoryBinary.GetString(request.Properties, "foreColor"), DefaultForeColor);
        var borderColor = MsFormsFactoryBinary.ParseColor(MsFormsFactoryBinary.GetString(request.Properties, "borderColor"), DefaultBorderColor);
        var variousBits = BuildVariousPropertyBits(request.Properties);
        var maxLength = MsFormsFactoryBinary.GetInt32(request.Properties, "maxLength") ?? 0;
        var borderStyle = MsFormsFactoryBinary.GetInt32(request.Properties, "borderStyle") ?? DefaultBorderStyle;
        var scrollBars = MsFormsFactoryBinary.GetInt32(request.Properties, "scrollBars") ?? 0;
        var mousePointer = MsFormsFactoryBinary.GetInt32(request.Properties, "mousePointer") ?? 0;
        var passwordChar = MsFormsFactoryBinary.GetString(request.Properties, "passwordChar");
        var specialEffect = MsFormsFactoryBinary.GetInt32(request.Properties, "specialEffect") ?? DefaultSpecialEffect;
        var mouseIcon = MsFormsFactoryBinary.GetNativePictureStream(request.Properties, "mouseIcon", Type, request.Name);
        var picture = MsFormsFactoryBinary.GetNativePictureStream(request.Properties, "picture", Type, request.Name);

        ulong propMask = 0x0000_0000_8000_0101ul; // VariousPropertyBits + Size + reserved bit.
        if (backColor != DefaultBackColor) propMask |= 1ul << 1;
        if (foreColor != DefaultForeColor) propMask |= 1ul << 2;
        if (maxLength != 0) propMask |= 1ul << 3;
        if (borderStyle != DefaultBorderStyle) propMask |= 1ul << 4;
        if (scrollBars != 0) propMask |= 1ul << 5;
        if (mousePointer != 0) propMask |= 1ul << 7;
        if (!string.IsNullOrEmpty(passwordChar)) propMask |= 1ul << 9;
        if (valueBytes.Length > 0) propMask |= 1ul << 22;
        if (borderColor != DefaultBorderColor) propMask |= 1ul << 25;
        if (specialEffect != DefaultSpecialEffect) propMask |= 1ul << 26;
        if (mouseIcon.Length > 0) propMask |= 1ul << 27;
        if (picture.Length > 0) propMask |= 1ul << 28;

        using var dataBlock = new MemoryStream();
        MsFormsFactoryBinary.WriteUInt32(dataBlock, variousBits);
        if ((propMask & (1ul << 1)) != 0) MsFormsFactoryBinary.WriteUInt32(dataBlock, backColor);
        if ((propMask & (1ul << 2)) != 0) MsFormsFactoryBinary.WriteUInt32(dataBlock, foreColor);
        if ((propMask & (1ul << 3)) != 0) MsFormsFactoryBinary.WriteUInt32(dataBlock, unchecked((uint)maxLength));
        if ((propMask & (1ul << 4)) != 0) dataBlock.WriteByte(checked((byte)borderStyle));
        if ((propMask & (1ul << 5)) != 0) dataBlock.WriteByte(checked((byte)scrollBars));
        if ((propMask & (1ul << 7)) != 0) dataBlock.WriteByte(checked((byte)mousePointer));
        if ((propMask & (1ul << 9)) != 0)
        {
            MsFormsFactoryBinary.WritePadding(dataBlock, 2);
            MsFormsFactoryBinary.WriteUInt16(dataBlock, passwordChar![0]);
        }
        if ((propMask & (1ul << 22)) != 0)
        {
            MsFormsFactoryBinary.WritePadding(dataBlock, 4);
            MsFormsFactoryBinary.WriteCount(dataBlock, valueBytes.Length);
        }
        if ((propMask & (1ul << 25)) != 0) MsFormsFactoryBinary.WriteUInt32(dataBlock, borderColor);
        if ((propMask & (1ul << 26)) != 0) MsFormsFactoryBinary.WriteUInt32(dataBlock, unchecked((uint)specialEffect));
        if ((propMask & (1ul << 27)) != 0) MsFormsFactoryBinary.WriteUInt16(dataBlock, 0xFFFF);
        if ((propMask & (1ul << 28)) != 0) MsFormsFactoryBinary.WriteUInt16(dataBlock, 0xFFFF);
        MsFormsFactoryBinary.WritePadding(dataBlock, 4);

        using var extra = new MemoryStream();
        MsFormsFactoryBinary.WriteSize(extra, request.Width, request.Height);
        if (valueBytes.Length > 0)
        {
            extra.Write(valueBytes);
            MsFormsFactoryBinary.WritePadding(extra, 4);
        }

        var control = MsFormsFactoryBinary.BuildVersionedMorphControl(propMask, dataBlock.ToArray(), extra.ToArray());
        var textPropsMask = TextPropsFactory.WithParagraphAlignIfNeeded(TextPropsFactory.StandardMask, request.Properties);
        var textProps = TextPropsFactory.Build(request.Properties, textPropsMask);
        return [.. control, .. mouseIcon, .. picture, .. textProps];
    }

    public IReadOnlyDictionary<string, object?> BuildMetadata(GeneratedControlRequest request, int objectPayloadSize)
    {
        var value = request.Value ?? MsFormsFactoryBinary.GetString(request.Properties, "value");
        var valueBytes = string.IsNullOrEmpty(value) ? [] : Encoding.Latin1.GetBytes(value);
        var backColor = MsFormsFactoryBinary.ParseColor(MsFormsFactoryBinary.GetString(request.Properties, "backColor"), DefaultBackColor);
        var foreColor = MsFormsFactoryBinary.ParseColor(MsFormsFactoryBinary.GetString(request.Properties, "foreColor"), DefaultForeColor);
        var borderColor = MsFormsFactoryBinary.ParseColor(MsFormsFactoryBinary.GetString(request.Properties, "borderColor"), DefaultBorderColor);
        var variousBits = BuildVariousPropertyBits(request.Properties);
        var maxLength = MsFormsFactoryBinary.GetInt32(request.Properties, "maxLength") ?? 0;
        var borderStyle = MsFormsFactoryBinary.GetInt32(request.Properties, "borderStyle") ?? DefaultBorderStyle;
        var scrollBars = MsFormsFactoryBinary.GetInt32(request.Properties, "scrollBars") ?? 0;
        var mousePointer = MsFormsFactoryBinary.GetInt32(request.Properties, "mousePointer") ?? 0;
        var passwordChar = MsFormsFactoryBinary.GetString(request.Properties, "passwordChar");
        var specialEffect = MsFormsFactoryBinary.GetInt32(request.Properties, "specialEffect") ?? DefaultSpecialEffect;
        var mouseIcon = MsFormsFactoryBinary.GetNativePictureStream(request.Properties, "mouseIcon", Type, request.Name);
        var picture = MsFormsFactoryBinary.GetNativePictureStream(request.Properties, "picture", Type, request.Name);
        ulong propMask = 0x0000_0000_8000_0101ul;
        if (backColor != DefaultBackColor) propMask |= 1ul << 1;
        if (foreColor != DefaultForeColor) propMask |= 1ul << 2;
        if (maxLength != 0) propMask |= 1ul << 3;
        if (borderStyle != DefaultBorderStyle) propMask |= 1ul << 4;
        if (scrollBars != 0) propMask |= 1ul << 5;
        if (mousePointer != 0) propMask |= 1ul << 7;
        if (!string.IsNullOrEmpty(passwordChar)) propMask |= 1ul << 9;
        if (valueBytes.Length > 0) propMask |= 1ul << 22;
        if (borderColor != DefaultBorderColor) propMask |= 1ul << 25;
        if (specialEffect != DefaultSpecialEffect) propMask |= 1ul << 26;
        if (mouseIcon.Length > 0) propMask |= 1ul << 27;
        if (picture.Length > 0) propMask |= 1ul << 28;

        var textPropsMask = TextPropsFactory.WithParagraphAlignIfNeeded(TextPropsFactory.StandardMask, request.Properties);
        var metadata = TextPropsFactory.BuildMetadata(textPropsMask, request.Properties);
        metadata["parser"] = "msOFormsMorphData";
        metadata["controlType"] = "TextBox";
        metadata["sizeSource"] = "morphDataExtraDataBlock";
        metadata["propMask"] = $"0x{propMask:X16}";
        metadata["variousPropertyBitsRaw"] = unchecked((int)variousBits);
        metadata["backColor"] = $"&H{backColor:X8}&";
        metadata["foreColor"] = $"&H{foreColor:X8}&";
        metadata["borderColor"] = $"&H{borderColor:X8}&";
        metadata["enabled"] = (variousBits & (1u << 1)) != 0;
        metadata["locked"] = (variousBits & (1u << 2)) != 0;
        metadata["backStyle"] = (variousBits & (1u << 3)) != 0 ? 1 : 0;
        metadata["integralHeight"] = (variousBits & (1u << 11)) != 0;
        metadata["dragBehavior"] = (variousBits & (1u << 19)) != 0 ? 1 : 0;
        metadata["enterKeyBehavior"] = (variousBits & (1u << 20)) != 0;
        metadata["enterFieldBehavior"] = (variousBits & (1u << 21)) != 0 ? 1 : 0;
        metadata["tabKeyBehavior"] = (variousBits & (1u << 22)) != 0;
        metadata["wordWrap"] = (variousBits & (1u << 23)) != 0;
        metadata["selectionMargin"] = (variousBits & (1u << 26)) != 0;
        metadata["autoWordSelect"] = (variousBits & (1u << 27)) != 0;
        metadata["autoSize"] = (variousBits & (1u << 28)) != 0;
        metadata["hideSelection"] = (variousBits & (1u << 29)) != 0;
        metadata["autoTab"] = (variousBits & (1u << 30)) != 0;
        metadata["multiLine"] = (variousBits & (1u << 31)) != 0;
        metadata["borderStyle"] = borderStyle;
        metadata["scrollBars"] = scrollBars;
        metadata["maxLength"] = maxLength;
        metadata["specialEffect"] = specialEffect;
        if (mousePointer != 0) metadata["mousePointer"] = mousePointer;
        if (!string.IsNullOrEmpty(passwordChar)) metadata["passwordChar"] = passwordChar[0].ToString();
        if (mouseIcon.Length > 0) metadata["mouseIcon"] = MsFormsFactoryBinary.GetString(request.Properties, "mouseIcon");
        if (picture.Length > 0) metadata["picture"] = MsFormsFactoryBinary.GetString(request.Properties, "picture");
        metadata["objectStreamSize"] = objectPayloadSize;
        metadata["siteBitFlags"] = $"0x{SiteFlags:X8}";
        metadata["tabStop"] = true;
        metadata["visible"] = true;
        metadata["streamed"] = true;
        if (!string.IsNullOrEmpty(value))
        {
            metadata["value"] = value;
        }

        return metadata;
    }

    private static uint BuildVariousPropertyBits(Dictionary<string, object?> properties)
    {
        var bits = MsFormsFactoryBinary.GetUInt32(properties, "variousPropertyBitsRaw") ?? DefaultVariousPropertyBits;
        SetBit(ref bits, 1, MsFormsFactoryBinary.GetBool(properties, "enabled"));
        SetBit(ref bits, 2, MsFormsFactoryBinary.GetBool(properties, "locked"));
        if (MsFormsFactoryBinary.GetInt32(properties, "backStyle") is int backStyle) SetBit(ref bits, 3, backStyle != 0);
        SetBit(ref bits, 11, MsFormsFactoryBinary.GetBool(properties, "integralHeight"));
        if (MsFormsFactoryBinary.GetInt32(properties, "dragBehavior") is int dragBehavior) SetBit(ref bits, 19, dragBehavior != 0);
        SetBit(ref bits, 20, MsFormsFactoryBinary.GetBool(properties, "enterKeyBehavior"));
        if (MsFormsFactoryBinary.GetInt32(properties, "enterFieldBehavior") is int enterFieldBehavior) SetBit(ref bits, 21, enterFieldBehavior != 0);
        SetBit(ref bits, 22, MsFormsFactoryBinary.GetBool(properties, "tabKeyBehavior"));
        SetBit(ref bits, 23, MsFormsFactoryBinary.GetBool(properties, "wordWrap"));
        SetBit(ref bits, 26, MsFormsFactoryBinary.GetBool(properties, "selectionMargin"));
        SetBit(ref bits, 27, MsFormsFactoryBinary.GetBool(properties, "autoWordSelect"));
        SetBit(ref bits, 28, MsFormsFactoryBinary.GetBool(properties, "autoSize"));
        SetBit(ref bits, 29, MsFormsFactoryBinary.GetBool(properties, "hideSelection"));
        SetBit(ref bits, 30, MsFormsFactoryBinary.GetBool(properties, "autoTab"));
        SetBit(ref bits, 31, MsFormsFactoryBinary.GetBool(properties, "multiLine"));
        if (MsFormsFactoryBinary.GetInt32(properties, "imeMode") is int imeMode)
        {
            bits &= ~(0xFu << 15);
            bits |= ((uint)imeMode & 0xFu) << 15;
        }

        return bits;
    }

    private static void SetBit(ref uint bits, int bit, bool? value)
    {
        if (value is null) return;
        var mask = 1u << bit;
        bits = value.Value ? bits | mask : bits & ~mask;
    }
}

internal abstract class MorphButtonControlSchema : IGeneratedControlSchema
{
    private const uint DefaultBackColor = 0x8000_000F;
    private const uint PersistedForeColor = 0x8000_0012;
    private const uint FileDefaultForeColor = 0x8000_0008;
    private const uint DefaultPicturePosition = 0x0007_0001;
    private const int DefaultSpecialEffect = 0;

    public abstract string Type { get; }
    public uint SiteFlags => 0x0000_0033; // tabStop + visible + streamed + fAutoSize file default.
    protected abstract byte DisplayStyle { get; }
    protected abstract uint DefaultVariousPropertyBits { get; }
    protected virtual uint TextPropsMask => TextPropsFactory.StandardMask;

    public byte[] BuildObjectPayload(GeneratedControlRequest request)
    {
        var value = request.Value ?? MsFormsFactoryBinary.GetString(request.Properties, "value") ?? "0";
        var captionProp = MsFormsFactoryBinary.GetString(request.Properties, "caption");
        var hasCaption = request.Caption is not null || request.Properties.ContainsKey("caption");
        var caption = request.Caption ?? captionProp ?? string.Empty;
        var valueBytes = Encoding.Latin1.GetBytes(value);
        var captionBytes = Encoding.Latin1.GetBytes(caption);
        var hasBackColor = request.Properties.ContainsKey("backColor");
        var hasForeColor = request.Properties.ContainsKey("foreColor");
        var backColor = MsFormsFactoryBinary.ParseColor(MsFormsFactoryBinary.GetString(request.Properties, "backColor"), DefaultBackColor);
        var foreColor = MsFormsFactoryBinary.ParseColor(MsFormsFactoryBinary.GetString(request.Properties, "foreColor"), PersistedForeColor);
        var variousBits = BuildVariousPropertyBits(request.Properties);
        var mousePointer = MsFormsFactoryBinary.GetInt32(request.Properties, "mousePointer") ?? 0;
        var multiSelect = MsFormsFactoryBinary.GetInt32(request.Properties, "multiSelect") ?? 0;
        var picturePosition = MsFormsFactoryBinary.GetInt32(request.Properties, "picturePosition") ?? unchecked((int)DefaultPicturePosition);
        var specialEffect = MsFormsFactoryBinary.GetInt32(request.Properties, "specialEffect") ?? DefaultSpecialEffect;
        var accelerator = MsFormsFactoryBinary.GetString(request.Properties, "accelerator");
        var groupName = MsFormsFactoryBinary.GetString(request.Properties, "groupName") ?? string.Empty;
        var groupNameBytes = Encoding.Latin1.GetBytes(groupName);
        var mouseIcon = MsFormsFactoryBinary.GetNativePictureStream(request.Properties, "mouseIcon", Type, request.Name);
        var picture = MsFormsFactoryBinary.GetNativePictureStream(request.Properties, "picture", Type, request.Name);
        var propMask = BuildPropMask(valueBytes.Length, hasCaption, groupNameBytes.Length, hasBackColor, backColor,
            hasForeColor, foreColor, variousBits,
            mousePointer, multiSelect, picturePosition, specialEffect, accelerator, mouseIcon.Length > 0, picture.Length > 0);

        using var dataBlock = new MemoryStream();
        WriteDataBlock(dataBlock, propMask, valueBytes, captionBytes, groupNameBytes, backColor, foreColor, variousBits,
            mousePointer, multiSelect, picturePosition, specialEffect, accelerator);

        using var extra = new MemoryStream();
        MsFormsFactoryBinary.WriteSize(extra, request.Width, request.Height);
        WriteFmString(extra, propMask, 22, valueBytes);
        WriteFmString(extra, propMask, 23, captionBytes);
        WriteFmString(extra, propMask, 32, groupNameBytes);

        var control = MsFormsFactoryBinary.BuildVersionedMorphControl(propMask, dataBlock.ToArray(), extra.ToArray());
        var textPropsMask = TextPropsFactory.WithParagraphAlignIfNeeded(TextPropsMask, request.Properties);
        var textProps = TextPropsFactory.Build(request.Properties, textPropsMask);
        return [.. control, .. mouseIcon, .. picture, .. textProps];
    }

    public IReadOnlyDictionary<string, object?> BuildMetadata(GeneratedControlRequest request, int objectPayloadSize)
    {
        var value = request.Value ?? MsFormsFactoryBinary.GetString(request.Properties, "value") ?? "0";
        var captionProp = MsFormsFactoryBinary.GetString(request.Properties, "caption");
        var hasCaption = request.Caption is not null || request.Properties.ContainsKey("caption");
        var caption = request.Caption ?? captionProp ?? string.Empty;
        var groupName = MsFormsFactoryBinary.GetString(request.Properties, "groupName") ?? string.Empty;
        var hasBackColor = request.Properties.ContainsKey("backColor");
        var hasForeColor = request.Properties.ContainsKey("foreColor");
        var backColor = MsFormsFactoryBinary.ParseColor(MsFormsFactoryBinary.GetString(request.Properties, "backColor"), DefaultBackColor);
        var foreColor = MsFormsFactoryBinary.ParseColor(MsFormsFactoryBinary.GetString(request.Properties, "foreColor"), PersistedForeColor);
        var variousBits = BuildVariousPropertyBits(request.Properties);
        var mousePointer = MsFormsFactoryBinary.GetInt32(request.Properties, "mousePointer") ?? 0;
        var multiSelect = MsFormsFactoryBinary.GetInt32(request.Properties, "multiSelect") ?? 0;
        var picturePosition = MsFormsFactoryBinary.GetInt32(request.Properties, "picturePosition") ?? unchecked((int)DefaultPicturePosition);
        var specialEffect = MsFormsFactoryBinary.GetInt32(request.Properties, "specialEffect") ?? DefaultSpecialEffect;
        var accelerator = MsFormsFactoryBinary.GetString(request.Properties, "accelerator");
        var mouseIcon = MsFormsFactoryBinary.GetNativePictureStream(request.Properties, "mouseIcon", Type, request.Name);
        var picture = MsFormsFactoryBinary.GetNativePictureStream(request.Properties, "picture", Type, request.Name);
        var propMask = BuildPropMask(Encoding.Latin1.GetByteCount(value), hasCaption,
            Encoding.Latin1.GetByteCount(groupName), hasBackColor, backColor, hasForeColor, foreColor, variousBits,
            mousePointer, multiSelect,
            picturePosition, specialEffect, accelerator, mouseIcon.Length > 0, picture.Length > 0);
        var textPropsMask = TextPropsFactory.WithParagraphAlignIfNeeded(TextPropsMask, request.Properties);
        var metadata = TextPropsFactory.BuildMetadata(textPropsMask, request.Properties);
        metadata["parser"] = "msOFormsMorphData";
        metadata["controlType"] = Type;
        metadata["sizeSource"] = "morphDataExtraDataBlock";
        metadata["propMask"] = $"0x{propMask:X16}";
        metadata["backColor"] = $"&H{backColor:X8}&";
        metadata["foreColor"] = $"&H{foreColor:X8}&";
        metadata["displayStyle"] = DisplayStyle;
        metadata["value"] = value;
        if (hasCaption) metadata["caption"] = caption;
        if (variousBits != DefaultVariousPropertyBits)
        {
            metadata["variousPropertyBitsRaw"] = unchecked((int)variousBits);
            AddVariousMetadata(metadata, variousBits);
        }
        if (mousePointer != 0) metadata["mousePointer"] = mousePointer;
        if (multiSelect != 0) metadata["multiSelect"] = multiSelect;
        if (unchecked((uint)picturePosition) != DefaultPicturePosition) metadata["picturePosition"] = picturePosition;
        if (specialEffect != DefaultSpecialEffect) metadata["specialEffect"] = specialEffect;
        if (!string.IsNullOrEmpty(accelerator)) metadata["accelerator"] = accelerator[0].ToString();
        if (!string.IsNullOrEmpty(groupName)) metadata["groupName"] = groupName;
        if (mouseIcon.Length > 0) metadata["mouseIcon"] = MsFormsFactoryBinary.GetString(request.Properties, "mouseIcon");
        if (picture.Length > 0) metadata["picture"] = MsFormsFactoryBinary.GetString(request.Properties, "picture");
        metadata["objectStreamSize"] = objectPayloadSize;
        metadata["siteBitFlags"] = $"0x{SiteFlags:X8}";
        metadata["tabStop"] = true;
        metadata["visible"] = true;
        metadata["streamed"] = true;
        return metadata;
    }

    private ulong BuildPropMask(int valueLength, bool hasCaption, int groupNameLength, bool hasBackColor,
        uint backColor, bool hasForeColor, uint foreColor, uint variousBits, int mousePointer, int multiSelect,
        int picturePosition, int specialEffect, string? accelerator, bool hasMouseIcon, bool hasPicture)
    {
        ulong mask = 1ul << 6 | 1ul << 8 | 1ul << 31; // DisplayStyle, Size, Reserved.
        if (variousBits != DefaultVariousPropertyBits) mask |= 1ul << 0;
        if (hasBackColor || backColor != DefaultBackColor) mask |= 1ul << 1;
        if (hasForeColor || foreColor != FileDefaultForeColor) mask |= 1ul << 2;
        if (mousePointer != 0) mask |= 1ul << 7;
        if (multiSelect != 0) mask |= 1ul << 21;
        if (valueLength > 0) mask |= 1ul << 22;
        if (hasCaption) mask |= 1ul << 23;
        if (unchecked((uint)picturePosition) != DefaultPicturePosition) mask |= 1ul << 24;
        if (specialEffect != DefaultSpecialEffect) mask |= 1ul << 26;
        if (!string.IsNullOrEmpty(accelerator)) mask |= 1ul << 29;
        if (groupNameLength > 0) mask |= 1ul << 32;
        if (hasMouseIcon) mask |= 1ul << 27;
        if (hasPicture) mask |= 1ul << 28;
        return mask;
    }

    private void WriteDataBlock(Stream dataBlock, ulong mask, byte[] valueBytes, byte[] captionBytes, byte[] groupNameBytes,
        uint backColor, uint foreColor, uint variousBits, int mousePointer, int multiSelect, int picturePosition,
        int specialEffect, string? accelerator)
    {
        if (Has(mask, 0)) MsFormsFactoryBinary.WriteUInt32(dataBlock, variousBits);
        if (Has(mask, 1)) MsFormsFactoryBinary.WriteUInt32(dataBlock, backColor);
        if (Has(mask, 2)) MsFormsFactoryBinary.WriteUInt32(dataBlock, foreColor);
        dataBlock.WriteByte(DisplayStyle);
        if (Has(mask, 7)) dataBlock.WriteByte(checked((byte)mousePointer));
        if (Has(mask, 21)) dataBlock.WriteByte(checked((byte)multiSelect));
        if (Has(mask, 22)) { MsFormsFactoryBinary.WritePadding(dataBlock, 4); MsFormsFactoryBinary.WriteCount(dataBlock, valueBytes.Length); }
        if (Has(mask, 23)) { MsFormsFactoryBinary.WritePadding(dataBlock, 4); MsFormsFactoryBinary.WriteCount(dataBlock, captionBytes.Length); }
        if (Has(mask, 24)) { MsFormsFactoryBinary.WritePadding(dataBlock, 4); MsFormsFactoryBinary.WriteUInt32(dataBlock, unchecked((uint)picturePosition)); }
        if (Has(mask, 26)) { MsFormsFactoryBinary.WritePadding(dataBlock, 4); MsFormsFactoryBinary.WriteUInt32(dataBlock, unchecked((uint)specialEffect)); }
        if (Has(mask, 27)) { MsFormsFactoryBinary.WritePadding(dataBlock, 2); MsFormsFactoryBinary.WriteUInt16(dataBlock, 0xFFFF); }
        if (Has(mask, 28)) { MsFormsFactoryBinary.WritePadding(dataBlock, 2); MsFormsFactoryBinary.WriteUInt16(dataBlock, 0xFFFF); }
        if (Has(mask, 29)) { MsFormsFactoryBinary.WritePadding(dataBlock, 2); MsFormsFactoryBinary.WriteUInt16(dataBlock, accelerator![0]); }
        if (Has(mask, 32)) { MsFormsFactoryBinary.WritePadding(dataBlock, 4); MsFormsFactoryBinary.WriteCount(dataBlock, groupNameBytes.Length); }
        MsFormsFactoryBinary.WritePadding(dataBlock, 4);
    }

    private static void WriteFmString(Stream extra, ulong mask, int bit, byte[] bytes)
    {
        if (!Has(mask, bit)) return;
        extra.Write(bytes);
        MsFormsFactoryBinary.WritePadding(extra, 4);
    }

    private uint BuildVariousPropertyBits(Dictionary<string, object?> properties)
    {
        var bits = MsFormsFactoryBinary.GetUInt32(properties, "variousPropertyBitsRaw") ?? DefaultVariousPropertyBits;
        SetBit(ref bits, 1, MsFormsFactoryBinary.GetBool(properties, "enabled"));
        SetBit(ref bits, 2, MsFormsFactoryBinary.GetBool(properties, "locked"));
        if (MsFormsFactoryBinary.GetInt32(properties, "backStyle") is int backStyle) SetBit(ref bits, 3, backStyle != 0);
        if (MsFormsFactoryBinary.GetInt32(properties, "alignment") is int alignment) SetBit(ref bits, 13, alignment == 0);
        SetBit(ref bits, 23, MsFormsFactoryBinary.GetBool(properties, "wordWrap"));
        SetBit(ref bits, 28, MsFormsFactoryBinary.GetBool(properties, "autoSize"));
        if (MsFormsFactoryBinary.GetInt32(properties, "imeMode") is int imeMode)
        {
            bits &= ~(0xFu << 15);
            bits |= ((uint)imeMode & 0xFu) << 15;
        }
        return bits;
    }

    private static void AddVariousMetadata(Dictionary<string, object?> metadata, uint bits)
    {
        metadata["enabled"] = (bits & (1u << 1)) != 0;
        metadata["locked"] = (bits & (1u << 2)) != 0;
        metadata["backStyle"] = (bits & (1u << 3)) != 0 ? 1 : 0;
        metadata["alignment"] = (bits & (1u << 13)) != 0 ? 0 : 1;
        metadata["imeMode"] = (int)((bits >> 15) & 0x0F);
        metadata["wordWrap"] = (bits & (1u << 23)) != 0;
        metadata["autoSize"] = (bits & (1u << 28)) != 0;
    }

    private static bool Has(ulong value, int bit) => (value & (1ul << bit)) != 0;

    private static void SetBit(ref uint bits, int bit, bool? value)
    {
        if (value is null) return;
        var mask = 1u << bit;
        bits = value.Value ? bits | mask : bits & ~mask;
    }
}

internal sealed class CheckBoxControlSchema : MorphButtonControlSchema
{
    public override string Type => "CheckBox";
    protected override byte DisplayStyle => 4;
    protected override uint DefaultVariousPropertyBits => 0x2C80_081B;
}

internal sealed class OptionButtonControlSchema : MorphButtonControlSchema
{
    public override string Type => "OptionButton";
    protected override byte DisplayStyle => 5;
    protected override uint DefaultVariousPropertyBits => 0x0080_001B;
}

internal sealed class ToggleButtonControlSchema : MorphButtonControlSchema
{
    public override string Type => "ToggleButton";
    protected override byte DisplayStyle => 6;
    protected override uint DefaultVariousPropertyBits => 0x2C80_081B;
    protected override uint TextPropsMask => TextPropsFactory.CommandButtonMask;
}

internal abstract class MorphListControlSchema : IGeneratedControlSchema
{
    private const uint DefaultBackColor = 0x8000_0005;
    private const uint DefaultForeColor = 0x8000_0008;
    private const uint DefaultBorderColor = 0x8000_0006;
    private const int DefaultBorderStyle = 1;
    private const int DefaultSpecialEffect = 2;

    public abstract string Type { get; }
    public uint SiteFlags => 0x0000_0033; // tabStop + visible + streamed + fAutoSize file default.
    protected abstract uint DefaultVariousPropertyBits { get; }
    protected abstract int DefaultDisplayStyle { get; }
    protected abstract int PersistedDisplayStyle { get; }
    protected abstract int PersistedMatchEntry { get; }
    protected virtual int DefaultScrollBars => 0;
    protected virtual int PersistedScrollBars => DefaultScrollBars;
    protected virtual int? PersistedShowDropButtonWhen => null;
    protected virtual bool SupportsMaxLength => false;
    protected virtual bool SupportsListRows => false;
    protected virtual bool SupportsShowDropButtonWhen => false;
    protected virtual bool SupportsDropButtonStyle => false;
    protected virtual bool SupportsMultiSelect => false;

    public byte[] BuildObjectPayload(GeneratedControlRequest request)
    {
        var value = request.Value ?? MsFormsFactoryBinary.GetString(request.Properties, "value") ?? string.Empty;
        var valueBytes = Encoding.Latin1.GetBytes(value);
        var backColor = MsFormsFactoryBinary.ParseColor(MsFormsFactoryBinary.GetString(request.Properties, "backColor"), DefaultBackColor);
        var foreColor = MsFormsFactoryBinary.ParseColor(MsFormsFactoryBinary.GetString(request.Properties, "foreColor"), DefaultForeColor);
        var borderColor = MsFormsFactoryBinary.ParseColor(MsFormsFactoryBinary.GetString(request.Properties, "borderColor"), DefaultBorderColor);
        var variousBits = BuildVariousPropertyBits(request.Properties);
        var maxLength = SupportsMaxLength ? MsFormsFactoryBinary.GetInt32(request.Properties, "maxLength") ?? 0 : 0;
        var borderStyle = MsFormsFactoryBinary.GetInt32(request.Properties, "borderStyle") ?? DefaultBorderStyle;
        var scrollBars = MsFormsFactoryBinary.GetInt32(request.Properties, "scrollBars") ?? PersistedScrollBars;
        var displayStyle = MsFormsFactoryBinary.GetInt32(request.Properties, "displayStyle") ?? PersistedDisplayStyle;
        var mousePointer = MsFormsFactoryBinary.GetInt32(request.Properties, "mousePointer") ?? 0;
        var listWidth = MsFormsFactoryBinary.GetInt32(request.Properties, "listWidth") ?? 0;
        var boundColumn = MsFormsFactoryBinary.GetInt32(request.Properties, "boundColumn") ?? 1;
        var textColumn = MsFormsFactoryBinary.GetInt32(request.Properties, "textColumn") ?? -1;
        var columnCount = MsFormsFactoryBinary.GetInt32(request.Properties, "columnCount") ?? 1;
        var listRows = SupportsListRows ? MsFormsFactoryBinary.GetInt32(request.Properties, "listRows") ?? 8 : 8;
        var matchEntry = MsFormsFactoryBinary.GetInt32(request.Properties, "matchEntry") ?? PersistedMatchEntry;
        var listStyle = MsFormsFactoryBinary.GetInt32(request.Properties, "listStyle") ?? 0;
        var showDropButtonWhen = SupportsShowDropButtonWhen
            ? MsFormsFactoryBinary.GetInt32(request.Properties, "showDropButtonWhen") ?? PersistedShowDropButtonWhen ?? 0
            : 0;
        var dropButtonStyle = SupportsDropButtonStyle ? MsFormsFactoryBinary.GetInt32(request.Properties, "dropButtonStyle") ?? 1 : 1;
        var multiSelect = SupportsMultiSelect ? MsFormsFactoryBinary.GetInt32(request.Properties, "multiSelect") ?? 0 : 0;
        var specialEffect = MsFormsFactoryBinary.GetInt32(request.Properties, "specialEffect") ?? DefaultSpecialEffect;
        var mouseIcon = MsFormsFactoryBinary.GetNativePictureStream(request.Properties, "mouseIcon", Type, request.Name);
        var picture = MsFormsFactoryBinary.GetNativePictureStream(request.Properties, "picture", Type, request.Name);

        var propMask = BuildPropMask(valueBytes.Length, backColor, foreColor, borderColor, variousBits, maxLength, borderStyle, scrollBars,
            displayStyle, mousePointer, listWidth, boundColumn, textColumn, columnCount, listRows, matchEntry, listStyle,
            showDropButtonWhen, dropButtonStyle, multiSelect, specialEffect);
        if (mouseIcon.Length > 0) propMask |= 1ul << 27;
        if (picture.Length > 0) propMask |= 1ul << 28;

        using var dataBlock = new MemoryStream();
        WriteDataBlock(dataBlock, propMask, valueBytes, backColor, foreColor, borderColor, variousBits, maxLength, borderStyle, scrollBars,
            displayStyle, mousePointer, listWidth, boundColumn, textColumn, columnCount, listRows, matchEntry, listStyle,
            showDropButtonWhen, dropButtonStyle, multiSelect, specialEffect);
        using var extra = new MemoryStream();
        MsFormsFactoryBinary.WriteSize(extra, request.Width, request.Height);
        if (valueBytes.Length > 0)
        {
            extra.Write(valueBytes);
            MsFormsFactoryBinary.WritePadding(extra, 4);
        }

        var control = MsFormsFactoryBinary.BuildVersionedMorphControl(propMask, dataBlock.ToArray(), extra.ToArray());
        var textPropsMask = TextPropsFactory.WithParagraphAlignIfNeeded(TextPropsFactory.StandardMask, request.Properties);
        var textProps = TextPropsFactory.Build(request.Properties, textPropsMask);
        return [.. control, .. mouseIcon, .. picture, .. textProps];
    }

    public IReadOnlyDictionary<string, object?> BuildMetadata(GeneratedControlRequest request, int objectPayloadSize)
    {
        var value = request.Value ?? MsFormsFactoryBinary.GetString(request.Properties, "value") ?? string.Empty;
        var valueBytes = Encoding.Latin1.GetBytes(value);
        var backColor = MsFormsFactoryBinary.ParseColor(MsFormsFactoryBinary.GetString(request.Properties, "backColor"), DefaultBackColor);
        var foreColor = MsFormsFactoryBinary.ParseColor(MsFormsFactoryBinary.GetString(request.Properties, "foreColor"), DefaultForeColor);
        var borderColor = MsFormsFactoryBinary.ParseColor(MsFormsFactoryBinary.GetString(request.Properties, "borderColor"), DefaultBorderColor);
        var variousBits = BuildVariousPropertyBits(request.Properties);
        var maxLength = SupportsMaxLength ? MsFormsFactoryBinary.GetInt32(request.Properties, "maxLength") ?? 0 : 0;
        var borderStyle = MsFormsFactoryBinary.GetInt32(request.Properties, "borderStyle") ?? DefaultBorderStyle;
        var scrollBars = MsFormsFactoryBinary.GetInt32(request.Properties, "scrollBars") ?? PersistedScrollBars;
        var displayStyle = MsFormsFactoryBinary.GetInt32(request.Properties, "displayStyle") ?? PersistedDisplayStyle;
        var mousePointer = MsFormsFactoryBinary.GetInt32(request.Properties, "mousePointer") ?? 0;
        var listWidth = MsFormsFactoryBinary.GetInt32(request.Properties, "listWidth") ?? 0;
        var boundColumn = MsFormsFactoryBinary.GetInt32(request.Properties, "boundColumn") ?? 1;
        var textColumn = MsFormsFactoryBinary.GetInt32(request.Properties, "textColumn") ?? -1;
        var columnCount = MsFormsFactoryBinary.GetInt32(request.Properties, "columnCount") ?? 1;
        var listRows = SupportsListRows ? MsFormsFactoryBinary.GetInt32(request.Properties, "listRows") ?? 8 : 8;
        var matchEntry = MsFormsFactoryBinary.GetInt32(request.Properties, "matchEntry") ?? PersistedMatchEntry;
        var listStyle = MsFormsFactoryBinary.GetInt32(request.Properties, "listStyle") ?? 0;
        var showDropButtonWhen = SupportsShowDropButtonWhen
            ? MsFormsFactoryBinary.GetInt32(request.Properties, "showDropButtonWhen") ?? PersistedShowDropButtonWhen ?? 0
            : 0;
        var dropButtonStyle = SupportsDropButtonStyle ? MsFormsFactoryBinary.GetInt32(request.Properties, "dropButtonStyle") ?? 1 : 1;
        var multiSelect = SupportsMultiSelect ? MsFormsFactoryBinary.GetInt32(request.Properties, "multiSelect") ?? 0 : 0;
        var specialEffect = MsFormsFactoryBinary.GetInt32(request.Properties, "specialEffect") ?? DefaultSpecialEffect;
        var mouseIcon = MsFormsFactoryBinary.GetNativePictureStream(request.Properties, "mouseIcon", Type, request.Name);
        var picture = MsFormsFactoryBinary.GetNativePictureStream(request.Properties, "picture", Type, request.Name);
        var propMask = BuildPropMask(valueBytes.Length, backColor, foreColor, borderColor, variousBits, maxLength, borderStyle, scrollBars,
            displayStyle, mousePointer, listWidth, boundColumn, textColumn, columnCount, listRows, matchEntry, listStyle,
            showDropButtonWhen, dropButtonStyle, multiSelect, specialEffect);
        if (mouseIcon.Length > 0) propMask |= 1ul << 27;
        if (picture.Length > 0) propMask |= 1ul << 28;

        var textPropsMask = TextPropsFactory.WithParagraphAlignIfNeeded(TextPropsFactory.StandardMask, request.Properties);
        var metadata = TextPropsFactory.BuildMetadata(textPropsMask, request.Properties);
        metadata["parser"] = "msOFormsMorphData";
        metadata["controlType"] = Type;
        metadata["sizeSource"] = "morphDataExtraDataBlock";
        metadata["propMask"] = $"0x{propMask:X16}";
        metadata["variousPropertyBitsRaw"] = unchecked((int)variousBits);
        AddVariousMetadata(metadata, variousBits);
        metadata["backColor"] = $"&H{backColor:X8}&";
        metadata["foreColor"] = $"&H{foreColor:X8}&";
        metadata["borderColor"] = $"&H{borderColor:X8}&";
        metadata["borderStyle"] = borderStyle;
        metadata["displayStyle"] = displayStyle;
        metadata["matchEntry"] = matchEntry;
        metadata["specialEffect"] = specialEffect;
        if (scrollBars != DefaultScrollBars || Type.Equals("ListBox", StringComparison.OrdinalIgnoreCase)) metadata["scrollBars"] = scrollBars;
        if (listWidth != 0) metadata["listWidth"] = listWidth;
        if (boundColumn != 1) metadata["boundColumn"] = boundColumn;
        if (textColumn != -1) metadata["textColumn"] = textColumn;
        if (columnCount != 1) metadata["columnCount"] = columnCount;
        if (SupportsMaxLength && maxLength != 0) metadata["maxLength"] = maxLength;
        if (SupportsListRows) metadata["listRows"] = listRows;
        if (SupportsShowDropButtonWhen) metadata["showDropButtonWhen"] = showDropButtonWhen;
        if (SupportsDropButtonStyle) metadata["dropButtonStyle"] = dropButtonStyle;
        if (SupportsMultiSelect) metadata["multiSelect"] = multiSelect;
        if (listStyle != 0) metadata["listStyle"] = listStyle;
        if (mousePointer != 0) metadata["mousePointer"] = mousePointer;
        if (value.Length > 0) metadata["value"] = value;
        if (mouseIcon.Length > 0) metadata["mouseIcon"] = MsFormsFactoryBinary.GetString(request.Properties, "mouseIcon");
        if (picture.Length > 0) metadata["picture"] = MsFormsFactoryBinary.GetString(request.Properties, "picture");
        metadata["objectStreamSize"] = objectPayloadSize;
        metadata["siteBitFlags"] = $"0x{SiteFlags:X8}";
        metadata["tabStop"] = true;
        metadata["visible"] = true;
        metadata["streamed"] = true;
        return metadata;
    }

    protected virtual uint BuildVariousPropertyBits(Dictionary<string, object?> properties)
    {
        var bits = MsFormsFactoryBinary.GetUInt32(properties, "variousPropertyBitsRaw") ?? DefaultVariousPropertyBits;
        SetBit(ref bits, 1, MsFormsFactoryBinary.GetBool(properties, "enabled"));
        SetBit(ref bits, 2, MsFormsFactoryBinary.GetBool(properties, "locked"));
        if (MsFormsFactoryBinary.GetInt32(properties, "backStyle") is int backStyle) SetBit(ref bits, 3, backStyle != 0);
        SetBit(ref bits, 10, MsFormsFactoryBinary.GetBool(properties, "columnHeads"));
        SetBit(ref bits, 11, MsFormsFactoryBinary.GetBool(properties, "integralHeight"));
        SetBit(ref bits, 12, MsFormsFactoryBinary.GetBool(properties, "matchRequired"));
        SetBit(ref bits, 14, MsFormsFactoryBinary.GetBool(properties, "editable"));
        if (MsFormsFactoryBinary.GetInt32(properties, "dragBehavior") is int dragBehavior) SetBit(ref bits, 19, dragBehavior != 0);
        if (MsFormsFactoryBinary.GetInt32(properties, "enterFieldBehavior") is int enterFieldBehavior) SetBit(ref bits, 21, enterFieldBehavior != 0);
        SetBit(ref bits, 23, MsFormsFactoryBinary.GetBool(properties, "wordWrap"));
        SetBit(ref bits, 26, MsFormsFactoryBinary.GetBool(properties, "selectionMargin"));
        SetBit(ref bits, 27, MsFormsFactoryBinary.GetBool(properties, "autoWordSelect"));
        SetBit(ref bits, 28, MsFormsFactoryBinary.GetBool(properties, "autoSize"));
        SetBit(ref bits, 29, MsFormsFactoryBinary.GetBool(properties, "hideSelection"));
        SetBit(ref bits, 30, MsFormsFactoryBinary.GetBool(properties, "autoTab"));
        if (MsFormsFactoryBinary.GetInt32(properties, "imeMode") is int imeMode)
        {
            bits &= ~(0xFu << 15);
            bits |= ((uint)imeMode & 0xFu) << 15;
        }

        return bits;
    }

    private ulong BuildPropMask(int valueLength, uint backColor, uint foreColor, uint borderColor, uint variousBits, int maxLength,
        int borderStyle, int scrollBars, int displayStyle, int mousePointer, int listWidth, int boundColumn, int textColumn,
        int columnCount, int listRows, int matchEntry, int listStyle, int showDropButtonWhen, int dropButtonStyle,
        int multiSelect, int specialEffect)
    {
        ulong mask = 1ul << 8 | 1ul << 31;
        if (variousBits != DefaultVariousPropertyBits || Type.Equals("ComboBox", StringComparison.OrdinalIgnoreCase)) mask |= 1ul << 0;
        if (backColor != DefaultBackColor) mask |= 1ul << 1;
        if (foreColor != DefaultForeColor) mask |= 1ul << 2;
        if (SupportsMaxLength && maxLength != 0) mask |= 1ul << 3;
        if (borderStyle != DefaultBorderStyle) mask |= 1ul << 4;
        if (scrollBars != DefaultScrollBars || Type.Equals("ListBox", StringComparison.OrdinalIgnoreCase)) mask |= 1ul << 5;
        if (displayStyle != DefaultDisplayStyle || Type.Equals("ListBox", StringComparison.OrdinalIgnoreCase) || Type.Equals("ComboBox", StringComparison.OrdinalIgnoreCase)) mask |= 1ul << 6;
        if (mousePointer != 0) mask |= 1ul << 7;
        if (listWidth != 0) mask |= 1ul << 10;
        if (boundColumn != 1) mask |= 1ul << 11;
        if (textColumn != -1) mask |= 1ul << 12;
        if (columnCount != 1) mask |= 1ul << 13;
        if (SupportsListRows && listRows != 8) mask |= 1ul << 14;
        if (matchEntry != 2 || Type.Equals("ListBox", StringComparison.OrdinalIgnoreCase) || Type.Equals("ComboBox", StringComparison.OrdinalIgnoreCase)) mask |= 1ul << 16;
        if (listStyle != 0) mask |= 1ul << 17;
        if (SupportsShowDropButtonWhen && showDropButtonWhen != 0) mask |= 1ul << 18;
        if (SupportsDropButtonStyle && dropButtonStyle != 1) mask |= 1ul << 20;
        if (SupportsMultiSelect && multiSelect != 0) mask |= 1ul << 21;
        if (valueLength > 0) mask |= 1ul << 22;
        if (borderColor != DefaultBorderColor) mask |= 1ul << 25;
        if (specialEffect != DefaultSpecialEffect) mask |= 1ul << 26;
        return mask;
    }

    private static void WriteDataBlock(Stream dataBlock, ulong mask, byte[] valueBytes, uint backColor, uint foreColor, uint borderColor,
        uint variousBits, int maxLength, int borderStyle, int scrollBars, int displayStyle, int mousePointer, int listWidth,
        int boundColumn, int textColumn, int columnCount, int listRows, int matchEntry, int listStyle, int showDropButtonWhen,
        int dropButtonStyle, int multiSelect, int specialEffect)
    {
        if (Has(mask, 0)) MsFormsFactoryBinary.WriteUInt32(dataBlock, variousBits);
        if (Has(mask, 1)) MsFormsFactoryBinary.WriteUInt32(dataBlock, backColor);
        if (Has(mask, 2)) MsFormsFactoryBinary.WriteUInt32(dataBlock, foreColor);
        if (Has(mask, 3)) MsFormsFactoryBinary.WriteUInt32(dataBlock, unchecked((uint)maxLength));
        if (Has(mask, 4)) dataBlock.WriteByte(checked((byte)borderStyle));
        if (Has(mask, 5)) dataBlock.WriteByte(checked((byte)scrollBars));
        if (Has(mask, 6)) dataBlock.WriteByte(checked((byte)displayStyle));
        if (Has(mask, 7)) dataBlock.WriteByte(checked((byte)mousePointer));
        if (Has(mask, 10)) { MsFormsFactoryBinary.WritePadding(dataBlock, 4); MsFormsFactoryBinary.WriteUInt32(dataBlock, unchecked((uint)listWidth)); }
        if (Has(mask, 11)) { MsFormsFactoryBinary.WritePadding(dataBlock, 2); MsFormsFactoryBinary.WriteUInt16(dataBlock, boundColumn); }
        if (Has(mask, 12)) { MsFormsFactoryBinary.WritePadding(dataBlock, 2); MsFormsFactoryBinary.WriteInt16(dataBlock, textColumn); }
        if (Has(mask, 13)) { MsFormsFactoryBinary.WritePadding(dataBlock, 2); MsFormsFactoryBinary.WriteInt16(dataBlock, columnCount); }
        if (Has(mask, 14)) { MsFormsFactoryBinary.WritePadding(dataBlock, 2); MsFormsFactoryBinary.WriteUInt16(dataBlock, listRows); }
        if (Has(mask, 16)) dataBlock.WriteByte(checked((byte)matchEntry));
        if (Has(mask, 17)) dataBlock.WriteByte(checked((byte)listStyle));
        if (Has(mask, 18)) dataBlock.WriteByte(checked((byte)showDropButtonWhen));
        if (Has(mask, 20)) dataBlock.WriteByte(checked((byte)dropButtonStyle));
        if (Has(mask, 21)) dataBlock.WriteByte(checked((byte)multiSelect));
        if (Has(mask, 22)) { MsFormsFactoryBinary.WritePadding(dataBlock, 4); MsFormsFactoryBinary.WriteCount(dataBlock, valueBytes.Length); }
        if (Has(mask, 25)) { MsFormsFactoryBinary.WritePadding(dataBlock, 4); MsFormsFactoryBinary.WriteUInt32(dataBlock, borderColor); }
        if (Has(mask, 26)) { MsFormsFactoryBinary.WritePadding(dataBlock, 4); MsFormsFactoryBinary.WriteUInt32(dataBlock, unchecked((uint)specialEffect)); }
        if (Has(mask, 27)) { MsFormsFactoryBinary.WritePadding(dataBlock, 2); MsFormsFactoryBinary.WriteUInt16(dataBlock, 0xFFFF); }
        if (Has(mask, 28)) { MsFormsFactoryBinary.WritePadding(dataBlock, 2); MsFormsFactoryBinary.WriteUInt16(dataBlock, 0xFFFF); }
        MsFormsFactoryBinary.WritePadding(dataBlock, 4);
    }

    private static void AddVariousMetadata(Dictionary<string, object?> metadata, uint bits)
    {
        metadata["enabled"] = (bits & (1u << 1)) != 0;
        metadata["locked"] = (bits & (1u << 2)) != 0;
        metadata["backStyle"] = (bits & (1u << 3)) != 0 ? 1 : 0;
        metadata["columnHeads"] = (bits & (1u << 10)) != 0;
        metadata["integralHeight"] = (bits & (1u << 11)) != 0;
        metadata["matchRequired"] = (bits & (1u << 12)) != 0;
        metadata["editable"] = (bits & (1u << 14)) != 0;
        metadata["dragBehavior"] = (bits & (1u << 19)) != 0 ? 1 : 0;
        metadata["enterFieldBehavior"] = (bits & (1u << 21)) != 0 ? 1 : 0;
        metadata["wordWrap"] = (bits & (1u << 23)) != 0;
        metadata["selectionMargin"] = (bits & (1u << 26)) != 0;
        metadata["autoWordSelect"] = (bits & (1u << 27)) != 0;
        metadata["autoSize"] = (bits & (1u << 28)) != 0;
        metadata["hideSelection"] = (bits & (1u << 29)) != 0;
        metadata["autoTab"] = (bits & (1u << 30)) != 0;
    }

    private static int? ParseTextAlign(Dictionary<string, object?> properties) =>
        MsFormsFactoryBinary.GetString(properties, "textAlign") is { } textAlign &&
        TextPropsFactory.TryParseTextAlign(textAlign, out var parsed)
            ? parsed
            : null;

    private static bool Has(ulong value, int bit) => (value & (1ul << bit)) != 0;

    private static void SetBit(ref uint bits, int bit, bool? value)
    {
        if (value is null) return;
        var mask = 1u << bit;
        bits = value.Value ? bits | mask : bits & ~mask;
    }
}

internal sealed class ComboBoxControlSchema : MorphListControlSchema
{
    public override string Type => "ComboBox";
    protected override uint DefaultVariousPropertyBits => 0x2C80_481B;
    protected override int DefaultDisplayStyle => 1;
    protected override int PersistedDisplayStyle => 3;
    protected override int PersistedMatchEntry => 1;
    protected override int? PersistedShowDropButtonWhen => 2;
    protected override bool SupportsMaxLength => true;
    protected override bool SupportsListRows => true;
    protected override bool SupportsShowDropButtonWhen => true;
    protected override bool SupportsDropButtonStyle => true;
}

internal sealed class ListBoxControlSchema : MorphListControlSchema
{
    public override string Type => "ListBox";
    protected override uint DefaultVariousPropertyBits => 0x0000_001B;
    protected override int DefaultDisplayStyle => 1;
    protected override int PersistedDisplayStyle => 2;
    protected override int PersistedMatchEntry => 0;
    protected override int PersistedScrollBars => 3;
    protected override bool SupportsMultiSelect => true;
}

internal abstract class SpinLikeControlSchema : IGeneratedControlSchema
{
    public abstract string Type { get; }
    public uint SiteFlags => 0x0000_0033; // tabStop + visible + streamed + fAutoSize file default.
    protected abstract string Parser { get; }
    protected abstract string SizeSource { get; }
    protected virtual string PropMaskName => "propMask";
    protected abstract bool IsScrollBar { get; }

    public byte[] BuildObjectPayload(GeneratedControlRequest request)
    {
        var state = BuildState(request);
        var mouseIcon = MsFormsFactoryBinary.GetNativePictureStream(request.Properties, "mouseIcon", Type, request.Name);

        using var dataBlock = new MemoryStream();
        if (Has(state.PropMask, 0)) MsFormsFactoryBinary.WriteUInt32(dataBlock, state.ForeColor);
        if (Has(state.PropMask, 1)) MsFormsFactoryBinary.WriteUInt32(dataBlock, state.BackColor);
        if (Has(state.PropMask, 2)) MsFormsFactoryBinary.WriteUInt32(dataBlock, state.VariousPropertyBits);

        if (IsScrollBar && Has(state.PropMask, 4))
        {
            MsFormsFactoryBinary.WritePadding(dataBlock, 4);
            dataBlock.WriteByte(checked((byte)state.MousePointer));
        }

        MsFormsFactoryBinary.WritePadding(dataBlock, 4);
        if (Has(state.PropMask, 5)) MsFormsFactoryBinary.WriteInt32(dataBlock, state.Min);
        if (Has(state.PropMask, 6)) MsFormsFactoryBinary.WriteInt32(dataBlock, state.Max);
        if (Has(state.PropMask, 7)) MsFormsFactoryBinary.WriteInt32(dataBlock, state.Position);
        if (Has(state.PropMask, IsScrollBar ? 11 : 10)) MsFormsFactoryBinary.WriteInt32(dataBlock, state.SmallChange);
        if (IsScrollBar && Has(state.PropMask, 12)) MsFormsFactoryBinary.WriteInt32(dataBlock, state.LargeChange);
        if (Has(state.PropMask, IsScrollBar ? 13 : 11)) MsFormsFactoryBinary.WriteInt32(dataBlock, state.Orientation);
        if (IsScrollBar && Has(state.PropMask, 14))
        {
            MsFormsFactoryBinary.WritePadding(dataBlock, 4);
            dataBlock.WriteByte((byte)(state.ProportionalThumb ? 1 : 0));
        }
        if (IsScrollBar)
        {
            MsFormsFactoryBinary.WritePadding(dataBlock, 4);
        }
        if (Has(state.PropMask, IsScrollBar ? 15 : 12)) MsFormsFactoryBinary.WriteInt32(dataBlock, state.Delay);
        if (Has(state.PropMask, IsScrollBar ? 16 : 13))
        {
            MsFormsFactoryBinary.WriteUInt16(dataBlock, 0xFFFF);
        }
        if (!IsScrollBar && Has(state.PropMask, 14))
        {
            MsFormsFactoryBinary.WritePadding(dataBlock, 4);
            dataBlock.WriteByte(checked((byte)state.MousePointer));
        }

        MsFormsFactoryBinary.WritePadding(dataBlock, 4);

        using var extra = new MemoryStream();
        MsFormsFactoryBinary.WriteSize(extra, request.Width, request.Height);

        var control = MsFormsFactoryBinary.BuildVersionedControl(0, 2, state.PropMask, dataBlock.ToArray(), extra.ToArray());
        return [.. control, .. mouseIcon];
    }

    public IReadOnlyDictionary<string, object?> BuildMetadata(GeneratedControlRequest request, int objectPayloadSize)
    {
        var state = BuildState(request);
        var mouseIcon = MsFormsFactoryBinary.GetNativePictureStream(request.Properties, "mouseIcon", Type, request.Name);
        var metadata = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["parser"] = Parser,
            ["sizeSource"] = SizeSource,
            [PropMaskName] = $"0x{state.PropMask:X8}",
            ["objectStreamSize"] = objectPayloadSize,
            ["siteBitFlags"] = $"0x{SiteFlags:X8}",
            ["tabStop"] = MsFormsFactoryBinary.GetBool(request.Properties, "tabStop") ?? true,
            ["visible"] = MsFormsFactoryBinary.GetBool(request.Properties, "visible") ?? true,
            ["streamed"] = true
        };
        if (Has(state.PropMask, 0)) metadata["foreColor"] = $"&H{state.ForeColor:X8}&";
        if (Has(state.PropMask, 1)) metadata["backColor"] = $"&H{state.BackColor:X8}&";
        if (Has(state.PropMask, 2))
        {
            metadata["variousPropertyBitsRaw"] = unchecked((int)state.VariousPropertyBits);
            MsFormsBinary.AddVariousPropertyBits(metadata, state.VariousPropertyBits);
        }
        if (Has(state.PropMask, IsScrollBar ? 4 : 14)) metadata["mousePointer"] = state.MousePointer;
        if (Has(state.PropMask, 5)) metadata["min"] = state.Min;
        if (Has(state.PropMask, 6)) metadata["max"] = state.Max;
        if (Has(state.PropMask, 7)) metadata["position"] = state.Position;
        if (Has(state.PropMask, IsScrollBar ? 11 : 10)) metadata["smallChange"] = state.SmallChange;
        if (IsScrollBar && Has(state.PropMask, 12)) metadata["largeChange"] = state.LargeChange;
        if (Has(state.PropMask, IsScrollBar ? 13 : 11)) metadata["orientation"] = state.Orientation;
        if (IsScrollBar && Has(state.PropMask, 14)) metadata["proportionalThumb"] = state.ProportionalThumb;
        if (Has(state.PropMask, IsScrollBar ? 15 : 12)) metadata["delay"] = state.Delay;
        if (mouseIcon.Length > 0) metadata["mouseIcon"] = MsFormsFactoryBinary.GetString(request.Properties, "mouseIcon");
        return metadata;
    }

    private SpinState BuildState(GeneratedControlRequest request)
    {
        const uint defaultForeColor = 0x8000_0012;
        const uint defaultBackColor = 0x8000_000F;
        var properties = request.Properties;
        var foreColor = MsFormsFactoryBinary.ParseColor(MsFormsFactoryBinary.GetString(properties, "foreColor"), defaultForeColor);
        var backColor = MsFormsFactoryBinary.ParseColor(MsFormsFactoryBinary.GetString(properties, "backColor"), defaultBackColor);
        var various = BuildVariousPropertyBits(properties);
        var mousePointer = MsFormsFactoryBinary.GetInt32(properties, "mousePointer") ?? 0;
        var min = MsFormsFactoryBinary.GetInt32(properties, "min") ?? 0;
        var max = MsFormsFactoryBinary.GetInt32(properties, "max") ?? (IsScrollBar ? 32767 : 100);
        var position = MsFormsFactoryBinary.GetInt32(properties, "position") ??
                       MsFormsFactoryBinary.GetInt32(properties, "value") ?? 0;
        var smallChange = MsFormsFactoryBinary.GetInt32(properties, "smallChange") ?? 1;
        var largeChange = MsFormsFactoryBinary.GetInt32(properties, "largeChange") ?? 1;
        var orientation = MsFormsFactoryBinary.GetInt32(properties, "orientation") ?? -1;
        var proportionalThumb = MsFormsFactoryBinary.GetBool(properties, "proportionalThumb") ?? false;
        var delay = MsFormsFactoryBinary.GetInt32(properties, "delay") ?? 50;
        var mouseIcon = MsFormsFactoryBinary.GetNativePictureStream(properties, "mouseIcon", Type, request.Name);

        uint propMask = 1u << 3;
        if (properties.ContainsKey("foreColor") || foreColor != defaultForeColor) propMask |= 1u << 0;
        if (properties.ContainsKey("backColor") || backColor != defaultBackColor) propMask |= 1u << 1;
        if (HasAnyVariousProperty(properties) || various != 0) propMask |= 1u << 2;
        if (mousePointer != 0 || properties.ContainsKey("mousePointer")) propMask |= 1u << (IsScrollBar ? 4 : 14);
        if (properties.ContainsKey("min") || min != 0) propMask |= 1u << 5;
        if (properties.ContainsKey("max") || max != (IsScrollBar ? 32767 : 100)) propMask |= 1u << 6;
        if (properties.ContainsKey("position") || properties.ContainsKey("value") || position != 0) propMask |= 1u << 7;
        if (properties.ContainsKey("smallChange") || smallChange != 1) propMask |= 1u << (IsScrollBar ? 11 : 10);
        if (IsScrollBar && (properties.ContainsKey("largeChange") || largeChange != 1)) propMask |= 1u << 12;
        if (properties.ContainsKey("orientation") || orientation != -1) propMask |= 1u << (IsScrollBar ? 13 : 11);
        if (IsScrollBar && (properties.ContainsKey("proportionalThumb") || proportionalThumb)) propMask |= 1u << 14;
        if (properties.ContainsKey("delay") || delay != 50) propMask |= 1u << (IsScrollBar ? 15 : 12);
        if (mouseIcon.Length > 0) propMask |= 1u << (IsScrollBar ? 16 : 13);

        return new SpinState(propMask, foreColor, backColor, various, mousePointer, min, max, position,
            smallChange, largeChange, orientation, proportionalThumb, delay);
    }

    private static uint BuildVariousPropertyBits(Dictionary<string, object?> properties)
    {
        var bits = MsFormsFactoryBinary.GetUInt32(properties, "variousPropertyBitsRaw") ?? 0u;
        SetBit(ref bits, 1, MsFormsFactoryBinary.GetBool(properties, "enabled"));
        SetBit(ref bits, 2, MsFormsFactoryBinary.GetBool(properties, "locked"));
        if (MsFormsFactoryBinary.GetInt32(properties, "backStyle") is int backStyle) SetBit(ref bits, 3, backStyle != 0);
        SetBit(ref bits, 10, MsFormsFactoryBinary.GetBool(properties, "columnHeads"));
        SetBit(ref bits, 11, MsFormsFactoryBinary.GetBool(properties, "integralHeight"));
        SetBit(ref bits, 12, MsFormsFactoryBinary.GetBool(properties, "matchRequired"));
        if (MsFormsFactoryBinary.GetInt32(properties, "alignment") is int alignment) SetBit(ref bits, 13, alignment == 0);
        SetBit(ref bits, 14, MsFormsFactoryBinary.GetBool(properties, "editable"));
        if (MsFormsFactoryBinary.GetInt32(properties, "imeMode") is int imeMode)
        {
            bits &= ~(0xFu << 15);
            bits |= ((uint)imeMode & 0xFu) << 15;
        }
        if (MsFormsFactoryBinary.GetInt32(properties, "dragBehavior") is int dragBehavior) SetBit(ref bits, 19, dragBehavior != 0);
        SetBit(ref bits, 20, MsFormsFactoryBinary.GetBool(properties, "enterKeyBehavior"));
        if (MsFormsFactoryBinary.GetInt32(properties, "enterFieldBehavior") is int enterFieldBehavior) SetBit(ref bits, 21, enterFieldBehavior != 0);
        SetBit(ref bits, 22, MsFormsFactoryBinary.GetBool(properties, "tabKeyBehavior"));
        SetBit(ref bits, 23, MsFormsFactoryBinary.GetBool(properties, "wordWrap"));
        SetBit(ref bits, 26, MsFormsFactoryBinary.GetBool(properties, "selectionMargin"));
        SetBit(ref bits, 27, MsFormsFactoryBinary.GetBool(properties, "autoWordSelect"));
        SetBit(ref bits, 28, MsFormsFactoryBinary.GetBool(properties, "autoSize"));
        SetBit(ref bits, 29, MsFormsFactoryBinary.GetBool(properties, "hideSelection"));
        SetBit(ref bits, 30, MsFormsFactoryBinary.GetBool(properties, "autoTab"));
        SetBit(ref bits, 31, MsFormsFactoryBinary.GetBool(properties, "multiLine"));
        return bits;
    }

    private static bool HasAnyVariousProperty(Dictionary<string, object?> properties) =>
        new[]
        {
            "variousPropertyBitsRaw", "enabled", "locked", "backStyle", "columnHeads", "integralHeight",
            "matchRequired", "alignment", "editable", "imeMode", "dragBehavior", "enterKeyBehavior",
            "enterFieldBehavior", "tabKeyBehavior", "wordWrap", "selectionMargin", "autoWordSelect",
            "autoSize", "hideSelection", "autoTab", "multiLine"
        }.Any(properties.ContainsKey);

    private static bool Has(uint value, int bit) => (value & (1u << bit)) != 0;

    private static void SetBit(ref uint bits, int bit, bool? value)
    {
        if (value is null) return;
        var mask = 1u << bit;
        bits = value.Value ? bits | mask : bits & ~mask;
    }

    private sealed record SpinState(
        uint PropMask,
        uint ForeColor,
        uint BackColor,
        uint VariousPropertyBits,
        int MousePointer,
        int Min,
        int Max,
        int Position,
        int SmallChange,
        int LargeChange,
        int Orientation,
        bool ProportionalThumb,
        int Delay);
}

internal sealed class ScrollBarControlSchema : SpinLikeControlSchema
{
    public override string Type => "ScrollBar";
    protected override string Parser => "msOFormsScrollBar";
    protected override string SizeSource => "scrollBarExtraDataBlock";
    protected override bool IsScrollBar => true;
}

internal sealed class SpinButtonControlSchema : SpinLikeControlSchema
{
    public override string Type => "SpinButton";
    protected override string Parser => "msOFormsSpinButton";
    protected override string SizeSource => "spinButtonExtraDataBlock";
    protected override bool IsScrollBar => false;
}

internal sealed class ImageControlSchema : IGeneratedControlSchema
{
    private const uint DefaultBorderColor = 0x8000_0006;
    private const uint DefaultBackColor = 0x8000_000F;
    private const uint DefaultVariousPropertyBits = 0x0000_0000;
    private const int DefaultBorderStyle = 1;
    private const int DefaultPictureSizeMode = 0;
    private const int DefaultSpecialEffect = 0;
    private const int DefaultPictureAlignment = 2;

    public string Type => "Image";
    public uint SiteFlags => 0x0000_0033; // tabStop + visible + streamed + fAutoSize file default.

    public byte[] BuildObjectPayload(GeneratedControlRequest request)
    {
        var picture = MsFormsFactoryBinary.GetNativePictureStream(request.Properties, "picture", Type, request.Name);
        var mouseIcon = MsFormsFactoryBinary.GetNativePictureStream(request.Properties, "mouseIcon", Type, request.Name);
        var borderColor = MsFormsFactoryBinary.ParseColor(MsFormsFactoryBinary.GetString(request.Properties, "borderColor"), DefaultBorderColor);
        var backColor = MsFormsFactoryBinary.ParseColor(MsFormsFactoryBinary.GetString(request.Properties, "backColor"), DefaultBackColor);
        var variousBits = BuildVariousPropertyBits(request.Properties);
        var borderStyle = MsFormsFactoryBinary.GetInt32(request.Properties, "borderStyle") ?? DefaultBorderStyle;
        var mousePointer = MsFormsFactoryBinary.GetInt32(request.Properties, "mousePointer") ?? 0;
        var pictureSizeMode = MsFormsFactoryBinary.GetInt32(request.Properties, "pictureSizeMode") ?? DefaultPictureSizeMode;
        var specialEffect = MsFormsFactoryBinary.GetInt32(request.Properties, "specialEffect") ?? DefaultSpecialEffect;
        var pictureAlignment = MsFormsFactoryBinary.GetInt32(request.Properties, "pictureAlignment") ?? DefaultPictureAlignment;
        var autoSize = MsFormsFactoryBinary.GetBool(request.Properties, "autoSize") is true;
        var pictureTiling = MsFormsFactoryBinary.GetBool(request.Properties, "pictureTiling") is true;
        var propMask = BuildPropMask(borderColor, backColor, variousBits, borderStyle, mousePointer, pictureSizeMode,
            specialEffect, pictureAlignment, autoSize, pictureTiling, picture.Length > 0, mouseIcon.Length > 0);

        using var dataBlock = new MemoryStream();
        if (Has(propMask, 3)) MsFormsFactoryBinary.WriteUInt32(dataBlock, borderColor);
        if (Has(propMask, 4)) MsFormsFactoryBinary.WriteUInt32(dataBlock, backColor);
        if (Has(propMask, 5)) dataBlock.WriteByte(checked((byte)borderStyle));
        if (Has(propMask, 6)) dataBlock.WriteByte(checked((byte)mousePointer));
        if (Has(propMask, 7)) dataBlock.WriteByte(checked((byte)pictureSizeMode));
        if (Has(propMask, 8)) dataBlock.WriteByte(checked((byte)specialEffect));
        if (Has(propMask, 10)) { MsFormsFactoryBinary.WritePadding(dataBlock, 2); MsFormsFactoryBinary.WriteUInt16(dataBlock, 0xFFFF); }
        if (Has(propMask, 11)) dataBlock.WriteByte(checked((byte)pictureAlignment));
        if (Has(propMask, 13)) { MsFormsFactoryBinary.WritePadding(dataBlock, 4); MsFormsFactoryBinary.WriteUInt32(dataBlock, variousBits); }
        if (Has(propMask, 14)) { MsFormsFactoryBinary.WritePadding(dataBlock, 2); MsFormsFactoryBinary.WriteUInt16(dataBlock, 0xFFFF); }
        MsFormsFactoryBinary.WritePadding(dataBlock, 4);

        using var extra = new MemoryStream();
        MsFormsFactoryBinary.WriteSize(extra, request.Width, request.Height);
        var control = MsFormsFactoryBinary.BuildVersionedControl(0, 2, propMask, dataBlock.ToArray(), extra.ToArray());
        return [.. control, .. picture, .. mouseIcon];
    }

    public IReadOnlyDictionary<string, object?> BuildMetadata(GeneratedControlRequest request, int objectPayloadSize)
    {
        var picture = MsFormsFactoryBinary.GetNativePictureStream(request.Properties, "picture", Type, request.Name);
        var mouseIcon = MsFormsFactoryBinary.GetNativePictureStream(request.Properties, "mouseIcon", Type, request.Name);
        var borderColor = MsFormsFactoryBinary.ParseColor(MsFormsFactoryBinary.GetString(request.Properties, "borderColor"), DefaultBorderColor);
        var backColor = MsFormsFactoryBinary.ParseColor(MsFormsFactoryBinary.GetString(request.Properties, "backColor"), DefaultBackColor);
        var variousBits = BuildVariousPropertyBits(request.Properties);
        var borderStyle = MsFormsFactoryBinary.GetInt32(request.Properties, "borderStyle") ?? DefaultBorderStyle;
        var mousePointer = MsFormsFactoryBinary.GetInt32(request.Properties, "mousePointer") ?? 0;
        var pictureSizeMode = MsFormsFactoryBinary.GetInt32(request.Properties, "pictureSizeMode") ?? DefaultPictureSizeMode;
        var specialEffect = MsFormsFactoryBinary.GetInt32(request.Properties, "specialEffect") ?? DefaultSpecialEffect;
        var pictureAlignment = MsFormsFactoryBinary.GetInt32(request.Properties, "pictureAlignment") ?? DefaultPictureAlignment;
        var autoSize = MsFormsFactoryBinary.GetBool(request.Properties, "autoSize") is true;
        var pictureTiling = MsFormsFactoryBinary.GetBool(request.Properties, "pictureTiling") is true;
        var propMask = BuildPropMask(borderColor, backColor, variousBits, borderStyle, mousePointer, pictureSizeMode,
            specialEffect, pictureAlignment, autoSize, pictureTiling, picture.Length > 0, mouseIcon.Length > 0);
        var metadata = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["parser"] = "msOFormsImage",
            ["sizeSource"] = "imageExtraDataBlock",
            ["propMask"] = $"0x{propMask:X8}",
            ["objectStreamSize"] = objectPayloadSize,
            ["siteBitFlags"] = $"0x{SiteFlags:X8}",
            ["tabStop"] = true,
            ["visible"] = true,
            ["streamed"] = true
        };

        if (Has(propMask, 2)) metadata["autoSize"] = true;
        if (Has(propMask, 3)) metadata["borderColor"] = $"&H{borderColor:X8}&";
        if (Has(propMask, 4)) metadata["backColor"] = $"&H{backColor:X8}&";
        if (Has(propMask, 5)) metadata["borderStyle"] = borderStyle;
        if (Has(propMask, 6)) metadata["mousePointer"] = mousePointer;
        if (Has(propMask, 7)) metadata["pictureSizeMode"] = pictureSizeMode;
        if (Has(propMask, 8)) metadata["specialEffect"] = specialEffect;
        if (Has(propMask, 10)) { metadata["pictureMarker"] = 0xFFFF; metadata["picture"] = MsFormsFactoryBinary.GetString(request.Properties, "picture"); }
        if (Has(propMask, 11)) metadata["pictureAlignment"] = pictureAlignment;
        if (Has(propMask, 12)) metadata["pictureTiling"] = true;
        if (Has(propMask, 13)) metadata["variousPropertyBitsRaw"] = unchecked((int)variousBits);
        if (Has(propMask, 14)) { metadata["mouseIconMarker"] = 0xFFFF; metadata["mouseIcon"] = MsFormsFactoryBinary.GetString(request.Properties, "mouseIcon"); }

        return metadata;
    }

    private static uint BuildPropMask(
        uint borderColor,
        uint backColor,
        uint variousBits,
        int borderStyle,
        int mousePointer,
        int pictureSizeMode,
        int specialEffect,
        int pictureAlignment,
        bool autoSize,
        bool pictureTiling,
        bool hasPicture,
        bool hasMouseIcon)
    {
        uint mask = 1u << 9;
        if (autoSize) mask |= 1u << 2;
        if (borderColor != DefaultBorderColor) mask |= 1u << 3;
        if (backColor != DefaultBackColor) mask |= 1u << 4;
        if (borderStyle != DefaultBorderStyle) mask |= 1u << 5;
        if (mousePointer != 0) mask |= 1u << 6;
        if (pictureSizeMode != DefaultPictureSizeMode) mask |= 1u << 7;
        if (specialEffect != DefaultSpecialEffect) mask |= 1u << 8;
        if (hasPicture) mask |= 1u << 10;
        if (pictureAlignment != DefaultPictureAlignment) mask |= 1u << 11;
        if (pictureTiling) mask |= 1u << 12;
        if (variousBits != DefaultVariousPropertyBits) mask |= 1u << 13;
        if (hasMouseIcon) mask |= 1u << 14;
        return mask;
    }

    private static uint BuildVariousPropertyBits(Dictionary<string, object?> properties)
    {
        var bits = MsFormsFactoryBinary.GetUInt32(properties, "variousPropertyBitsRaw") ?? DefaultVariousPropertyBits;
        SetBit(ref bits, 1, MsFormsFactoryBinary.GetBool(properties, "enabled"));
        SetBit(ref bits, 2, MsFormsFactoryBinary.GetBool(properties, "locked"));
        if (MsFormsFactoryBinary.GetInt32(properties, "imeMode") is int imeMode)
        {
            bits &= ~(0xFu << 15);
            bits |= ((uint)imeMode & 0xFu) << 15;
        }
        return bits;
    }

    private static bool Has(uint value, int bit) => (value & (1u << bit)) != 0;

    private static void SetBit(ref uint bits, int bit, bool? value)
    {
        if (value is null) return;
        var mask = 1u << bit;
        bits = value.Value ? bits | mask : bits & ~mask;
    }
}

internal sealed class TabStripControlSchema : IGeneratedControlSchema
{
    private const uint BasePropMask = 0x00FA_8031;

    public string Type => "TabStrip";
    public uint SiteFlags => 0x0000_0033; // tabStop + visible + streamed + fAutoSize file default.

    public byte[] BuildObjectPayload(GeneratedControlRequest request)
    {
        var captions = GetTabCaptions(request);
        var names = GetTabNames(request, captions);
        var tooltips = GetTabStrings(request, "tabTooltips", captions.Count);
        var tags = GetTabStrings(request, "tabTags", captions.Count);
        var accelerators = GetTabStrings(request, "tabAccelerators", captions.Count);
        var flags = GetTabFlags(request, captions.Count);
        var listIndex = GetListIndex(request, captions.Count);
        var tabStyle = GetTabStyle(request);
        var mouseIcon = MsFormsFactoryBinary.GetNativePictureStream(request.Properties, "mouseIcon", Type, request.Name);
        var propMask = BasePropMask |
                       (tabStyle is not null ? 1u << 9 : 0) |
                       (mouseIcon.Length > 0 ? 1u << 24 : 0);
        var captionBlock = BuildArrayStringBlock(captions);
        var tooltipBlock = BuildArrayStringBlock(tooltips);
        var nameBlock = BuildArrayStringBlock(names);
        var tagBlock = BuildArrayStringBlock(tags);
        var acceleratorBlock = BuildArrayStringBlock(accelerators);

        using var dataBlock = new MemoryStream();
        MsFormsFactoryBinary.WriteInt32(dataBlock, listIndex);
        MsFormsFactoryBinary.WriteUInt32(dataBlock, checked((uint)captionBlock.Length));
        if (tabStyle is not null)
        {
            MsFormsFactoryBinary.WriteInt32(dataBlock, tabStyle.Value);
        }
        MsFormsFactoryBinary.WriteUInt32(dataBlock, checked((uint)tooltipBlock.Length));
        MsFormsFactoryBinary.WriteUInt32(dataBlock, checked((uint)nameBlock.Length));
        MsFormsFactoryBinary.WriteInt32(dataBlock, captions.Count);
        MsFormsFactoryBinary.WriteUInt32(dataBlock, checked((uint)tagBlock.Length));
        MsFormsFactoryBinary.WriteInt32(dataBlock, captions.Count);
        MsFormsFactoryBinary.WriteUInt32(dataBlock, checked((uint)acceleratorBlock.Length));
        if (mouseIcon.Length > 0)
        {
            MsFormsFactoryBinary.WritePadding(dataBlock, 2);
            MsFormsFactoryBinary.WriteUInt16(dataBlock, 0xFFFF);
        }

        using var extra = new MemoryStream();
        MsFormsFactoryBinary.WriteSize(extra, request.Width, request.Height);
        extra.Write(captionBlock);
        extra.Write(tooltipBlock);
        extra.Write(nameBlock);
        extra.Write(tagBlock);
        extra.Write(acceleratorBlock);

        var control = MsFormsFactoryBinary.BuildVersionedControl(0, 2, propMask, dataBlock.ToArray(), extra.ToArray());
        var textPropsMask = TextPropsFactory.WithParagraphAlignIfNeeded(
            TextPropsFactory.StandardMask,
            request.Properties);
        var textProps = TextPropsFactory.Build(request.Properties, textPropsMask);

        using var tabFlags = new MemoryStream();
        foreach (var flag in flags)
        {
            MsFormsFactoryBinary.WriteUInt32(tabFlags, flag);
        }

        return [.. control, .. mouseIcon, .. textProps, .. tabFlags.ToArray()];
    }

    public IReadOnlyDictionary<string, object?> BuildMetadata(GeneratedControlRequest request, int objectPayloadSize)
    {
        var captions = GetTabCaptions(request);
        var names = GetTabNames(request, captions);
        var tooltips = GetTabStrings(request, "tabTooltips", captions.Count);
        var tags = GetTabStrings(request, "tabTags", captions.Count);
        var accelerators = GetTabStrings(request, "tabAccelerators", captions.Count);
        var flags = GetTabFlags(request, captions.Count);
        var listIndex = GetListIndex(request, captions.Count);
        var tabStyle = GetTabStyle(request);
        var mouseIcon = MsFormsFactoryBinary.GetNativePictureStream(request.Properties, "mouseIcon", Type, request.Name);
        var propMask = BasePropMask |
                       (tabStyle is not null ? 1u << 9 : 0) |
                       (mouseIcon.Length > 0 ? 1u << 24 : 0);
        var textPropsMask = TextPropsFactory.WithParagraphAlignIfNeeded(
            TextPropsFactory.StandardMask,
            request.Properties);
        var metadata = TextPropsFactory.BuildMetadata(textPropsMask, request.Properties);
        metadata["parser"] = "msOFormsTabStrip";
        metadata["sizeSource"] = "tabStripExtraDataBlock";
        metadata["propMask"] = $"0x{propMask:X8}";
        metadata["listIndex"] = listIndex;
        if (tabStyle is not null) metadata["tabStyle"] = tabStyle.Value;
        metadata["tabsAllocated"] = captions.Count;
        metadata["tabData"] = captions.Count;
        metadata["tabCaptions"] = captions;
        metadata["tabTooltips"] = tooltips;
        metadata["tabNames"] = names;
        metadata["tabTags"] = tags;
        metadata["tabAccelerators"] = accelerators;
        metadata["tabFlags"] = flags.Select(flag => new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["raw"] = flag,
            ["visible"] = (flag & 0x0000_0001u) != 0,
            ["enabled"] = (flag & 0x0000_0002u) != 0
        }).ToList();
        if (mouseIcon.Length > 0) metadata["mouseIcon"] = MsFormsFactoryBinary.GetString(request.Properties, "mouseIcon");
        metadata["objectStreamSize"] = objectPayloadSize;
        metadata["siteBitFlags"] = $"0x{SiteFlags:X8}";
        metadata["tabStop"] = true;
        metadata["visible"] = true;
        metadata["streamed"] = true;
        return metadata;
    }

    private static int GetListIndex(GeneratedControlRequest request, int tabCount)
    {
        var listIndex = MsFormsFactoryBinary.GetInt32(request.Properties, "listIndex") ??
                        MsFormsFactoryBinary.GetInt32(request.Properties, "value") ?? 0;
        if (listIndex < -1 || listIndex >= tabCount)
        {
            throw new CliException($"Generated TabStrip '{request.Name}' listIndex {listIndex} is outside the available tab range.");
        }

        return listIndex;
    }

    private static int? GetTabStyle(GeneratedControlRequest request)
    {
        var tabStyle = MsFormsFactoryBinary.GetInt32(request.Properties, "tabStyle") ??
                       MsFormsFactoryBinary.GetInt32(request.Properties, "style");
        if (tabStyle is < 0 or > 2)
        {
            throw new CliException($"Generated TabStrip '{request.Name}' style {tabStyle} must be between 0 and 2.");
        }

        return tabStyle;
    }

    private static IReadOnlyList<string> GetTabCaptions(GeneratedControlRequest request)
    {
        var captions = MsFormsFactoryBinary.GetStringList(request.Properties, "tabCaptions");
        if (captions is { Count: > 0 })
        {
            return captions;
        }

        if (request.Caption is not null)
        {
            return [request.Caption!, "Tab2"];
        }

        if (request.Properties.ContainsKey("caption"))
        {
            return [MsFormsFactoryBinary.GetString(request.Properties, "caption") ?? string.Empty, "Tab2"];
        }

        return ["Tab1", "Tab2"];
    }

    private static IReadOnlyList<string> GetTabNames(GeneratedControlRequest request, IReadOnlyList<string> captions)
    {
        var names = MsFormsFactoryBinary.GetStringList(request.Properties, "tabNames");
        if (names is not null && names.Count == captions.Count)
        {
            return names;
        }

        return captions.Select((_, index) => $"Tab{index + 1}").ToArray();
    }

    private static IReadOnlyList<string> GetTabStrings(GeneratedControlRequest request, string propertyName, int count)
    {
        var values = MsFormsFactoryBinary.GetStringList(request.Properties, propertyName);
        if (values is null) return Enumerable.Repeat(string.Empty, count).ToArray();
        if (values.Count != count)
        {
            throw new CliException($"Generated TabStrip '{request.Name}' {propertyName} contains {values.Count} entries; expected {count}.");
        }
        return values;
    }

    private static IReadOnlyList<uint> GetTabFlags(GeneratedControlRequest request, int count)
    {
        var values = MsFormsFactoryBinary.GetUInt32List(request.Properties, "tabFlags");
        if (values is null) return Enumerable.Repeat(0x0000_0003u, count).ToArray();
        if (values.Count != count)
        {
            throw new CliException($"Generated TabStrip '{request.Name}' tabFlags contains {values.Count} entries; expected {count}.");
        }
        return values;
    }

    private static byte[] BuildArrayStringBlock(IReadOnlyList<string> values)
    {
        using var block = new MemoryStream();
        foreach (var value in values)
        {
            var bytes = Encoding.Latin1.GetBytes(value);
            MsFormsFactoryBinary.WriteCount(block, bytes.Length, compressed: bytes.Length > 0);
            block.Write(bytes);
            MsFormsFactoryBinary.WritePadding(block, 4);
        }

        return block.ToArray();
    }
}


