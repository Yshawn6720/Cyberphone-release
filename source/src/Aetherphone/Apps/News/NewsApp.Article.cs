using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.News;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.News;

internal sealed partial class NewsApp
{
    private const float ArticleTopGap = 4f;
    private const float EyebrowGap = 8f;
    private const float TitleToMeta = 8f;
    private const float MetaToMedia = 18f;
    private const float MediaGap = 18f;
    private const float ParagraphGap = 12f;
    private const float ActionGap = 22f;
    private const float ActionHeight = Button.LargeHeight;
    private const int NextStoryCount = 3;
    private const float LoadingOffset = 120f;
    private const float LoadingRadius = 13f;
    private const string ArticleNavId = "news.article.nav";
    private const string ReadButtonId = "news.article.read";
    private const string ArticleAnchor = "news.article";
    private const int NoArticleTop = -1;

    private readonly NavBarButton[] articleButtons = new NavBarButton[2];
    private readonly string?[] categoryEyebrows = new string?[NewsCategories.All.Length];
    private CultureInfo? categoryEyebrowCulture;
    private int articleTopDepth = NoArticleTop;

    private void DrawArticle(Rect area, NewsView view, int depth)
    {
        var scale = UiScale.Current;
        var context = new PhoneContext(area, theme, navigation);
        var inlineHeight = NavBarMetrics.InlineHeight * scale;
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + inlineHeight), area.Max);
        AppSurface.ArmNavBar(body.Min.Y, inlineHeight);
        var frame = new NavBarFrame(area, body, scale, inlineHeight);
        var feed = Feed(view.Category);
        var index = feed.IndexOf(view.StoryId);
        var backTitle = depth > 2 ? string.Empty : PageTitle(activeCategory);
        if (index < 0)
        {
            DrawMissingStory(body, feed, scale);
            AppHeader.EndLargeTitle(in frame, context, ArticleNavId, string.Empty, NavBarStyle.From(ui),
                ReadOnlySpan<NavBarButton>.Empty, backTitle, back);
            return;
        }

        var story = feed.Stories[index];
        using (var surface = AppSurface.Begin(body))
        {
            if (depth == articleTopDepth && depth == router.Depth)
            {
                surface.JumpToTop();
                articleTopDepth = NoArticleTop;
            }

            DrawStory(feed, index, scale);
        }

        articleButtons[0] = new NavBarButton(IconGlyph.Of(FontAwesomeIcon.Link), Loc.T(L.News.CopyLink));
        articleButtons[1] = new NavBarButton(IconGlyph.Of(FontAwesomeIcon.ExternalLinkAlt),
            Loc.T(L.News.ReadOnLodestone));
        var pressed = AppHeader.EndLargeTitle(in frame, context, ArticleNavId, story.Item.Title, NavBarStyle.From(ui),
            articleButtons, backTitle, back);
        if (pressed == 0)
        {
            CopyLink(story);
        }
        else if (pressed == 1)
        {
            OpenOnLodestone(story);
        }
    }

    private void DrawMissingStory(Rect body, NewsFeed feed, float scale)
    {
        if (feed.State is NewsState.Idle or NewsState.Loading)
        {
            LoadingPulse.Draw(new Vector2(body.Center.X, body.Min.Y + LoadingOffset * scale), LoadingRadius * scale,
                ui.Accent, ui.MutedInk, Loc.T(L.Common.Loading));
            return;
        }

        NewsArt.StatePanel(body, ui, FontAwesomeIcon.Newspaper, Loc.T(L.News.StoryGone), Loc.T(L.News.StoryGoneHint),
            string.Empty);
    }

    private void DrawStory(NewsFeed feed, int index, float scale)
    {
        var story = feed.Stories[index];
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var left = origin.X;
        var cursorY = origin.Y + ArticleTopGap * scale;
        Typography.Draw(drawList, new Vector2(left, cursorY),
            Typography.FitText(CategoryEyebrow(feed.Category), width, TextStyles.FootnoteEmphasized), ui.Accent,
            TextStyles.FootnoteEmphasized);
        cursorY += Typography.LineHeight(TextStyles.FootnoteEmphasized) + EyebrowGap * scale;
        cursorY += Typography.DrawWrappedLeft(new Vector2(left, cursorY), story.Item.Title, ui.TitleInk,
            TextStyles.Title1, width);
        cursorY += TitleToMeta * scale;
        Typography.Draw(drawList, new Vector2(left, cursorY),
            Typography.FitText(story.LongDate, width, TextStyles.Subheadline), ui.MutedInk, TextStyles.Subheadline);
        cursorY += Typography.LineHeight(TextStyles.Subheadline) + MetaToMedia * scale;

        if (story.HasImage)
        {
            var texture = Banner(story.Item.Image, width, out var reveal, out var failed);
            var height = NewsArt.BannerHeight(texture, width);
            NewsArt.Banner(drawList, ui, texture, failed, reveal, new Vector2(left, cursorY),
                new Vector2(left + width, cursorY + height), Metrics.Radius.Grouped * scale, ImDrawFlags.RoundCornersAll,
                scale);
            cursorY += height + MediaGap * scale;
        }

        var window = feed.Board.WindowOf(index);
        if (!window.IsEmpty)
        {
            var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            cursorY = DrawStatusHero(drawList, left, cursorY, width, story, window, window.StatusAt(nowUnix),
                ref articleCountdown, false, nowUnix, scale) + MediaGap * scale;
        }

        var paragraphs = story.Paragraphs;
        for (var paragraphIndex = 0; paragraphIndex < paragraphs.Length; paragraphIndex++)
        {
            if (paragraphIndex > 0)
            {
                cursorY += ParagraphGap * scale;
            }

            cursorY += Typography.DrawWrappedLeft(new Vector2(left, cursorY), paragraphs[paragraphIndex], ui.BodyInk,
                TextStyles.Body, width);
        }

        if (paragraphs.Length == 0)
        {
            cursorY += Typography.DrawWrappedLeft(new Vector2(left, cursorY), Loc.T(L.News.FullPostHint), ui.MutedInk,
                TextStyles.Subheadline, width);
        }

        if (UiAnchors.Recording)
        {
            UiAnchors.Report(ArticleAnchor, new Rect(origin, new Vector2(origin.X + width, cursorY)));
        }

        cursorY += ActionGap * scale;
        var action = new Rect(new Vector2(left, cursorY), new Vector2(left + width, cursorY + ActionHeight * scale));
        if (story.Item.Url.Length > 0 &&
            Button.Draw(drawList, action, Loc.T(L.News.ReadOnLodestone), ui.Ink, id: ReadButtonId))
        {
            OpenOnLodestone(story);
        }

        cursorY = action.Max.Y;
        cursorY = DrawNextStories(drawList, left, cursorY, width, feed, index, scale);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cursorY - origin.Y + BottomGap * scale));
    }

    private float DrawNextStories(ImDrawListPtr drawList, float left, float top, float width, NewsFeed feed,
        int index, float scale)
    {
        var count = 0;
        var total = feed.Stories.Length;
        for (var step = 1; step < total && count < NextStoryCount; step++)
        {
            rowBuffer[count++] = (index + step) % total;
        }

        if (count == 0)
        {
            return top;
        }

        var cursorY = top + NewsArt.SectionTopGap * scale;
        cursorY += CardSectionHeader.Draw(drawList, new Vector2(left, cursorY), width, MoreInLabel(feed.Category),
            ui.TitleInk) + NewsArt.SectionHeaderGap * scale;
        var anchorTaken = true;
        return DrawRowGroup(drawList, left, cursorY, width, feed, rowBuffer.AsSpan(0, count), ref anchorTaken, scale);
    }

    private void CopyLink(NewsStory story)
    {
        if (story.Item.Url.Length == 0)
        {
            return;
        }

        UiFeedback.Play(UiSound.Tap);
        ImGui.SetClipboardText(story.Item.Url);
        toast.Show(Loc.T(L.Common.Copied));
    }

    private static void OpenOnLodestone(NewsStory story)
    {
        if (story.Item.Url.Length == 0)
        {
            return;
        }

        UiFeedback.Play(UiSound.Tap);
        UrlActions.OpenInBrowser(story.Item.Url);
    }

    private string CategoryEyebrow(NewsCategory category)
    {
        if (!ReferenceEquals(categoryEyebrowCulture, Loc.Culture))
        {
            Array.Clear(categoryEyebrows);
            categoryEyebrowCulture = Loc.Culture;
        }

        ref var label = ref categoryEyebrows[(int)category];
        label ??= Loc.Upper(Loc.T(NewsCategories.Label(category)));
        return label;
    }
}
