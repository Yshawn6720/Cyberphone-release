using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Xunit;

namespace Aetherphone.Tests;

public sealed class LayerTransformTests
{
    private const float Tolerance = 1e-4f;
    private static readonly Rect Screen = new(new Vector2(100f, 50f), new Vector2(460f, 830f));

    [Fact]
    public void FitMapsTheSourceCornersOntoTheCardWidth()
    {
        var card = new Rect(new Vector2(200f, 300f), new Vector2(290f, 420f));
        var transform = LayerTransform.Fit(Screen, card, card);
        var min = transform.Map(Screen.Min);
        var topRight = transform.Map(new Vector2(Screen.Max.X, Screen.Min.Y));
        Assert.Equal(card.Min.X, min.X, Tolerance);
        Assert.Equal(card.Min.Y, min.Y, Tolerance);
        Assert.Equal(card.Max.X, topRight.X, Tolerance);
        Assert.Equal(card.Min.Y, topRight.Y, Tolerance);
        Assert.Equal(card.Width / Screen.Width, transform.Scale, Tolerance);
    }

    [Fact]
    public void ScaleAboutKeepsThePivotFixed()
    {
        var pivot = new Vector2(180f, 400f);
        var transform = LayerTransform.ScaleAbout(pivot, 1.32f, Screen);
        var mapped = transform.Map(pivot);
        Assert.Equal(pivot.X, mapped.X, Tolerance);
        Assert.Equal(pivot.Y, mapped.Y, Tolerance);
        var away = transform.Map(pivot + new Vector2(100f, 0f));
        Assert.Equal(pivot.X + 132f, away.X, Tolerance);
    }

    [Fact]
    public void TranslateIsNotIdentityButIdentityIs()
    {
        Assert.True(LayerTransform.Identity(Screen).IsIdentity);
        Assert.False(LayerTransform.Translate(new Vector2(0f, 12f), Screen).IsIdentity);
        Assert.False(LayerTransform.ScaleAbout(Screen.Center, 1f, Screen, 0.5f).IsIdentity);
    }

    [Fact]
    public void MapClipIntersectsWithTheLayerClip()
    {
        var card = new Rect(new Vector2(200f, 300f), new Vector2(290f, 420f));
        var transform = LayerTransform.Fit(Screen, card, card);
        var clip = transform.MapClip(new Vector4(Screen.Min.X, Screen.Min.Y, Screen.Max.X, Screen.Max.Y));
        Assert.Equal(card.Min.X, clip.X, Tolerance);
        Assert.Equal(card.Min.Y, clip.Y, Tolerance);
        Assert.Equal(card.Max.X, clip.Z, Tolerance);
        Assert.Equal(card.Max.Y, clip.W, Tolerance);
    }

    [Fact]
    public void MapClipCollapsesRectanglesThatLeaveTheLayerClip()
    {
        var band = new Rect(Screen.Min, new Vector2(Screen.Max.X, Screen.Min.Y + 10f));
        var transform = LayerTransform.Identity(band);
        var below = transform.MapClip(new Vector4(Screen.Min.X, Screen.Min.Y + 200f, Screen.Max.X, Screen.Max.Y));
        Assert.True(below.Z <= below.X || below.W <= below.Y);
    }

    [Fact]
    public void MapColorScalesOnlyTheAlphaByte()
    {
        const uint opaqueRed = 0xFF0000FF;
        var half = LayerTransform.ScaleAbout(Vector2.Zero, 1f, Screen, 0.5f);
        var faded = half.MapColor(opaqueRed);
        Assert.Equal(0x000000FFu, faded & 0x00FFFFFF);
        Assert.InRange(faded >> 24, 127u, 128u);
        Assert.Equal(opaqueRed, LayerTransform.Identity(Screen).MapColor(opaqueRed));
    }
}
