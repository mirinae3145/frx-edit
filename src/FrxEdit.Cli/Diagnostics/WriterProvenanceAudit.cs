using System.Security.Cryptography;
using FrxEdit.Cli.MsForms.Model;

internal sealed class WriterProvenanceAuditCollector(
    string command,
    string formName,
    string patchPath,
    string? patchDirectory)
{
    private readonly Dictionary<string, WriterAuditStage> stages = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<WriterAuditEvent> writerEvents = [];
    private readonly Dictionary<string, byte[]> semanticAssets = new(StringComparer.Ordinal);
    private readonly List<WriterAuditStorageSnapshot> storageSnapshots = [];
    private WriterAuditBinary? binary;
    private WriterAuditFailure? failure;

    public void RecordFailure(string boundary, string codePath, Exception exception)
    {
        failure = new WriterAuditFailure(boundary, codePath, exception.GetType().Name, exception.Message);
    }

    public void CapturePatch(string stage, PatchDocument patch)
    {
        var items = new List<WriterAuditItem>();
        var addByName = (patch.Add ?? [])
            .Where(add => !string.IsNullOrWhiteSpace(add.Name))
            .ToDictionary(add => add.Name!, StringComparer.OrdinalIgnoreCase);

        foreach (var (entityName, properties) in patch.Properties ??
                     new Dictionary<string, Dictionary<string, JsonElement>>(StringComparer.OrdinalIgnoreCase))
        {
            addByName.TryGetValue(entityName, out var add);
            var entityKind = IsFormName(entityName) ? "form" : "control";
            foreach (var (property, value) in properties)
            {
                AddItem(items, stage, entityKind, entityName, add?.Type, add?.Parent, null, property, value,
                    $"properties.{entityName}.{property}", "PatchDocument.Properties");
            }
        }

        foreach (var (entityName, layout) in patch.Layout ??
                     new Dictionary<string, LayoutPatch>(StringComparer.OrdinalIgnoreCase))
        {
            AddOptional(items, stage, entityName, addByName, "left", layout.Left, $"layout.{entityName}.left");
            AddOptional(items, stage, entityName, addByName, "top", layout.Top, $"layout.{entityName}.top");
            AddOptional(items, stage, entityName, addByName, "rawWidth", layout.RawWidth ?? layout.Width, $"layout.{entityName}.rawWidth");
            AddOptional(items, stage, entityName, addByName, "rawHeight", layout.RawHeight ?? layout.Height, $"layout.{entityName}.rawHeight");
            AddOptional(items, stage, entityName, addByName, "leftPt", layout.LeftPt, $"layout.{entityName}.leftPt");
            AddOptional(items, stage, entityName, addByName, "topPt", layout.TopPt, $"layout.{entityName}.topPt");
            AddOptional(items, stage, entityName, addByName, "widthPt", layout.WidthPt, $"layout.{entityName}.widthPt");
            AddOptional(items, stage, entityName, addByName, "heightPt", layout.HeightPt, $"layout.{entityName}.heightPt");
        }

        var addOrderByParent = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < (patch.Add?.Count ?? 0); index++)
        {
            var add = patch.Add![index];
            if (string.IsNullOrWhiteSpace(add.Name)) continue;
            var name = add.Name!;
            var parentKey = string.IsNullOrWhiteSpace(add.Parent) ? string.Empty : add.Parent.Trim();
            var siblingOrder = addOrderByParent.GetValueOrDefault(parentKey);
            addOrderByParent[parentKey] = siblingOrder + 1;
            AddItem(items, stage, "control", name, add.Type, add.Parent, null, "$exists", true,
                $"add[{index}]", "PatchDocument.Add");
            AddOptional(items, stage, name, addByName, "type", add.Type, $"add[{index}].type");
            AddOptional(items, stage, name, addByName, "parent", add.Parent, $"add[{index}].parent");
            AddOptional(items, stage, name, addByName, "order", siblingOrder, $"add[{index}]");
            AddOptional(items, stage, name, addByName, "left", add.Left, $"add[{index}].left");
            AddOptional(items, stage, name, addByName, "top", add.Top, $"add[{index}].top");
            AddOptional(items, stage, name, addByName, "rawWidth", add.RawWidth ?? add.Width, $"add[{index}].rawWidth");
            AddOptional(items, stage, name, addByName, "rawHeight", add.RawHeight ?? add.Height, $"add[{index}].rawHeight");
            AddOptional(items, stage, name, addByName, "leftPt", add.LeftPt, $"add[{index}].leftPt");
            AddOptional(items, stage, name, addByName, "topPt", add.TopPt, $"add[{index}].topPt");
            AddOptional(items, stage, name, addByName, "widthPt", add.WidthPt, $"add[{index}].widthPt");
            AddOptional(items, stage, name, addByName, "heightPt", add.HeightPt, $"add[{index}].heightPt");
            AddOptional(items, stage, name, addByName, "caption", add.Caption, $"add[{index}].caption");
            AddOptional(items, stage, name, addByName, "value", add.Value, $"add[{index}].value");
            foreach (var (property, value) in add.Properties ??
                         new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase))
            {
                AddItem(items, stage, "control", name, add.Type, add.Parent, null, property, value,
                    $"add[{index}].properties.{property}", "PatchDocument.Add.Properties");
            }
        }

        foreach (var (name, parent) in patch.Move ?? new Dictionary<string, string?>())
        {
            AddItem(items, stage, "control", name, null, parent, null, "parent", parent,
                $"move.{name}", "PatchDocument.Move");
        }

        stages[stage] = new WriterAuditStage(stage, StageDescription(stage), Deduplicate(items));
    }

    public void CaptureLayout(string stage, LayoutInspection layout)
    {
        var items = new List<WriterAuditItem>();
        if (layout.FrxFormControl is not null)
        {
            foreach (var (property, value) in layout.FrxFormControl)
            {
                AddItem(items, stage, "form", formName, "UserForm", null,
                    GetString(layout.FrxFormControl, "storagePath"), property, value,
                    $"frxFormControl.{property}", "LayoutInspection.FrxFormControl");
            }
        }

        var controlOrderByParent = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < layout.Controls.Count; index++)
        {
            var control = layout.Controls[index];
            var parentKey = string.IsNullOrWhiteSpace(control.Parent) ? string.Empty : control.Parent.Trim();
            var siblingOrder = controlOrderByParent.GetValueOrDefault(parentKey);
            controlOrderByParent[parentKey] = siblingOrder + 1;
            var scope = GetString(control.Properties, "generatedStoragePath") ??
                        GetString(control.Properties, "storagePath");
            AddItem(items, stage, "control", control.Name, control.Type, control.Parent, scope, "$exists", true,
                $"controls[{index}]", "LayoutInspection.Controls");
            AddItem(items, stage, "control", control.Name, control.Type, control.Parent, scope, "type", control.Type,
                $"controls[{index}].type", "LayoutInspection.Controls");
            AddItem(items, stage, "control", control.Name, control.Type, control.Parent, scope, "parent", control.Parent,
                $"controls[{index}].parent", "LayoutInspection.Controls");
            AddItem(items, stage, "control", control.Name, control.Type, control.Parent, scope, "order", siblingOrder,
                $"controls[{index}]", "LayoutInspection.Controls");
            AddOptional(items, stage, control, scope, "left", control.Left, $"controls[{index}].left");
            AddOptional(items, stage, control, scope, "top", control.Top, $"controls[{index}].top");
            AddOptional(items, stage, control, scope, "rawWidth", control.RawWidth, $"controls[{index}].rawWidth");
            AddOptional(items, stage, control, scope, "rawHeight", control.RawHeight, $"controls[{index}].rawHeight");
            AddOptional(items, stage, control, scope, "leftPt", control.LeftPt, $"controls[{index}].leftPt");
            AddOptional(items, stage, control, scope, "topPt", control.TopPt, $"controls[{index}].topPt");
            AddOptional(items, stage, control, scope, "widthPt", control.WidthPt, $"controls[{index}].widthPt");
            AddOptional(items, stage, control, scope, "heightPt", control.HeightPt, $"controls[{index}].heightPt");
            foreach (var (property, value) in control.Properties ??
                         new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase))
            {
                AddItem(items, stage, "control", control.Name, control.Type, control.Parent, scope, property, value,
                    $"controls[{index}].properties.{property}", "LayoutInspection.ControlInfo.Properties");
            }
        }

        stages[stage] = new WriterAuditStage(stage, StageDescription(stage), Deduplicate(items));
    }

    public void RecordGeneratedControl(
        string name,
        string type,
        string? parent,
        string? storagePath,
        byte[] sitePayload,
        byte[] objectPayload,
        IReadOnlyDictionary<string, object?> metadata,
        IReadOnlyDictionary<string, object?> requestedProperties)
    {
        var decoded = ObjectStreamParser.Read(
            StorageEntryDump.CreateSegment(objectPayload, Enumerable.Range(0, objectPayload.Length).ToArray()),
            type);
        writerEvents.Add(new WriterAuditEvent(
            "generated-control-payload",
            name,
            type,
            parent,
            storagePath,
            "GeneratedControlFactory.Create -> IGeneratedControlSchema.BuildObjectPayload / FormSiteFactory.BuildOleSiteConcrete",
            new Dictionary<string, object?>
            {
                ["requestedProperties"] = SummarizeDictionary(requestedProperties),
                ["generatedMetadata"] = SummarizeDictionary(metadata),
                ["decodedObjectProperties"] = SummarizeDictionary(decoded.Properties),
                ["sitePayload"] = DescribeBytes(sitePayload, "FormSiteData site payload"),
                ["objectPayload"] = DescribeBytes(objectPayload, "object stream payload")
            }));
    }

    public void RecordGeneratedStorage(
        string name,
        string type,
        string? parent,
        string storagePath,
        byte[] sitePayload,
        IReadOnlyDictionary<string, byte[]> streams,
        string codePath)
    {
        writerEvents.Add(new WriterAuditEvent(
            "generated-storage-payload",
            name,
            type,
            parent,
            storagePath,
            codePath,
            new Dictionary<string, object?>
            {
                ["sitePayload"] = DescribeBytes(sitePayload, "FormSiteData site payload"),
                ["streams"] = streams.ToDictionary(pair => pair.Key, pair => (object?)DescribeBytes(pair.Value, $"{pair.Key} stream"), StringComparer.OrdinalIgnoreCase)
            }));
    }

    public void RecordStorageSnapshot(string substage, CompoundStorageDump dump)
    {
        storageSnapshots.Add(new WriterAuditStorageSnapshot(
            substage,
            dump.Streams.Select(stream => new WriterAuditStream(
                stream.Path,
                stream.ParentPath,
                stream.Name,
                stream.Kind,
                stream.Data.Length,
                stream.Kind.Equals("Stream", StringComparison.OrdinalIgnoreCase)
                    ? Hash(stream.Data)
                    : null)).ToList()));
    }

    public void RecordStreamRewrite(string kind, string path, byte[] before, byte[] after, string codePath)
    {
        writerEvents.Add(new WriterAuditEvent(
            "stream-rewrite",
            null,
            null,
            null,
            path,
            codePath,
            new Dictionary<string, object?>
            {
                ["streamKind"] = kind,
                ["before"] = DescribeBytes(before, "stream"),
                ["after"] = DescribeBytes(after, "stream"),
                ["changed"] = !before.AsSpan().SequenceEqual(after)
            }));
    }

    public void CaptureBinary(byte[] frxBytes, int oleOffset)
    {
        var storage = CompoundStorageInspector.Inspect(frxBytes, oleOffset);
        var assets = semanticAssets.Select(pair =>
        {
            var matches = storage.Streams
                .Where(stream => stream.Kind.Equals("Stream", StringComparison.OrdinalIgnoreCase))
                .Where(stream => Contains(stream.Data, pair.Value))
                .Select(stream => stream.Path)
                .ToList();
            return new WriterAuditAssetEvidence(pair.Key, pair.Value.Length, Hash(pair.Value), matches.Count > 0, matches);
        }).ToList();

        binary = new WriterAuditBinary(
            frxBytes.Length,
            oleOffset,
            Hash(frxBytes),
            frxBytes.Length - oleOffset,
            Hash(frxBytes.AsSpan(oleOffset).ToArray()),
            storage.Streams.Count,
            assets);
        RecordStorageSnapshot("B: rebuilt physical CFB", storage);
    }

    public WriterProvenanceAuditDocument Build()
    {
        var j = StageMap("J");
        var n = StageMap("N");
        var t = StageMap("T");
        var c = StageMap("C");
        var keys = j.Keys.Concat(n.Keys).Concat(t.Keys).Concat(c.Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();
        var provenance = keys.Select(key => BuildRecord(key, j, n, t, c)).ToList();
        var summary = provenance
            .GroupBy(record => record.Classification, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        return new WriterProvenanceAuditDocument(
            1,
            command,
            formName,
            Path.GetFileName(patchPath),
            new WriterAuditPipeline(
                "J: deserialized exported template PatchDocument before Normalize",
                "N: PatchDocument after Normalize",
                "T: target LayoutInspection after RebuildPatchApplier.ApplyObjectPropertyPatch",
                "B: generated FRX plus logical/physical CFB and Writer substage evidence",
                "C: LayoutInspection after strict re-read of the generated FRX"),
            summary,
            stages.Values.OrderBy(stage => stage.Stage, StringComparer.Ordinal).ToList(),
            writerEvents,
            storageSnapshots,
            binary,
            failure,
            provenance);
    }

    private WriterAuditProvenanceRecord BuildRecord(
        string key,
        IReadOnlyDictionary<string, WriterAuditItem> j,
        IReadOnlyDictionary<string, WriterAuditItem> n,
        IReadOnlyDictionary<string, WriterAuditItem> t,
        IReadOnlyDictionary<string, WriterAuditItem> c)
    {
        j.TryGetValue(key, out var jItem);
        n.TryGetValue(key, out var nItem);
        t.TryGetValue(key, out var tItem);
        c.TryGetValue(key, out var cItem);
        var identity = jItem ?? nItem ?? tItem ?? cItem!;
        var classification = "preserved";
        var responsibleRule = "values match across every stage where the semantic item is represented";

        if (failure is not null && tItem is null && failure.Boundary.Equals("N -> T", StringComparison.Ordinal))
        {
            classification = "unresolved";
            responsibleRule = $"pipeline aborted at {failure.Boundary}: {failure.Message}";
            return new WriterAuditProvenanceRecord(
                key,
                identity.EntityKind,
                identity.EntityName,
                identity.ControlType,
                identity.Parent,
                identity.StorageScope,
                identity.Property,
                identity.SemanticKind,
                State(jItem),
                State(nItem),
                State(tItem),
                BinaryState(key, tItem),
                State(cItem),
                classification,
                responsibleRule);
        }

        if (jItem is not null && nItem is null)
        {
            classification = "J -> N loss";
            responsibleRule = "PatchDocument.Normalize removed the item without an equivalent normalized representation";
        }
        else if (jItem is not null && nItem is not null &&
                 (!ValuesEqual(jItem.Value, nItem.Value) || !string.Equals(jItem.Location, nItem.Location, StringComparison.Ordinal)))
        {
            classification = ValuesEqual(jItem.Value, nItem.Value)
                ? "J -> N transformation"
                : "J -> N loss";
            responsibleRule = "PatchDocument.Normalize representation movement or value conversion";
        }
        else if (nItem is not null && tItem is null && IsMeaningful(nItem.SemanticKind))
        {
            classification = "N -> T loss";
            responsibleRule = "RebuildPatchApplier.ApplyObjectPropertyPatch did not project the normalized item into LayoutInspection";
        }
        else if (nItem is null && tItem is not null && IsMeaningful(tItem.SemanticKind))
        {
            classification = "N -> T generated/default mutation";
            responsibleRule = "RebuildPatchApplier or a generated-control/storage factory introduced the item";
        }
        else if (nItem is not null && tItem is not null && !ValuesEqual(nItem.Value, tItem.Value) && IsMeaningful(nItem.SemanticKind))
        {
            classification = IsRepresentationNormalization(identity.Property)
                ? "representation/default normalization"
                : "N -> T generated/default mutation";
            responsibleRule = "RebuildPatchApplier property conversion or generated factory default";
        }
        else if (tItem is not null && cItem is null && IsMeaningful(tItem.SemanticKind))
        {
            if (IsOmittedDefault(tItem))
            {
                classification = "representation/default normalization";
                responsibleRule = "the Writer omitted an MSForms default value and the Reader reports absence rather than materializing that default";
            }
            else
            {
                classification = ClassifyTargetToObservable(key, tItem, out responsibleRule);
            }
        }
        else if (tItem is not null && cItem is not null && !ValuesEqual(tItem.Value, cItem.Value) && IsMeaningful(tItem.SemanticKind))
        {
            classification = IsRepresentationNormalization(identity.Property)
                ? "representation/default normalization"
                : ClassifyTargetToObservable(key, tItem, out responsibleRule);
        }

        return new WriterAuditProvenanceRecord(
            key,
            identity.EntityKind,
            identity.EntityName,
            identity.ControlType,
            identity.Parent,
            identity.StorageScope,
            identity.Property,
            identity.SemanticKind,
            State(jItem),
            State(nItem),
            State(tItem),
            BinaryState(key, tItem),
            State(cItem),
            classification,
            responsibleRule);
    }

    private string ClassifyTargetToObservable(string key, WriterAuditItem target, out string responsibleRule)
    {
        if (target.SemanticKind == "binaryAsset" && binary is not null)
        {
            var evidence = binary.Assets.FirstOrDefault(asset => asset.SemanticKey.Equals(key, StringComparison.Ordinal));
            if (evidence is not null && evidence.FoundInReachableStream)
            {
                responsibleRule = "asset payload is physically present in a reachable B stream but absent or different in C";
                return "B -> C Reader/observation limitation";
            }

            responsibleRule = "requested asset hash is absent from every reachable B stream; generated schema/stream serialization is the last emitting stage";
            return "T -> B serialization loss";
        }

        if (target.Property == "$exists" || target.SemanticKind == "structural")
        {
            responsibleRule = "target control/container relationship is absent from the effective re-read graph; inspect generated-storage and f/o/x stream events";
            return "T -> B structural serialization defect";
        }

        responsibleRule = "T and C differ, but raw B evidence does not independently decode this scalar property";
        return "unresolved";
    }

    private WriterAuditStageState BinaryState(string key, WriterAuditItem? target)
    {
        if (target?.SemanticKind == "binaryAsset" && binary is not null)
        {
            var evidence = binary.Assets.FirstOrDefault(asset => asset.SemanticKey.Equals(key, StringComparison.Ordinal));
            if (evidence is not null)
            {
                return new WriterAuditStageState(
                    evidence.FoundInReachableStream,
                    target.Value,
                    evidence.StreamPaths.Count == 0 ? "not found in any B stream" : string.Join("; ", evidence.StreamPaths),
                    true);
            }
        }

        return new WriterAuditStageState(null, null, "not independently decoded from B; see Writer events and storage snapshots", false);
    }

    private Dictionary<string, WriterAuditItem> StageMap(string stage) =>
        stages.TryGetValue(stage, out var value)
            ? value.Items.ToDictionary(item => item.SemanticKey, StringComparer.Ordinal)
            : new Dictionary<string, WriterAuditItem>(StringComparer.Ordinal);

    private void AddOptional(
        List<WriterAuditItem> items,
        string stage,
        string entityName,
        IReadOnlyDictionary<string, AddControlPatch> addByName,
        string property,
        object? value,
        string location)
    {
        if (value is null) return;
        addByName.TryGetValue(entityName, out var add);
        AddItem(items, stage, "control", entityName, add?.Type, add?.Parent, null, property, value,
            location, location.StartsWith("layout.", StringComparison.Ordinal) ? "PatchDocument.Layout" : "PatchDocument.Add");
    }

    private void AddOptional(
        List<WriterAuditItem> items,
        string stage,
        ControlInfo control,
        string? scope,
        string property,
        object? value,
        string location)
    {
        if (value is null) return;
        AddItem(items, stage, "control", control.Name, control.Type, control.Parent, scope, property, value,
            location, "LayoutInspection.Controls");
    }

    private void AddItem(
        List<WriterAuditItem> items,
        string stage,
        string entityKind,
        string entityName,
        string? controlType,
        string? parent,
        string? storageScope,
        string property,
        object? value,
        string location,
        string codePath)
    {
        var semanticKind = ClassifyProperty(property);
        var summarized = Summarize(value, property, out var assetBytes);
        var key = SemanticKey(entityKind, entityName, property);
        if (assetBytes is not null)
        {
            semanticAssets[key] = assetBytes;
        }
        items.Add(new WriterAuditItem(
            key,
            entityKind,
            entityName,
            controlType,
            parent,
            storageScope,
            property,
            semanticKind,
            summarized,
            location,
            codePath));
    }

    private WriterAuditValue Summarize(object? value, string property, out byte[]? assetBytes)
    {
        assetBytes = null;
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

        if (value is null) return new WriterAuditValue("null", null, null, null, null);
        if (value is byte[] bytes) return DescribeBytes(bytes, "binary payload");
        if (value is string text && ClassifyProperty(property) == "binaryAsset")
        {
            assetBytes = ResolveAsset(text);
            if (assetBytes is not null)
            {
                return new WriterAuditValue("binaryAsset", null, assetBytes.Length, Hash(assetBytes),
                    text.StartsWith("file://", StringComparison.OrdinalIgnoreCase) ? "file asset" : "base64 asset");
            }
        }
        if (value is char character)
        {
            return new WriterAuditValue("string", character.ToString(), 1, null, null);
        }
        if (value is string stringValue)
        {
            return stringValue.Length > 512
                ? new WriterAuditValue("string", null, stringValue.Length, Hash(Encoding.UTF8.GetBytes(stringValue)), "large text")
                : new WriterAuditValue("string", stringValue, stringValue.Length, null, null);
        }
        if (value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal)
        {
            return new WriterAuditValue("number", Convert.ToDecimal(value, CultureInfo.InvariantCulture), null, null, null);
        }
        if (value is bool boolean) return new WriterAuditValue("boolean", boolean, null, null, null);

        var json = JsonSerializer.Serialize(value);
        return json.Length > 1024
            ? new WriterAuditValue(value.GetType().Name, null, json.Length, Hash(Encoding.UTF8.GetBytes(json)), "large JSON")
            : new WriterAuditValue(value.GetType().Name, value, null, null, null);
    }

    private byte[]? ResolveAsset(string value)
    {
        try
        {
            byte[] bytes;
            if (value.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                var path = value[7..];
                if (!Path.IsPathRooted(path) && !string.IsNullOrWhiteSpace(patchDirectory))
                {
                    path = Path.Combine(patchDirectory, path);
                }
                if (!File.Exists(path)) return null;
                bytes = File.ReadAllBytes(path);
            }
            else if (value.StartsWith("base64:", StringComparison.OrdinalIgnoreCase))
            {
                bytes = Convert.FromBase64String(value[7..]);
            }
            else
            {
                return null;
            }

            return HasPictureEnvelope(bytes) ? bytes[24..] : bytes;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static IReadOnlyList<WriterAuditItem> Deduplicate(IEnumerable<WriterAuditItem> items) =>
        items.GroupBy(item => item.SemanticKey, StringComparer.Ordinal)
            .Select(group => group.Last())
            .OrderBy(item => item.SemanticKey, StringComparer.Ordinal)
            .ToList();

    private static WriterAuditStageState State(WriterAuditItem? item) => item is null
        ? new WriterAuditStageState(false, null, null, true)
        : new WriterAuditStageState(true, item.Value, item.Location, true);

    private static bool ValuesEqual(WriterAuditValue left, WriterAuditValue right)
    {
        if (left.Sha256 is not null && right.Sha256 is not null)
        {
            return left.Length == right.Length && left.Sha256.Equals(right.Sha256, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(JsonSerializer.Serialize(left), JsonSerializer.Serialize(right), StringComparison.Ordinal);
    }

    private static string SemanticKey(string entityKind, string entityName, string property)
    {
        var canonical = CanonicalProperty(property);
        if (entityKind.Equals("form", StringComparison.OrdinalIgnoreCase))
        {
            canonical = canonical switch
            {
                "widthpt" => "displayedwidthpt",
                "heightpt" => "displayedheightpt",
                _ => canonical
            };
        }
        return $"{entityKind.ToLowerInvariant()}/{entityName.ToLowerInvariant()}/{canonical}";
    }

    private static string CanonicalProperty(string property) => property.ToLowerInvariant() switch
    {
        "width" => "rawwidth",
        "height" => "rawheight",
        "pagenames" => "tabnames",
        "pagecaptions" => "tabcaptions",
        _ => property.ToLowerInvariant()
    };

    private bool IsFormName(string name) =>
        name.Equals(formName, StringComparison.OrdinalIgnoreCase) ||
        name.Equals("UserForm", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Form", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("root", StringComparison.OrdinalIgnoreCase);

    private static string StageDescription(string stage) => stage switch
    {
        "J" => "deserialized exported template before normalization",
        "N" => "normalized PatchDocument",
        "T" => "requested target LayoutInspection",
        "C" => "strict re-read LayoutInspection",
        _ => stage
    };

    private static string? GetString(IReadOnlyDictionary<string, object?>? values, string key) =>
        values is not null && values.TryGetValue(key, out var value) ? value?.ToString() : null;

    private static IReadOnlyDictionary<string, WriterAuditValue> SummarizeDictionary(IReadOnlyDictionary<string, object?> values) =>
        values.ToDictionary(
            pair => pair.Key,
            pair => StaticSummarize(pair.Value),
            StringComparer.OrdinalIgnoreCase);

    private static WriterAuditValue StaticSummarize(object? value)
    {
        if (value is byte[] bytes) return DescribeBytes(bytes, "binary payload");
        if (value is JsonElement element) value = element.Clone();
        if (value is null) return new WriterAuditValue("null", null, null, null, null);
        var json = JsonSerializer.Serialize(value);
        return json.Length > 1024
            ? new WriterAuditValue(value.GetType().Name, null, json.Length, Hash(Encoding.UTF8.GetBytes(json)), "large JSON")
            : new WriterAuditValue(value.GetType().Name, value, null, null, null);
    }

    private static WriterAuditValue DescribeBytes(byte[] bytes, string representation) =>
        new("bytes", null, bytes.Length, Hash(bytes), representation);

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static bool Contains(byte[] haystack, byte[] needle)
    {
        if (needle.Length == 0 || needle.Length > haystack.Length) return false;
        return haystack.AsSpan().IndexOf(needle) >= 0;
    }

    private static bool HasPictureEnvelope(byte[] bytes) =>
        bytes.Length > 24 &&
        bytes.AsSpan(0, 20).SequenceEqual(new byte[]
        {
            0x04, 0x52, 0xE3, 0x0B, 0x91, 0x8F, 0xCE, 0x11,
            0x9D, 0xE3, 0x00, 0xAA, 0x00, 0x4B, 0xB8, 0x51,
            0x6C, 0x74, 0x00, 0x00
        });

    internal static string ClassifyProperty(string name)
    {
        var normalized = CanonicalProperty(name);
        if (normalized == "$exists" || StructuralProperties.Contains(normalized)) return "structural";
        if (SerializationProperties.Contains(normalized)) return "serialization";
        if (DesignerProperties.Contains(normalized) || normalized.StartsWith("form", StringComparison.Ordinal) && !IsDiagnostic(normalized))
        {
            return normalized is "picture" or "mouseicon" or "formpicture" or "formmouseicon"
                ? "binaryAsset"
                : "designer";
        }
        return "diagnostic";
    }

    private static bool IsMeaningful(string kind) => kind is "structural" or "designer" or "binaryAsset";
    private static bool IsOmittedDefault(WriterAuditItem item)
    {
        var property = CanonicalProperty(item.Property);
        var serialized = item.Value.Value?.ToString();
        return property switch
        {
            "enabled" or "visible" or "tabstop" or "takefocusonclick" =>
                string.Equals(serialized, bool.TrueString, StringComparison.OrdinalIgnoreCase),
            "locked" or "autosize" or "default" or "cancel" or "multiline" or
                "fontitalic" or "fontunderline" or "fontstrikethrough" =>
                string.Equals(serialized, bool.FalseString, StringComparison.OrdinalIgnoreCase),
            "wordwrap" when item.ControlType?.Equals("CheckBox", StringComparison.OrdinalIgnoreCase) == true =>
                string.Equals(serialized, bool.TrueString, StringComparison.OrdinalIgnoreCase),
            "wordwrap" => string.Equals(serialized, bool.FalseString, StringComparison.OrdinalIgnoreCase),
            "alignment" or "backstyle" or "borderstyle" or "boundcolumn" or "columncount" or
                "dropbuttonstyle" => serialized is "0" or "1",
            "imemode" or "liststyle" or "listwidth" or "maxlength" or "scrollbars" => serialized == "0",
            "specialeffect" => serialized is "0" or "2",
            "pictureposition" => serialized == "458753",
            "listrows" => serialized == "8",
            "textcolumn" => serialized == "-1",
            "textalign" => string.Equals(serialized, "left", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }
    private static bool IsRepresentationNormalization(string property) =>
        CanonicalProperty(property) is "left" or "top" or "rawwidth" or "rawheight" or
            "leftpt" or "toppt" or "widthpt" or "heightpt" or "textalign" or
            "paragraphalign" or "siteid" or "id";

    private static bool IsDiagnostic(string name) =>
        name.Contains("offset", StringComparison.Ordinal) ||
        name.Contains("parser", StringComparison.Ordinal) ||
        name.Contains("mask", StringComparison.Ordinal) ||
        name.Contains("payload", StringComparison.Ordinal) ||
        name.Contains("stream", StringComparison.Ordinal) ||
        name.Contains("generated", StringComparison.Ordinal) ||
        name.Contains("validation", StringComparison.Ordinal);

    private static readonly HashSet<string> StructuralProperties = new(StringComparer.Ordinal)
    {
        "name", "type", "parent", "order", "left", "top", "rawwidth", "rawheight", "leftpt", "toppt", "widthpt", "heightpt",
        "multipageparent", "multipagepageindex", "multipagepagecount", "tabnames", "tabcaptions"
    };

    private static readonly HashSet<string> SerializationProperties = new(StringComparer.Ordinal)
    {
        "siteid", "id", "multipagepageid", "multipagepageids"
    };

    private static readonly HashSet<string> DesignerProperties = new(StringComparer.Ordinal)
    {
        "caption", "text", "value", "tag", "controltiptext", "controlsource", "rowsource", "helpcontextid", "groupid",
        "accelerator", "textalign", "paragraphalign", "backcolor", "forecolor", "bordercolor", "fontname", "fontsize", "fontweight",
        "fontbold", "fontitalic", "fontunderline", "fontstrikethrough", "fontcharset", "enabled", "visible", "locked", "tabindex",
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

internal sealed record WriterProvenanceAuditDocument(
    int SchemaVersion,
    string Command,
    string FormName,
    string PatchFile,
    WriterAuditPipeline Pipeline,
    IReadOnlyDictionary<string, int> Summary,
    IReadOnlyList<WriterAuditStage> Stages,
    IReadOnlyList<WriterAuditEvent> WriterEvents,
    IReadOnlyList<WriterAuditStorageSnapshot> StorageSnapshots,
    WriterAuditBinary? Binary,
    WriterAuditFailure? Failure,
    IReadOnlyList<WriterAuditProvenanceRecord> Provenance);

internal sealed record WriterAuditPipeline(string J, string N, string T, string B, string C);
internal sealed record WriterAuditStage(string Stage, string Description, IReadOnlyList<WriterAuditItem> Items);
internal sealed record WriterAuditItem(
    string SemanticKey,
    string EntityKind,
    string EntityName,
    string? ControlType,
    string? Parent,
    string? StorageScope,
    string Property,
    string SemanticKind,
    WriterAuditValue Value,
    string Location,
    string CodePath);
internal sealed record WriterAuditValue(string Type, object? Value, int? Length, string? Sha256, string? Representation);
internal sealed record WriterAuditEvent(
    string Event,
    string? EntityName,
    string? ControlType,
    string? Parent,
    string? StoragePath,
    string CodePath,
    IReadOnlyDictionary<string, object?> Evidence);
internal sealed record WriterAuditStream(string Path, string? ParentPath, string Name, string Kind, int Length, string? Sha256);
internal sealed record WriterAuditStorageSnapshot(string Substage, IReadOnlyList<WriterAuditStream> Streams);
internal sealed record WriterAuditAssetEvidence(
    string SemanticKey,
    int ByteLength,
    string Sha256,
    bool FoundInReachableStream,
    IReadOnlyList<string> StreamPaths);
internal sealed record WriterAuditBinary(
    int FrxByteLength,
    int OleOffset,
    string FrxSha256,
    int OleByteLength,
    string OleSha256,
    int StorageEntryCount,
    IReadOnlyList<WriterAuditAssetEvidence> Assets);
internal sealed record WriterAuditFailure(string Boundary, string CodePath, string ExceptionType, string Message);
internal sealed record WriterAuditStageState(bool? Present, WriterAuditValue? Value, string? Location, bool IndependentlyObserved);
internal sealed record WriterAuditProvenanceRecord(
    string SemanticKey,
    string EntityKind,
    string EntityName,
    string? ControlType,
    string? Parent,
    string? StorageScope,
    string Property,
    string SemanticKind,
    WriterAuditStageState J,
    WriterAuditStageState N,
    WriterAuditStageState T,
    WriterAuditStageState B,
    WriterAuditStageState C,
    string Classification,
    string ResponsibleRule);
