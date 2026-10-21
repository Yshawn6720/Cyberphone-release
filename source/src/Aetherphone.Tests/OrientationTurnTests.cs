using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Shell;
using Aetherphone.Core.Theme;
using Xunit;

namespace Aetherphone.Tests;

public sealed class OrientationTurnTests
{
    private const float PortraitWidth = 486f;
    private const float Growth = PhoneSizeCatalog.LandscapeGrowth;
    private const float SeamTolerance = 1.5f;
    private const float FrameSeconds = 1f / 60f;

    private static readonly Rect Unbounded = new(new Vector2(-100000f, -100000f), new Vector2(100000f, 100000f));

    [Fact]
    public void RestingOrientationsCarryNoTurn()
    {
        var turn = new OrientationTurn();
        Assert.False(turn.Turning);
        Assert.False(turn.ShowsLandscape);
        Assert.Equal(0f, turn.Angle, 1e-5f);
        Assert.Equal(1f, turn.ScaleFor(Growth), 1e-5f);
        Assert.Equal(1f, turn.ContentAlpha, 1e-5f);

        turn.Advance(OrientationTurn.TurnSeconds, true, true);
        Assert.False(turn.Turning);
        Assert.True(turn.ShowsLandscape);
        Assert.Equal(0f, turn.Angle, 1e-5f);
        Assert.Equal(1f, turn.ScaleFor(Growth), 1e-5f);
        Assert.Equal(1f, turn.ContentAlpha, 1e-5f);
    }

    [Fact]
    public void TheLayoutSwapHappensWhileTheScreenIsBlank()
    {
        var turn = new OrientationTurn();
        var wasLandscape = turn.ShowsLandscape;
        for (var frame = 0; frame < 60; frame++)
        {
            turn.Advance(FrameSeconds, true, true);
            if (turn.ShowsLandscape != wasLandscape)
            {
                Assert.Equal(0f, turn.ContentAlpha, 1e-5f);
            }

            wasLandscape = turn.ShowsLandscape;
        }

        Assert.True(turn.ShowsLandscape);
    }

    [Fact]
    public void ThePortraitAndLandscapeHalvesMeetAtTheSameFootprint()
    {
        var before = new OrientationTurn();
        before.Advance(OrientationTurn.TurnSeconds * 0.4999f, true, true);
        var after = new OrientationTurn();
        after.Advance(OrientationTurn.TurnSeconds * 0.5001f, true, true);
        Assert.False(before.ShowsLandscape);
        Assert.True(after.ShowsLandscape);

        var portrait = Footprint(Portrait(), before);
        var landscape = Footprint(Landscape(), after);
        Assert.Equal(portrait.Min.X, landscape.Min.X, SeamTolerance);
        Assert.Equal(portrait.Min.Y, landscape.Min.Y, SeamTolerance);
        Assert.Equal(portrait.Max.X, landscape.Max.X, SeamTolerance);
        Assert.Equal(portrait.Max.Y, landscape.Max.Y, SeamTolerance);
    }

    [Fact]
    public void TheTurnCarriesThePortraitRightRailOntoTheLandscapeTopRail()
    {
        var turn = new OrientationTurn();
        turn.Advance(OrientationTurn.TurnSeconds * 0.4999f, true, true);
        var device = Portrait();
        var transform = Transform(device, turn);
        var rightRail = transform.Map(new Vector2(device.Max.X, device.Center.Y));
        var bottomRail = transform.Map(new Vector2(device.Center.X, device.Max.Y));
        Assert.True(rightRail.Y < device.Center.Y);
        Assert.True(rightRail.X > device.Center.X);
        Assert.True(bottomRail.X > device.Center.X);
        Assert.True(bottomRail.Y > device.Center.Y);
    }

    [Fact]
    public void ReversingMidTurnWalksBackToPortrait()
    {
        var turn = new OrientationTurn();
        for (var frame = 0; frame < 8; frame++)
        {
            turn.Advance(FrameSeconds, true, true);
        }

        Assert.True(turn.Turning);
        for (var frame = 0; frame < 60; frame++)
        {
            turn.Advance(FrameSeconds, false, true);
        }

        Assert.False(turn.Turning);
        Assert.False(turn.ShowsLandscape);
    }

    [Fact]
    public void TravelRunsFromTheOriginToTheDestination()
    {
        var turn = new OrientationTurn();
        Assert.Equal(0f, turn.TravelTo(true), 1e-5f);
        Assert.Equal(1f, turn.TravelTo(false), 1e-5f);

        turn.Advance(OrientationTurn.TurnSeconds * 0.5f, true, true);
        Assert.Equal(0.5f, turn.TravelTo(true), 1e-4f);
        Assert.Equal(0.5f, turn.TravelTo(false), 1e-4f);

        turn.Advance(OrientationTurn.TurnSeconds, true, true);
        Assert.Equal(1f, turn.TravelTo(true), 1e-5f);
        Assert.Equal(0f, turn.TravelTo(false), 1e-5f);
    }

    [Fact]
    public void AFrozenPhoneSnapsInsteadOfTurning()
    {
        var turn = new OrientationTurn();
        turn.Advance(FrameSeconds, true, false);
        Assert.False(turn.Turning);
        Assert.True(turn.ShowsLandscape);
    }

    private static Rect Portrait()
    {
        var size = PhoneSizeCatalog.SizeFor(PortraitWidth);
        return new Rect(Vector2.Zero, size);
    }

    private static Rect Landscape()
    {
        var size = PhoneSizeCatalog.LandscapeSizeFor(PortraitWidth * Growth);
        var portrait = Portrait();
        return new Rect(portrait.Center - size * 0.5f, portrait.Center + size * 0.5f);
    }

    private static LayerTransform Transform(Rect device, OrientationTurn turn) =>
        LayerTransform.Turn(device.Center, turn.Angle, turn.ScaleFor(Growth), Unbounded);

    private static Rect Footprint(Rect device, OrientationTurn turn)
    {
        var transform = Transform(device, turn);
        var first = transform.Map(device.Min);
        var second = transform.Map(new Vector2(device.Max.X, device.Min.Y));
        var third = transform.Map(device.Max);
        var fourth = transform.Map(new Vector2(device.Min.X, device.Max.Y));
        var min = Vector2.Min(Vector2.Min(first, second), Vector2.Min(third, fourth));
        var max = Vector2.Max(Vector2.Max(first, second), Vector2.Max(third, fourth));
        return new Rect(min, max);
    }
}
