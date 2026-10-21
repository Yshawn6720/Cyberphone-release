using Aetherphone.Core;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;

namespace Aetherphone.Windows.Components;

internal sealed class ArtworkCache : IDisposable
{
    private const int Size = 256;
    private readonly ITextureProvider textures;
    private readonly Dictionary<int, IDalamudTextureWrap> cache = new();
    private readonly Dictionary<int, IDalamudTextureWrap> placeholders = new();
    private readonly HashSet<int> pending = new();
    private bool disposed;

    public ArtworkCache(ITextureProvider textures)
    {
        this.textures = textures;
    }

    public ImTextureID Handle(int seed)
    {
        if (cache.TryGetValue(seed, out var wrap))
        {
            return wrap.Handle;
        }

        if (pending.Add(seed))
        {
            var swatch = ArtGradient.From(seed);
            _ = Task.Run(() =>
            {
                var pixels = Rasterize(swatch, Size);
                return Plugin.Framework.RunOnFrameworkThread(() => Store(seed, pixels));
            });
        }

        return PlaceholderHandle(seed);
    }

    public ImTextureID HandleForName(string value) => Handle(ArtGradient.Seed(value));

    private void Store(int seed, byte[] pixels)
    {
        pending.Remove(seed);
        if (disposed || cache.ContainsKey(seed))
        {
            return;
        }

        try
        {
            cache[seed] = textures.CreateFromRaw(RawImageSpecification.Rgba32(Size, Size), pixels,
                $"Aetherphone.Art.{seed}");
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, $"[Artwork] creating gradient texture {seed} failed");
        }
    }

    private ImTextureID PlaceholderHandle(int seed)
    {
        if (placeholders.TryGetValue(seed, out var placeholder))
        {
            return placeholder.Handle;
        }

        var swatch = ArtGradient.From(seed);
        var mid = Vector4.Lerp(swatch.Top, swatch.Bottom, 0.5f);
        var pixel = new[] { ToByte(mid.X), ToByte(mid.Y), ToByte(mid.Z), (byte)255 };
        var wrap = textures.CreateFromRaw(RawImageSpecification.Rgba32(1, 1), pixel,
            $"Aetherphone.Art.Flat.{seed}");
        placeholders[seed] = wrap;
        return wrap.Handle;
    }

    private static byte[] Rasterize(ArtGradient.Swatch swatch, int size)
    {
        var pixels = new byte[size * size * 4];
        var last = size > 1 ? size - 1 : 1;
        var glowCenter = new Vector2(size * 0.30f, size * 0.26f);
        var glowRadius = size * 0.85f;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var diagonal = (x + y) / (2f * last);
                var color = Vector4.Lerp(swatch.Top, swatch.Bottom, diagonal);
                var deltaX = x - glowCenter.X;
                var deltaY = y - glowCenter.Y;
                var distance = MathF.Sqrt(deltaX * deltaX + deltaY * deltaY);
                var glow = Math.Clamp(1f - distance / glowRadius, 0f, 1f);
                glow *= glow * 0.45f;
                color = Vector4.Lerp(color, swatch.Glow, glow);
                var index = (y * size + x) * 4;
                pixels[index] = ToByte(color.X);
                pixels[index + 1] = ToByte(color.Y);
                pixels[index + 2] = ToByte(color.Z);
                pixels[index + 3] = 255;
            }
        }

        return pixels;
    }

    private static byte ToByte(float value) => (byte)Math.Clamp((int)(value * 255f + 0.5f), 0, 255);

    public void Dispose()
    {
        disposed = true;
        foreach (var wrap in cache.Values)
        {
            wrap.Dispose();
        }

        foreach (var wrap in placeholders.Values)
        {
            wrap.Dispose();
        }

        cache.Clear();
        placeholders.Clear();
    }
}
