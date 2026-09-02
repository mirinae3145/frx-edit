internal static class MsFormsFactoryBinary
{
    private static ReadOnlySpan<byte> StdFontClsid =>
    [
        0x03, 0x52, 0xE3, 0x0B, 0x91, 0x8F, 0xCE, 0x11,
        0x9D, 0xE3, 0x00, 0xAA, 0x00, 0x4B, 0xB8, 0x51
    ];

    private static ReadOnlySpan<byte> StdPictureClsid =>
    [
        0x04, 0x52, 0xE3, 0x0B, 0x91, 0x8F, 0xCE, 0x11,
        0x9D, 0xE3, 0x00, 0xAA, 0x00, 0x4B, 0xB8, 0x51
    ];

    private static ReadOnlySpan<byte> TextPropsClsid =>
    [
        0x20, 0x09, 0xC2, 0xAF, 0x4E, 0xDA, 0xCE, 0x11,
        0xB9, 0x43, 0x00, 0xAA, 0x00, 0x68, 0x87, 0xB4
    ];

    public static byte[] BuildVersionedControl(byte minor, byte major, uint propMask, byte[] dataBlock, byte[] extraBlock)
    {
        using var output = new MemoryStream();
        output.WriteByte(minor);
        output.WriteByte(major);
        WriteUInt16(output, checked((ushort)(4 + dataBlock.Length + extraBlock.Length)));
        WriteUInt32(output, propMask);
        output.Write(dataBlock);
        output.Write(extraBlock);
        return output.ToArray();
    }

    public static byte[] BuildVersionedMorphControl(ulong propMask, byte[] dataBlock, byte[] extraBlock)
    {
        using var output = new MemoryStream();
        output.WriteByte(0);
        output.WriteByte(2);
        WriteUInt16(output, checked((ushort)(8 + dataBlock.Length + extraBlock.Length)));
        Span<byte> mask = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(mask, propMask);
        output.Write(mask);
        output.Write(dataBlock);
        output.Write(extraBlock);
        return output.ToArray();
    }

    public static byte[] BuildGuidAndStdFont(
        string faceName = "Tahoma",
        ushort weight = 400,
        uint height = 0x0001_4244,
        ushort charSet = 0,
        byte flags = 0)
    {
        var faceNameBytes = Encoding.Latin1.GetBytes(faceName);
        if (faceNameBytes.Length > byte.MaxValue)
        {
            throw new CliException($"StdFont face name is too long: {faceNameBytes.Length} bytes.");
        }

        using var output = new MemoryStream();
        output.Write(StdFontClsid);
        output.WriteByte(1); // StdFont.Version
        WriteUInt16(output, charSet);
        output.WriteByte(flags);
        WriteUInt16(output, weight);
        WriteUInt32(output, height);
        output.WriteByte(checked((byte)faceNameBytes.Length));
        output.Write(faceNameBytes);
        return output.ToArray();
    }

    public static byte[] BuildGuidAndTextProps(Dictionary<string, object?> properties, uint supportedMask)
    {
        using var output = new MemoryStream();
        output.Write(TextPropsClsid);
        output.Write(TextPropsFactory.Build(properties, supportedMask));
        return output.ToArray();
    }

    public static void WriteFmString(Stream stream, string value)
    {
        var bytes = Encoding.Latin1.GetBytes(value);
        stream.Write(bytes);
        WritePadding(stream, 4);
    }

    public static void WriteCount(Stream stream, int count, bool compressed = true)
    {
        var raw = checked((uint)count);
        if (compressed)
        {
            raw |= 0x8000_0000;
        }

        WriteUInt32(stream, raw);
    }

    public static void WriteSize(Stream stream, int width, int height)
    {
        WriteInt32(stream, width);
        WriteInt32(stream, height);
    }

    public static void WriteInt32(Stream stream, int value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
        stream.Write(buffer);
    }

    public static void WriteUInt32(Stream stream, uint value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
        stream.Write(buffer);
    }

    public static void WriteUInt16(Stream stream, int value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(buffer, checked((ushort)value));
        stream.Write(buffer);
    }

    public static void WriteInt16(Stream stream, int value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BinaryPrimitives.WriteInt16LittleEndian(buffer, checked((short)value));
        stream.Write(buffer);
    }

    public static void WritePadding(Stream stream, int alignment)
    {
        while (stream.Length % alignment != 0)
        {
            stream.WriteByte(0);
        }
    }

    public static string? GetString(Dictionary<string, object?> properties, string name) =>
        properties.TryGetValue(name, out var value) ? value?.ToString() : null;

    public static double? GetDouble(Dictionary<string, object?> properties, string name)
    {
        if (!properties.TryGetValue(name, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            double d => d,
            int i => i,
            JsonElement element when element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out var d) => d,
            string text when double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) => d,
            _ => null
        };
    }

    public static int? GetInt32(Dictionary<string, object?> properties, string name)
    {
        if (!properties.TryGetValue(name, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            int i => i,
            uint ui when ui <= int.MaxValue => (int)ui,
            short s => s,
            ushort us => us,
            byte b => b,
            JsonElement element when element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var i) => i,
            string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) => i,
            _ => null
        };
    }

    public static uint? GetUInt32(Dictionary<string, object?> properties, string name)
    {
        if (!properties.TryGetValue(name, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            uint ui => ui,
            int i => unchecked((uint)i),
            long l when l >= int.MinValue && l <= uint.MaxValue => unchecked((uint)l),
            ulong ul when ul <= uint.MaxValue => (uint)ul,
            short s => unchecked((uint)s),
            ushort us => us,
            byte b => b,
            sbyte sb => unchecked((uint)sb),
            JsonElement element when element.ValueKind == JsonValueKind.Number && element.TryGetUInt32(out var ui) => ui,
            JsonElement element when element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var i) => unchecked((uint)i),
            JsonElement element when element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out var l) && l >= int.MinValue && l <= uint.MaxValue => unchecked((uint)l),
            string text when TryParseUInt32(text, out var parsed) => parsed,
            _ => null
        };
    }

    public static byte[] GetNativePictureStream(
        Dictionary<string, object?> properties,
        string propertyName,
        string controlType,
        string controlName)
    {
        var value = GetString(properties, propertyName);
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        const string prefix = "base64:";
        if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new CliException($"Generated {controlType} '{controlName}' {propertyName} must be resolved to a base64 native picture stream.");
        }

        try
        {
            return Convert.FromBase64String(value[prefix.Length..]);
        }
        catch (FormatException ex)
        {
            throw new CliException($"Generated {controlType} '{controlName}' {propertyName} contains invalid base64 data: {ex.Message}");
        }
    }

    public static bool IsNativePictureStream(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 24 ||
            !bytes[..16].SequenceEqual(StdPictureClsid) ||
            BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(16, 4)) != 0x0000_746C)
        {
            return false;
        }

        var declaredLength = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(20, 4));
        return declaredLength == bytes.Length - 24;
    }

    public static ReadOnlySpan<byte> GetPicturePayload(ReadOnlySpan<byte> bytes) =>
        IsNativePictureStream(bytes) ? bytes[24..] : bytes;

    public static bool? GetBool(Dictionary<string, object?> properties, string name)
    {
        if (!properties.TryGetValue(name, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            bool b => b,
            JsonElement element when element.ValueKind is JsonValueKind.True or JsonValueKind.False => element.GetBoolean(),
            string text when bool.TryParse(text, out var b) => b,
            string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) => i != 0,
            int i => i != 0,
            _ => null
        };
    }

    public static IReadOnlyList<string>? GetStringList(Dictionary<string, object?> properties, string name)
    {
        if (!properties.TryGetValue(name, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            string text => [text],
            string[] array => array,
            IReadOnlyList<string> list => list,
            JsonElement { ValueKind: JsonValueKind.Array } element => element.EnumerateArray()
                .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() ?? string.Empty : item.ToString())
                .ToArray(),
            _ => null
        };
    }

    public static IReadOnlyList<uint>? GetUInt32List(Dictionary<string, object?> properties, string name)
    {
        if (!properties.TryGetValue(name, out var value) || value is null)
        {
            return null;
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Array } element)
        {
            var result = new List<uint>();
            foreach (var item in element.EnumerateArray())
            {
                var rawItem = item;
                if (item.ValueKind == JsonValueKind.Object)
                {
                    if (!item.TryGetProperty("raw", out rawItem) && !item.TryGetProperty("rawHex", out rawItem)) return null;
                }
                var wrapper = new Dictionary<string, object?> { [name] = rawItem };
                if (GetUInt32(wrapper, name) is not uint parsed) return null;
                result.Add(parsed);
            }
            return result;
        }

        if (value is IEnumerable<uint> unsignedValues) return unsignedValues.ToArray();
        if (value is IEnumerable<int> signedValues) return signedValues.Select(item => unchecked((uint)item)).ToArray();
        return null;
    }

    public static uint ParseColor(string? text, uint fallback)
    {
        if (FrxEdit.Cli.MsForms.OleColorConverter.TryParse(text ?? string.Empty, out var value))
        {
            return value;
        }

        return fallback;
    }

    private static bool TryParseUInt32(string text, out uint value)
    {
        text = text.Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return uint.TryParse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        }

        if (uint.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var signed))
        {
            value = unchecked((uint)signed);
            return true;
        }

        value = 0;
        return false;
    }
}
