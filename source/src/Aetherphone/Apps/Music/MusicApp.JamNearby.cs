using Aetherphone.Apps.Music.Components;
using Aetherphone.Apps.Music.Radio.Live;
using Aetherphone.Core;
using Aetherphone.Core.Jam;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Music;

internal readonly record struct JamNearbyLabels(string Title, string Subtitle, string Track);

internal sealed partial class MusicApp
{
    private const float JamNearbyAvatarRadius = 22f;
    private const float JamNearbyJoinHeight = 32f;
    private const float JamNearbyTrackGlyphScale = 0.62f;
    private const float JamNearbyHoverAlpha = 0.06f;
    private const float JamNearbySpinnerShare = 0.6f;
    private const string JamNearbySeparator = " · ";

    private readonly RadioCountLabel jamNearbyCountLabel = new();
    private readonly RadioCountLabel jamNearbyStatusLabel = new();
    private JamNearbyLabels[] jamNearbyLabels = Array.Empty<JamNearbyLabels>();
    private int jamNearbyLabelsVersion = -1;
    private LanguageInfo? jamNearbyLanguage;
    private int jamDiscoverableDraft = JamNoDraft;
    private float jamDiscoverableDraftAt;

    private void EnsureJamNearbyLabels()
    {
        if (jamNearbyLabelsVersion == jam.NearbyVersion && ReferenceEquals(jamNearbyLanguage, Loc.Current))
        {
            return;
        }

        jamNearbyLabelsVersion = jam.NearbyVersion;
        jamNearbyLanguage = Loc.Current;
        var nearby = jam.NearbyJams;
        var labels = new JamNearbyLabels[nearby.Length];
        var listeningFormat = Loc.T(L.Music.ListeningCount);
        for (var index = 0; index < nearby.Length; index++)
        {
            var entry = nearby[index];
            var host = entry.HostName.Length > 0 ? entry.HostName : Loc.T(L.Music.Jam.SomeoneName);
            var title = entry.Title.Length > 0 ? entry.Title : Loc.T(L.Music.Jam.JamWith, host);
            var subtitle = string.Concat(host, JamNearbySeparator,
                string.Format(Loc.Culture, listeningFormat, entry.MemberCount));
            var track = entry.Track.IsEmpty
                ? Loc.T(L.Music.Jam.NearbyIdle)
                : entry.Track.Author.Length > 0
                    ? string.Concat(entry.Track.Title, JamNearbySeparator, entry.Track.Author)
                    : entry.Track.Title;
            labels[index] = new JamNearbyLabels(title, subtitle, track);
        }

        jamNearbyLabels = labels;
    }

    private void DrawJamNearby(float scale)
    {
        EnsureJamNearbyLabels();
        var nearby = jam.NearbyJams;
        var listed = nearby.Length > 0 && jamNearbyLabels.Length == nearby.Length;
        if (jam.RelayMode)
        {
            SectionHeader.Draw(ui, Loc.T(L.Music.Jam.NearbyHeader), false);
            DrawJamNearbyRefreshRow(listed ? nearby.Length : 0, scale);
            if (!listed && jam.NearbyUnsupported && !jam.NearbyLoading)
            {
                DrawJamNearbyUnsupported();
            }
        }
        else if (listed)
        {
            SectionHeader.Draw(ui, Loc.T(L.Music.Jam.NearbyHeader), false);
        }

        if (!listed)
        {
            return;
        }

        for (var index = 0; index < nearby.Length; index++)
        {
            ImGui.PushID(index);
            DrawJamNearbyRow(nearby[index], jamNearbyLabels[index], scale);
            ImGui.PopID();
        }
    }

