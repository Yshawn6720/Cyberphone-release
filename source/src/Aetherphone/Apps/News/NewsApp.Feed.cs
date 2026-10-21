using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.News;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.News;

internal sealed partial class NewsApp
{
    private const float DatelineGap = 14f;
    private const float CardGap = 14f;
    private const float CardTitleGap = 4f;
    private const float CardMetaGap = 8f;
    private const int HeroTitleLines = 4;
    private const int HeroSummaryLines = 3;
    private const int CardTitleLines = 3;
    private const int RowTitleLines = 3;
    private const int MaintenanceRowTitleLines = 2;
    private const float RowPadX = 16f;
    private const float RowPadY = 12f;
    private const float RowLineGap = 3f;
    private const float RowTrailingGap = 8f;
    private const float RowChevronGap = 7f;
    private const float RowHoverTarget = 0.5f;
    private const float RowHighlightAlpha = 0.10f;
    private const float NoticeHeight = 44f;
    private const float NoticePad = 14f;
    private const float NoticeGlyphScale = 0.8f;
    private const float NoticeGap = 14f;
    private const float BottomGap = 28f;
    private const float SkeletonBarHeight = 10f;
    private const float SkeletonTitleHeight = 15f;
    private const float SkeletonRowHeight = 64f;
    private const int SkeletonRows = 4;
    private const int SkeletonCards = 2;
    private const string NoticeId = "news.notice";
    private const string RowAnchor = "news.row";
    private const string StoryAnchor = "news.story";
    private const string MetaSeparator = " · ";

    private static readonly string[] RegionKeys = { "na", "eu", "fr", "de", "jp", "cn" };

    private readonly PullToRefresh feedRefresh = new();
    private readonly NavBarButton[] rootButtons = new NavBarButton[1];
    private readonly int[] rowBuffer = new int[64];
    private readonly string?[] moreInLabels = new string?[NewsCategories.All.Length];
    private CultureInfo? moreInCulture;
    private CachedText dateline;

    private void DrawRoot(Rect area)
    {
        var scale = UiScale.Current;
        var context = new PhoneContext(area, theme, navigation);
        var feed = Feed(activeCategory);
        if (activeCategory == NewsCategory.Topics)
        {
            Feed(NewsCategory.Maintenance);
        }

        using (TabBar.ReserveContent(scale))
        {
            var navBar = AppHeader.BeginLargeTitle(context, false);
            using (var surface = AppSurface.Begin(navBar.Body))
            {
                if (resetScroll)
                {
                    surface.JumpToTop();
                    resetScroll = false;
                }

                feedRefresh.Draw(navBar.Body, surface.Pull, surface.Dragging, feed.State == NewsState.Loading,
                    ui.MutedInk, refresh);
                DrawFeedBody(navBar.Body, feed, scale);
            }

            rootButtons[0] = new NavBarButton(IconGlyph.Of(FontAwesomeIcon.Sync), Loc.T(L.News.Refresh));
            UiAnchors.Report("news.refresh", AppHeader.LargeTitleButtonRect(in navBar, 0, rootButtons.Length));
            var pressed = AppHeader.EndLargeTitle(in navBar, context, NavIds[(int)activeCategory],
                PageTitle(activeCategory), NavBarStyle.From(ui), rootButtons);
            if (pressed == 0)
            {
                UiFeedback.Play(UiSound.Refresh);
                Refresh();
            }
        }

        DrawTabBar(area);
    }

    private void DrawTabBar(Rect area)
    {
        tabCount = 0;
        var active = 0;
        for (var index = 0; index < NewsCategories.All.Length; index++)
        {
            var category = NewsCategories.All[index];
            if (!NewsCategories.AvailableFor(category, locale))
            {
                continue;
            }

            if (category == activeCategory)
            {
                active = tabCount;
            }

            tabCategories[tabCount] = category;
            tabItems[tabCount] = new TabItem(Loc.T(NewsCategories.Label(category)), TabGlyph(category),
                AnchorKey: TabAnchors[(int)category]);
            tabCount++;
        }

        var result = tabBar.Draw(area, ui, tabItems.AsSpan(0, tabCount), active);
        if (result.Tapped >= 0 && result.Tapped < tabCount)
        {
            SelectCategory(tabCategories[result.Tapped]);
        }
    }

