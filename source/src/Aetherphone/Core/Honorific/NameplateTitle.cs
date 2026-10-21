namespace Aetherphone.Core.Honorific;

internal readonly record struct TitleLook(
    Vector3 Color,
    Vector3? Glow,
    Vector3? Color3,
    int? GradientColourSet,
    int? GradientAnimationStyle)
{
    public static TitleLook Solid(Vector3 color, Vector3 glow) => new(color, glow, null, null, null);
}

internal readonly record struct NameplateTitle(NameplateStatus Kind, string Text, TitleLook Look, bool Prefix)
{
    public static readonly NameplateTitle None = new(NameplateStatus.None, string.Empty, default, false);

    public bool IsNone => Kind == NameplateStatus.None;
}

internal readonly record struct NameplateValues(
    string Code,
    string Name,
    string Station,
    string Type,
    string Handle,
    string Song,
    string Artist,
    string Game,
    string Chips)
{
    public static readonly NameplateValues Empty = new(string.Empty, string.Empty, string.Empty, string.Empty,
        string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
}
