using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Velvet.Kit;

internal static class VSectionHeader
{
    public static void Overline(string label, string trailing = "", float inset = 0f)
    {
        var scale = UiScale.Current;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var left = origin.X + inset;
        var right = origin.X + width - inset;
        var overlineMaxWidth = right - left;
        if (trailing.Length > 0)
        {
            var size = Typography.Measure(trailing, TextStyles.FootnoteEmphasized);
            overlineMaxWidth -= size.X + 8f * scale;
            Typography.Draw(new Vector2(right - size.X, origin.Y), trailing, VelvetTheme.MutedInk,
                TextStyles.FootnoteEmphasized);
        }

        Typography.Draw(new Vector2(left, origin.Y),
            Typography.FitText(Loc.Upper(label), overlineMaxWidth, TextStyles.FootnoteEmphasized),
            VelvetTheme.MutedInk, TextStyles.FootnoteEmphasized);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, 22f * scale));
    }
}
