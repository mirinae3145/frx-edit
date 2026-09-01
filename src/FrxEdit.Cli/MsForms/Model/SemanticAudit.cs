using System.Security.Cryptography;
using FrxEdit.Cli.MsForms.Model;

internal sealed class SemanticAuditCollector(string formName)
{
    private readonly List<ParsedSemanticObservation> parsed = [];
    private readonly List<TemplateSemanticDecision> template = [];
    private readonly HashSet<string> parsedKeys = new(StringComparer.Ordinal);

    public string FormName { get; } = formName;

    public void ObserveFrmProperties(IReadOnlyDictionary<string, object?> properties)
    {
        foreach (var (name, value) in properties)
        {
            ObserveParsed(
                "form",
                FormName,
                "UserForm",
                null,
                "frm text",
                name,
                value,
                "UserFormProject.FormProperties");
        }
    }

    public void ObserveFormControl(
        string entityKind,
        string entityName,
        string? controlType,
        string? parent,
        string? storageScope,
        FormControlProperties properties)
    {
        foreach (var (name, value) in properties.Properties)
        {
            ObserveParsed(
                entityKind,
                entityName,
                controlType,
                parent,
                storageScope,
                name,
                value,
                "FormControlParser.FormControlProperties");
        }
    }

    public void ObserveSite(
        StorageEntryDump stream,
        SiteDescriptor site,
        string? streamOwner)
    {
        var isInternal = site.IsInternalSite;
        var entityKind = isInternal ? "internalSite" : "control";
        var entityName = isInternal
            ? streamOwner ?? $"<internal-site:{stream.ParentPath}:{site.SiteIndex}>"
            : site.Name ?? $"<unnamed-site:{stream.ParentPath}:{site.SiteIndex}>";
        var controlType = site.ControlType;
        if (string.IsNullOrWhiteSpace(controlType) && site.ClsidCacheIndex is ushort typeCode &&
            ControlTypeSchema.TryGetMsFormsType((byte)typeCode, out var resolvedType))
        {
            controlType = resolvedType;
        }

        var scope = stream.ParentPath ?? stream.Path ?? stream.Name;
        ObserveParsed(entityKind, entityName, controlType, streamOwner, scope, "type", controlType, "StructuredMsFormsParser.SiteDescriptor");
        if (!isInternal)
        {
            ObserveParsed(entityKind, entityName, controlType, streamOwner, scope, "name", site.Name, "StructuredMsFormsParser.SiteDescriptor");
        }
        ObserveParsed(entityKind, entityName, controlType, streamOwner, scope, "left", site.Left, "StructuredMsFormsParser.SiteDescriptor");
        ObserveParsed(entityKind, entityName, controlType, streamOwner, scope, "top", site.Top, "StructuredMsFormsParser.SiteDescriptor");
        ObserveOptional(entityKind, entityName, controlType, streamOwner, scope, "tag", site.Tag, "StructuredMsFormsParser.SiteDescriptor");
        ObserveOptional(entityKind, entityName, controlType, streamOwner, scope, "siteId", site.Id, "StructuredMsFormsParser.SiteDescriptor");
        ObserveOptional(entityKind, entityName, controlType, streamOwner, scope, "helpContextId", site.HelpContextId, "StructuredMsFormsParser.SiteDescriptor");
        ObserveOptional(entityKind, entityName, controlType, streamOwner, scope, "groupId", site.GroupId, "StructuredMsFormsParser.SiteDescriptor");
        ObserveOptional(entityKind, entityName, controlType, streamOwner, scope, "tabIndex", site.TabIndex, "StructuredMsFormsParser.SiteDescriptor");

        foreach (var (name, value) in site.ExtraProperties)
        {
            ObserveParsed(entityKind, entityName, controlType, streamOwner, scope, name, value, "StructuredMsFormsParser.SiteDescriptor.ExtraProperties");
        }

        if (site.ObjectProperties is not null)
        {
            var objectSource = site.ObjectProperties.Properties.TryGetValue("parser", out var parserName)
                ? $"ObjectStreamParser.{parserName}"
                : "ObjectStreamParser.ObjectStreamProperties";
            foreach (var (name, value) in site.ObjectProperties.Properties)
            {
                ObserveParsed(entityKind, entityName, controlType, streamOwner, scope, name, value, objectSource);
            }

            ObserveOptional(entityKind, entityName, controlType, streamOwner, scope, "rawWidth", site.ObjectProperties.Width, objectSource);
            ObserveOptional(entityKind, entityName, controlType, streamOwner, scope, "rawHeight", site.ObjectProperties.Height, objectSource);
        }
    }