    private static string TabGlyph(NewsCategory category) =>
        category switch
        {
            NewsCategory.Notices => PhoneIcons.InfoCircle,
            NewsCategory.Maintenance => PhoneIcons.Settings,
            NewsCategory.Updates => PhoneIcons.Download,
            NewsCategory.Status => PhoneIcons.ShieldCheck,
            _ => PhoneIcons.Sparkles,
        };

    private void DrawFeedBody(Rect body, NewsFeed feed, float scale)
    {
        if (!feed.HasStories)
        {
            DrawFeedState(body, feed, scale);
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var left = origin.X;
        var cursorY = DrawDateline(drawList, left, origin.Y, width, feed, scale);
        if (feed.State == NewsState.Failed)
        {
            cursorY = DrawOfflineNotice(drawList, left, cursorY, width, scale);
        }

        switch (feed.Category)
        {
            case NewsCategory.Topics:
                cursorY = DrawMaintenanceBanner(drawList, left, cursorY, width, scale);
                cursorY = DrawTopics(drawList, left, cursorY, width, feed, scale);
                break;
            case NewsCategory.Maintenance:
                cursorY = DrawMaintenancePage(drawList, left, cursorY, width, feed, scale);
                break;
            default:
                cursorY = DrawDaySections(drawList, left, cursorY, width, feed, scale);
                break;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, MathF.Max(1f, cursorY - origin.Y + BottomGap * scale)));
    }

    private void DrawFeedState(Rect body, NewsFeed feed, float scale)
    {
        if (feed.State == NewsState.Failed)
        {
            if (NewsArt.StatePanel(body, ui, FontAwesomeIcon.Wifi, Loc.T(L.News.CouldntReach),
                    Loc.T(L.News.CouldntReachHint), Loc.T(L.News.TryAgain)))
            {
                UiFeedback.Play(UiSound.Refresh);
                Refresh();
            }

            return;
        }

        if (feed.State == NewsState.Empty)
        {
            NewsArt.StatePanel(body, ui, FontAwesomeIcon.Newspaper, Loc.T(L.News.NoNews), Loc.T(L.News.NoNewsHint),
                string.Empty);
            return;
        }

        if (feed.Category == NewsCategory.Topics)
        {
            DrawCardSkeleton(scale);
            return;
        }

        DrawRowSkeleton(scale);
    }

    private float DrawDateline(ImDrawListPtr drawList, float left, float top, float width, NewsFeed feed, float scale)
    {
        var text = Typography.FitText(DatelineText(feed), width, TextStyles.Subheadline);
        Typography.Draw(drawList, new Vector2(left, top), text, ui.MutedInk, TextStyles.Subheadline);
        return top + Typography.LineHeight(TextStyles.Subheadline) + DatelineGap * scale;
    }

    private string DatelineText(NewsFeed feed)
    {
        var success = feed.Entry?.LastSuccessUtc ?? default;
        var loading = feed.State == NewsState.Loading;
        var minutes = success == default ? -1L : (long)(DateTime.UtcNow - success).TotalMinutes;
        var key = ((minutes + 1L) << 5) | ((long)RegionIndex(locale) << 1) | (loading ? 1L : 0L);
        if (dateline.IsCurrent(key))
        {
            return dateline.Value;
        }

        var region = RegionLabel(locale);
        var status = loading
            ? Loc.T(L.News.Checking)
            : minutes < 0 ? string.Empty : Loc.T(L.News.UpdatedAgo, TimeText.Ago(success));
        return dateline.Store(key, status.Length == 0 ? region : string.Concat(region, MetaSeparator, status));
    }

