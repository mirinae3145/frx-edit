using System.Text;

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException($"Assertion failed: {message}");
    }
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

var tabStripBytes = (byte[])multiPage.Metadata["generatedStorageO"]!;
var tabStrip = ObjectStreamParser.Read(Stream(tabStripBytes), "TabStrip");
Assert(tabStrip is not null, "generated internal TabStrip must parse");
var parsedTabStrip = tabStrip ?? throw new InvalidOperationException("Generated internal TabStrip did not parse.");
Assert(Convert.ToInt32(parsedTabStrip.Properties["tabsAllocated"]) == 1, "TabsAllocated must equal inserted tab count");
Assert(Convert.ToInt32(parsedTabStrip.Properties["tabData"]) == 1, "TabData must equal inserted tab count");

var malformedBytes = Insert(formBytes, formControl.FormStreamDataEndLocalOffset, new byte[8]);
var malformedStream = Stream(malformedBytes);
Assert(FormControlParser.TryRead(malformedStream, out var malformedFormControl), "malformed fixture FormControl prefix must parse");
Assert(
    StructuredMsFormsParser.Parse(malformedStream, ParserMode.Strict, malformedFormControl).Count == 0,
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

Console.WriteLine("PASS: GuidAndFont serialization and exact FormStreamData/FormSiteData boundaries");
