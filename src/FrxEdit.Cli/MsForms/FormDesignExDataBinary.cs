internal sealed record FormDesignExDataInfo(
    int Start,
    int End,
    byte MinorVersion,
    byte MajorVersion,
    ushort Cb,
    uint PropMask,
    byte[] Bytes);

internal enum FormDesignExDefaultKind
{
    UserForm,
    Container,
    MultiPage
}

internal static class FormDesignExDataBinary
{
    public const uint PersistedFlag = 0x0000_4000u;

    private static readonly byte[] DefaultUserFormBytes =
        Convert.FromHexString("00020C0019000000F37F0100FF010000");

    private static readonly byte[] DefaultContainerBytes =
        Convert.FromHexString("00020C0019000000F3FF0100FF010000");

    private static readonly byte[] DefaultMultiPageBytes =
        Convert.FromHexString("00020C0019000000F08F0000FF010000");

    public static byte[] ResolveForGeneration(
        uint formBooleanProperties,
        object? requestedValue,
        FormDesignExDefaultKind defaultKind,
        string owner)
    {
        var persisted = (formBooleanProperties & PersistedFlag) != 0;
        if (!persisted)
        {
            if (requestedValue is not null)
            {
                throw new CliException(
                    $"Property 'formDesignExData' for '{owner}' requires FORM_FLAG_DESINKPERSISTED (0x00004000) in formBooleanProperties.");
            }

            return [];
        }

        if (requestedValue is not null)
        {
            return DecodeAndValidate(requestedValue, owner);
        }

        return defaultKind switch
        {
            FormDesignExDefaultKind.UserForm => DefaultUserFormBytes.ToArray(),
            FormDesignExDefaultKind.MultiPage => DefaultMultiPageBytes.ToArray(),
            _ => DefaultContainerBytes.ToArray()
        };
    }

    public static byte[] DecodeAndValidate(object value, string owner)
    {
        var text = value switch
        {
            string stringValue => stringValue,
            JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
            _ => null
        };

        if (string.IsNullOrWhiteSpace(text) ||
            !text.StartsWith("base64:", StringComparison.OrdinalIgnoreCase))
        {
            throw new CliException(
                $"Property 'formDesignExData' for '{owner}' must be a base64: value.");
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(text["base64:".Length..]);
        }
        catch (FormatException ex)
        {
            throw new CliException(
                $"Property 'formDesignExData' for '{owner}' contains invalid base64 data: {ex.Message}");
        }

        if (!TryRead(bytes, 0, out var parsed) || parsed.End != bytes.Length)
        {
            throw new CliException(
                $"Property 'formDesignExData' for '{owner}' must contain exactly one valid FormDesignExData structure.");
        }

        return bytes;
    }

    public static bool TryRead(byte[] data, int offset, out FormDesignExDataInfo info)
    {
        info = default!;
        if (offset < 0 || offset + 8 > data.Length)
        {
            return false;
        }

        var minorVersion = data[offset];
        var majorVersion = data[offset + 1];
        var cb = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset + 2, 2));
        if (minorVersion != 0 || majorVersion != 2 || cb < 4)
        {
            return false;
        }

        var end = offset + 4 + cb;
        if (end > data.Length)
        {
            return false;
        }

        var propMask = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset + 4, 4));
        info = new FormDesignExDataInfo(
            offset,
            end,
            minorVersion,
            majorVersion,
            cb,
            propMask,
            data.AsSpan(offset, end - offset).ToArray());
        return true;
    }

    public static string ToBase64(byte[] bytes) =>
        $"base64:{Convert.ToBase64String(bytes)}";
}
