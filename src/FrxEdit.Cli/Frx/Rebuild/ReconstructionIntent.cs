using System.Runtime.CompilerServices;

internal sealed record ReconstructionIntent(
    IReadOnlySet<string> ObjectControls,
    IReadOnlySet<string> SiteControls,
    IReadOnlySet<string> MultiPageControls,
    IReadOnlySet<string> FrmRootProperties,
    bool RootBinaryChanged,
    bool StructuralChanged)
{
    public static ReconstructionIntent Empty { get; } = new(
        new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        RootBinaryChanged: false,
        StructuralChanged: false);

    public bool HasNativeFrxChanges =>
        RootBinaryChanged ||
        StructuralChanged ||
        ObjectControls.Count > 0 ||
        SiteControls.Count > 0 ||
        MultiPageControls.Count > 0;
}

internal static class ReconstructionIntentRegistry
{
    private sealed class Holder(ReconstructionIntent intent)
    {
        public ReconstructionIntent Intent { get; } = intent;
    }

    private static readonly ConditionalWeakTable<LayoutInspection, Holder> Intents = new();

    public static void Set(LayoutInspection layout, ReconstructionIntent intent)
    {
        Intents.Remove(layout);
        Intents.Add(layout, new Holder(intent));
    }

    public static ReconstructionIntent? Get(LayoutInspection layout) =>
        Intents.TryGetValue(layout, out var holder) ? holder.Intent : null;
}
