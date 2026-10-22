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
}
