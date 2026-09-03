using System.Text;
using System.Text.Json;

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException($"Assertion failed: {message}");
    }
}

static void AssertThrows<TException>(Action action, string message, string? expectedText = null)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException ex)
    {
        if (expectedText is not null && !ex.Message.Contains(expectedText, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Assertion failed: {message}. Exception did not contain '{expectedText}': {ex.Message}");
        }

        return;
    }

    throw new InvalidOperationException($"Assertion failed: {message}. Expected {typeof(TException).Name}.");
}

static StorageEntryDump Stream(byte[] data) => new(
    0,
    "f",
    "Stream",
    0,
    (ulong)data.Length,
    false,
    string.Empty,
    null,
    null,
    [],
    data,
    Enumerable.Range(0, data.Length).ToArray())
{
    Path = "Root Entry/i02/f",
    ParentPath = "Root Entry/i02"
};

static byte[] Insert(byte[] source, int offset, byte[] addition) =>
    [.. source.AsSpan(0, offset), .. addition, .. source.AsSpan(offset)];

var stdFont = MsFormsFactoryBinary.BuildGuidAndStdFont();
Assert(stdFont.Length == 33, "default GuidAndStdFont must contain exactly 33 bytes");
Assert(
    Convert.ToHexString(stdFont) == "0352E30B918FCE119DE300AA004BB85101000000900144420100065461686F6D61",
    "default GuidAndStdFont must match the typed StdFont encoding without trailing padding");

var stdFontProperties = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
var stdFontCursor = 0;
Assert(
    ObjectStreamParser.TryReadGuidAndFont(
        stdFont,
        Enumerable.Range(0, stdFont.Length).ToArray(),
        ref stdFontCursor,
        "formFont",
        stdFontProperties),
    "StdFont GuidAndFont must parse");
Assert(stdFontCursor == stdFont.Length, "StdFont parser must consume exactly");
Assert((string?)stdFontProperties["formFontKind"] == "StdFont", "StdFont kind must be reported");
Assert((string?)stdFontProperties["formFontName"] == "Tahoma", "StdFont face name must be reported");

var textProps = TextPropsFactory.Build(
    new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
    {
        ["fontName"] = "Calibri",
        ["fontSize"] = 9.0,
        ["fontCharSet"] = 0,
        ["fontPitchAndFamily"] = 2
    },
    TextPropsFactory.StandardMask);
var textPropsGuidAndFont = new byte[16 + textProps.Length];
new Guid("{AFC20920-DA4E-11CE-B943-00AA006887B4}").TryWriteBytes(textPropsGuidAndFont);
textProps.CopyTo(textPropsGuidAndFont, 16);
var textPropsProperties = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
var textPropsCursor = 0;
Assert(
    ObjectStreamParser.TryReadGuidAndFont(
        textPropsGuidAndFont,
        Enumerable.Range(0, textPropsGuidAndFont.Length).ToArray(),
        ref textPropsCursor,
        "formFont",
        textPropsProperties),
    "TextProps GuidAndFont must parse");
Assert(textPropsCursor == textPropsGuidAndFont.Length, "TextProps GuidAndFont parser must consume exactly");
Assert((string?)textPropsProperties["formFontKind"] == "TextProps", "TextProps font kind must be reported");
Assert((string?)textPropsProperties["fontName"] == "Calibri", "TextProps font name must be reported");

var page = new GeneratedPageDefinition(
    "Page1",
    "First page",
    0,
    0,
    0,
    4_000,
    3_000,
    0x0004_0021u,
    null);
var multiPage = GeneratedStorageFactory.CreateMultiPage(
    "Tabs",
    2,
    0,
    0,
    0,
    5_000,
    4_000,
    "Root Entry/i02",
    [page],
    0,
    new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
    {
        ["fontPitchAndFamily"] = 2
    });
var formBytes = (byte[])multiPage.Metadata["generatedStorageF"]!;
var formStream = Stream(formBytes);
Assert(FormControlParser.TryRead(formStream, out var formControl), "generated MultiPage FormControl must parse");
Assert(formControl.FormStreamDataValid, "generated MultiPage FormStreamData must be valid");
Assert(
    Convert.ToInt32(formControl.Properties["formFontStreamByteCount"]) == 28,
    "generated MultiPage font stream must contain the canonical GuidAndTextProps bytes");
Assert(
    (string?)formControl.Properties["formFontKind"] == "TextProps",
    "generated MultiPage font must use the canonical TextProps encoding");
Assert(
    Convert.ToInt32(formControl.Properties["fontPitchAndFamily"]) == 2,
    "generated MultiPage font must retain VARIABLE_PITCH");
