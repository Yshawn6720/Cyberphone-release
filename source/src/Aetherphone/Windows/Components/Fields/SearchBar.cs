using Aetherphone.Core;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class SearchBar
{
    public const float HeightUnits = 36f;

    public static Rect Capsule(Rect bar)
    {
        var height = MathF.Min(bar.Height, HeightUnits * UiScale.Current);
        var half = height * 0.5f;
        return new Rect(new Vector2(bar.Min.X, bar.Center.Y - half), new Vector2(bar.Max.X, bar.Center.Y + half));
    }

    public static void Surface(ImDrawListPtr drawList, Rect field, in ControlInk ink, float opacity = 1f)
    {
        var capsule = Capsule(field);
        var fill = Surfaces.Fill(ink, FillLevel.Tertiary);
        Squircle.Fill(drawList, capsule.Min, capsule.Max, capsule.Height * 0.5f,
            ImGui.GetColorU32(fill with { W = fill.W * opacity }));
    }

    public static void Draw(Rect bar, string imguiId, string hint, ref string text, in ControlInk ink,
        int maxLength = 100, bool focus = false)
    {
        var drawList = ImGui.GetWindowDrawList();
        Surface(drawList, bar, ink);
        GlassField.Search(drawList, bar, imguiId, hint, ref text, ink.Ink, ink.Muted, UiScale.Current, maxLength,
            focus);
    }
}
