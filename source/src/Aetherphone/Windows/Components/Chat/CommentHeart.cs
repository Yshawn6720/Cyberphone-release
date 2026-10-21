using Aetherphone.Core.Localization;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Windows.Components;

internal static class CommentHeart
{
    public static readonly Vector4 LikeRed = new(0.95f, 0.27f, 0.36f, 1f);

    private const float CountTextScale = 0.7f;
    private const float HitRadius = 9f;
    private const float CountGap = 5f;

    public static bool Draw(AppSkin ui, Vector2 rightMiddle, bool liked, int likeCount, Vector4 idleColor,
        Vector4 countColor, string tooltip)
    {
        var scale = UiScale.Current;
        var hitRadius = HitRadius * scale;
        var countText = likeCount > 0 ? likeCount.ToString(Loc.Culture) : string.Empty;
        var countSize = countText.Length > 0 ? Typography.Measure(countText, CountTextScale) : Vector2.Zero;
        var countWidth = countText.Length > 0 ? countSize.X + CountGap * scale : 0f;
        var center = new Vector2(rightMiddle.X - countWidth - hitRadius, rightMiddle.Y);
        var clicked = ui.IconButton(center, hitRadius, IconGlyph.Of(FontAwesomeIcon.Heart),
            liked ? LikeRed : idleColor, AppSkin.Transparent, 0.8f, tooltip);
        if (countText.Length > 0)
        {
            var countPos = new Vector2(rightMiddle.X - countSize.X, rightMiddle.Y - countSize.Y * 0.5f);
            Typography.Draw(ImGui.GetWindowDrawList(), countPos, countText, countColor, CountTextScale);
        }

        return clicked;
    }
}
