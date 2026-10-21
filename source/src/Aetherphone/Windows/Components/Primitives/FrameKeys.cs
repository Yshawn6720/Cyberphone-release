using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class FrameKeys
{
    private static int frame = -1;
    private static int ordinal;

    public static uint Next(string seed)
    {
        var current = ImGui.GetFrameCount();
        if (current != frame)
        {
            frame = current;
            ordinal = 0;
        }

        return (uint)HashCode.Combine(ImGui.GetID(seed), ordinal++);
    }
}
