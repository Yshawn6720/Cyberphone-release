using System.Diagnostics;
using Aetherphone.Core;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class PerfHud
{
    private const int WindowFrames = 240;
    private const float EmaRate = 0.08f;

    private static readonly float[] FrameMilliseconds = new float[WindowFrames];
    private static readonly Stopwatch ShellWatch = new();
    private static int cursor;
    private static float emaMilliseconds;
    private static float shellMilliseconds;
    private static int cachedTenthsKey = -1;
    private static string cachedLine = string.Empty;

    public static void BeginShell()
    {
        ShellWatch.Restart();
    }

    public static void EndShell()
    {
        ShellWatch.Stop();
        shellMilliseconds = (float)ShellWatch.Elapsed.TotalMilliseconds;
    }

    public static void Draw(Rect device, float scale)
    {
        var frame = ImGui.GetIO().DeltaTime * 1000f;
        FrameMilliseconds[cursor] = frame;
        cursor = (cursor + 1) % WindowFrames;
        emaMilliseconds += (frame - emaMilliseconds) * EmaRate;
        var worst = 0f;
        for (var index = 0; index < WindowFrames; index++)
        {
            if (FrameMilliseconds[index] > worst)
            {
                worst = FrameMilliseconds[index];
            }
        }

        var key = ((int)(emaMilliseconds * 10f) << 20) ^ ((int)(worst * 10f) << 10) ^ (int)(shellMilliseconds * 10f);
        if (key != cachedTenthsKey)
        {
            cachedTenthsKey = key;
            cachedLine = $"{emaMilliseconds:0.0} ms  worst {worst:0.0}  shell {shellMilliseconds:0.00}";
        }

        var drawList = ImGui.GetForegroundDrawList();
        var size = Typography.Measure(cachedLine, TextStyles.Caption1);
        var position = new Vector2(device.Min.X + 10f * scale, device.Min.Y - size.Y - 4f * scale);
        var pad = 4f * scale;
        drawList.AddRectFilled(position - new Vector2(pad, pad), position + size + new Vector2(pad, pad),
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.55f)), 5f * scale);
        var color = worst > 33f ? new Vector4(0.98f, 0.45f, 0.35f, 1f) :
            worst > 20f ? new Vector4(0.98f, 0.80f, 0.35f, 1f) : new Vector4(0.55f, 0.95f, 0.55f, 1f);
        Typography.Draw(drawList, position, cachedLine, color, TextStyles.Caption1);
    }
}
