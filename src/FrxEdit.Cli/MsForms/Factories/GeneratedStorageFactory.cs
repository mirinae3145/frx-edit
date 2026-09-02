internal static class GeneratedStorageFactory
{
    public static bool CanCreate(string type) =>
        type.Equals("Frame", StringComparison.OrdinalIgnoreCase) ||
        type.Equals("MultiPage", StringComparison.OrdinalIgnoreCase) ||
        type.Equals("Page", StringComparison.OrdinalIgnoreCase);

    public static GeneratedStorageControlBytes CreateFrame(
        string name,
        int siteId,
        int tabIndex,
        int left,
        int top,
        int width,
        int height,
        string? caption,
        string storagePath,
        Dictionary<string, object?>? properties = null)
    {
        var siteFlags = GeneratedControlFactory.BuildSiteFlags(0x0004_0023u, properties);
        var sitePayload = FormSiteFactory.BuildStorageOleSiteConcrete(
            name,
            siteId,
            tabIndex,
            0x0E,
            left,
            top,
            siteFlags,
            properties);

        var hasCaption = caption is not null ||
                         properties?.ContainsKey("caption") == true ||
                         properties?.ContainsKey("formCaption") == true;
        var frameCaption = caption ?? (properties is null
            ? null
            : MsFormsFactoryBinary.GetString(properties, "formCaption") ??
              MsFormsFactoryBinary.GetString(properties, "caption"));
        var fStream = BuildFrameFormStream(frameCaption, hasCaption, width, height, properties);
        var formPropMask = BinaryPrimitives.ReadUInt32LittleEndian(fStream.AsSpan(4, 4));
        var formBooleanProperties = GetFormBooleanProperties(properties, 0x0000_8004u);
        var formDrawBuffer = properties is null
            ? null
            : MsFormsFactoryBinary.GetUInt32(properties, "formDrawBuffer");

        var metadata = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["parser"] = "msOFormsFormSiteData",
            ["siteParser"] = "msOFormsOleSiteConcrete",
            ["siteBitFlags"] = "0x00040023",
            ["siteAutoSize"] = true,
            ["formControlParser"] = "msOFormsFormControl",
            ["formPropMask"] = $"0x{formPropMask:X8}",
            ["formBooleanProperties"] = $"0x{formBooleanProperties:X8}",
            ["formDrawBuffer"] = formDrawBuffer ?? 32_000u,
            ["sizeSource"] = "formControlDisplayedSize",
            ["displayedWidth"] = width,
            ["displayedHeight"] = height,
            ["logicalWidth"] = 0,
            ["logicalHeight"] = 0,
            ["generatedStoragePath"] = storagePath,
            ["generatedStorageF"] = fStream,
            ["generatedStorageO"] = Array.Empty<byte>(),
            ["generatedStorageCompObjKind"] = "Frame"
        };
        if (hasCaption)
        {
            metadata["formCaption"] = frameCaption ?? string.Empty;
        }
        CopyFormDesignExMetadata(formBooleanProperties, properties, metadata, name);
        CopyContainerFontMetadata(properties, metadata);
        if (properties is not null &&
            (MsFormsFactoryBinary.GetInt32(properties, "formSpecialEffect") ??
             MsFormsFactoryBinary.GetInt32(properties, "specialEffect")) is int specialEffect)
        {
            metadata["formSpecialEffect"] = specialEffect;
        }
        GeneratedControlFactory.SynchronizeSiteFlagMetadata(metadata, siteFlags);

        return new GeneratedStorageControlBytes(sitePayload, metadata);
    }

    private static byte[] BuildFrameFormStream(
        string? caption,
        bool hasCaption,
        int width,
        int height,
        Dictionary<string, object?>? properties)
    {
        var captionBytes = hasCaption ? Encoding.Latin1.GetBytes(caption ?? string.Empty) : Array.Empty<byte>();
        var formBooleanProperties = GetFormBooleanProperties(properties, 0x0000_8004u);
        var formDrawBuffer = properties is null
            ? 32_000u
            : MsFormsFactoryBinary.GetUInt32(properties, "formDrawBuffer") ?? 32_000u;
        var specialEffect = properties is null
            ? null
            : MsFormsFactoryBinary.GetInt32(properties, "formSpecialEffect")
                ?? MsFormsFactoryBinary.GetInt32(properties, "specialEffect");
        uint propMask = 0x0810_0C40u; // BooleanProperties + sizes + font + draw buffer.
        if (specialEffect is not null) propMask |= 1u << 17;
        if (hasCaption) propMask |= 1u << 19;

        using var dataBlock = new MemoryStream();
        MsFormsFactoryBinary.WriteUInt32(dataBlock, formBooleanProperties);
        if (specialEffect is not null)
        {
            dataBlock.WriteByte(checked((byte)specialEffect.Value));
        }
        MsFormsFactoryBinary.WritePadding(dataBlock, 4);
        if (hasCaption)
        {
            MsFormsFactoryBinary.WriteCount(dataBlock, captionBytes.Length, compressed: captionBytes.Length > 0);
        }
        MsFormsFactoryBinary.WriteUInt16(dataBlock, 0xFFFF);
        MsFormsFactoryBinary.WritePadding(dataBlock, 4);
        MsFormsFactoryBinary.WriteUInt32(dataBlock, formDrawBuffer);

        using var extra = new MemoryStream();
        MsFormsFactoryBinary.WriteSize(extra, width, height);
        MsFormsFactoryBinary.WriteSize(extra, 0, 0);
        if (hasCaption)
        {
            extra.Write(captionBytes);
            MsFormsFactoryBinary.WritePadding(extra, 4);
        }

        using var formControl = new MemoryStream();
        formControl.WriteByte(0);
        formControl.WriteByte(4);
        MsFormsFactoryBinary.WriteUInt16(formControl, checked((ushort)(4 + dataBlock.Length + extra.Length)));
        MsFormsFactoryBinary.WriteUInt32(formControl, propMask);
        formControl.Write(dataBlock.ToArray());
        formControl.Write(extra.ToArray());
        formControl.Write(BuildContainerFontStreamData(properties));

        var designExData = ResolveFormDesignExData(formBooleanProperties, properties, "Frame");
        return [.. formControl.ToArray(), .. designExData];
    }

    public static GeneratedMultiPageControlBytes CreateMultiPage(
        string name,
        int multiPageId,
        int tabIndex,
        int left,
        int top,
        int width,
        int height,
        string storagePath,
        IReadOnlyList<GeneratedPageDefinition> pageDefinitions,
        int selectedPageIndex,
        Dictionary<string, object?>? properties = null)
    {
        if (pageDefinitions.Count == 0)
        {
            throw new CliException($"Cannot create MultiPage '{name}': at least one page definition is required.");
        }

        var tabStripId = multiPageId + 1;
        var pageIds = Enumerable.Range(multiPageId + 2, pageDefinitions.Count).ToArray();
        var siteFlags = GeneratedControlFactory.BuildSiteFlags(0x0004_0023u, properties);
        var sitePayload = FormSiteFactory.BuildStorageOleSiteConcrete(name, multiPageId, tabIndex, 0x39, left, top, siteFlags, properties);
        var tabStripPayload = BuildInternalTabStripPayload(
            pageDefinitions.Select(page => page.Name).ToArray(),
            pageDefinitions.Select(page => page.Caption).ToArray(),
            width,
            height,
            selectedPageIndex,
            properties,
            out var tabStripMetadata);
        var pageSites = new List<byte[]>(pageDefinitions.Count);
        var pages = new List<GeneratedPageControlBytes>(pageDefinitions.Count);
        for (var i = 0; i < pageDefinitions.Count; i++)
        {
            var definition = pageDefinitions[i];
            var pageId = pageIds[i];
            var pageName = definition.Name;
            var pageStoragePath = $"{storagePath}/i{FormatStorageId(pageId)}";
            var pageSite = FormSiteFactory.BuildStorageOleSiteConcrete(
                pageName,
                pageId,
                definition.TabIndex,
                0x07,
                definition.Left,
                definition.Top,
                definition.SiteFlags,
                definition.Properties);
            pageSites.Add(pageSite);
            pages.Add(new GeneratedPageControlBytes(
                pageName,
                pageId,
                pageStoragePath,
                BuildPageFormStream(definition.Width, definition.Height, pageId, definition.Properties),
                Array.Empty<byte>(),
                pageSite,
                BuildDefaultPageProperties(),
                definition.SiteFlags));
        }

        var internalTabSite = BuildInternalObjectSite(tabStripId, 0x12, tabStripPayload.Length);

        // The complete page plan is now known, so the MultiPage's mutually dependent
        // internal TabStrip, Page sites, and x metadata can be emitted consistently.
        var fStream = BuildMultiPageFormStream(width, height, multiPageId, [internalTabSite, .. pageSites], properties);
        var xStream = BuildMultiPageXStream(multiPageId, pageIds);
        var metadata = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["parser"] = "msOFormsFormSiteData",
            ["siteParser"] = "msOFormsOleSiteConcrete",
            ["siteBitFlags"] = "0x00040023",
            ["siteAutoSize"] = true,
            ["formControlParser"] = "msOFormsFormControl",
            ["formPropMask"] = "0x0C100C48",
            ["formBooleanProperties"] = $"0x{GetFormBooleanProperties(properties, 0x0000_C004u):X8}",
            ["formDrawBuffer"] = properties is null ? 32_000u : MsFormsFactoryBinary.GetUInt32(properties, "formDrawBuffer") ?? 32_000u,
            ["sizeSource"] = "formControlDisplayedSize",
            ["displayedWidth"] = width,
            ["displayedHeight"] = height,
            ["logicalWidth"] = 0,
            ["logicalHeight"] = 0,
            ["multiPagePageCount"] = pageDefinitions.Count,
            ["multiPageId"] = tabStripId,
            ["multiPageXStreamPath"] = $"{storagePath}/x",
            ["generatedStoragePath"] = storagePath,
            ["generatedStorageF"] = fStream,
            ["generatedStorageO"] = tabStripPayload,
            ["generatedStorageX"] = xStream,
            ["generatedStorageCompObjKind"] = "MultiPage"
        };
        CopyFormDesignExMetadata(
            GetFormBooleanProperties(properties, 0x0000_C004u),
            properties,
            metadata,
            "MultiPage");
        CopyContainerFontMetadata(properties, metadata);
        CopyTabMetadata(tabStripMetadata, metadata);
        GeneratedControlFactory.SynchronizeSiteFlagMetadata(metadata, siteFlags);

        return new GeneratedMultiPageControlBytes(sitePayload, metadata, pages);
    }

    public static GeneratedPageControlBytes CreatePage(
        string name,
        int siteId,
        int tabIndex,
        int width,
        int height,
        string storagePath,
        uint siteFlags,
        Dictionary<string, object?>? properties = null)
    {
        var sitePayload = FormSiteFactory.BuildStorageOleSiteConcrete(
            name,
            siteId,
            tabIndex,
            0x07,
            0,
            0,
            siteFlags,
            properties);

        return new GeneratedPageControlBytes(
            name,
            siteId,
            storagePath,
            BuildPageFormStream(width, height, siteId, properties),
            Array.Empty<byte>(),
            sitePayload,
            BuildDefaultPageProperties(),
            siteFlags);
    }

    private static byte[] BuildMultiPageFormStream(int width, int height, int nextAvailableId, IReadOnlyList<byte[]> sites, Dictionary<string, object?>? properties) =>
        BuildContainerFormStream(width, height, nextAvailableId + 2 + sites.Count, sites, includePageTail: true, includeFont: true, properties);

    private static byte[] BuildPageFormStream(int width, int height, int nextAvailableId, Dictionary<string, object?>? properties) =>
        BuildContainerFormStream(width, height, nextAvailableId + 2, [], includePageTail: false, includeFont: false, properties);

    private static byte[] BuildContainerFormStream(
        int width,
        int height,
        int nextAvailableId,
        IReadOnlyList<byte[]> sites,
        bool includePageTail,
        bool includeFont,
        Dictionary<string, object?>? properties)
    {
        var formBooleanProperties = GetFormBooleanProperties(properties, 0x0000_C004u);
        var formDrawBuffer = properties is null
            ? 32_000u
            : MsFormsFactoryBinary.GetUInt32(properties, "formDrawBuffer") ?? 32_000u;
        using var dataBlock = new MemoryStream();
        MsFormsFactoryBinary.WriteUInt32(dataBlock, checked((uint)nextAvailableId));
        MsFormsFactoryBinary.WriteUInt32(dataBlock, formBooleanProperties);
        if (includeFont)
        {
            MsFormsFactoryBinary.WriteUInt16(dataBlock, 0xFFFF);
            MsFormsFactoryBinary.WritePadding(dataBlock, 4);
        }
        MsFormsFactoryBinary.WriteUInt32(dataBlock, 1);
        MsFormsFactoryBinary.WriteUInt32(dataBlock, formDrawBuffer);

        using var extra = new MemoryStream();
        MsFormsFactoryBinary.WriteSize(extra, width, height);
        MsFormsFactoryBinary.WriteSize(extra, 0, 0);

        using var formControl = new MemoryStream();
        formControl.WriteByte(0);
        formControl.WriteByte(4);
        MsFormsFactoryBinary.WriteUInt16(formControl, checked((ushort)(4 + dataBlock.Length + extra.Length)));
        var propMask = 0x0C00_0C48u | (includeFont ? 1u << 20 : 0u);
        MsFormsFactoryBinary.WriteUInt32(formControl, propMask);
        formControl.Write(dataBlock.ToArray());
        formControl.Write(extra.ToArray());
        if (includeFont)
        {
            formControl.Write(BuildContainerFontStreamData(properties));
        }

        using var payload = new MemoryStream();
        if (sites.Count > 0)
        {
            payload.WriteByte(0);
            payload.WriteByte(checked((byte)(0x80 | sites.Count)));
            payload.WriteByte(1);
            MsFormsFactoryBinary.WritePadding(payload, 4);
        }
        foreach (var site in sites)
        {
            payload.Write(site);
        }

        using var siteData = new MemoryStream();
        MsFormsFactoryBinary.WriteUInt32(siteData, checked((uint)sites.Count));
        MsFormsFactoryBinary.WriteUInt32(siteData, checked((uint)payload.Length));
        siteData.Write(payload.ToArray());
        var designExData = ResolveFormDesignExData(
            formBooleanProperties,
            properties,
            includePageTail ? "MultiPage" : "Page");
        return [.. formControl.ToArray(), .. siteData.ToArray(), .. designExData];
    }

    private static byte[] BuildInternalObjectSite(int siteId, byte typeCode, int objectStreamSize)
    {
        using var dataBlock = new MemoryStream();
        MsFormsFactoryBinary.WriteUInt32(dataBlock, checked((uint)siteId));
        MsFormsFactoryBinary.WriteUInt32(dataBlock, checked((uint)objectStreamSize));
        MsFormsFactoryBinary.WriteUInt16(dataBlock, 0);
        MsFormsFactoryBinary.WriteUInt16(dataBlock, typeCode);

        using var extra = new MemoryStream();
        MsFormsFactoryBinary.WriteInt32(extra, 0);
        MsFormsFactoryBinary.WriteInt32(extra, 0);

        using var output = new MemoryStream();
        MsFormsFactoryBinary.WriteUInt16(output, 0);
        MsFormsFactoryBinary.WriteUInt16(output, checked((ushort)(4 + dataBlock.Length + extra.Length)));
        MsFormsFactoryBinary.WriteUInt32(output, 0x0000_01E4);
        output.Write(dataBlock.ToArray());
        output.Write(extra.ToArray());
        return output.ToArray();
    }

    private static byte[] BuildMultiPageXStream(int multiPageId, IReadOnlyList<int> pageIds)
    {
        using var output = new MemoryStream();
        for (var i = 0; i < pageIds.Count + 1; i++)
        {
            output.WriteByte(0);
            output.WriteByte(2);
            MsFormsFactoryBinary.WriteUInt16(output, 4);
            MsFormsFactoryBinary.WriteUInt32(output, 0);
        }

        output.WriteByte(0);
        output.WriteByte(2);
        MsFormsFactoryBinary.WriteUInt16(output, 12);
        MsFormsFactoryBinary.WriteUInt32(output, 0x0000_0006);
        MsFormsFactoryBinary.WriteInt32(output, pageIds.Count);
        MsFormsFactoryBinary.WriteInt32(output, multiPageId + 1);
        foreach (var pageId in pageIds)
        {
            MsFormsFactoryBinary.WriteUInt32(output, checked((uint)pageId));
        }

        return output.ToArray();
    }

    public static byte[] BuildDefaultPageProperties()
    {
        using var output = new MemoryStream();
        output.WriteByte(0);
        output.WriteByte(2);
        MsFormsFactoryBinary.WriteUInt16(output, 4);
        MsFormsFactoryBinary.WriteUInt32(output, 0);
        return output.ToArray();
    }

    public static byte[] BuildDefaultPageTail() =>
        FormDesignExDataBinary.ResolveForGeneration(
            0x0000_C004u,
            requestedValue: null,
            FormDesignExDefaultKind.Container,
            "Page");

    private static byte[] ResolveFormDesignExData(
        uint formBooleanProperties,
        Dictionary<string, object?>? properties,
        string owner)
    {
        object? requested = null;
        properties?.TryGetValue("formDesignExData", out requested);
        return FormDesignExDataBinary.ResolveForGeneration(
            formBooleanProperties,
            requested,
            owner.Equals("MultiPage", StringComparison.OrdinalIgnoreCase)
                ? FormDesignExDefaultKind.MultiPage
                : FormDesignExDefaultKind.Container,
            owner);
    }

    private static void CopyFormDesignExMetadata(
        uint formBooleanProperties,
        Dictionary<string, object?>? properties,
        Dictionary<string, object?> metadata,
        string owner)
    {
        var bytes = ResolveFormDesignExData(formBooleanProperties, properties, owner);
        if (bytes.Length > 0)
        {
            metadata["formDesignExData"] = FormDesignExDataBinary.ToBase64(bytes);
        }
    }

    private static byte[] BuildInternalTabStripPayload(
        IReadOnlyList<string> pageNames,
        IReadOnlyList<string> pageCaptions,
        int width,
        int height,
        int selectedPageIndex,
        Dictionary<string, object?>? properties,
        out IReadOnlyDictionary<string, object?> metadata)
    {
        var tabProperties = properties is null
            ? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, object?>(properties, StringComparer.OrdinalIgnoreCase);
        tabProperties.TryAdd("tabCaptions", pageCaptions.ToArray());
        tabProperties.TryAdd("tabNames", pageNames.ToArray());
        tabProperties["listIndex"] = selectedPageIndex;
        var request = new GeneratedControlRequest(
            "TabStrip",
            "__internal",
            0,
            0,
            0,
            0,
            width,
            height,
            null,
            null,
            tabProperties);
        var schema = new TabStripControlSchema();
        var payload = schema.BuildObjectPayload(request);
        metadata = schema.BuildMetadata(request, payload.Length);
        return payload;
    }

    private static byte[] BuildContainerFontStreamData(Dictionary<string, object?>? properties)
    {
        var fontProperties = BuildContainerFontProperties(properties);
        return HasContainerFontProperties(fontProperties)
            ? MsFormsFactoryBinary.BuildGuidAndTextProps(fontProperties, TextPropsFactory.StandardMask)
            : MsFormsFactoryBinary.BuildGuidAndStdFont();
    }

    private static Dictionary<string, object?> BuildContainerFontProperties(Dictionary<string, object?>? properties)
    {
        var fontProperties = properties is null
            ? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, object?>(properties, StringComparer.OrdinalIgnoreCase);
        return fontProperties;
    }

    private static bool HasContainerFontProperties(Dictionary<string, object?> properties) =>
        properties.Keys.Any(name => name.Equals("fontName", StringComparison.OrdinalIgnoreCase) ||
                                    name.Equals("fontSize", StringComparison.OrdinalIgnoreCase) ||
                                    name.Equals("fontSizeRaw", StringComparison.OrdinalIgnoreCase) ||
                                    name.Equals("fontEffects", StringComparison.OrdinalIgnoreCase) ||
                                    name.Equals("fontEffectsHex", StringComparison.OrdinalIgnoreCase) ||
                                    name.Equals("fontWeight", StringComparison.OrdinalIgnoreCase) ||
                                    name.Equals("fontBold", StringComparison.OrdinalIgnoreCase) ||
                                    name.Equals("fontItalic", StringComparison.OrdinalIgnoreCase) ||
                                    name.Equals("fontUnderline", StringComparison.OrdinalIgnoreCase) ||
                                    name.Equals("fontStrikethrough", StringComparison.OrdinalIgnoreCase) ||
                                    name.Equals("fontCharSet", StringComparison.OrdinalIgnoreCase) ||
                                    name.Equals("fontPitchAndFamily", StringComparison.OrdinalIgnoreCase));

    private static void CopyContainerFontMetadata(
        Dictionary<string, object?>? properties,
        Dictionary<string, object?> metadata)
    {
        var fontProperties = BuildContainerFontProperties(properties);
        var hasTextProps = HasContainerFontProperties(fontProperties);
        if (hasTextProps)
        {
            foreach (var (name, value) in TextPropsFactory.BuildMetadata(TextPropsFactory.StandardMask, fontProperties))
            {
                metadata[name] = value;
            }
        }
        metadata["formFontKind"] = hasTextProps ? "TextProps" : "StdFont";
        metadata["formFontStreamByteCount"] = BuildContainerFontStreamData(properties).Length;
    }

    private static void CopyTabMetadata(
        IReadOnlyDictionary<string, object?> tabStripMetadata,
        Dictionary<string, object?> metadata)
    {
        foreach (var name in new[] { "tabsAllocated", "tabData", "tabCaptions", "tabTooltips", "tabNames", "tabTags", "tabAccelerators", "tabFlags", "tabStyle" })
        {
            if (tabStripMetadata.TryGetValue(name, out var value))
            {
                metadata[name] = value;
            }
        }
        if (tabStripMetadata.TryGetValue("tabStyle", out var tabStyle))
        {
            metadata["style"] = tabStyle;
        }
    }

    private static string FormatStorageId(int id) => id is >= 0 and < 10 ? $"0{id}" : id.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static uint GetFormBooleanProperties(Dictionary<string, object?>? properties, uint defaultValue)
    {
        if (properties is null)
        {
            return defaultValue;
        }

        var bits = MsFormsFactoryBinary.GetUInt32(properties, "formBooleanProperties") ?? defaultValue;
        SetBit(ref bits, 0, MsFormsFactoryBinary.GetBool(properties, "enabled"));
        SetBit(ref bits, 4, MsFormsFactoryBinary.GetBool(properties, "pictureTiling"));
        SetBit(ref bits, 21, MsFormsFactoryBinary.GetBool(properties, "keepScrollBarsVisible"));
        SetBit(ref bits, 22, MsFormsFactoryBinary.GetBool(properties, "rightToLeft"));
        return bits;
    }

    private static void SetBit(ref uint bits, int bit, bool? value)
    {
        if (value is null)
        {
            return;
        }

        var mask = 1u << bit;
        bits = value.Value ? bits | mask : bits & ~mask;
    }
}

internal sealed record GeneratedStorageControlBytes(
    byte[] SitePayload,
    IReadOnlyDictionary<string, object?> Metadata);

internal sealed record GeneratedMultiPageControlBytes(
    byte[] SitePayload,
    IReadOnlyDictionary<string, object?> Metadata,
    IReadOnlyList<GeneratedPageControlBytes> Pages);

internal sealed record GeneratedPageDefinition(
    string Name,
    string Caption,
    int TabIndex,
    int Left,
    int Top,
    int Width,
    int Height,
    uint SiteFlags,
    Dictionary<string, object?>? Properties);

internal sealed record GeneratedPageControlBytes(
    string Name,
    int SiteId,
    string StoragePath,
    byte[] FStream,
    byte[] OStream,
    byte[] SitePayload,
    byte[] PageProperties,
    uint SiteFlags);
