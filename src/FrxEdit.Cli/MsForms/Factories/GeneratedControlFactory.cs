internal static class GeneratedControlFactory
{
    private const int DefaultWidth = 72 * 2540 / 72;
    private const int DefaultHeight = 18 * 2540 / 72;

    private static readonly IGeneratedControlSchema[] Schemas =
    [
        new CommandButtonControlSchema(),
        new LabelControlSchema(),
        new TextBoxControlSchema(),
        new ComboBoxControlSchema(),
        new ListBoxControlSchema(),
        new CheckBoxControlSchema(),
        new OptionButtonControlSchema(),
        new ToggleButtonControlSchema(),
        new ImageControlSchema(),
        new ScrollBarControlSchema(),
        new SpinButtonControlSchema(),
        new TabStripControlSchema()
    ];

    public static bool CanCreate(string type) => TryGetSchema(type, out _);
    public static string SupportedTypes => string.Join(", ", Schemas.Select(schema => schema.Type));

    public static GeneratedControlBytes Create(
        string type,
        string name,
        int siteId,
        int tabIndex,
        int left,
        int top,
        int? rawWidth,
        int? rawHeight,
        string? caption,
        string? value,
        Dictionary<string, object?> properties)
    {
        if (!MsFormsControlSchemaCatalog.TryGet(type, out var catalogEntry) ||
            catalogEntry.FactoryStatus != FactoryStatus.Ready)
        {
            throw new CliException($"Cannot create '{name}': type '{type}' does not have a document-backed factory yet.");
        }

        if (!TryGetSchema(type, out var schema))
        {
            throw new CliException($"Cannot create '{name}': type '{type}' does not have a document-backed factory yet.");
        }

        if (!ControlTypeSchema.TryGetMsFormsTypeCode(schema.Type, out var typeCode))
        {
            throw new CliException($"Cannot create '{name}': unsupported MSForms type '{schema.Type}'.");
        }

        var request = new GeneratedControlRequest(
            schema.Type,
            name,
            siteId,
            tabIndex,
            left,
            top,
            rawWidth ?? DefaultWidth,
            rawHeight ?? DefaultHeight,
            caption,
            value,
            properties);

        var objectPayload = schema.BuildObjectPayload(request);
        var siteFlags = BuildSiteFlags(schema.SiteFlags, properties);
        var sitePayload = FormSiteFactory.BuildOleSiteConcrete(
            name,
            siteId,
            tabIndex,
            typeCode,
            left,
            top,
            objectPayload.Length,
            siteFlags,
            properties);

        var metadata = new Dictionary<string, object?>(
            schema.BuildMetadata(request, objectPayload.Length),
            StringComparer.OrdinalIgnoreCase);
        SynchronizeSiteFlagMetadata(metadata, siteFlags);
        return new GeneratedControlBytes(sitePayload, objectPayload, metadata);
    }

    internal static uint BuildSiteFlags(uint defaults, Dictionary<string, object?>? properties)
    {
        var flags = properties is null
            ? defaults
            : MsFormsFactoryBinary.GetUInt32(properties, "siteBitFlagsRaw") ??
              MsFormsFactoryBinary.GetUInt32(properties, "siteBitFlags") ??
              defaults;
        if (properties is null)
        {
            return flags;
        }
        SetFlag(ref flags, 0, MsFormsFactoryBinary.GetBool(properties, "tabStop"));
        SetFlag(ref flags, 1, MsFormsFactoryBinary.GetBool(properties, "visible"));
        SetFlag(ref flags, 2, MsFormsFactoryBinary.GetBool(properties, "default"));
        SetFlag(ref flags, 3, MsFormsFactoryBinary.GetBool(properties, "cancel"));
        SetFlag(ref flags, 5, MsFormsFactoryBinary.GetBool(properties, "siteAutoSize"));
        SetFlag(ref flags, 8, MsFormsFactoryBinary.GetBool(properties, "preserveHeight"));
        SetFlag(ref flags, 9, MsFormsFactoryBinary.GetBool(properties, "fitToParent"));
        SetFlag(ref flags, 13, MsFormsFactoryBinary.GetBool(properties, "selectChild"));
        const uint structuralSiteFlagMask = (1u << 4) | (1u << 18);
        if (((flags ^ defaults) & structuralSiteFlagMask) != 0)
        {
            throw new CliException(
                "Generated siteBitFlags streamed/promoteControls bits must match the control's object-stream/storage representation.");
        }
        return flags;
    }

    internal static void SynchronizeSiteFlagMetadata(Dictionary<string, object?> metadata, uint flags)
    {
        metadata["siteBitFlags"] = $"0x{flags:X8}";
        metadata["siteBitFlagsRaw"] = unchecked((int)flags);
        metadata["tabStop"] = (flags & (1u << 0)) != 0;
        metadata["visible"] = (flags & (1u << 1)) != 0;
        metadata["default"] = (flags & (1u << 2)) != 0;
        metadata["cancel"] = (flags & (1u << 3)) != 0;
        metadata["streamed"] = (flags & (1u << 4)) != 0;
        metadata["siteAutoSize"] = (flags & (1u << 5)) != 0;
        metadata["preserveHeight"] = (flags & (1u << 8)) != 0;
        metadata["fitToParent"] = (flags & (1u << 9)) != 0;
        metadata["selectChild"] = (flags & (1u << 13)) != 0;
        metadata["promoteControls"] = (flags & (1u << 18)) != 0;
    }

    private static void SetFlag(ref uint flags, int bit, bool? value)
    {
        if (value is null)
        {
            return;
        }

        var mask = 1u << bit;
        flags = value.Value ? flags | mask : flags & ~mask;
    }

    private static bool TryGetSchema(string type, out IGeneratedControlSchema schema)
    {
        schema = Schemas.FirstOrDefault(candidate => candidate.Type.Equals(type, StringComparison.OrdinalIgnoreCase))!;
        return schema is not null;
    }
}

internal sealed record GeneratedControlBytes(byte[] SitePayload, byte[] ObjectPayload, IReadOnlyDictionary<string, object?> Metadata);