    private void DrawJamNearbyRefreshRow(int count, float scale)
    {
        var radius = RoundButton.SmallRadius * scale;
        var lineHeight = Typography.LineHeight(TextStyles.Footnote);
        var block = BeginJamBlock(MathF.Max(radius * 2f, lineHeight));
        var drawList = ImGui.GetWindowDrawList();
        var center = new Vector2(block.Max.X - radius, block.Center.Y);
        var loading = jam.NearbyLoading;
        if (loading)
        {
            LoadingPulse.Spinner(center, radius * JamNearbySpinnerShare, ui.Accent, 1f, drawList);
        }
        else if (RoundButton.Icon(drawList, center, radius, PhoneIcons.Refresh, ui.Ink,
                     tooltip: Loc.T(L.Common.Refresh), enabled: jam.CanRefreshNearby))
        {
            jam.RefreshNearby();
        }

        if ((!loading || count > 0) && !jam.NearbyUnsupported)
        {
            var label = jamNearbyStatusLabel.Plural(L.Music.Jam.NearbyCount, count);
            var labelWidth = MathF.Max(1f, center.X - radius - Metrics.Space.Md * scale - block.Min.X);
            Typography.Draw(drawList, new Vector2(block.Min.X, block.Center.Y - lineHeight * 0.5f),
                Typography.FitText(label, labelWidth, TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);
        }

        EndJamBlock();
        JamGap(Metrics.Space.Sm);
    }

    private void DrawJamNearbyUnsupported()
    {
        var text = Loc.T(L.AetherStream.NearbyUnsupported);
        var width = ScrollLayout.StableContentWidth() - MusicUi.Inset * 2f * UiScale.Current;
        var height = Typography.MeasureWrappedBlock(text, TextStyles.Footnote, width).Y;
        var block = BeginJamBlock(height);
        Typography.DrawWrappedLeft(block.Min, text, ui.MutedInk, TextStyles.Footnote, width);
        EndJamBlock();
        JamGap(Metrics.Space.Sm);
    }

    private void DrawJamNearbyRow(JamNearbyJam entry, in JamNearbyLabels labels, float scale)
    {
        var pad = Metrics.Space.Md * scale;
        var radius = JamNearbyAvatarRadius * scale;
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var lineHeight = Typography.LineHeight(TextStyles.Footnote);
        var height = MathF.Max(radius * 2f, titleHeight + lineHeight * 2f) + pad * 2f;
        var card = BeginJamBlock(height);
        var drawList = ImGui.GetWindowDrawList();
        var rounding = Metrics.Radius.Md * scale;
        var hovered = UiInteract.Hover(card.Min, card.Max);
        ui.Card(drawList, card.Min, card.Max, rounding);
        if (hovered)
        {
            Squircle.Fill(drawList, card.Min, card.Max, rounding,
                ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, JamNearbyHoverAlpha)));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var avatarCenter = new Vector2(card.Min.X + pad + radius, card.Center.Y);
        AvatarView.DrawRemote(drawList, avatarCenter, radius, theme, entry.HostName, string.Empty, entry.HostAvatarUrl,
            images, lodestone, 0.8f, 24);
        var joinLabel = Loc.T(L.Music.Jam.Join);
        var joinHeight = JamNearbyJoinHeight * scale;
        var joinWidth = Typography.Measure(joinLabel, TextStyles.SubheadlineEmphasized).X + joinHeight;
        var join = new Rect(new Vector2(card.Max.X - pad - joinWidth, card.Center.Y - joinHeight * 0.5f),
            new Vector2(card.Max.X - pad, card.Center.Y + joinHeight * 0.5f));
        var textLeft = avatarCenter.X + radius + pad;
        var textWidth = MathF.Max(1f, join.Min.X - Metrics.Space.Md * scale - textLeft);
        var top = card.Center.Y - (titleHeight + lineHeight * 2f) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, top), Typography.FitText(labels.Title, textWidth,
            TextStyles.Headline), ui.TitleInk, TextStyles.Headline);
        top += titleHeight;
        Typography.Draw(drawList, new Vector2(textLeft, top), Typography.FitText(labels.Subtitle, textWidth,
            TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);
        top += lineHeight;
        var glyphCenter = new Vector2(textLeft + Metrics.Space.Xs * scale, top + lineHeight * 0.5f);
        AppSkin.Icon(drawList, glyphCenter, IconGlyph.Of(FontAwesomeIcon.Music), ui.Accent, JamNearbyTrackGlyphScale);
        var trackLeft = glyphCenter.X + Metrics.Space.Md * scale;
        Typography.Draw(drawList, new Vector2(trackLeft, top),
            Typography.FitText(labels.Track, MathF.Max(1f, textWidth - (trackLeft - textLeft)), TextStyles.Footnote),
            ui.BodyInk, TextStyles.Footnote);
        var joinTapped = ui.AccentPill(join, joinLabel, true, TextStyles.SubheadlineEmphasized);
        var overJoin = UiInteract.Hover(join.Min, join.Max);
        var rowTapped = !overJoin && UiInteract.Click(card.Min, card.Max, hovered);
        EndJamBlock();
        JamGap(Metrics.Space.Sm);
        if (joinTapped || rowTapped)
        {
            JoinJam(entry.Code, true);
        }
    }

    private string JamHomeIdleBody()
    {
        var count = jam.NearbyJams.Length;
        return count > 0
            ? jamNearbyCountLabel.Plural(L.Music.Jam.NearbyCount, count)
            : Loc.T(L.Music.Jam.HomeIdleBody);
    }

    private void DrawJamDiscoverableSetting(float scale)
    {
        var discoverable = ShownJamFlag(ref jamDiscoverableDraft, jamDiscoverableDraftAt, jam.Discoverable);
        var hint = jam.RelayMode ? L.Music.Jam.DiscoverableRelayHint : L.Music.Jam.DiscoverableHint;
        var next = DrawJamFeature("music.jam.setting.discoverable", FontAwesomeIcon.MapMarkerAlt, AccentRing.Teal,
            Loc.T(L.Music.Jam.Discoverable), Loc.T(hint), discoverable, scale);
        if (next == discoverable)
        {
            return;
        }

        jamDiscoverableDraft = next ? 1 : 0;
        jamDiscoverableDraftAt = clock;
        jam.SetDiscoverable(next);
    }
}
