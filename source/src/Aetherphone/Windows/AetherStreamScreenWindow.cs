using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Video;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace Aetherphone.Windows;

internal sealed class AetherStreamScreenWindow : Window
{
    private const double ControlsIdleSeconds = 2.5d;
    private const ImGuiWindowFlags WindowFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
    private const ImGuiWindowFlags FullscreenFlags = WindowFlags | ImGuiWindowFlags.NoDecoration
        | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings;
    private static readonly Vector2 TextureSize = new(VideoEngine.ScreenWidth, VideoEngine.ScreenHeight);
    private static readonly Vector4 WhiteInk = Vector4.One;
    private static readonly Vector4 BackingInk = new(0f, 0f, 0f, 1f);
    private static readonly ControlInk ControlsInk = new(PhoneTheme.Default.Accent, WhiteInk, WhiteInk, WhiteInk);

    private readonly VideoSuite suite;
    private bool fullscreen;
    private bool restoreWindow;
    private Rect? windowBounds;
    private VideoFitMode fitMode;
    private double controlsActiveUntil;
    private Spring controlsFade;
    private float previousPhoneScale;
    private bool previousWindowHovered;
    private bool previousWindowFocused;
    private bool scrubbing;
    private float scrubValue;

    private bool CanDrive => !suite.WatchAlong.IsViewing || suite.WatchAlong.CanControlPlayback;

    internal bool IsFullscreen => fullscreen && IsOpen;

    internal AetherStreamScreenWindow(VideoSuite suite)
        : base("MogCast Screen###AetherStreamScreenWindow", WindowFlags)
    {
        this.suite = suite;
        Size = new Vector2(640f, 360f);
        SizeCondition = ImGuiCond.FirstUseEver;
        RespectCloseHotkey = true;
        IsOpen = false;
    }

    internal void OpenFullscreen()
    {
        fullscreen = true;
        controlsActiveUntil = double.MaxValue;
        IsOpen = true;
        BringToFront();
    }

    internal void OpenWindow()
    {
        fullscreen = false;
        restoreWindow = true;
        IsOpen = true;
        BringToFront();
    }

    public override void OnClose()
    {
        fullscreen = false;
        restoreWindow = true;
        scrubbing = false;
    }

