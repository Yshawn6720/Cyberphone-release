using Aetherphone.Core.Theme;

namespace Aetherphone.Windows.Components;

internal sealed class SocialInk
{
    public readonly Vector4 Accent;
    public readonly Vector4 AccentDeep;
    public readonly Vector4 AccentLink;
    public readonly Vector4 AccentWash;
    public readonly Vector4 AccentShadow;
    public readonly Vector4 TitleInk;
    public readonly Vector4 BodyInk;
    public readonly Vector4 MutedInk;
    public readonly Vector4 FaintInk;
    public readonly Vector4 SegmentIdleInk;
    public readonly Vector4 Hairline;
    public readonly Vector4 HoverTint;
    public readonly Vector4 BackdropTop;
    public readonly Vector4 FieldFill = new(1f, 1f, 1f, 0.08f);
    public readonly Vector4 ButtonFill = new(1f, 1f, 1f, 0.12f);
    public readonly Vector4 ButtonHover = new(1f, 1f, 1f, 0.18f);
    public readonly Vector4 ChipFill = new(1f, 1f, 1f, 0.055f);
    public readonly Vector4 ChipStroke = new(1f, 1f, 1f, 0.08f);
    public readonly Vector4 ChipHover = new(1f, 1f, 1f, 0.09f);
    public readonly Vector4 GlassPanel;
    public readonly Vector4 GlassStroke = new(1f, 1f, 1f, 0.12f);
    public readonly Vector4 BackChipFill = new(1f, 1f, 1f, 0.08f);
    public readonly Vector4 BackChipHover = new(1f, 1f, 1f, 0.14f);
    public readonly Vector4 ThumbFill = new(1f, 1f, 1f, 0.06f);
    public readonly Vector4 Scrim = new(0f, 0f, 0f, 0.55f);
    public readonly Vector4 White = new(1f, 1f, 1f, 1f);
    public readonly Vector4 LikeRed = new(1f, 0.216f, 0.373f, 1f);
    public readonly Vector4 Danger = new(1f, 0.373f, 0.420f, 1f);
    public readonly Vector4 PresenceGreen = new(0.188f, 0.820f, 0.345f, 1f);

    public ControlInk Control => new(Accent, TitleInk, MutedInk, Danger);

    public SocialInk(in AppPalette palette)
    {
        Accent = palette.Accent;
        AccentDeep = Palette.Darken(palette.Accent, 0.22f);
        AccentLink = Palette.Lighten(palette.Accent, 0.18f);
        AccentWash = Palette.WithAlpha(palette.Accent, 0.14f);
        AccentShadow = Palette.Darken(palette.Accent, 0.78f);
        TitleInk = palette.TitleInk;
        BodyInk = palette.BodyInk;
        MutedInk = palette.MutedInk;
        FaintInk = Palette.WithAlpha(palette.MutedInk, 0.62f);
        SegmentIdleInk = Palette.WithAlpha(palette.MutedInk, 0.95f);
        Hairline = palette.Hairline;
        HoverTint = palette.HoverWash;
        BackdropTop = palette.BackdropTop;
        GlassPanel = Palette.WithAlpha(Palette.Lighten(palette.BackdropTop, 0.10f), 0.92f);
    }
}