    public void ObserveProperties(
        string entityKind,
        string entityName,
        string? controlType,
        string? parent,
        string? storageScope,
        IReadOnlyDictionary<string, object?> properties,
        string parserSource)
    {
        foreach (var (name, value) in properties)
        {
            ObserveParsed(entityKind, entityName, controlType, parent, storageScope, name, value, parserSource);
        }
    }

    public void RecordTemplateDecision(
        string entityKind,
        string entityName,
        string? controlType,
        string? parent,
        string? storageScope,
        string property,
        bool rawPresent,
        object? rawValue,
        bool emitted,
        string? emittedProperty,
        object? emittedValue,
        string classification,
        string responsibleRule,
        string? notes = null)
    {
        template.Add(new TemplateSemanticDecision(
            entityKind,
            entityName,
            controlType,
            parent,
            storageScope,
            property,
            rawPresent,
            rawValue,
            emitted,
            emittedProperty,
            emittedValue,
            classification,
            responsibleRule,
            notes));
    }

    public SemanticAuditDocument Build(
        string frxFile,
        ParserMode parserMode,
        string templateKind,
        LayoutInspection raw,
        PatchDocument patch)
    {
        var records = new List<SemanticAuditRecord>();
        var matchedParsed = new HashSet<int>();

        foreach (var decision in template)
        {
            var rawState = new SemanticAuditStageState(
                decision.RawPresent,
                decision.RawPresent ? Summarize(decision.RawValue) : null,
                RawLocation(decision));
            var finalJ = ResolveFinalTemplateState(decision, patch);
            var candidateIndexes = Enumerable.Range(0, parsed.Count)
                .Where(index => SameSemanticKey(parsed[index], decision))
                .ToList();
            var matchingIndex = candidateIndexes.FirstOrDefault(index =>
                decision.RawPresent && ValuesEqual(parsed[index].Value, decision.RawValue), -1);
            if (matchingIndex < 0 && candidateIndexes.Count > 0)
            {
                matchingIndex = candidateIndexes[0];
            }

            ParsedSemanticObservation? parsedObservation = null;
            if (matchingIndex >= 0)
            {
                parsedObservation = parsed[matchingIndex];
                matchedParsed.Add(matchingIndex);
            }

            var parsedState = new SemanticAuditStageState(
                parsedObservation is not null,
                parsedObservation is not null ? Summarize(parsedObservation.Value) : null,
                parsedObservation?.ParserSource);
            var classification = Classify(decision, parsedObservation, finalJ);
            records.Add(new SemanticAuditRecord(
                decision.EntityKind,
                decision.EntityName,
                decision.ControlType,
                decision.Parent,
                decision.StorageScope,
                decision.Property,
                ClassifyProperty(decision.Property),
                parsedObservation?.ParserSource,
                parsedState,
                rawState,
                finalJ,
                classification,
                decision.ResponsibleRule,
                decision.Notes));
        }

        for (var index = 0; index < parsed.Count; index++)
        {
            if (matchedParsed.Contains(index))
            {
                continue;
            }

            var observation = parsed[index];
            var rawState = ResolveRawState(observation, raw);
            if (rawState.Present && template.Any(decision => SameSemanticKey(observation, decision)))
            {
                continue;
            }

            var kind = ClassifyProperty(observation.Property);
            var classification = !rawState.Present && IsAbsentDefault(observation)
                ? "default/representation normalization"
                : rawState.Present
                    ? IntentionalExclusion(kind) ?? "R -> J loss"
                    : IntentionalExclusion(kind) ?? "P -> R loss";
            var rule = rawState.Present
                ? "PatchDocumentGenerator produced no decision for this raw property"
                : observation.EntityKind == "internalSite"
                    ? "FormStreamParser.Read internal-site filter"
                    : "parser result was not projected into LayoutInspection";
            records.Add(new SemanticAuditRecord(
                observation.EntityKind,
                observation.EntityName,
                observation.ControlType,
                observation.Parent,
                observation.StorageScope,
                observation.Property,
                kind,
                observation.ParserSource,
                new SemanticAuditStageState(true, Summarize(observation.Value), observation.ParserSource),
                rawState,
                new SemanticAuditStageState(false, null, null),
                classification,
                rule,
                observation.EntityKind == "internalSite"
                    ? "The site is not a user control; Designer semantics carried only by it remain meaningful losses."
                    : null));
        }

        var ordered = records
            .OrderBy(record => record.EntityKind, StringComparer.Ordinal)
            .ThenBy(record => record.EntityName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(record => record.Property, StringComparer.OrdinalIgnoreCase)
            .ThenBy(record => record.ParserSource, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var classifications = ordered
            .GroupBy(record => record.Classification, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var meaningful = ordered.Where(record => record.SemanticKind is not ("diagnostic" or "serialization")).ToList();
        var summary = new SemanticAuditSummary(
            ordered.Count,
            meaningful.Count,
            meaningful.Count(record => record.Classification == "P -> R loss"),
            meaningful.Count(record => record.Classification == "R -> J loss"),
            meaningful.Count(record => record.Classification == "R -> J transformation"),
            ordered.Count(record => record.Classification == "intentional diagnostic-only exclusion"),
            classifications);

        return new SemanticAuditDocument(
            1,
            FormName,
            frxFile,
            parserMode.ToString().ToLowerInvariant(),
            templateKind,
            new SemanticAuditPipeline(
                "P: parser result objects plus UserFormProject .frm metadata",
                "R: LayoutInspection / ControlInfo / FrxFormControl used by --raw-out",
                "J: PatchDocumentGenerator output after optional ExtractImages transformation"),
            summary,
            ordered);
    }

    public static string ClassifyProperty(string name)
    {
        var normalized = name.ToLowerInvariant();
        if (SerializationPropertyNames.Contains(normalized))
        {
            return "serialization";
        }

        if (DesignerPropertyNames.Contains(normalized) ||
            normalized.StartsWith("pagepropertiestransition", StringComparison.Ordinal) ||
            normalized.StartsWith("form", StringComparison.Ordinal) && !IsDiagnosticName(normalized))
        {
            return normalized is "picture" or "mouseicon" or "formpicture" or "formmouseicon"
                ? "binaryAsset"
                : "designer";
        }

        if (StructuralPropertyNames.Contains(normalized))
        {
            return "structural";
        }

        return "diagnostic";
    }

    private void ObserveOptional(
        string entityKind,
        string entityName,
        string? controlType,
        string? parent,
        string? storageScope,
        string property,
        object? value,
        string parserSource)
    {
        if (value is not null)
        {
            ObserveParsed(entityKind, entityName, controlType, parent, storageScope, property, value, parserSource);
        }
    }

    private void ObserveParsed(
        string entityKind,
        string entityName,
        string? controlType,
        string? parent,
        string? storageScope,
        string property,
        object? value,
        string parserSource)
    {
        var key = string.Join('\u001f', entityKind, entityName, property, parserSource, Fingerprint(value));
        if (!parsedKeys.Add(key))
        {
            return;
        }

        parsed.Add(new ParsedSemanticObservation(
            entityKind,
            entityName,
            controlType,
            parent,
            storageScope,
            property,
            value,
            parserSource));
    }

    private static bool SameSemanticKey(ParsedSemanticObservation parsed, TemplateSemanticDecision decision) =>
        parsed.EntityKind.Equals(decision.EntityKind, StringComparison.OrdinalIgnoreCase) &&
        parsed.EntityName.Equals(decision.EntityName, StringComparison.OrdinalIgnoreCase) &&
        parsed.Property.Equals(decision.Property, StringComparison.OrdinalIgnoreCase);

    private static SemanticAuditStageState ResolveRawState(ParsedSemanticObservation observation, LayoutInspection raw)
    {
        if (observation.EntityKind.Equals("form", StringComparison.OrdinalIgnoreCase))
        {
            return TryDictionaryState(raw.FrxFormControl, observation.Property, "frxFormControl");
        }

        if (observation.EntityKind.Equals("internalSite", StringComparison.OrdinalIgnoreCase))
        {
            return new SemanticAuditStageState(false, null, null);
        }

        var control = raw.Controls.FirstOrDefault(candidate => candidate.Name.Equals(observation.EntityName, StringComparison.OrdinalIgnoreCase));
        if (control is null)
        {
            return new SemanticAuditStageState(false, null, null);
        }

        return observation.Property.ToLowerInvariant() switch
        {
            "name" => Present(control.Name, "control.name"),
            "type" => Present(control.Type, "control.type"),
            "parent" => Present(control.Parent, "control.parent"),
            "left" => Present(control.Left, "control.left"),
            "top" => Present(control.Top, "control.top"),
            "rawwidth" => Present(control.RawWidth, "control.rawWidth"),
            "rawheight" => Present(control.RawHeight, "control.rawHeight"),
            "leftpt" => Present(control.LeftPt, "control.leftPt"),
            "toppt" => Present(control.TopPt, "control.topPt"),
            "widthpt" => Present(control.WidthPt, "control.widthPt"),
            "heightpt" => Present(control.HeightPt, "control.heightPt"),
            _ => TryDictionaryState(control.Properties, observation.Property, "control.properties")
        };
    }

    private static SemanticAuditStageState ResolveFinalTemplateState(TemplateSemanticDecision decision, PatchDocument patch)
    {
        if (!decision.Emitted)
        {
            return new SemanticAuditStageState(false, null, null);
        }

        object? value = decision.EmittedValue;
        var location = decision.EmittedProperty;
        if (!string.IsNullOrWhiteSpace(decision.EmittedProperty) &&
            !decision.EmittedProperty.StartsWith("$add.", StringComparison.Ordinal) &&
            patch.Properties is not null &&
            patch.Properties.TryGetValue(decision.EntityName, out var properties) &&
            properties.TryGetValue(decision.EmittedProperty, out var element))
        {
            value = element;
            location = $"properties.{decision.EntityName}.{decision.EmittedProperty}";
        }

        return new SemanticAuditStageState(true, Summarize(value), location);
    }

    private static SemanticAuditStageState TryDictionaryState(
        IReadOnlyDictionary<string, object?>? dictionary,
        string property,
        string location)
    {
        if (dictionary is not null && dictionary.TryGetValue(property, out var value))
        {
            return Present(value, $"{location}.{property}");
        }

        return new SemanticAuditStageState(false, null, null);
    }

    private static SemanticAuditStageState Present(object? value, string location) =>
        new(true, Summarize(value), location);

    private static string Classify(
        TemplateSemanticDecision decision,
        ParsedSemanticObservation? parsedObservation,
        SemanticAuditStageState finalJ)
    {
        var kind = ClassifyProperty(decision.Property);
        if (parsedObservation is not null && decision.RawPresent && !ValuesEqual(parsedObservation.Value, decision.RawValue))
        {
            return IntentionalExclusion(kind) ?? "P -> R loss";
        }

        if (!decision.RawPresent && finalJ.Present)
        {
            return "default/representation normalization";
        }

        if (decision.RawPresent && !finalJ.Present)
        {
            if (decision.Classification == "default/representation normalization")
            {
                return decision.Classification;
            }

            return IntentionalExclusion(kind) ?? "R -> J loss";
        }

        if (decision.RawPresent && finalJ.Present &&
            (!AuditValuesEqual(Summarize(decision.RawValue), finalJ.Value) ||
             !string.Equals(decision.Property, decision.EmittedProperty, StringComparison.OrdinalIgnoreCase)))
        {
            return "R -> J transformation";
        }

        return decision.Classification;
    }

    private static string? IntentionalExclusion(string semanticKind) => semanticKind switch
    {
        "diagnostic" => "intentional diagnostic-only exclusion",
        "serialization" => "intentional serialization-only exclusion",
        _ => null
    };

    private static bool IsAbsentDefault(ParsedSemanticObservation observation) =>
        observation.Property.ToLowerInvariant() is "tabtags" or "tabtooltips" &&
        observation.Value is IEnumerable<string> strings &&
        strings.All(string.IsNullOrEmpty);

    private static string RawLocation(TemplateSemanticDecision decision) => decision.EntityKind == "form"
        ? $"frxFormControl.{decision.Property}"
        : decision.Property.ToLowerInvariant() switch
        {
            "name" => "control.name",
            "type" => "control.type",
            "parent" => "control.parent",
            "leftpt" => "control.leftPt",
            "toppt" => "control.topPt",
            "widthpt" => "control.widthPt",
            "heightpt" => "control.heightPt",
            _ => $"control.properties.{decision.Property}"
        };

    private static bool ValuesEqual(object? left, object? right) =>
        string.Equals(Fingerprint(left), Fingerprint(right), StringComparison.Ordinal);

    private static bool AuditValuesEqual(SemanticAuditValue left, SemanticAuditValue? right) =>
        right is not null &&
        string.Equals(JsonSerializer.Serialize(left), JsonSerializer.Serialize(right), StringComparison.Ordinal);

    private static string Fingerprint(object? value)
    {
        if (value is null)
        {
            return "null";
        }

        if (value is JsonElement element)
        {
            return element.GetRawText();
        }

        if (value is byte[] bytes)
        {
            return $"bytes:{Convert.ToHexString(SHA256.HashData(bytes))}";
        }

        return JsonSerializer.Serialize(value);
    }

    private static SemanticAuditValue Summarize(object? value)
    {
        if (value is null)
        {
            return new SemanticAuditValue("null", null, null, null, null);
        }

        if (value is JsonElement element)
        {
            value = element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number when element.TryGetDecimal(out var number) => number,
                JsonValueKind.Number => element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => element.Clone()
            };
        }

        if (value is null)
        {
            return new SemanticAuditValue("null", null, null, null, null);
        }

        if (value is byte[] bytes)
        {
            return new SemanticAuditValue("bytes", null, bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)), "binary");
        }

        if (value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal)
        {
            var number = Convert.ToDecimal(value, System.Globalization.CultureInfo.InvariantCulture);
            return new SemanticAuditValue("number", number, null, null, null);
        }

        if (value is string text)
        {
            if (text.StartsWith("base64:", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var decoded = Convert.FromBase64String(text[7..]);
                    var hasPictureEnvelope = HasMsFormsPictureEnvelope(decoded);
                    var semanticPayload = hasPictureEnvelope ? decoded[24..] : decoded;
                    return new SemanticAuditValue(
                        "binaryAsset",
                        null,
                        semanticPayload.Length,
                        Convert.ToHexString(SHA256.HashData(semanticPayload)),
                        hasPictureEnvelope
                            ? "base64 asset; semantic hash excludes the 24-byte MSForms picture envelope"
                            : "base64 asset");
                }
                catch (FormatException)
                {
                    return new SemanticAuditValue("string", "<invalid base64>", text.Length, null, "base64 asset");
                }
            }

            if (text.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                return new SemanticAuditValue("string", text, null, null, "file asset reference");
            }

            if (text.Length > 512)
            {
                var textBytes = Encoding.UTF8.GetBytes(text);
                return new SemanticAuditValue("string", null, text.Length, Convert.ToHexString(SHA256.HashData(textBytes)), "large text");
            }

            return new SemanticAuditValue("string", text, text.Length, null, null);
        }

        var type = value.GetType().Name;
        var json = JsonSerializer.Serialize(value);
        if (json.Length > 1024)
        {
            var jsonBytes = Encoding.UTF8.GetBytes(json);
            return new SemanticAuditValue(type, null, json.Length, Convert.ToHexString(SHA256.HashData(jsonBytes)), "large JSON value");
        }

        return new SemanticAuditValue(type, value, null, null, null);
    }