    public override void PreDraw()
    {
        previousPhoneScale = UiScale.Phone;
        previousWindowHovered = UiInteract.WindowHovered;
        previousWindowFocused = UiInteract.WindowFocused;
        UiScale.SetPhone(1f);
        var style = ImGui.GetStyle();
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, fullscreen ? Vector2.Zero : style.WindowPadding);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, fullscreen ? 0f : style.WindowBorderSize);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, fullscreen ? 0f : style.WindowRounding);
        Flags = fullscreen ? FullscreenFlags : WindowFlags;
        Size = null;
        if (fullscreen)
        {
            var viewport = ImGui.GetMainViewport();
            ImGui.SetNextWindowViewport(viewport.ID);
            ImGui.SetNextWindowPos(viewport.Pos);
            ImGui.SetNextWindowSize(viewport.Size);
        }
        else if (restoreWindow)
        {
            if (windowBounds is { } bounds)
            {
                ImGui.SetNextWindowPos(bounds.Min);
                ImGui.SetNextWindowSize(bounds.Size);
            }
            else
            {
                ImGui.SetNextWindowSize(new Vector2(640f, 360f) * UiScale.Global);
            }

            restoreWindow = false;
        }
    }

    public override void PostDraw()
    {
        ImGui.PopStyleVar(3);
        UiScale.SetPhone(previousPhoneScale);
        UiInteract.SetWindowHovered(previousWindowHovered);
        UiInteract.SetWindowFocused(previousWindowFocused);
    }

    public override void Draw()
    {
        UiInteract.SetWindowHovered(ImGui.IsWindowHovered(
            ImGuiHoveredFlags.ChildWindows | ImGuiHoveredFlags.AllowWhenBlockedByActiveItem));
        UiInteract.SetWindowFocused(ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows));
        if (fullscreen && ImGui.IsKeyPressed(ImGuiKey.Escape, false))
        {
            IsOpen = false;
            return;
        }

        if (!fullscreen)
        {
            var position = ImGui.GetWindowPos();
            windowBounds = new Rect(position, position + ImGui.GetWindowSize());
        }

        var origin = ImGui.GetCursorScreenPos();
        var available = ImGui.GetContentRegionAvail();
        if (available.X <= 0f || available.Y <= 0f)
        {
            return;
        }

        var area = new Rect(origin, origin + available);
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(area.Min, area.Max, ImGui.GetColorU32(BackingInk));
        var engine = suite.Screen.Engine;
        if (!suite.Player.HasMedia)
        {
            if (fullscreen)
            {
                IsOpen = false;
            }
            else
            {
                Typography.DrawCentered(drawList, area.Center, Loc.T(L.AetherStream.NothingPlaying), WhiteInk,
                    TextStyles.Body);
            }

            return;
        }

        var handle = engine.ScreenViewHandle;
        var layout = VideoFrameLayout.Calculate(area, TextureSize, engine.VideoAspect,
            fullscreen ? fitMode : VideoFitMode.Fit);
        if (handle != nint.Zero && suite.Player.FrameVersion > 0)
        {
            drawList.AddImage(new ImTextureID(handle), layout.Frame.Min, layout.Frame.Max, layout.UvMin, layout.UvMax);
        }

        var scale = UiScale.Current;
        drawList.PushClipRect(area.Min, area.Max, true);
        VideoStageOverlay.DrawBubbles(drawList, layout.Frame, suite.ChatFeed, scale);
        VideoStageOverlay.DrawReactions(drawList, layout.Frame, suite.WatchAlong.Reactions, scale);
        if (Plugin.Cfg.VideoDanmaku)
        {
            VideoStageOverlay.DrawDanmaku(drawList, layout.Frame, suite.WatchAlong.Danmaku,
                Plugin.Cfg.VideoDanmakuLanes, scale);
        }

        drawList.PopClipRect();
        if (fullscreen)
        {
            DrawControls(drawList, area, scale);
        }
    }

    private void DrawControls(ImDrawListPtr drawList, Rect area, float scale)
    {
        var now = ImGui.GetTime();
        var input = ImGui.GetIO();
        if (CanDrive && ImGui.IsKeyPressed(ImGuiKey.Space, false))
        {
            TogglePlayback();
            controlsActiveUntil = now + ControlsIdleSeconds;
        }

        if (controlsActiveUntil == double.MaxValue || input.MouseDelta != Vector2.Zero
            || ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            controlsActiveUntil = now + ControlsIdleSeconds;
        }

        var opacity = Math.Clamp(controlsFade.Step(now < controlsActiveUntil ? 1f : 0f, Motion.Appear,
            input.DeltaTime), 0f, 1f);
        if (opacity <= 0.01f)
        {
            return;
        }

        var gap = Metrics.Space.Sm * scale;
        var height = Button.RegularHeight * scale;
        var left = area.Min.X + Metrics.Space.Md * scale;
        var top = area.Max.Y - height - Metrics.Space.Md * scale;
        var volumeHeight = 26f * scale;
        var volumeTop = top - height - gap * 2f - volumeHeight;
        var scrim = new Rect(new Vector2(area.Min.X, volumeTop - gap), area.Max);
        drawList.AddRectFilled(scrim.Min, scrim.Max, ImGui.GetColorU32(BackingInk with { W = opacity * 0.85f }));
        var volumeRight = area.Max.X - Metrics.Space.Md * scale;
        var volumeWidth = MathF.Min(260f * scale, volumeRight - left);
        if (volumeWidth > 0f)
        {
            DrawVolume(new Rect(new Vector2(volumeRight - volumeWidth, volumeTop),
                new Vector2(volumeRight, volumeTop + volumeHeight)), opacity);
        }

        var maxButtonWidth = MathF.Max(1f, (area.Width - Metrics.Space.Md * scale * 2f - gap * 2f) / 3f);
        DrawModeButton(drawList, ref left, top, height, gap, maxButtonWidth, Loc.T(L.AetherStream.FullscreenFit),
            VideoFitMode.Fit, opacity);
        DrawModeButton(drawList, ref left, top, height, gap, maxButtonWidth, Loc.T(L.AetherStream.FullscreenFill),
            VideoFitMode.Fill, opacity);
        var exitLabel = Loc.T(L.AetherStream.ExitFullscreen);
        var width = MathF.Min(Button.WidthFor(exitLabel, ButtonSize.Regular), area.Max.X - left - gap);
        if (width > 0f && Button.Draw(drawList, new Rect(new Vector2(left, top), new Vector2(left + width, top + height)),
                exitLabel, ControlsInk, ButtonStyle.Gray, id: "video.fullscreen.exit", opacity: opacity))
        {
            IsOpen = false;
        }

        var playCenter = new Vector2(area.Min.X + Metrics.Space.Md * scale + height * 0.5f,
            top - gap - height * 0.5f);
        if (CanDrive && TransportButton.Draw(playCenter, height * 0.5f,
                suite.Player.Progress.Paused ? TransportAction.Play : TransportAction.Pause,
                WhiteInk, WhiteInk, opacity, true))
        {
            TogglePlayback();
        }

        var progress = suite.Player.Progress;
        var track = new Rect(new Vector2(playCenter.X + height, playCenter.Y - Metrics.Stroke.Thin * scale),
            new Vector2(area.Max.X - Metrics.Space.Md * scale, playCenter.Y + Metrics.Stroke.Thin * scale));
        if (track.Width <= 0f || progress.Duration <= 0f)
        {
            return;
        }

        var normalized = scrubbing ? scrubValue : progress.Fraction;
        var updated = Scrubber.Draw(track, normalized, WhiteInk, WhiteInk with { W = 0.3f }, opacity);
        if (!CanDrive)
        {
            return;
        }

        if (Scrubber.IsHovered(track) && ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            scrubbing = true;
            scrubValue = updated;
        }
        else if (scrubbing)
        {
            scrubbing = false;
            var seconds = scrubValue * progress.Duration;
            if (suite.WatchAlong.IsViewing)
            {
                suite.WatchAlong.ControlSeek(seconds);
            }
            else
            {
                suite.Player.Seek(seconds);
            }
        }
    }

    private void DrawVolume(Rect row, float opacity)
    {
        var configuration = Plugin.Cfg;
        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, ImGui.GetStyle().Alpha * opacity);
        var result = VolumeSlider.Draw("video.fullscreen.volume", row, configuration.VideoVolume, PhoneTheme.Default);
        ImGui.PopStyleVar();
        if (result.Dragging && Math.Abs(result.Value - configuration.VideoVolume) > 0.001f)
        {
            configuration.VideoVolume = result.Value;
            suite.Player.SetVolume((int)(result.Value * 100f));
        }

        if (result.Released)
        {
            configuration.VideoVolume = result.Value;
            suite.Player.SetVolume((int)(result.Value * 100f));
            configuration.Save();
        }
    }

    private void TogglePlayback()
    {
        var paused = !suite.Player.Progress.Paused;
        if (suite.WatchAlong.IsViewing)
        {
            suite.WatchAlong.ControlPause(paused);
        }
        else
        {
            suite.Player.Pause(paused);
        }
    }

    private void DrawModeButton(ImDrawListPtr drawList, ref float left, float top, float height, float gap,
        float maxWidth, string label, VideoFitMode mode, float opacity)
    {
        var width = MathF.Min(Button.WidthFor(label, ButtonSize.Regular), maxWidth);
        var rect = new Rect(new Vector2(left, top), new Vector2(left + width, top + height));
        if (Button.Draw(drawList, rect, label, ControlsInk,
                fitMode == mode ? ButtonStyle.Prominent : ButtonStyle.Gray,
                id: mode == VideoFitMode.Fit ? "video.fullscreen.fit" : "video.fullscreen.fill", opacity: opacity))
        {
            fitMode = mode;
        }

        left += width + gap;
    }
}
