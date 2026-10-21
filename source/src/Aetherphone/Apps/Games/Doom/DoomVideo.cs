using Aetherphone.Core;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using ManagedDoom;
using ManagedDoom.Video;

namespace Aetherphone.Apps.Games.Doom;

internal sealed class DoomVideo : IVideo, IDisposable
{
    private const string TextureName = "Aetherphone.Doom.Frame";
    private readonly Renderer renderer;
    private readonly byte[] pixels;
    private IDalamudTextureWrap? wrap;
    private bool dirty;

    public DoomVideo(Config config, GameContent content)
    {
        renderer = new Renderer(config, content);
        pixels = new byte[4 * renderer.Width * renderer.Height];
    }

    public int Width => renderer.Width;
    public int Height => renderer.Height;

    public void Render(ManagedDoom.Doom doom, Fixed frameFrac)
    {
        renderer.Render(doom, pixels, frameFrac);
        dirty = true;
    }

    public void Present(ImDrawListPtr drawList, Rect screen)
    {
        if (dirty)
        {
            var next = Plugin.TextureProvider.CreateFromRaw(RawImageSpecification.Rgba32(renderer.Height, renderer.Width),
                pixels, TextureName);
            wrap?.Dispose();
            wrap = next;
            dirty = false;
        }

        if (wrap is null)
        {
            return;
        }

        var topLeft = screen.Min;
        var topRight = new Vector2(screen.Max.X, screen.Min.Y);
        var bottomRight = screen.Max;
        var bottomLeft = new Vector2(screen.Min.X, screen.Max.Y);
        drawList.AddImageQuad(wrap.Handle, topLeft, topRight, bottomRight, bottomLeft, new Vector2(0f, 0f),
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f), 0xFFFFFFFF);
    }

    public void InitializeWipe()
    {
        renderer.InitializeWipe();
    }

    public bool HasFocus() => true;

    public int MaxWindowSize => renderer.MaxWindowSize;

    public int WindowSize
    {
        get => renderer.WindowSize;
        set => renderer.WindowSize = value;
    }

    public bool DisplayMessage
    {
        get => renderer.DisplayMessage;
        set => renderer.DisplayMessage = value;
    }

    public int MaxGammaCorrectionLevel => renderer.MaxGammaCorrectionLevel;

    public int GammaCorrectionLevel
    {
        get => renderer.GammaCorrectionLevel;
        set => renderer.GammaCorrectionLevel = value;
    }

    public int WipeBandCount => renderer.WipeBandCount;
    public int WipeHeight => renderer.WipeHeight;

    public void Dispose()
    {
        wrap?.Dispose();
        wrap = null;
    }
}
