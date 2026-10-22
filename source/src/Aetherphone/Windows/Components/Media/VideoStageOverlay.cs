using Aetherphone.Core;
using Aetherphone.Core.Video;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class VideoStageOverlay
{
    private const float BubbleMaxWidth = 320f;
    private const float BubbleWidthShare = 0.62f;
    private const float BubbleWidthStep = 32f;
    private const float BubblePad = 8f;
    private const float BubbleGap = 5f;
    private const float BubbleRadius = 12f;
    private const float BubbleInset = 12f;
    private const float BubbleFadeIn = 0.05f;
    private const float BubbleFadeOut = 0.86f;
    private const float BubbleBackdrop = 0.64f;
    private const float MinimumStageWidth = 200f;
    private const float ReactionSizeShare = 0.13f;
    private const float ReactionMinSize = 18f;
    private const float ReactionMaxSize = 46f;
    private const float ReactionRise = 0.72f;
    private const float ReactionFadeStart = 0.7f;
    private const float DanmakuInset = 6f;
    private const float DanmakuOwnPad = 3f;
    private const float DanmakuOwnRadius = 6f;
    private const float DanmakuOwnStroke = 1.5f;
    private const float DanmakuShadowOffset = 1f;

    private static readonly TextStyle SenderStyle = TextStyles.FootnoteEmphasized;
    private static readonly TextStyle LineStyle = TextStyles.Footnote;
    private static readonly Vector4 SenderInk = new(1f, 0.84f, 0.45f, 1f);
    private static readonly Vector4 LineInk = new(1f, 1f, 1f, 1f);
    private static readonly TextStyle DanmakuMeasureStyle = TextStyles.Headline;
    private static readonly TextStyle[] DanmakuStyles =
    [
        TextStyles.Title3, TextStyles.Headline, TextStyles.SubheadlineEmphasized, TextStyles.FootnoteEmphasized,
        TextStyles.Caption1, TextStyles.Caption2,
    ];
    private static readonly DanmakuMeasure MeasureDanmaku = MeasureInFontHeights;
    private static readonly Vector4 DanmakuNameInk = new(0.78f, 0.84f, 0.92f, 0.9f);
    private static readonly Vector4 DanmakuInk = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 DanmakuShadowInk = new(0f, 0f, 0f, 0.75f);
    private static readonly Vector4 DanmakuOwnInk = new(1f, 1f, 1f, 0.85f);

    public static void DrawDanmaku(ImDrawListPtr drawList, Rect stage, PartyDanmaku danmaku, int lanes, float scale)
    {
        if (stage.Width < MinimumStageWidth * scale)
        {
            return;
        }

        var now = Environment.TickCount64;
        danmaku.Release(now, lanes, MeasureDanmaku);
        var laneHeight = MathF.Min(stage.Height, stage.Width * 9f / 16f) / PartyDanmaku.LanesPerScreenHeight;
        if (!TryPickDanmakuStyle(laneHeight * PartyDanmaku.TextShareOfLane, out var style, out var fontHeight))
        {
            return;
        }

        var top = stage.Min.Y + DanmakuInset * scale;
        var shadow = new Vector2(DanmakuShadowOffset * scale);
        var pad = DanmakuOwnPad * scale;
        var active = danmaku.Active;
        drawList.PushClipRect(stage.Min, stage.Max, true);
        for (var index = 0; index < active.Length; index++)
        {
            ref readonly var line = ref active[index];
            if (!PartyDanmaku.TryProgress(line, now, out var progress))
            {
                continue;
            }

            var width = line.Width * fontHeight;
            var left = stage.Max.X - progress * (stage.Width + width);
            var y = top + line.Lane * laneHeight + (laneHeight - fontHeight) * 0.5f;
            var namePosition = new Vector2(left, y);
            var textPosition = new Vector2(left + (line.NameWidth + PartyDanmaku.NameGap) * fontHeight, y);
            if (line.Own)
            {
                Squircle.Stroke(drawList, new Vector2(left - pad, y - pad),
                    new Vector2(left + width + pad, y + fontHeight + pad), DanmakuOwnRadius * scale,
                    ImGui.GetColorU32(DanmakuOwnInk), DanmakuOwnStroke * scale);
            }

            Typography.Draw(drawList, namePosition + shadow, line.Name, DanmakuShadowInk, style);
            Typography.Draw(drawList, namePosition, line.Name, DanmakuNameInk, style);
            Typography.Draw(drawList, textPosition + shadow, line.Text, DanmakuShadowInk, style);
            Typography.Draw(drawList, textPosition, line.Text, DanmakuInk, style);
        }

        drawList.PopClipRect();
    }

    private static bool TryPickDanmakuStyle(float roomHeight, out TextStyle style, out float fontHeight)
    {
        for (var index = 0; index < DanmakuStyles.Length; index++)
        {
            fontHeight = Typography.FontHeight(DanmakuStyles[index]);
            if (fontHeight <= roomHeight)
            {
                style = DanmakuStyles[index];
                return true;
            }
        }

        style = default;
        fontHeight = 0f;
        return false;
    }

    private static float MeasureInFontHeights(string text) =>
        Typography.Measure(text, DanmakuMeasureStyle).X / Typography.FontHeight(DanmakuMeasureStyle);

    public static void DrawReactions(ImDrawListPtr drawList, Rect stage, PartyReactions reactions, float scale)
    {
        var now = Environment.TickCount64;
        var active = reactions.Active;
        var baseSize = Math.Clamp(stage.Height * ReactionSizeShare, ReactionMinSize * scale, ReactionMaxSize * scale);
        for (var index = 0; index < active.Length; index++)
        {
            ref readonly var reaction = ref active[index];
            if (!PartyReactions.TryProgress(reaction, now, out var progress))
            {
                continue;
            }

            var rise = 1f - (1f - progress) * (1f - progress);
            var pop = 0.7f + 0.3f * MathF.Min(1f, progress * 8f);
            var size = baseSize * pop;
            var sway = MathF.Sin(progress * MathF.Tau + reaction.Lane * 12f) * size * 0.3f;
            var center = new Vector2(
                stage.Min.X + stage.Width * (0.58f + 0.34f * reaction.Lane) + sway,
                stage.Max.Y - size - stage.Height * ReactionRise * rise);
            var alpha = progress < ReactionFadeStart
                ? 1f
                : 1f - (progress - ReactionFadeStart) / (1f - ReactionFadeStart);
            ReactionArt.Draw(drawList, PartyReactions.Tokens[reaction.Kind], center, size, alpha, 1f);
        }
    }

    public static void DrawBubbles(ImDrawListPtr drawList, Rect stage, ScreenChatFeed feed, float scale)
    {
        if (!feed.Enabled || stage.Width < MinimumStageWidth * scale)
        {
            return;
        }

        var now = Environment.TickCount64;
        var lines = feed.Lines;
        var newest = feed.Newest;
        var pad = BubblePad * scale;
        var widthStep = BubbleWidthStep * scale;
        var maxWidth = MathF.Min(MathF.Floor(stage.Width * BubbleWidthShare / widthStep) * widthStep,
            BubbleMaxWidth * scale);
        var textWidth = maxWidth - pad * 2f;
        var senderHeight = Typography.LineHeight(SenderStyle);
        var lineHeight = Typography.LineHeight(LineStyle);
        var left = stage.Min.X + BubbleInset * scale;
        var bottom = stage.Max.Y - BubbleInset * scale;
        for (var offset = 0; offset < lines.Length; offset++)
        {
            ref readonly var line = ref lines[(newest - offset + lines.Length) % lines.Length];
            if (!ScreenChatFeed.TryAge(line, now, out var age))
            {
                continue;
            }

            var wrapped = Typography.WrapText(line.Text, LineStyle, textWidth);
            var sender = Typography.FitText(line.Sender, textWidth, SenderStyle);
            var widest = Typography.Measure(sender, SenderStyle).X;
            for (var lineIndex = 0; lineIndex < wrapped.Length; lineIndex++)
            {
                widest = MathF.Max(widest, Typography.Measure(wrapped[lineIndex], LineStyle).X);
            }

            var height = pad * 2f + senderHeight + wrapped.Length * lineHeight;
            var top = bottom - height;
            if (top < stage.Min.Y + BubbleInset * scale)
            {
                return;
            }

            var alpha = age < BubbleFadeIn
                ? age / BubbleFadeIn
                : age > BubbleFadeOut ? (1f - age) / (1f - BubbleFadeOut) : 1f;
            var min = new Vector2(left, top);
            var max = new Vector2(left + MathF.Min(maxWidth, widest + pad * 2f), bottom);
            Squircle.Fill(drawList, min, max, BubbleRadius * scale,
                ImGui.GetColorU32(new Vector4(0f, 0f, 0f, BubbleBackdrop * alpha)));
            Typography.Draw(drawList, new Vector2(min.X + pad, min.Y + pad), sender,
                new Vector4(SenderInk.X, SenderInk.Y, SenderInk.Z, alpha), SenderStyle);
            var textY = min.Y + pad + senderHeight;
            for (var lineIndex = 0; lineIndex < wrapped.Length; lineIndex++)
            {
                Typography.Draw(drawList, new Vector2(min.X + pad, textY), wrapped[lineIndex],
                    new Vector4(LineInk.X, LineInk.Y, LineInk.Z, alpha), LineStyle);
                textY += lineHeight;
            }

            bottom = top - BubbleGap * scale;
        }
    }
}
