using System.Numerics;
using Aetherphone.Core.Video;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ScreenGeometryTests
{
    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(1.2f, 0.6f)]
    [InlineData(-2.4f, -0.8f)]
    public void LargeScreenFacesTheCameraAndCentersOnItsSightline(float yaw, float pitch)
    {
        var camera = new Vector3(100f, 7f, -80f);
        var forward = Vector3.Transform(-Vector3.UnitZ, Quaternion.CreateFromYawPitchRoll(yaw, pitch, 0f));

        Assert.True(ScreenGeometry.TryLargeScreen(camera, camera + forward, 0.8f, 16f / 9f, out var pose));

        var toScreen = Vector3.Normalize(pose.Position - camera);
        Assert.InRange(Vector3.Distance(forward, toScreen), 0f, 0.0001f);
        Assert.InRange(Vector3.Distance(-forward, ScreenGeometry.Facing(pose)), 0f, 0.0001f);
    }

    [Theory]
    [InlineData(16f, 9f)]
    [InlineData(21f, 9f)]
    [InlineData(9f, 16f)]
    public void LargeScreenFitsInsideTheCameraViewport(float width, float height)
    {
        const float fieldOfView = 0.8f;
        var camera = new Vector3(3f, 7f, -5f);
        var target = new Vector3(-2f, 4f, 2f);
        var aspect = width / height;
        Assert.True(ScreenGeometry.TryLargeScreen(camera, target, fieldOfView, aspect, out var pose));
        var projection = Matrix4x4.CreateLookAt(camera, target, Vector3.UnitY)
            * Matrix4x4.CreatePerspectiveFieldOfView(fieldOfView, aspect, 0.1f, 1000f);
        var largestExtent = 0f;
        for (var horizontal = -1; horizontal <= 1; horizontal += 2)
        {
            for (var vertical = -1; vertical <= 1; vertical += 2)
            {
                var corner = ScreenGeometry.Corner(pose, horizontal, vertical);
                var projected = Vector4.Transform(new Vector4(corner, 1f), projection);
                Assert.True(projected.W > 0f);
                var extent = MathF.Max(MathF.Abs(projected.X / projected.W), MathF.Abs(projected.Y / projected.W));
                Assert.InRange(extent, 0f, 0.8501f);
                largestExtent = MathF.Max(largestExtent, extent);
            }
        }

        Assert.Equal(0.85f, largestExtent, 3);
    }

    [Fact]
    public void LargeScreenRespectsTheWorldScreenScaleLimit()
    {
        Assert.True(ScreenGeometry.TryLargeScreen(Vector3.Zero, -Vector3.UnitZ, 2f, 2f, out var pose));
        Assert.Equal(VideoEngine.MaxScreenScale, pose.Scale);
    }

    [Fact]
    public void LargeScreenRejectsAnUnavailableCamera()
    {
        Assert.False(ScreenGeometry.TryLargeScreen(Vector3.Zero, Vector3.Zero, 0.8f, 1f, out _));
        Assert.False(ScreenGeometry.TryLargeScreen(Vector3.Zero, -Vector3.UnitZ, 0f, 1f, out _));
        Assert.False(ScreenGeometry.TryLargeScreen(Vector3.Zero, -Vector3.UnitZ, 0.8f, float.NaN, out _));
    }

    [Theory]
    [InlineData((byte)ScreenWallLayout.SideBySide, 16f, 9f)]
    [InlineData((byte)ScreenWallLayout.SideBySide, 4f, 3f)]
    [InlineData((byte)ScreenWallLayout.SideBySide, 21f, 9f)]
    [InlineData((byte)ScreenWallLayout.SideBySide, 9f, 16f)]
    [InlineData((byte)ScreenWallLayout.Stacked, 16f, 9f)]
    [InlineData((byte)ScreenWallLayout.Stacked, 4f, 3f)]
    [InlineData((byte)ScreenWallLayout.Stacked, 21f, 9f)]
    [InlineData((byte)ScreenWallLayout.Stacked, 9f, 16f)]
    public void ScreenWallJoinsBothScreensEdgeToEdgeAndFitsTheView(byte layout, float width, float height)
    {
        const float fieldOfView = 0.8f;
        var camera = new Vector3(3f, 7f, -5f);
        var target = new Vector3(-2f, 4f, 2f);
        var aspect = width / height;
        var stacked = (ScreenWallLayout)layout == ScreenWallLayout.Stacked;

        Assert.True(ScreenGeometry.TryScreenWall(camera, target, fieldOfView, aspect, (ScreenWallLayout)layout,
            out var first, out var second));

        Assert.Equal(first with { Position = second.Position }, second);
        for (var along = -1; along <= 1; along += 2)
        {
            var firstEdge = stacked
                ? ScreenGeometry.Corner(first, along, -1f)
                : ScreenGeometry.Corner(first, 1f, along);
            var secondEdge = stacked
                ? ScreenGeometry.Corner(second, along, 1f)
                : ScreenGeometry.Corner(second, -1f, along);
            Assert.InRange(Vector3.Distance(firstEdge, secondEdge), 0f, 0.0001f);
        }

        var projection = Matrix4x4.CreateLookAt(camera, target, Vector3.UnitY)
            * Matrix4x4.CreatePerspectiveFieldOfView(fieldOfView, aspect, 0.1f, 1000f);
        var firstCenter = Project(first.Position, projection);
        var secondCenter = Project(second.Position, projection);
        if (stacked)
        {
            Assert.True(firstCenter.Y > secondCenter.Y);
        }
        else
        {
            Assert.True(firstCenter.X < secondCenter.X);
        }

        var largestExtent = MathF.Max(LargestCornerExtent(first, projection),
            LargestCornerExtent(second, projection));
        Assert.Equal(0.85f, largestExtent, 3);
    }

    [Theory]
    [InlineData((byte)ScreenWallLayout.SideBySide)]
    [InlineData((byte)ScreenWallLayout.Stacked)]
    public void ScreenWallStaysSeamlessWhenTheScaleHitsItsLimit(byte layout)
    {
        Assert.True(ScreenGeometry.TryScreenWall(Vector3.Zero, -Vector3.UnitZ, 2.5f, 2f, (ScreenWallLayout)layout,
            out var first, out var second));

        Assert.Equal(VideoEngine.MaxScreenScale, first.Scale);
        var expected = (ScreenWallLayout)layout == ScreenWallLayout.Stacked
            ? 2f * ScreenGeometry.HalfHeight * first.Scale
            : 2f * ScreenGeometry.HalfWidth * first.Scale;
        Assert.Equal(expected, Vector3.Distance(first.Position, second.Position), 4);
    }

    [Fact]
    public void ScreenWallRejectsAnUnavailableCamera()
    {
        Assert.False(ScreenGeometry.TryScreenWall(Vector3.Zero, Vector3.Zero, 0.8f, 1f, ScreenWallLayout.SideBySide,
            out _, out _));
        Assert.False(ScreenGeometry.TryScreenWall(Vector3.Zero, -Vector3.UnitZ, 0.8f, 0f, ScreenWallLayout.Stacked,
            out _, out _));
    }

    private static Vector2 Project(Vector3 point, Matrix4x4 projection)
    {
        var projected = Vector4.Transform(new Vector4(point, 1f), projection);
        Assert.True(projected.W > 0f);
        return new Vector2(projected.X / projected.W, projected.Y / projected.W);
    }

    private static float LargestCornerExtent(in ScreenPose pose, Matrix4x4 projection)
    {
        var largest = 0f;
        for (var horizontal = -1; horizontal <= 1; horizontal += 2)
        {
            for (var vertical = -1; vertical <= 1; vertical += 2)
            {
                var corner = Project(ScreenGeometry.Corner(pose, horizontal, vertical), projection);
                largest = MathF.Max(largest, MathF.Max(MathF.Abs(corner.X), MathF.Abs(corner.Y)));
            }
        }

        return largest;
    }
}
