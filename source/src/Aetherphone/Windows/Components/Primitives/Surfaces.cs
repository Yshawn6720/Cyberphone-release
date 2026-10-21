using Aetherphone.Core.Theme;

namespace Aetherphone.Windows.Components;

internal enum FillLevel : byte
{
    Primary,
    Secondary,
    Tertiary,
    Quaternary,
}

internal readonly record struct ControlInk(Vector4 Accent, Vector4 Ink, Vector4 Muted, Vector4 Danger)
{
    private const float AccentInkLift = 0.35f;
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    public bool Light => Palette.Luminance(Ink) < 0.5f;

    public Vector4 AccentInk => Light ? Accent : Palette.Mix(Accent, White, AccentInkLift);

    public Vector4 DangerInk => Light ? Danger : Palette.Mix(Danger, White, AccentInkLift);

    public static ControlInk From(AppSkin ui) => new(ui.Accent, ui.TitleInk, ui.MutedInk, ui.Theme.Danger);

    public static ControlInk From(PhoneTheme theme) =>
        new(theme.Accent, theme.TextStrong, theme.TextMuted, theme.Danger);

    public ControlInk WithAccent(Vector4 accent) => this with { Accent = accent };
}

internal static class Surfaces
{
    private static readonly float[] DarkAlphas = [0.16f, 0.12f, 0.09f, 0.05f];
    private static readonly float[] LightAlphas = [0.12f, 0.09f, 0.07f, 0.04f];

    public static Vector4 Fill(in ControlInk ink, FillLevel level) => Fill(ink.Ink, level);

    public static Vector4 Fill(Vector4 ink, FillLevel level)
    {
        var alphas = Palette.Luminance(ink) < 0.5f ? LightAlphas : DarkAlphas;
        return ink with { W = alphas[(int)level] };
    }
}
