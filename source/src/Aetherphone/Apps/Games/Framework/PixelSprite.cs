using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Framework;

internal sealed class PixelSprite
{
    private const char Lit = '#';
    private const int MaxWidth = 32;

    private readonly uint[] rows;

    public readonly int Width;
    public readonly int Height;

    public PixelSprite(params string[] lines)
    {
        Height = lines.Length;
        rows = new uint[Height];
        var width = 0;
        for (var rowIndex = 0; rowIndex < Height; rowIndex++)
        {
            var line = lines[rowIndex];
            var lineWidth = Math.Min(line.Length, MaxWidth);
            width = Math.Max(width, lineWidth);
            uint bits = 0;
            for (var columnIndex = 0; columnIndex < lineWidth; columnIndex++)
            {
                if (line[columnIndex] == Lit)
                {
                    bits |= 1u << columnIndex;
                }
            }

            rows[rowIndex] = bits;
        }

        Width = width;
    }

    public bool IsLit(int column, int row) =>
        row >= 0 && row < Height && column >= 0 && column < Width && (rows[row] & (1u << column)) != 0;

    public void Draw(ImDrawListPtr drawList, Vector2 topLeft, float unit, uint color)
    {
        for (var rowIndex = 0; rowIndex < Height; rowIndex++)
        {
            var bits = rows[rowIndex];
            if (bits == 0)
            {
                continue;
            }

            var rowTop = topLeft.Y + rowIndex * unit;
            var rowBottom = rowTop + unit;
            var columnIndex = 0;
            while (columnIndex < Width)
            {
                if ((bits & (1u << columnIndex)) == 0)
                {
                    columnIndex++;
                    continue;
                }

                var runStart = columnIndex;
                while (columnIndex < Width && (bits & (1u << columnIndex)) != 0)
                {
                    columnIndex++;
                }

                drawList.AddRectFilled(new Vector2(topLeft.X + runStart * unit, rowTop),
                    new Vector2(topLeft.X + columnIndex * unit, rowBottom), color);
            }
        }
    }

    public void DrawCentered(ImDrawListPtr drawList, Vector2 center, float unit, uint color)
    {
        var topLeft = center - new Vector2(Width * unit * 0.5f, Height * unit * 0.5f);
        Draw(drawList, topLeft, unit, color);
    }
}
