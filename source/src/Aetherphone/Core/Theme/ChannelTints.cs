namespace Aetherphone.Core.Theme;

internal static class ChannelTints
{
    public static readonly Vector4 Say = new(0.867f, 0.890f, 0.882f, 1f);
    public static readonly Vector4 Shout = new(1f, 0.627f, 0.361f, 1f);
    public static readonly Vector4 Yell = new(1f, 0.824f, 0.392f, 1f);
    public static readonly Vector4 Emote = new(0.725f, 0.659f, 0.910f, 1f);
    public static readonly Vector4 Tell = new(1f, 0.561f, 0.749f, 1f);
    public static readonly Vector4 Party = new(0.388f, 0.702f, 1f, 1f);
    public static readonly Vector4 Alliance = new(1f, 0.682f, 0.541f, 1f);
    public static readonly Vector4 PvpTeam = new(1f, 0.490f, 0.541f, 1f);
    public static readonly Vector4 FreeCompany = new(0.373f, 0.839f, 0.753f, 1f);
    public static readonly Vector4 NoviceNetwork = new(0.561f, 0.890f, 0.659f, 1f);
    public static readonly Vector4 Linkshell = new(0.718f, 0.867f, 0.420f, 1f);
    public static readonly Vector4 CrossWorldLinkshell = new(0.435f, 0.847f, 0.871f, 1f);
    public static readonly Vector4 Echo = new(0.604f, 0.627f, 0.659f, 1f);
    public static readonly Vector4 System = new(0.541f, 0.561f, 0.596f, 1f);

    public static readonly Vector4[] TabPalette =
    {
        FreeCompany,
        Linkshell,
        Party,
        Tell,
        Shout,
        Emote,
        Yell,
        CrossWorldLinkshell,
    };
}