var strictSites = StructuredMsFormsParser.Parse(formStream, ParserMode.Strict, formControl);
Assert(strictSites.Count == 2, "strict parser must read the internal TabStrip and Page at the exact boundary");
Assert(
    (string?)formControl.Properties["formSiteDataBoundaryValidation"] == "exact",
    "generated MultiPage SiteData boundary must be exact");
Assert(
    Convert.ToInt32(formControl.Properties["formSiteDataGapByteCount"]) == 0,
    "generated MultiPage must not contain bytes between FormStreamData and FormSiteData");
Assert(
    (string?)formControl.Properties["formDesignExDataValidation"] == "exact",
    "generated MultiPage FormDesignExData must validate exactly");
Assert(
    (string?)formControl.Properties["formDesignExData"] == "base64:AAIMABkAAADwjwAA/wEAAA==",
    "generated MultiPage must use the native-validated default FormDesignExData");

var pageControl = GeneratedStorageFactory.CreatePage(
    "Page1",
    3,
    0,
    4_000,
    3_000,
    "Root Entry/i02/i03",
    0x0004_0021u);
var pageStream = Stream(pageControl.FStream);
Assert(FormControlParser.TryRead(pageStream, out var pageFormControl), "generated Page FormControl must parse");
Assert(
    StructuredMsFormsParser.Parse(pageStream, ParserMode.Strict, pageFormControl).Count == 0,
    "generated empty Page must strict-parse with no sites");
Assert(
    (string?)pageFormControl.Properties["formDesignExData"] == "base64:AAIMABkAAADz/wEA/wEAAA==",
    "generated Page must use the native-validated container FormDesignExData");

var frameControlBytes = GeneratedStorageFactory.CreateFrame(
    "Frame1",
    4,
    0,
    0,
    0,
    4_000,
    3_000,
    null,
    "Root Entry/i04",
    new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
    {
        ["formBooleanProperties"] = "0x0000C004"
    });
var frameStream = Stream((byte[])frameControlBytes.Metadata["generatedStorageF"]!);
Assert(FormControlParser.TryRead(frameStream, out var frameFormControl), "generated Frame FormControl must parse");
Assert(
    StructuredMsFormsParser.Parse(frameStream, ParserMode.Strict, frameFormControl).Count == 0,
    "generated empty Frame must strict-parse without FormSiteData");
Assert(
    (string?)frameFormControl.Properties["formDesignExData"] == "base64:AAIMABkAAADz/wEA/wEAAA==",
    "generated Frame must use the native-validated container FormDesignExData");

var missingDesignExBytes = formBytes[..^16];
var missingDesignExStream = Stream(missingDesignExBytes);
Assert(FormControlParser.TryRead(missingDesignExStream, out var missingDesignExControl), "missing DesignExtender fixture must parse its FormControl");
AssertThrows<CliException>(
    () => StructuredMsFormsParser.Parse(missingDesignExStream, ParserMode.Strict, missingDesignExControl),
    "strict parsing must reject a missing persisted FormDesignExData",
    "missing");
var missingDesignExSites = StructuredMsFormsParser.Parse(missingDesignExStream, ParserMode.Tolerant, missingDesignExControl);
Assert(missingDesignExSites.Count == 2, "tolerant parsing must retain sites when FormDesignExData is missing");
Assert(
    (string?)missingDesignExControl.Properties["formDesignExDataValidation"] == "missing",
    "tolerant parsing must report missing FormDesignExData precisely");

var noPersistMultiPage = GeneratedStorageFactory.CreateMultiPage(
    "TabsWithoutDesignEx",
    6,
    0,
    0,
    0,
    5_000,
    4_000,
    "Root Entry/i06",
    [page],
    0,
    new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
    {
        ["formBooleanProperties"] = "0x00008004"
    });
byte[] unexpectedDesignExBytes = [
    .. (byte[])noPersistMultiPage.Metadata["generatedStorageF"]!,
    .. Convert.FromHexString("00020C0019000000F37F0100FF010000")
];
var unexpectedDesignExStream = Stream(unexpectedDesignExBytes);
Assert(FormControlParser.TryRead(unexpectedDesignExStream, out var unexpectedDesignExControl), "unexpected DesignExtender fixture must parse its FormControl");
AssertThrows<CliException>(
    () => StructuredMsFormsParser.Parse(unexpectedDesignExStream, ParserMode.Strict, unexpectedDesignExControl),
    "strict parsing must reject FormDesignExData when the persistence flag is clear",
    "unexpected");
StructuredMsFormsParser.Parse(unexpectedDesignExStream, ParserMode.Tolerant, unexpectedDesignExControl);
Assert(
    (string?)unexpectedDesignExControl.Properties["formDesignExDataValidation"] == "unexpected",
    "tolerant parsing must report unexpected FormDesignExData precisely");