    private float DrawOfflineNotice(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var height = NoticeHeight * scale;
        var min = new Vector2(left, top);
        var max = new Vector2(left + width, top + height);
        var hovered = UiInteract.Hover(min, max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var press = PressFx.Scale(NoticeId, pressed, PressFx.CardPressedScale);
        var center = (min + max) * 0.5f;
        var half = new Vector2(width, height) * 0.5f * press;
        ui.Card(drawList, center - half, center + half, height * 0.5f * press);
        var pad = NoticePad * scale;
        var glyphCenter = new Vector2(left + pad + Metrics.Space.Sm * scale, center.Y);
        AppSkin.Icon(drawList, glyphCenter, IconGlyph.Of(FontAwesomeIcon.ExclamationTriangle), ui.MutedInk,
            NoticeGlyphScale);
        var retry = Loc.T(L.Common.Retry);
        var retrySize = Typography.Measure(retry, TextStyles.FootnoteEmphasized);
        var retryLeft = max.X - pad - retrySize.X;
        Typography.Draw(drawList, new Vector2(retryLeft, center.Y - retrySize.Y * 0.5f), retry, ui.Accent,
            TextStyles.FootnoteEmphasized);
        var textLeft = glyphCenter.X + Metrics.Space.Lg * scale;
        var message = Typography.FitText(Loc.T(L.News.OfflineNotice),
            MathF.Max(1f, retryLeft - Metrics.Space.Sm * scale - textLeft), TextStyles.Footnote);
        var messageHeight = Typography.Measure(message, TextStyles.Footnote).Y;
        Typography.Draw(drawList, new Vector2(textLeft, center.Y - messageHeight * 0.5f), message, ui.MutedInk,
            TextStyles.Footnote);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(min, max, hovered))
        {
            UiFeedback.Play(UiSound.Refresh);
            Refresh();
        }

        return max.Y + NoticeGap * scale;
    }

    private float DrawTopics(ImDrawListPtr drawList, float left, float top, float width, NewsFeed feed, float scale)
    {
        var stories = feed.Stories;
        var cursorY = DrawStoryCard(drawList, left, top, width, stories[0], true, scale);
        if (stories.Length == 1)
        {
            return cursorY;
        }

        cursorY += NewsArt.SectionTopGap * scale;
        cursorY += CardSectionHeader.Draw(drawList, new Vector2(left, cursorY), width, MoreInLabel(feed.Category),
            ui.TitleInk) + NewsArt.SectionHeaderGap * scale;
        for (var index = 1; index < stories.Length; index++)
        {
            if (index > 1)
            {
                cursorY += CardGap * scale;
            }

            cursorY = DrawStoryCard(drawList, left, cursorY, width, stories[index], false, scale);
        }

        return cursorY;
    }

    private float DrawStoryCard(ImDrawListPtr drawList, float left, float top, float width, NewsStory story,
        bool hero, float scale)
    {
        var pad = Metrics.Space.Lg * scale;
        var innerWidth = MathF.Max(1f, width - pad * 2f);
        var reveal = 0f;
        var failed = false;
        var texture = story.HasImage ? Banner(story.Item.Image, width, out reveal, out failed) : null;
        var bannerHeight = story.HasImage ? NewsArt.BannerHeight(texture, width) : 0f;
        var titleStyle = hero ? TextStyles.Title2 : TextStyles.Headline;
        var titleLines = story.Title.Get(story.Item.Title, titleStyle, innerWidth,
            hero ? HeroTitleLines : CardTitleLines, fontKey);
        var summaryLines = hero
            ? story.Summary.Get(story.Lead, TextStyles.Subheadline, innerWidth, HeroSummaryLines, fontKey)
            : WidgetText.NoLines;
        var titleLineHeight = Typography.LineHeight(titleStyle);
        var summaryLineHeight = Typography.LineHeight(TextStyles.Subheadline);
        var metaHeight = Typography.LineHeight(TextStyles.Footnote);
        var summaryBlock = summaryLines.Length > 0
            ? CardTitleGap * scale + summaryLines.Length * summaryLineHeight
            : 0f;
        var height = bannerHeight + pad + titleLines.Length * titleLineHeight + summaryBlock +
                     CardMetaGap * scale + metaHeight + pad;
        var rest = new Rect(new Vector2(left, top), new Vector2(left + width, top + height));
        if (hero)
        {
            UiAnchors.Report(StoryAnchor, rest);
        }

        if (!ImGui.IsRectVisible(rest.Min, rest.Max))
        {
            return rest.Max.Y;
        }

        var hovered = UiInteract.Hover(rest.Min, rest.Max);
        var radius = Metrics.Radius.Grouped * scale;
        var card = NewsArt.Lift(drawList, ui, rest, story.Id, hovered, radius);
        if (story.HasImage)
        {
            var factor = card.Width / rest.Width;
            NewsArt.Banner(drawList, ui, texture, failed, reveal, card.Min,
                new Vector2(card.Max.X, card.Min.Y + bannerHeight * factor), radius * factor,
                ImDrawFlags.RoundCornersTop, scale);
        }

        var textLeft = left + pad;
        var cursorY = top + bannerHeight + pad;
        for (var lineIndex = 0; lineIndex < titleLines.Length; lineIndex++)
        {
            Typography.Draw(drawList, new Vector2(textLeft, cursorY), titleLines[lineIndex], ui.TitleInk, titleStyle);
            cursorY += titleLineHeight;
        }

        if (summaryLines.Length > 0)
        {
            cursorY += CardTitleGap * scale;
            for (var lineIndex = 0; lineIndex < summaryLines.Length; lineIndex++)
            {
                Typography.Draw(drawList, new Vector2(textLeft, cursorY), summaryLines[lineIndex], ui.BodyInk,
                    TextStyles.Subheadline);
                cursorY += summaryLineHeight;
            }
        }

        cursorY += CardMetaGap * scale;
        Typography.Draw(drawList, new Vector2(textLeft, cursorY), story.AgoText(), ui.MutedInk, TextStyles.Footnote);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(rest.Min, rest.Max, hovered))
        {
            OpenStory(NewsCategory.Topics, story);
        }

