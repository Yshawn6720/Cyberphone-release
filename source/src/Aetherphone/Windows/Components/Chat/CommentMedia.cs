using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class CommentMedia
{
    private const float MaxHeightUnits = 200f;

    public static float MeasureHeight(CommentDto comment, float availableWidth, float scale)
    {
        if (comment.MediaUrl is null)
        {
            return 0f;
        }

        return Fit(comment, availableWidth, scale).Height;
    }

    public static Rect Draw(ImDrawListPtr drawList, RemoteImageCache images, CommentDto comment, Vector2 origin,
        float availableWidth, float scale, Vector4 placeholderFill, Vector4 mutedInk)
    {
        var (width, height) = Fit(comment, availableWidth, scale);
        var rect = new Rect(origin, origin + new Vector2(width, height));
        var rounding = 10f * scale;
        var texture = GifMedia.Texture(images, comment.MediaUrl, ImGui.GetTime());
        if (texture is null)
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(placeholderFill));
            Typography.DrawCentered(rect.Center,
                Loc.T(images.Failed(comment.MediaUrl) ? L.Common.ImageFailed : L.Common.Loading), mutedInk, 0.8f);
        }
        else
        {
            ImageFit.DrawLetterboxed(drawList, texture, rect, Vector2.Zero, Vector2.One, rounding);
        }

        if (GifMedia.IsGif(comment.MediaUrl))
        {
            GifBadge.Draw(drawList, rect);
        }

        return rect;
    }

    private static (float Width, float Height) Fit(CommentDto comment, float availableWidth, float scale)
    {
        var aspect = comment.MediaWidth > 0 && comment.MediaHeight > 0
            ? (float)comment.MediaWidth / comment.MediaHeight
            : 1f;
        var height = MathF.Min(MaxHeightUnits * scale, availableWidth / aspect);
        return (height * aspect, height);
    }
}