var trailingDesignExStream = Stream([.. formBytes, 0xA5]);
Assert(FormControlParser.TryRead(trailingDesignExStream, out var trailingDesignExControl), "trailing-data fixture must parse its FormControl");
AssertThrows<CliException>(
    () => StructuredMsFormsParser.Parse(trailingDesignExStream, ParserMode.Strict, trailingDesignExControl),
    "strict parsing must reject trailing data after FormDesignExData",
    "trailing");

AssertThrows<CliException>(
    () => FormDesignExDataBinary.ResolveForGeneration(
        0x0000_0004u,
        "base64:AAIMABkAAADzfwEA/wEAAA==",
        FormDesignExDefaultKind.UserForm,
        "FlagMismatch"),
    "explicit FormDesignExData must be rejected when the persistence flag is clear",
    "requires FORM_FLAG_DESINKPERSISTED");

var clearPersistencePatch = new PatchDocument
{
    Properties = new Dictionary<string, Dictionary<string, JsonElement>>(StringComparer.OrdinalIgnoreCase)
    {
        ["TestForm"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["formBooleanProperties"] = JsonSerializer.SerializeToElement("0x00000004")
        }
    }
};
var clearedPersistenceLayout = RebuildPatchApplier.ApplyObjectPropertyPatch(
    new LayoutInspection(
        [],
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["formBooleanProperties"] = "0x00004004",
            ["formBooleanPropertiesRawOffset"] = 12,
            ["formDesignExData"] = "base64:AAIMABkAAADzfwEA/wEAAA=="
        }),
    clearPersistencePatch,
    formName: "TestForm");
Assert(
    clearedPersistenceLayout.FrxFormControl?.ContainsKey("formDesignExData") == false,
    "clearing FORM_FLAG_DESINKPERSISTED must remove FormDesignExData from the reconstruction target");

var setPersistencePatch = new PatchDocument
{
    Properties = new Dictionary<string, Dictionary<string, JsonElement>>(StringComparer.OrdinalIgnoreCase)
    {
        ["TestForm"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["formBooleanProperties"] = JsonSerializer.SerializeToElement("0x00004004")
        }
    }
};
var setPersistenceLayout = RebuildPatchApplier.ApplyObjectPropertyPatch(
    new LayoutInspection(
        [],
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["formBooleanProperties"] = "0x00000004",
            ["formBooleanPropertiesRawOffset"] = 12
        }),
    setPersistencePatch,
    formName: "TestForm");
Assert(
    (string?)setPersistenceLayout.FrxFormControl?["formDesignExData"] == "base64:AAIMABkAAADzfwEA/wEAAA==",
    "setting FORM_FLAG_DESINKPERSISTED must synthesize the native-validated UserForm FormDesignExData");

var opaqueInPlacePatch = new PatchDocument
{
    Properties = new Dictionary<string, Dictionary<string, JsonElement>>(StringComparer.OrdinalIgnoreCase)
    {
        ["TestForm"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["formDesignExData"] = JsonSerializer.SerializeToElement("base64:AAIMABkAAADzfwEA/wEAAA==")
        }
    }
};
AssertThrows<CliException>(
    () => RebuildPatchApplier.ValidateObjectPatch(opaqueInPlacePatch, formName: "TestForm"),
    "opaque FormDesignExData must remain unavailable to in-place root patches",
    "not supported");

var tabStripBytes = (byte[])multiPage.Metadata["generatedStorageO"]!;
var tabStrip = ObjectStreamParser.Read(Stream(tabStripBytes), "TabStrip");
Assert(tabStrip is not null, "generated internal TabStrip must parse");
var parsedTabStrip = tabStrip ?? throw new InvalidOperationException("Generated internal TabStrip did not parse.");
Assert(Convert.ToInt32(parsedTabStrip.Properties["tabsAllocated"]) == 1, "TabsAllocated must equal inserted tab count");
Assert(Convert.ToInt32(parsedTabStrip.Properties["tabData"]) == 1, "TabData must equal inserted tab count");

var malformedBytes = Insert(formBytes, formControl.FormStreamDataEndLocalOffset, new byte[8]);
var malformedStream = Stream(malformedBytes);
Assert(FormControlParser.TryRead(malformedStream, out var malformedFormControl), "malformed fixture FormControl prefix must parse");
AssertThrows<CliException>(
    () => StructuredMsFormsParser.Parse(malformedStream, ParserMode.Strict, malformedFormControl),
    "strict parsing must reject bytes between FormStreamData and FormSiteData");
