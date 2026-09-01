internal sealed record RebuildComparison(
    int SourceControlCount,
    int RebuiltControlCount,
    bool SemanticMatch,
    IReadOnlyList<string> Differences)
{
    public int? InputControlCount { get; init; }
    public int? ExpectedControlCount { get; init; }

    public static RebuildComparison From(
        LayoutInspection source,
        LayoutInspection rebuilt,
        string? sourceFormName = null,
        string? rebuiltFormName = null)
    {
        var differences = CanonicalSemanticComparer.Compare(source, rebuilt, sourceFormName, rebuiltFormName).ToList();
        return new RebuildComparison(source.Controls.Count, rebuilt.Controls.Count, differences.Count == 0, differences);
    }
}