        return rest.Max.Y;
    }

    private float DrawDaySections(ImDrawListPtr drawList, float left, float top, float width, NewsFeed feed,
        float scale)
    {
        var stories = feed.Stories;
        var cursorY = top;
        var position = 0;
        var anchorTaken = false;
        while (position < stories.Length)
        {
            var end = position + 1;
            while (end < stories.Length && stories[end].LocalDay == stories[position].LocalDay)
            {
                end++;
            }

            if (position > 0)
            {
                cursorY += NewsArt.SectionTopGap * scale;
            }

            cursorY += CardSectionHeader.Draw(drawList, new Vector2(left, cursorY), width, stories[position].DayLabel,
                ui.TitleInk) + NewsArt.SectionHeaderGap * scale;
            var count = 0;
            for (var index = position; index < end && count < rowBuffer.Length; index++)
            {
                rowBuffer[count++] = index;
            }

            cursorY = DrawRowGroup(drawList, left, cursorY, width, feed, rowBuffer.AsSpan(0, count), ref anchorTaken,
                scale);
            position = end;
        }

        return cursorY;
    }

    private float DrawRowGroup(ImDrawListPtr drawList, float left, float top, float width, NewsFeed feed,
        ReadOnlySpan<int> indices, ref bool anchorTaken, float scale)
    {
        if (indices.Length == 0)
        {
            return top;
        }

        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var right = left + width;
        var total = 0f;
        for (var position = 0; position < indices.Length; position++)
        {
            total += MeasureRow(feed, indices[position], left, right, nowUnix, scale, out _, out _, out _);
        }

        var cardMax = new Vector2(right, top + total);
        ui.Card(drawList, new Vector2(left, top), cardMax, Metrics.Radius.Grouped * scale);
        var rowTop = top;
        for (var position = 0; position < indices.Length; position++)
        {
            var index = indices[position];
            var height = MeasureRow(feed, index, left, right, nowUnix, scale, out var titleLines, out var subtitle,
                out var trailing);
            var row = new Rect(new Vector2(left, rowTop), new Vector2(right, rowTop + height));
            if (!anchorTaken)
            {
                UiAnchors.Report(RowAnchor, row);
                anchorTaken = true;
            }

            if (ImGui.IsRectVisible(row.Min, row.Max))
            {
                DrawRow(drawList, feed, feed.Stories[index], row, titleLines, subtitle, trailing, position == 0,
                    position == indices.Length - 1, scale);
            }

            rowTop += height;
        }

        return cardMax.Y;
    }

    private float MeasureRow(NewsFeed feed, int index, float left, float right, long nowUnix, float scale,
        out string[] titleLines, out string subtitle, out string trailing)
    {
        var story = feed.Stories[index];
        var maintenance = feed.Category == NewsCategory.Maintenance;
        subtitle = string.Empty;
        trailing = story.Clock;
        if (maintenance)
        {
            var window = feed.Board.WindowOf(index);
            subtitle = window.IsEmpty ? story.Clock : story.WindowText(window);
            trailing = window.StatusAt(nowUnix) == MaintenanceStatus.Upcoming
                ? story.UntilText(window.StartUnix, nowUnix)
                : string.Empty;
        }

        var textLeft = left + RowPadX * scale;
        var textRight = right - RowPadX * scale;
        var trailingWidth = (RowChevronGap + NewsArt.ChevronSize) * scale + RowTrailingGap * scale;
        if (trailing.Length > 0)
        {
            trailingWidth += Typography.Measure(trailing, TextStyles.Footnote).X;
        }

        titleLines = story.Title.Get(story.Item.Title, TextStyles.Headline,
            MathF.Max(1f, textRight - trailingWidth - textLeft), maintenance ? MaintenanceRowTitleLines : RowTitleLines,
            fontKey);
        var titleBlock = Math.Max(1, titleLines.Length) * Typography.LineHeight(TextStyles.Headline);
        var subtitleBlock = subtitle.Length > 0
            ? RowLineGap * scale + Typography.LineHeight(TextStyles.Footnote)
            : 0f;
        return RowPadY * 2f * scale + titleBlock + subtitleBlock;
    }

    private void DrawRow(ImDrawListPtr drawList, NewsFeed feed, NewsStory story, Rect row, string[] titleLines,
        string subtitle, string trailing, bool first, bool last, float scale)
    {
        var hovered = UiInteract.Hover(row.Min, row.Max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var highlight = story.StepHighlight(pressed ? 1f : hovered ? RowHoverTarget : 0f, deltaSeconds);
        if (highlight > 0.001f)
        {
            var corners = (first ? ImDrawFlags.RoundCornersTop : ImDrawFlags.None) |
                          (last ? ImDrawFlags.RoundCornersBottom : ImDrawFlags.None);
            if (corners == ImDrawFlags.None)
            {
                corners = ImDrawFlags.RoundCornersNone;
            }

            drawList.AddRectFilled(row.Min, row.Max,
                ImGui.GetColorU32(ui.TitleInk with { W = RowHighlightAlpha * highlight }),
                Metrics.Radius.Grouped * scale, corners);
        }

        var textLeft = row.Min.X + RowPadX * scale;
        var textRight = row.Max.X - RowPadX * scale;
        var titleLineHeight = Typography.LineHeight(TextStyles.Headline);
        var cursorY = row.Min.Y + RowPadY * scale;
        var firstLineCenterY = cursorY + Typography.Measure("Ag", TextStyles.Headline).Y * 0.5f;
        var chevronTip = new Vector2(textRight, firstLineCenterY);
        NewsArt.Chevron(drawList, chevronTip, NewsArt.ChevronSize * scale, Metrics.Stroke.Thin * scale,
            hovered ? ui.TitleInk : ui.MutedInk);
        if (trailing.Length > 0)
        {
            var trailingSize = Typography.Measure(trailing, TextStyles.Footnote);
            Typography.Draw(drawList,
                new Vector2(chevronTip.X - (NewsArt.ChevronSize + RowChevronGap) * scale - trailingSize.X,
                    firstLineCenterY - trailingSize.Y * 0.5f), trailing, ui.MutedInk, TextStyles.Footnote);
        }

        for (var lineIndex = 0; lineIndex < titleLines.Length; lineIndex++)
        {
            Typography.Draw(drawList, new Vector2(textLeft, cursorY), titleLines[lineIndex], ui.TitleInk,
                TextStyles.Headline);
            cursorY += titleLineHeight;
        }

        if (titleLines.Length == 0)
        {
            cursorY += titleLineHeight;
        }

        if (subtitle.Length > 0)
        {
            cursorY += RowLineGap * scale;
            Typography.Draw(drawList, new Vector2(textLeft, cursorY),
                Typography.FitText(subtitle, MathF.Max(1f, textRight - textLeft), TextStyles.Footnote), ui.MutedInk,
                TextStyles.Footnote);
        }

        if (!last)
        {
            FeedCell.Hairline(drawList, textLeft, row.Max.X, row.Max.Y, ui.Hairline);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(row.Min, row.Max, hovered))
        {
            OpenStory(feed.Category, story);
        }
    }

    private void DrawCardSkeleton(float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = Metrics.Space.Lg * scale;
        var radius = Metrics.Radius.Grouped * scale;
        var cursorY = origin.Y;
        SkeletonBar(drawList, origin.X, cursorY, width * 0.46f, SkeletonBarHeight * scale);
        cursorY += Typography.LineHeight(TextStyles.Subheadline) + DatelineGap * scale;
        for (var cardIndex = 0; cardIndex < SkeletonCards; cardIndex++)
        {
            var bannerHeight = width * NewsArt.BannerAspect;
            var cardHeight = bannerHeight + pad * 2f + (SkeletonTitleHeight * 2f + SkeletonBarHeight) * scale +
                             Metrics.Space.Sm * 3f * scale;
            var min = new Vector2(origin.X, cursorY);
            var max = new Vector2(origin.X + width, cursorY + cardHeight);
            ui.Card(drawList, min, max, radius);
            drawList.AddRectFilled(min, new Vector2(max.X, min.Y + bannerHeight), Skeleton.Fill(), radius,
                ImDrawFlags.RoundCornersTop);
            var barY = min.Y + bannerHeight + pad;
            SkeletonBar(drawList, min.X + pad, barY, width * 0.78f, SkeletonTitleHeight * scale);
            barY += (SkeletonTitleHeight + Metrics.Space.Sm) * scale;
            SkeletonBar(drawList, min.X + pad, barY, width * 0.52f, SkeletonTitleHeight * scale);
            barY += (SkeletonTitleHeight + Metrics.Space.Sm * 2f) * scale;
            SkeletonBar(drawList, min.X + pad, barY, width * 0.24f, SkeletonBarHeight * scale);
            cursorY = max.Y + CardGap * scale;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cursorY - origin.Y));
    }

    private void DrawRowSkeleton(float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var cursorY = origin.Y;
        SkeletonBar(drawList, origin.X, cursorY, width * 0.46f, SkeletonBarHeight * scale);
        cursorY += Typography.LineHeight(TextStyles.Subheadline) + DatelineGap * scale;
        SkeletonBar(drawList, origin.X, cursorY, width * 0.30f, SkeletonTitleHeight * scale);
        cursorY += CardSectionHeader.HeightUnits * scale + NewsArt.SectionHeaderGap * scale;
        var rowHeight = SkeletonRowHeight * scale;
        var groupMax = new Vector2(origin.X + width, cursorY + rowHeight * SkeletonRows);
        ui.Card(drawList, new Vector2(origin.X, cursorY), groupMax, Metrics.Radius.Grouped * scale);
        var textLeft = origin.X + RowPadX * scale;
        for (var rowIndex = 0; rowIndex < SkeletonRows; rowIndex++)
        {
            var rowTop = cursorY + rowIndex * rowHeight;
            var lineY = rowTop + RowPadY * scale;
            SkeletonBar(drawList, textLeft, lineY, width * 0.68f, SkeletonTitleHeight * scale);
            lineY += (SkeletonTitleHeight + Metrics.Space.Sm) * scale;
            SkeletonBar(drawList, textLeft, lineY, width * 0.36f, SkeletonBarHeight * scale);
            if (rowIndex < SkeletonRows - 1)
            {
                FeedCell.Hairline(drawList, textLeft, groupMax.X, rowTop + rowHeight, ui.Hairline);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, groupMax.Y - origin.Y));
    }

    private static void SkeletonBar(ImDrawListPtr drawList, float left, float top, float width, float height) =>
        Skeleton.Bar(drawList, new Vector2(left, top), new Vector2(left + width, top + height), height * 0.5f);

    private string MoreInLabel(NewsCategory category)
    {
        if (!ReferenceEquals(moreInCulture, Loc.Culture))
        {
            Array.Clear(moreInLabels);
            moreInCulture = Loc.Culture;
        }

        ref var label = ref moreInLabels[(int)category];
        label ??= Loc.T(L.News.MoreIn, Loc.T(NewsCategories.Label(category)));
        return label;
    }

    private static int RegionIndex(string region)
    {
        for (var index = 0; index < RegionKeys.Length; index++)
        {
            if (string.Equals(RegionKeys[index], region, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return 0;
    }

    private static string RegionLabel(string region) =>
        region switch
        {
            "jp" => Loc.T(L.News.RegionJapan),
            "fr" => Loc.T(L.News.RegionFrance),
            "de" => Loc.T(L.News.RegionGermany),
            "eu" => Loc.T(L.News.RegionEurope),
            "cn" => Loc.T(L.News.RegionChina),
            _ => Loc.T(L.News.RegionNorthAmerica),
        };
}