    private static bool HasMsFormsPictureEnvelope(byte[] bytes) =>
        bytes.Length > 24 &&
        bytes.AsSpan(0, 20).SequenceEqual(new byte[]
        {
            0x04, 0x52, 0xE3, 0x0B, 0x91, 0x8F, 0xCE, 0x11,
            0x9D, 0xE3, 0x00, 0xAA, 0x00, 0x4B, 0xB8, 0x51,
            0x6C, 0x74, 0x00, 0x00
        });

    private static bool IsDiagnosticName(string normalized) =>
        normalized.Contains("offset", StringComparison.Ordinal) ||
        normalized.Contains("parser", StringComparison.Ordinal) ||
        normalized.Contains("propmask", StringComparison.Ordinal) ||
        normalized.Contains("marker", StringComparison.Ordinal) ||
        normalized.Contains("bytecount", StringComparison.Ordinal) ||
        normalized.Contains("compressed", StringComparison.Ordinal) ||
        normalized.Contains("validation", StringComparison.Ordinal) ||
        normalized.Contains("warning", StringComparison.Ordinal) ||
        normalized.Contains("error", StringComparison.Ordinal);

    private static readonly HashSet<string> StructuralPropertyNames = new(StringComparer.Ordinal)
    {
        "name", "type", "parent", "left", "top", "rawwidth", "rawheight", "leftpt", "toppt", "widthpt", "heightpt",
        "siteid", "id", "multipageparent", "multipagepageindex", "multipagepageid", "multipagepagecount", "multipagepageids"
    };

