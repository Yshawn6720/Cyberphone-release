using Aetherphone.Core.Video;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace Aetherphone.Windows;

internal sealed class VideoDebugWindow : Window, IDisposable
{
    private readonly VideoPlayer video;
    private readonly ScreenController screen;
    private string path = string.Empty;

    public VideoDebugWindow(VideoPlayer video, ScreenController screen)
        : base("Aetherphone: Video Decode Debug (Stage 3/6)###AetherphoneVideoDebug")
    {
        this.video = video;
        this.screen = screen;
        Size = new Vector2(760, 620);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        DrawTvSection();
        ImGui.Separator();
        DrawPlaybackSection();
    }

    private void DrawTvSection()
    {
        ImGui.TextUnformatted("Screen");
        if (screen.Engine.IsActive)
        {
            ImGui.TextColored(new Vector4(0.4f, 1f, 0.5f, 1f), "Active.");
            ImGui.TextUnformatted($"Position: {screen.Engine.ScreenPosition} Yaw: {screen.Engine.ScreenYaw:0.00} Scale: {screen.Engine.ScreenScale:0.00}");
        }
        else
        {
            ImGui.TextColored(new Vector4(1f, 0.8f, 0.3f, 1f), "Idle.");
        }
    }

    private void DrawPlaybackSection()
    {
        ImGui.InputText("Local file path or YouTube URL", ref path, 1000);
        ImGui.SameLine();
        if (ImGui.Button("Play"))
        {
            video.Play(path);
        }

        ImGui.SameLine();
        if (ImGui.Button("Stop"))
        {
            video.Stop();
        }

        ImGui.Text($"State: {video.State}");
        if (video.LastError is not null)
        {
            ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), video.LastError);
        }

        var progress = video.Progress;
        ImGui.Text($"Position: {progress.Position:F1}s / {progress.Duration:F1}s   Paused: {progress.Paused}");
        ImGui.Text($"Frames: {video.FrameVersion}");

        var handle = screen.Engine.ScreenViewHandle;
        if (handle == nint.Zero || !video.HasMedia)
        {
            ImGui.TextDisabled("No frame yet.");
            return;
        }

        var avail = ImGui.GetContentRegionAvail();
        var aspect = (float)VideoEngine.ScreenWidth / VideoEngine.ScreenHeight;
        var drawWidth = avail.X;
        var drawHeight = drawWidth / aspect;
        if (drawHeight > avail.Y && avail.Y > 0f)
        {
            drawHeight = avail.Y;
            drawWidth = drawHeight * aspect;
        }

        ImGui.Image(new ImTextureID(handle), new Vector2(drawWidth, drawHeight));
    }

    public void Dispose()
    {
        video.Stop();
    }
}
