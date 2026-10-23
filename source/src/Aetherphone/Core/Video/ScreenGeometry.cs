namespace Aetherphone.Core.Video;

internal readonly record struct ScreenPose(Vector3 Position, float Yaw, float Pitch, float Roll, float Scale);

internal enum ScreenWallLayout : byte
{
    SideBySide,
    Stacked,
}

internal static class ScreenGeometry
{
    internal const float HalfWidth = 1.0f;
    internal const float HalfHeight = 0.6f;
    private const float LargeScreenDistance = 10f;
    private const float LargeScreenViewShare = 0.85f;

    internal static bool TryLargeScreen(Vector3 cameraPosition, Vector3 cameraTarget, float fieldOfView,
        float aspectRatio, out ScreenPose pose) =>
        TryFillView(cameraPosition, cameraTarget, fieldOfView, aspectRatio, HalfWidth, HalfHeight, out pose);

    internal static bool TryScreenWall(Vector3 cameraPosition, Vector3 cameraTarget, float fieldOfView,
        float aspectRatio, ScreenWallLayout layout, out ScreenPose first, out ScreenPose second)
    {
        first = default;
        second = default;
        var sideBySide = layout == ScreenWallLayout.SideBySide;
        var wallHalfWidth = sideBySide ? 2f * HalfWidth : HalfWidth;
        var wallHalfHeight = sideBySide ? HalfHeight : 2f * HalfHeight;
        if (!TryFillView(cameraPosition, cameraTarget, fieldOfView, aspectRatio, wallHalfWidth, wallHalfHeight,
                out var center))
        {
            return false;
        }

        var toSecond = sideBySide
            ? Right(center) * (HalfWidth * center.Scale)
            : Up(center) * (-HalfHeight * center.Scale);
        first = center with { Position = center.Position - toSecond };
        second = center with { Position = center.Position + toSecond };
        return true;
    }

    private static bool TryFillView(Vector3 cameraPosition, Vector3 cameraTarget, float fieldOfView,
        float aspectRatio, float halfWidth, float halfHeight, out ScreenPose pose)
    {
        pose = default;
        var direction = cameraTarget - cameraPosition;
        var lengthSquared = direction.LengthSquared();
        if (!float.IsFinite(lengthSquared) || lengthSquared < 0.0001f
            || !float.IsFinite(cameraPosition.LengthSquared())
            || !(fieldOfView > 0f && fieldOfView < MathF.PI)
            || !float.IsFinite(aspectRatio) || aspectRatio <= 0f)
        {
            return false;
        }

        var forward = direction / MathF.Sqrt(lengthSquared);
        var halfViewHeight = LargeScreenDistance * MathF.Tan(fieldOfView * 0.5f);
        var scale = LargeScreenViewShare
            * MathF.Min(halfViewHeight / halfHeight, halfViewHeight * aspectRatio / halfWidth);
        pose = new ScreenPose(cameraPosition + forward * LargeScreenDistance,
            MathF.Atan2(-forward.X, -forward.Z), MathF.Asin(Math.Clamp(forward.Y, -1f, 1f)), 0f,
            Math.Clamp(scale, VideoEngine.MinScreenScale, VideoEngine.MaxScreenScale));
        return true;
    }

    internal static Quaternion Rotation(in ScreenPose pose) =>
        Quaternion.CreateFromYawPitchRoll(pose.Yaw, pose.Pitch, pose.Roll);

    internal static Vector3 Right(in ScreenPose pose) => Vector3.Transform(Vector3.UnitX, Rotation(pose));

    internal static Vector3 Up(in ScreenPose pose) => Vector3.Transform(Vector3.UnitY, Rotation(pose));

    internal static Vector3 Facing(in ScreenPose pose) => Vector3.Transform(Vector3.UnitZ, Rotation(pose));

    internal static Vector3 Nudge(in ScreenPose pose, float right, float up, float toward)
    {
        var rotation = Rotation(pose);
        return pose.Position
            + Vector3.Transform(Vector3.UnitX, rotation) * right
            + Vector3.Transform(Vector3.UnitY, rotation) * up
            + Vector3.Transform(Vector3.UnitZ, rotation) * toward;
    }

    internal static Vector3 Corner(in ScreenPose pose, float horizontal, float vertical, float curveDepth = 0f)
    {
        var rotation = Rotation(pose);
        return pose.Position
            + Vector3.Transform(Vector3.UnitX, rotation) * (horizontal * HalfWidth * pose.Scale)
            + Vector3.Transform(Vector3.UnitY, rotation) * (vertical * HalfHeight * pose.Scale)
            + Vector3.Transform(Vector3.UnitZ, rotation) * (curveDepth * horizontal * horizontal * pose.Scale);
    }

    internal static float YawFacing(Vector3 screenPosition, Vector3 viewerPosition)
    {
        var toViewer = viewerPosition - screenPosition;
        return MathF.Atan2(toViewer.X, toViewer.Z);
    }

    internal static float AlongAxis(Vector2 pointerDelta, Vector2 axisStart, Vector2 axisEnd, float axisWorldLength)
    {
        var axis = axisEnd - axisStart;
        var lengthSquared = axis.LengthSquared();
        if (lengthSquared < 1f)
        {
            return 0f;
        }

        return Vector2.Dot(pointerDelta, axis) / lengthSquared * axisWorldLength;
    }

    internal const float CompanionGapShare = 0.2f;

    internal static Vector3 Beside(in ScreenPose pose) =>
        pose.Position + Right(pose) * (2f * HalfWidth * pose.Scale * (1f + CompanionGapShare));

    internal static void OrderFarToNear(ReadOnlySpan<float> distances, Span<int> order)
    {
        for (var index = 0; index < order.Length; index++)
        {
            order[index] = index;
        }

        for (var index = 1; index < order.Length; index++)
        {
            var current = order[index];
            var slot = index;
            while (slot > 0 && distances[order[slot - 1]] < distances[current])
            {
                order[slot] = order[slot - 1];
                slot--;
            }

            order[slot] = current;
        }
    }
}

internal static class SpatialVolume
{
    internal const float DefaultRange = 30f;
    internal const float MinRange = 8f;
    internal const float MaxRange = 80f;

    internal const float FullVolumeShare = 0.25f;

    internal static float Gain(float distance, float range)
    {
        var reach = Math.Clamp(range, MinRange, MaxRange);
        var near = reach * FullVolumeShare;
        if (distance <= near)
        {
            return 1f;
        }

        if (distance >= reach)
        {
            return 0f;
        }

        var fade = 1f - (distance - near) / (reach - near);
        return fade * fade;
    }
}

internal static class ScreenPlaces
{
    internal const int MaxRemembered = 40;
    internal const float ReuseDistance = 60f;

    internal static string Key(uint territoryId, uint worldId, short ward, short plot, short room) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"{territoryId}:{worldId}:{ward}:{plot}:{room}");
}