    private static readonly HashSet<string> SerializationPropertyNames = new(StringComparer.Ordinal)
    {
        "siteid", "id", "formcontrolscope", "formmajorversion", "formminorversion", "formshapecookie",
        "formbooleanpropertiesraw", "multipagepageid", "multipagepageids"
    };

    private static readonly HashSet<string> DesignerPropertyNames = new(StringComparer.Ordinal)
    {
        "caption", "text", "value", "tag", "controltiptext", "controlsource", "rowsource", "runtimelickey", "helpcontextid", "groupid",
        "accelerator", "textalign", "paragraphalign", "backcolor", "forecolor", "bordercolor", "fontname", "fontsize", "fontweight",
        "fonteffects", "fontitalic", "fontunderline", "fontstrikethrough", "fontcharset", "enabled", "visible", "locked", "tabindex",
        "tabstop", "default", "cancel", "backstyle", "alignment", "wordwrap", "autosize", "autotab", "autowordselect", "hideselection",
        "integralheight", "multiline", "selectionmargin", "enterkeybehavior", "tabkeybehavior", "enterfieldbehavior", "dragbehavior", "imemode",
        "takefocusonclick", "maxlength", "passwordchar", "scrollbars", "specialeffect", "borderstyle", "displaystyle", "listwidth", "boundcolumn",
        "textcolumn", "columncount", "listrows", "matchentry", "liststyle", "showdropbuttonwhen", "dropbuttonstyle", "multiselect", "columnheads",
        "matchrequired", "editable", "mousepointer", "pictureposition", "picture", "mouseicon", "picturesizemode", "picturealignment", "picturetiling",
        "min", "max", "position", "smallchange", "largechange", "orientation", "delay", "proportionalthumb", "logicalwidth", "logicalheight",
        "scrollleft", "scrolltop", "logicalwidthpt", "logicalheightpt", "scrollleftpt", "scrolltoppt", "tabnames", "tabcaptions", "tabtags",
        "tabtooltips", "tabaccelerators", "transitioneffect", "transitionperiod", "startupposition", "showmodal", "drawbuffer", "whatsthisbutton",
        "whatsthishelp", "clientleft", "clienttop", "clientwidth", "clientheight", "keepscrollbarsvisible", "righttoleft"
    };
}

