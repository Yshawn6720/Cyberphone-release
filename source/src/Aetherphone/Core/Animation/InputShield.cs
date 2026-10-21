using Dalamud.Bindings.ImGui;

namespace Aetherphone.Core.Animation;

internal readonly ref struct InputShield
{
    private static readonly Vector2 OffScreen = new(-100000f, -100000f);
    private static int engagedDepth;
    private readonly Vector2 saved;
    private readonly bool restores;
    private readonly bool counted;

    public static bool Active => engagedDepth > 0;

    private InputShield(Vector2 saved, bool restores, bool counted)
    {
        this.saved = saved;
        this.restores = restores;
        this.counted = counted;
    }

    public static InputShield Engage(bool active = true)
    {
        if (!active)
        {
            return new InputShield(default, false, false);
        }

        var io = ImGui.GetIO();
        var shield = new InputShield(io.MousePos, true, true);
        engagedDepth++;
        io.MousePos = OffScreen;
        return shield;
    }

    public static InputShield Warp(in LayerTransform transform)
    {
        var io = ImGui.GetIO();
        var shield = new InputShield(io.MousePos, true, false);
        io.MousePos = transform.Unmap(io.MousePos);
        return shield;
    }

    public void Dispose()
    {
        if (!restores)
        {
            return;
        }

        if (counted)
        {
            engagedDepth--;
        }

        ImGui.GetIO().MousePos = saved;
    }
}
