using Aetherphone.Core;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Velvet.Kit;

internal static class VSegmented
{
    public static int Draw(string id, Rect rect, IReadOnlyList<string> labels, int current)
    {
        var selected = SegmentStrip.Draw(id, rect, labels, current,
            Surfaces.Fill(VelvetTheme.Ink, FillLevel.Tertiary), VelvetTheme.Ink.Accent, VelvetTheme.Ink.Muted,
            VelvetTheme.OnAccent, out var pressed);
        return pressed ? selected : -1;
    }
}