internal sealed record ParsedSemanticObservation(
    string EntityKind,
    string EntityName,
    string? ControlType,
    string? Parent,
    string? StorageScope,
    string Property,
    object? Value,
    string ParserSource);

internal sealed record TemplateSemanticDecision(
    string EntityKind,
    string EntityName,
    string? ControlType,
    string? Parent,
    string? StorageScope,
    string Property,
    bool RawPresent,
    object? RawValue,
    bool Emitted,
    string? EmittedProperty,
    object? EmittedValue,
    string Classification,
    string ResponsibleRule,
    string? Notes);

internal sealed record SemanticAuditDocument(
    int SchemaVersion,
    string FormName,
    string FrxFile,
    string ParserMode,
    string TemplateKind,
    SemanticAuditPipeline Pipeline,
    SemanticAuditSummary Summary,
    IReadOnlyList<SemanticAuditRecord> Records);

internal sealed record SemanticAuditPipeline(string P, string R, string J);

internal sealed record SemanticAuditSummary(
    int RecordCount,
    int MeaningfulRecordCount,
    int MeaningfulPToRLossCount,
    int MeaningfulRToJLossCount,
    int MeaningfulRToJTransformationCount,
    int DiagnosticExclusionCount,
    Dictionary<string, int> Classifications);

internal sealed record SemanticAuditRecord(
    string EntityKind,
    string EntityName,
    string? ControlType,
    string? Parent,
    string? StorageScope,
    string Property,
    string SemanticKind,
    string? ParserSource,
    SemanticAuditStageState P,
    SemanticAuditStageState R,
    SemanticAuditStageState J,
    string Classification,
    string ResponsibleRule,
    string? Notes);

internal sealed record SemanticAuditStageState(bool Present, SemanticAuditValue? Value, string? Location);

internal sealed record SemanticAuditValue(
    string Type,
    object? Value,
    int? Length,
    string? Sha256,
    string? Representation);
