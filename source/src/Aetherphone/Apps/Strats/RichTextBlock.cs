using Aetherphone.Core;
using Aetherphone.Core.Media;
using Aetherphone.Core.Strats;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Strats;

internal sealed class RichTextBlock
{
    private const float IndentUnits = 14f;
    private const float BulletUnits = 14f;
    private const float ParaGapUnits = 4f;
    private const float IconGapUnits = 3f;

    private readonly struct Piece
    {
        public readonly string Text;
        public readonly float X;
        public readonly float Width;
        public readonly int Line;
        public readonly bool Bold;
        public readonly string Color;
        public readonly string Icon;

        public Piece(string text, float x, float width, int line, bool bold, string color, string icon)
        {
            Text = text;
            X = x;
            Width = width;
            Line = line;
            Bold = bold;
            Color = color;
            Icon = icon;
        }
    }

    private sealed class Layout
    {
        public readonly List<Piece> Pieces = new();
        public readonly List<float> LineTops = new();
        public readonly List<float> LineHeights = new();
        public readonly List<int> BulletLines = new();
        public readonly List<float> BulletX = new();
        public float Height;
    }

    private readonly Dictionary<(GuideText Text, int Style, int Width), Layout> layouts = new();
    private readonly List<string> words = new();
    private float cachedScale;
    private int cachedFontGeneration;

    public float Measure(GuideText text, float width, in TextStyle style, float scale) =>
        Resolve(text, width, style, scale).Height;

    public float Draw(ImDrawListPtr drawList, Vector2 topLeft, GuideText text, float width, in TextStyle style,
        Vector4 ink, Vector4 mutedInk, float scale, RemoteImageCache images)
    {
        var layout = Resolve(text, width, style, scale);
        var bold = new TextStyle(style.Scale, FontWeight.SemiBold);
        var bulletColor = ImGui.GetColorU32(ink);
        for (var index = 0; index < layout.BulletLines.Count; index++)
        {
            var line = layout.BulletLines[index];
            var centerY = topLeft.Y + layout.LineTops[line] + layout.LineHeights[line] * 0.5f;
            drawList.AddCircleFilled(new Vector2(topLeft.X + layout.BulletX[index], centerY), 2.2f * scale,
                bulletColor, 10);
        }

        for (var index = 0; index < layout.Pieces.Count; index++)
        {
            var piece = layout.Pieces[index];
            var lineTop = topLeft.Y + layout.LineTops[piece.Line];
            var lineHeight = layout.LineHeights[piece.Line];
            var position = new Vector2(topLeft.X + piece.X, lineTop);
            if (piece.Icon.Length > 0)
            {
                DrawIcon(drawList, position, lineHeight, piece.Icon, images, mutedInk);
                continue;
            }

            var color = StratsInk.Resolve(piece.Color, ink, mutedInk);
            Typography.Draw(drawList, position, piece.Text, color, piece.Bold ? bold : style);
        }

        return layout.Height;
    }

    private static void DrawIcon(ImDrawListPtr drawList, Vector2 position, float lineHeight, string iconKey,
        RemoteImageCache images, Vector4 mutedInk)
    {
        var size = lineHeight * 0.92f;
        var min = new Vector2(position.X, position.Y + (lineHeight - size) * 0.5f);
        var max = new Vector2(min.X + size, min.Y + size);
        var texture = images.Sized(StratsContent.Url(iconKey), size);
        if (texture is null)
        {
            drawList.AddRectFilled(min, max, ImGui.GetColorU32(Palette.WithAlpha(mutedInk, 0.25f)), size * 0.25f);
            return;
        }

        drawList.AddImageRounded(texture.Handle, min, max, Vector2.Zero, Vector2.One, 0xFFFFFFFFu, size * 0.2f);
    }

    private Layout Resolve(GuideText text, float width, in TextStyle style, float scale)
    {
        var fontGeneration = Plugin.Fonts.Generation;
        if (cachedScale != scale || cachedFontGeneration != fontGeneration)
        {
            layouts.Clear();
            cachedScale = scale;
            cachedFontGeneration = fontGeneration;
        }

        var key = (text, style.GetHashCode(), (int)MathF.Round(width * 2f));
        if (layouts.TryGetValue(key, out var layout))
        {
            return layout;
        }

        layout = BuildLayout(text, width, style, scale);
        layouts[key] = layout;
        return layout;
    }

