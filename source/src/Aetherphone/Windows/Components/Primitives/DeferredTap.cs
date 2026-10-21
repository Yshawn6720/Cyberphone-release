using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal sealed class DeferredTap
{
    private const double DoubleClickWindow = 0.30;

    private string? payload;
    private double armedAt;

    public void Arm(string value)
    {
        payload = value;
        armedAt = ImGui.GetTime();
    }

    public void Cancel() => payload = null;

    public bool Ready(out string value)
    {
        value = string.Empty;
        if (payload is not { } armed)
        {
            return false;
        }

        if (DragScrollHost.AnyDragging)
        {
            payload = null;
            return false;
        }

        if (ImGui.GetTime() - armedAt < DoubleClickWindow)
        {
            return false;
        }

        payload = null;
        value = armed;
        return true;
    }
}
