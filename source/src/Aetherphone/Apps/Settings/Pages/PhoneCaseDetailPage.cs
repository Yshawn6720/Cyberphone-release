using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class PhoneCaseDetailPage : ISettingsPage
{
    private const float HeroWidthFraction = 0.58f;
    private const float HeroPreviewScale = 0.85f;
    private const float ApplyHeight = Button.LargeHeight;

    public string Title => CatalogLabels.PhoneCase(option.Id);
    public string Summary => string.Empty;
    public FontAwesomeIcon Icon => FontAwesomeIcon.MobileAlt;
    public Vector4 Tint => new(0.55f, 0.45f, 0.95f, 1f);
    private readonly PhoneCase option;
    private readonly Configuration configuration;
    private readonly ThemeProvider themes;
    private readonly string artistLinkHost;

    public PhoneCaseDetailPage(PhoneCase option, Configuration configuration, ThemeProvider themes)
    {
        this.option = option;
        this.configuration = configuration;
        this.themes = themes;
        artistLinkHost = option.ArtistUrl.Length > 0 && Uri.TryCreate(option.ArtistUrl, UriKind.Absolute, out var parsed)
            ? parsed.Host
            : string.Empty;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var theme = context.Theme;
        var scale = UiScale.Current;
        using (AppSurface.Begin(body))
        {
            var width = ScrollLayout.StableContentWidth();
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
            DrawHero(width, theme, scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
            DrawTitleBlock(width, theme);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
            if (option.HasArtist)
            {
                DrawArtistCard(theme);
                ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
            }

            DrawApplyButton(width, theme, scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
        }
    }

    private void DrawHero(float width, PhoneTheme theme, float scale)
    {
        var heroWidth = width * HeroWidthFraction;
        var heroHeight = heroWidth / PhoneCasePreview.CanvasAspect;
        var origin = ImGui.GetCursorScreenPos();
        ImGui.Dummy(new Vector2(width, heroHeight));
        var hero = new Rect(new Vector2(origin.X + (width - heroWidth) * 0.5f, origin.Y),
            new Vector2(origin.X + (width + heroWidth) * 0.5f, origin.Y + heroHeight));
        var drawList = ImGui.GetWindowDrawList();
        var device = PhoneCasePreview.BodyRect(hero);
        Elevation.Floating(drawList, device.Min, device.Max, device.Width * 0.155f, scale, 0.7f);
        PhoneCasePreview.Draw(drawList, hero, option, theme, scale * HeroPreviewScale, true);
    }

    private void DrawTitleBlock(float width, PhoneTheme theme)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var name = CatalogLabels.PhoneCase(option.Id);
        var nameSize = Typography.Measure(name, TextStyles.Title2);
        Typography.DrawWrappedCentered(drawList, new Vector2(origin.X + width * 0.5f, origin.Y + nameSize.Y * 0.5f),
            name, theme.TextStrong, TextStyles.Title2, width);
        var category = CatalogLabels.CaseCategory(option.Category);
        var categorySize = Typography.Measure(category, TextStyles.Subheadline);
        var categoryTop = origin.Y + nameSize.Y + Metrics.Space.Xs * UiScale.Current;
        Typography.DrawWrappedCentered(drawList,
            new Vector2(origin.X + width * 0.5f, categoryTop + categorySize.Y * 0.5f), category, theme.TextMuted,
            TextStyles.Subheadline, width);
        ImGui.Dummy(new Vector2(width, categoryTop + categorySize.Y - origin.Y));
    }

    private void DrawArtistCard(PhoneTheme theme)
    {
        var card = GroupCard.Begin(theme, 1);
        var row = card.NextRow();
        var label = string.Format(Loc.Culture, Loc.T(L.Settings.CaseDesignBy), option.ArtistName);
        if (artistLinkHost.Length == 0)
        {
            SettingsRow.Info(row, label, string.Empty, theme);
        }
        else if (SettingsRow.Link(row, FontAwesomeIcon.Pen, new Vector4(0.95f, 0.40f, 0.65f, 1f), label,
                     artistLinkHost, theme))
        {
            UrlActions.OpenInBrowser(option.ArtistUrl);
        }

        card.End();
    }

    private void DrawApplyButton(float width, PhoneTheme theme, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var height = ApplyHeight * scale;
        ImGui.Dummy(new Vector2(width, height));
        var button = new Rect(origin, origin + new Vector2(width, height));
        var drawList = ImGui.GetWindowDrawList();
        var ink = ControlInk.From(theme);
        if (option.Id == configuration.PhoneCaseName)
        {
            var face = Button.Surface(drawList, button, ink, ButtonStyle.Gray, ButtonRole.Normal, true, false,
                ImGui.GetID("case.applied"));
            DrawApplyLabel(drawList, face.Face, Loc.T(L.Settings.CaseApplied), theme.TextMuted, scale);
            return;
        }

        if (Button.Draw(drawList, button, Loc.T(L.Settings.CaseApply), ink, ButtonStyle.Prominent))
        {
            Apply();
        }
    }

    private static void DrawApplyLabel(ImDrawListPtr drawList, Rect button, string label, Vector4 ink, float scale)
    {
        var labelSize = Typography.Measure(label, TextStyles.Headline);
        var iconGap
 = 8f * scale;
        var iconSize = labelSize.Y * 0.8f;
        var contentWidth = iconSize + iconGap + labelSize.X;
        var startX = button.Center.X - contentWidth * 0.5f;
        ProgressRing.CenterIcon(drawList, new Vector2(startX + iconSize * 0.5f, button.Center.Y),
            FontAwesomeIcon.Check, ink, iconSize);
        Typography.Draw(drawList, new Vector2(startX + iconSize + iconGap, button.Center.Y - labelSize.Y * 0.5f),
            label, ink, TextStyles.Headline);
    }

    private void Apply()
    {
        configuration.PhoneCaseName = option.Id;
        themes.Apply(configuration);
        configuration.Save();
    }
}