var recoveredSites = StructuredMsFormsParser.Parse(malformedStream, ParserMode.Tolerant, malformedFormControl);
Assert(recoveredSites.Count == 2, "tolerant parsing may recover the deliberately malformed SiteData");
Assert(
    (string?)malformedFormControl.Properties["formSiteDataBoundaryValidation"] == "recovered",
    "tolerant parsing must label boundary recovery");
Assert(
    Convert.ToInt32(malformedFormControl.Properties["formSiteDataGapByteCount"]) == 8,
    "tolerant parsing must report the exact eight-byte gap");
Assert(
    (string?)malformedFormControl.Properties["formSiteDataGapHex"] == "0000000000000000",
    "tolerant parsing must report the recovered gap bytes");

var existingControl = new ControlInfo(
    "DeleteMe",
    "CommandButton",
    null,
    null,
    null,
    null,
    10,
    20,
    80,
    24,
    new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase),
    null,
    null,
    null,
    null,
    null,
    0,
    null,
    null,
    null,
    null);

var legacyRemovePatch = new PatchDocument
{
    Properties = new Dictionary<string, Dictionary<string, JsonElement>>(StringComparer.OrdinalIgnoreCase)
    {
        ["DeleteMe"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["$action"] = JsonSerializer.SerializeToElement("remove"),
            ["type"] = JsonSerializer.SerializeToElement("CommandButton"),
            ["caption"] = JsonSerializer.SerializeToElement("discarded"),
            ["leftPt"] = JsonSerializer.SerializeToElement(42.0)
        }
    }
};
legacyRemovePatch.Normalize("TestForm");
Assert(
    legacyRemovePatch.Remove?.Count == 1 && legacyRemovePatch.Remove[0] == "DeleteMe",
    "legacy remove action must normalize to the canonical remove list");
Assert(
    legacyRemovePatch.Properties?.ContainsKey("DeleteMe") == false,
    "legacy remove action must discard its properties payload");
Assert(
    legacyRemovePatch.Layout?.ContainsKey("DeleteMe") == false,
    "legacy remove action must discard its flattened layout payload");
PatchValidator.Validate(legacyRemovePatch, [existingControl], formName: "TestForm");

var conflictingRemovePatch = new PatchDocument
{
    Renames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["DeleteMe"] = "RenamedControl"
    },
    Layout = new Dictionary<string, LayoutPatch>(StringComparer.OrdinalIgnoreCase)
    {
        ["DeleteMe"] = new() { LeftPt = 12 }
    },
    Properties = new Dictionary<string, Dictionary<string, JsonElement>>(StringComparer.OrdinalIgnoreCase)
    {
        ["DeleteMe"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["$action"] = JsonSerializer.SerializeToElement("remove")
        }
    }
};
conflictingRemovePatch.Normalize("TestForm");
AssertThrows<CliException>(
    () => PatchValidator.Validate(conflictingRemovePatch, [existingControl], formName: "TestForm"),
    "explicit rename/layout conflicts must remain invalid after legacy remove normalization",
    "cannot also be renamed");

var conflictingRemoveLayoutPatch = new PatchDocument
{
    Layout = new Dictionary<string, LayoutPatch>(StringComparer.OrdinalIgnoreCase)
    {
        ["DeleteMe"] = new() { LeftPt = 12 }
    },
    Properties = new Dictionary<string, Dictionary<string, JsonElement>>(StringComparer.OrdinalIgnoreCase)
    {
        ["DeleteMe"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["$action"] = JsonSerializer.SerializeToElement("remove")
        }
    }
};
conflictingRemoveLayoutPatch.Normalize("TestForm");
AssertThrows<CliException>(
    () => PatchValidator.Validate(conflictingRemoveLayoutPatch, [existingControl], formName: "TestForm"),
    "explicit layout conflicts must remain invalid after legacy remove normalization",
    "cannot also receive a layout patch");

var conflictingRemoveMovePatch = new PatchDocument
{
    Move = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
    {
        ["DeleteMe"] = "DestinationFrame"
    },
    Properties = new Dictionary<string, Dictionary<string, JsonElement>>(StringComparer.OrdinalIgnoreCase)
    {
        ["DeleteMe"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["$action"] = JsonSerializer.SerializeToElement("remove")
        }
    }
};
conflictingRemoveMovePatch.Normalize("TestForm");
AssertThrows<CliException>(
    () => PatchValidator.Validate(conflictingRemoveMovePatch, [existingControl], formName: "TestForm"),
    "explicit move conflicts must remain invalid after legacy remove normalization",
    "cannot also be moved");

Console.WriteLine("PASS: focused binary-boundary and patch-contract regressions");
