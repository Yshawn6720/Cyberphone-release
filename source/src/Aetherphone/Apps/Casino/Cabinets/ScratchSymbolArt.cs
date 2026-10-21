using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Cabinets;

internal static class ScratchSymbolArt
{
    private static readonly int[] SlotsArtForSymbol = { 0, 1, 2, 6, 5, 4, 7 };

    public static void Draw(ImDrawListPtr drawList, int symbolId, Vector2 center, float extent, float alpha = 1f)
    {
        var artIndex = symbolId >= 0 && symbolId < SlotsArtForSymbol.Length ? SlotsArtForSymbol[symbolId] : 0;
        SlotsSymbolArt.Draw(drawList, artIndex, center, extent, alpha);
    }
}