    private Layout BuildLayout(GuideText text, float width, in TextStyle style, float scale)
    {
        var layout = new Layout();
        var bold = new TextStyle(style.Scale, FontWeight.SemiBold);
        var lineHeight = Typography.LineHeight(style);
        var spaceWidth = Typography.Measure(" ", style).X;
        var iconSize = lineHeight * 0.92f;
        var y = 0f;
        var line = 0;
        for (var paraIndex = 0; paraIndex < text.Paras.Length; paraIndex++)
        {
            var para = text.Paras[paraIndex];
            var left = para.Indent * IndentUnits * scale + (para.Bullet ? BulletUnits * scale : 0f);
            if (para.Bullet)
            {
                layout.BulletLines.Add(line);
                layout.BulletX.Add(left - BulletUnits * scale * 0.55f);
            }

            layout.LineTops.Add(y);
            layout.LineHeights.Add(lineHeight);
            var x = left;
            var pieceCount = 0;
            var trailingSpace = false;
            for (var runIndex = 0; runIndex < para.Runs.Length; runIndex++)
            {
                var run = para.Runs[runIndex];
                if (run.Icon.Length > 0)
                {
                    if (x + iconSize > width && x > left)
                    {
                        line++;
                        y += lineHeight;
                        layout.LineTops.Add(y);
                        layout.LineHeights.Add(lineHeight);
                        x = left;
                    }

                    layout.Pieces.Add(new Piece(string.Empty, x, iconSize, line, false, string.Empty, run.Icon));
                    x += iconSize + IconGapUnits * scale;
                    pieceCount++;
                    trailingSpace = false;
                    continue;
                }

                SplitWords(run.Text);
                var runStyle = run.Bold ? bold : style;
                var leadingSpace = run.Text.Length > 0 && run.Text[0] == ' ';
                for (var wordIndex = 0; wordIndex < words.Count; wordIndex++)
                {
                    var word = words[wordIndex];
                    var wordWidth = Typography.Measure(word, runStyle).X;
                    var needsSpace = x > left && pieceCount > 0 && (wordIndex > 0 || leadingSpace || trailingSpace);
                    var advance = needsSpace ? spaceWidth : 0f;
                    if (x + advance + wordWidth > width && x > left)
                    {
                        line++;
                        y += lineHeight;
                        layout.LineTops.Add(y);
                        layout.LineHeights.Add(lineHeight);
                        x = left;
                        advance = 0f;
                    }

                    x += advance;
                    layout.Pieces.Add(new Piece(word, x, wordWidth, line, run.Bold, run.Color, string.Empty));
                    x += wordWidth;
                    pieceCount++;
                }

                trailingSpace = run.Text.Length > 0 && run.Text[^1] == ' ';
            }

            line++;
            y += lineHeight + ParaGapUnits * scale;
        }

        layout.Height = MathF.Max(0f, y - ParaGapUnits * scale);
        return layout;
    }

    private void SplitWords(string text)
    {
        words.Clear();
        var start = -1;
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == ' ')
            {
                if (start >= 0)
                {
                    words.Add(text.Substring(start, index - start));
                    start = -1;
                }

                continue;
            }

            if (start < 0)
            {
                start = index;
            }
        }

        if (start >= 0)
        {
            words.Add(text.Substring(start));
        }
    }

    public void Clear() => layouts.Clear();
}

internal static class StratsInk
{
    private static readonly Vector4 Red = new(0.98f, 0.42f, 0.42f, 1f);
    private static readonly Vector4 Orange = new(0.98f, 0.62f, 0.30f, 1f);
    private static readonly Vector4 Yellow = new(0.97f, 0.80f, 0.30f, 1f);
    private static readonly Vector4 Green = new(0.40f, 0.82f, 0.50f, 1f);
    private static readonly Vector4 Blue = new(0.45f, 0.65f, 1.00f, 1f);
    private static readonly Vector4 Purple = new(0.72f, 0.56f, 1.00f, 1f);
    private static readonly Vector4 Pink = new(0.98f, 0.55f, 0.80f, 1f);
    private static readonly Vector4 Cyan = new(0.40f, 0.82f, 0.90f, 1f);

    public static Vector4 Resolve(string name, Vector4 ink, Vector4 mutedInk) =>
        name switch
        {
            "red" => Red,
            "orange" => Orange,
            "yellow" => Yellow,
            "green" => Green,
            "blue" => Blue,
            "purple" => Purple,
            "pink" => Pink,
            "cyan" => Cyan,
            "muted" => mutedInk,
            _ => ink,
        };
}
