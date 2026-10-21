using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Photos;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Photos;

internal sealed partial class PhotosApp
{
    private const float GridGap = 1.5f;
    private const float GridBottomPad = 8f;
    private const float DayHeaderHeight = 52f;
    private const float DayHeaderTitleDrop = 14f;
    private const float DayHeaderSubtitleGap = 2f;
    private const float FooterHeight = 48f;
    private const float CardGap = 14f;
    private const float YearCardAspect = 0.72f;
    private const float MonthCardAspect = 0.6f;
    private const float CardTitleInset = 16f;
    private const float CardScrimFraction = 0.55f;
    private const float CardScrimAlpha = 0.5f;
    private const float JumpMargin = 8f;
    private const float LevelBarHeight = 36f;
    private const float LevelBarWidth = 280f;
    private const float LevelBarGap = 10f;
    private const float LevelHighlightInset = 3f;
    private const float LevelHighlightAlpha = 0.22f;
    private const string SortMenuId = "photos.sort";
    private const int SortMenuItemCount = 7;
    private const int MenuRowFilter = 4;
    private const int MenuRowView = 5;
    private const int MenuRowFolder = 6;
    private const int PageRowBack = 0;
    private const int FilterRowAll = 1;
    private const int FilterRowFavorites = 2;
    private const int FilterRowNotInAlbum = 3;
    private const int FilterRowCount = 4;
    private const int ViewRowZoomIn = 1;
    private const int ViewRowZoomOut = 2;
    private const int ViewRowAspect = 3;
    private const int ViewRowCount = 4;
    private const int LevelCount = 4;
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 WhiteMuted = new(1f, 1f, 1f, 0.78f);

    private enum SortMenuPage : byte
    {
        Main,
        Filter,
        View,
    }

    private readonly struct JumpTarget
    {
        public static readonly JumpTarget None = new(LibraryLevel.All, 0);

        public readonly LibraryLevel Level;
        public readonly int Key;

        public JumpTarget(LibraryLevel level, int key)
        {
            Level = level;
            Key = key;
        }

        public bool Active => Key != 0;
    }

    private readonly TabBar tabBar = new();
    private readonly TabItem[] tabItems = new TabItem[2];
    private readonly NavBarButton[] libraryButtons = new NavBarButton[2];
    private readonly NavBarButton[] selectingButtons = new NavBarButton[1];
    private readonly DropdownMenu sortMenu = new();
    private readonly DropdownMenu.Item[] sortMenuItems = new DropdownMenu.Item[SortMenuItemCount];
    private readonly List<GridBand> dayBands = new();
    private readonly string[] levelLabels = new string[LevelCount];
    private SortMenuPage sortMenuPage;
    private string sortRowLabel = string.Empty;
    private int sortRowKey = -1;
    private bool sortRowAscending;
    private JumpTarget pendingJump = JumpTarget.None;
    private int layoutVersion;
    private int dayBandsVersion = -1;
    private float dayBandsCell = -1f;
    private int dayBandsColumns;
    private float dayBandsHeight;
    private Spring levelHighlight;
    private bool levelHighlightSettled;

    private LibraryLevel Level =>
        SortedByDate ? (LibraryLevel)Math.Clamp(configuration.PhotosLibraryLevel, 0, LevelCount - 1) : LibraryLevel.All;

    private void InvalidateLayouts() => layoutVersion++;

    private void DrawRoot(in PhoneContext context, Rect area)
    {
        using (TabBar.Host(!selecting))
        {
            if (activeTab == PhotosTab.Collections && !selecting)
            {
                DrawCollections(context, area);
            }
            else
            {
                DrawLibrary(context, area);
            }
        }

        if (selecting)
        {
            DrawSelectToolbar(context.Content);
            return;
        }

        DrawTabBar(context.Content);
    }

    private void DrawTabBar(Rect content)
    {
        tabItems[0] = new TabItem(Loc.T(L.Photos.Library), PhoneIcons.Photo, PhoneIcons.PhotoFilled,
            AnchorKey: "photos.tab.library");
        tabItems[1] = new TabItem(Loc.T(L.Photos.Collections), PhoneIcons.LibraryPhoto,
            AnchorKey: "photos.tab.albums");
        var result = tabBar.Draw(content, ui, tabItems, (int)activeTab);
        if (result.Tapped < 0)
        {
            return;
        }

        var picked = (PhotosTab)result.Tapped;
        if (picked == activeTab)
        {
            return;
        }

        activeTab = picked;
        configuration.PhotosSegment = (int)picked;
        configuration.Save();
    }

    private void DrawLibrary(in PhoneContext context, Rect area)
    {
        var scale = UiScale.Current;
        var navBar = AppHeader.BeginLargeTitle(context, false);
        var level = Level;
        var hasPhotos = filteredEntries.Length > 0;
        var showLevels = hasPhotos && SortedByDate && !selecting;
        var bottomInset = TabBar.ContentInset(scale) + (showLevels ? (LevelBarHeight + LevelBarGap) * scale : 0f);
        var surfaceRect = new Rect(new Vector2(area.Min.X, navBar.Body.Min.Y),
            new Vector2(area.Max.X, context.Content.Max.Y));
        UiAnchors.Report("photos.grid", new Rect(navBar.Body.Min, context.Content.Max));
        if (entries.Length == 0)
        {
            using (AppSurface.Begin(navBar.Body))
            {
                DrawLibraryEmpty(VisibleBody(navBar.Body, scale));
            }
        }
        else if (!hasPhotos)
        {
            using (AppSurface.Begin(navBar.Body))
            {
                DrawFilterEmpty(VisibleBody(navBar.Body, scale));
            }
        }
        else
        {
            ImGui.PushID($"photos.level{(int)level}");
            using (AppSurface.ReserveBottom(bottomInset))
            using (var surface = AppSurface.BeginEdgeToEdge(surfaceRect))
            {
                var origin = ImGui.GetCursorScreenPos();
                var originY = ImGui.GetCursorPosY();
                var width = area.Width;
                var drawList = ImGui.GetWindowDrawList();
                var height = level switch
                {
                    LibraryLevel.Years => DrawYearCards(drawList, origin, width, context.Content, scale),
                    LibraryLevel.Months => DrawMonthCards(drawList, origin, width, context.Content, scale),
                    LibraryLevel.Days => DrawDayGrid(drawList, origin, width, context.Content, scale),
                    _ => DrawFlatGrid(drawList, origin, width, filteredPaths, GridMode.Library, 0, scale),
                };
                height += DrawFooter(drawList, new Vector2(origin.X, origin.Y + height), width,
                    CountLabel(filteredEntries.Length));
                ApplyJump(surface, level, originY, scale);
                ImGui.SetCursorScreenPos(origin);
                ImGui.Dummy(new Vector2(width, height));
            }

            ImGui.PopID();
        }

        var buttons = LibraryButtons(hasPhotos);
        var title = selecting ? SelectionTitle() : Loc.T(L.Photos.Library);
        var pressed = AppHeader.EndLargeTitle(in navBar, context, "photos.nav.library", title, NavBarStyle.From(ui),
            buttons);
        HandleLibraryButton(navBar, pressed, buttons.Length);
        if (showLevels)
        {
            DrawLevelBar(context.Content, scale);
        }
    }

    private ReadOnlySpan<NavBarButton> LibraryButtons(bool hasPhotos)
    {
        if (selecting)
        {
            selectingButtons[0] = new NavBarButton(PhoneIcons.X, Loc.T(L.Common.Cancel));
            return selectingButtons;
        }

        if (entries.Length == 0)
        {
            return ReadOnlySpan<NavBarButton>.Empty;
        }

        libraryButtons[0] = new NavBarButton(PhoneIcons.CircleCheck, Loc.T(L.Photos.Select));
        libraryButtons[1] = new NavBarButton(PhoneIcons.Dots, Loc.T(L.Photos.ViewOptions));
        return hasPhotos ? libraryButtons : libraryButtons.AsSpan(1, 1);
    }

    private void HandleLibraryButton(in NavBarFrame navBar, int pressed, int count)
    {
        if (count == 0)
        {
            return;
        }

        var menuIndex = count - 1;
        var menuRect = AppHeader.LargeTitleButtonRect(navBar, menuIndex, count);
        if (!selecting)
        {
            UiAnchors.Report("photos.library.menu", menuRect);
        }

        if (pressed < 0)
        {
            return;
        }

        if (selecting)
        {
            EndSelect();
            return;
        }

        if (pressed == menuIndex)
        {
            sortMenu.Toggle(SortMenuId, menuRect);
            return;
        }

        BeginSelect(SelectionScope.Library);
    }

    private static Rect VisibleBody(Rect body, float scale) =>
        new(body.Min, new Vector2(body.Max.X, MathF.Max(body.Min.Y + 1f, body.Max.Y - TabBar.ContentInset(scale))));

    private void DrawLibraryEmpty(Rect body)
    {
        var hint = Loc.T(configuration.ImportScreenshots ? L.Photos.EmptyHintScreenshots : L.Photos.UseCameraHint);
        if (EmptyState.Draw(body, ui, PhoneIcons.Photo, Loc.T(L.Photos.NoPhotos), hint, Loc.T(L.Apps.Camera)))
        {
            frameNavigation.Open("camera");
        }
    }

    private void DrawFilterEmpty(Rect body)
    {
        var favoritesOnly = Filter == PhotoFilter.Favorites;
        if (EmptyState.Draw(body, ui, favoritesOnly ? PhoneIcons.Heart : PhoneIcons.Photo,
                Loc.T(favoritesOnly ? L.Photos.NoFavorites : L.Photos.FilterEmpty),
                Loc.T(favoritesOnly ? L.Photos.NoFavoritesHint : L.Photos.FilterEmptyHint),
                Loc.T(L.Photos.ShowAllItems)))
        {
            SetFilter(PhotoFilter.All);
        }
    }

    private float DrawFooter(ImDrawListPtr drawList, Vector2 origin, float width, string label)
    {
        var height = FooterHeight * UiScale.Current;
        var fitted = Typography.FitText(label, width, TextStyles.Footnote);
        Typography.DrawCentered(drawList, new Vector2(origin.X + width * 0.5f, origin.Y + height * 0.5f), fitted,
            ui.MutedInk, TextStyles.Footnote);
        return height;
    }

    private void ApplyJump(in AppSurface.SurfaceScope surface, LibraryLevel level, float originY, float scale)
    {
        if (!pendingJump.Active || pendingJump.Level != level)
        {
            return;
        }

        var target = JumpOffset(level, pendingJump.Key, scale);
        pendingJump = JumpTarget.None;
        if (target < 0f)
        {
            return;
        }

        var inline = NavBarMetrics.InlineHeight * scale;
        surface.JumpTo(MathF.Max(0f, originY + target - inline - JumpMargin * scale));
    }

    private float JumpOffset(LibraryLevel level, int key, float scale)
    {
        if (level == LibraryLevel.Days)
        {
            for (var index = 0; index < dayBands.Count; index++)
            {
                var band = dayBands[index];
                if (band.Header && PhotoGrouping.MonthKey(band.Day) == key)
                {
                    return band.Top;
                }
            }

            return -1f;
        }

        if (level != LibraryLevel.Months)
        {
            return -1f;
        }

        var width = frameScreen.Width - frameTheme.SidePadding * 2f * scale;
        var stride = width * MonthCardAspect + CardGap * scale;
        for (var index = 0; index < monthRuns.Count; index++)
        {
            if (PhotoGrouping.YearOfMonthKey(monthRuns[index].Key) == key)
            {
                return CardGap * scale + index * stride;
            }
        }

        return -1f;
    }

    private void OpenLevel(LibraryLevel level, int jumpKey)
    {
        configuration.PhotosLibraryLevel = (int)level;
        configuration.Save();
        pendingJump = jumpKey == 0 ? JumpTarget.None : new JumpTarget(level, jumpKey);
    }

    private float DrawYearCards(ImDrawListPtr drawList, Vector2 origin, float width, Rect content, float scale)
    {
        var left = content.Min.X;
        var cardWidth = content.Width;
        var cardHeight = cardWidth * YearCardAspect;
        var gap = CardGap * scale;
        var y = origin.Y + gap;
        var visible = VisibleWindow(cardHeight);
        for (var index = 0; index < yearRuns.Count; index++)
        {
            var run = yearRuns[index];
            var rect = new Rect(new Vector2(left, y), new Vector2(left + cardWidth, y + cardHeight));
            y += cardHeight + gap;
            if (rect.Max.Y < visible.X || rect.Min.Y > visible.Y)
            {
                continue;
            }

            var cover = filteredEntries[run.Start + run.Count / 2].Path;
            if (DrawLibraryCard(drawList, rect, cover, YearTitle(run.Key), CountLabel(run.Count), TextStyles.Title1,
                    scale))
            {
                OpenLevel(LibraryLevel.Months, run.Key);
            }
        }

        return y - origin.Y;
    }

    private float DrawMonthCards(ImDrawListPtr drawList, Vector2 origin, float width, Rect content, float scale)
    {
        var left = content.Min.X;
        var cardWidth = content.Width;
        var cardHeight = cardWidth * MonthCardAspect;
        var gap = CardGap * scale;
        var y = origin.Y + gap;
        var visible = VisibleWindow(cardHeight);
        for (var index = 0; index < monthRuns.Count; index++)
        {
            var run = monthRuns[index];
            var rect = new Rect(new Vector2(left, y), new Vector2(left + cardWidth, y + cardHeight));
            y += cardHeight + gap;
            if (rect.Max.Y < visible.X || rect.Min.Y > visible.Y)
            {
                continue;
            }

            var cover = filteredEntries[run.Start].Path;
            if (DrawLibraryCard(drawList, rect, cover, MonthTitle(run.Key), CountLabel(run.Count), TextStyles.Title2,
                    scale))
            {
                OpenLevel(LibraryLevel.Days, run.Key);
            }
        }

        return y - origin.Y;
    }

    private bool DrawLibraryCard(ImDrawListPtr drawList, Rect rect, string coverPath, string title, string subtitle,
        in TextStyle titleStyle, float scale)
    {
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var down = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var press = PressFx.Scale(ImGui.GetID(title), down, PressFx.CardPressedScale);
        var half = rect.Size * 0.5f * press;
        var min = rect.Center - half;
        var max = rect.Center + half;
        var rounding = Metrics.Radius.Widget * scale;
        PhotosChrome.Cover(drawList, GetCover(coverPath) ?? GetThumbnail(coverPath), min, max, rounding, ui, scale,
            hovered);
        var scrimTop = max.Y - (max.Y - min.Y) * CardScrimFraction;
        Squircle.FillVerticalGradient(drawList, new Vector2(min.X, scrimTop), max, rounding,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0f)), ImGui.GetColorU32(new Vector4(0f, 0f, 0f, CardScrimAlpha)));
        var inset = CardTitleInset * scale;
        var textWidth = MathF.Max(1f, max.X - min.X - inset * 2f);
        var subtitleHeight = Typography.LineHeight(TextStyles.SubheadlineEmphasized);
        var subtitleTop = max.Y - inset - subtitleHeight;
        Typography.Draw(drawList, new Vector2(min.X + inset, subtitleTop),
            Typography.FitText(subtitle, textWidth, TextStyles.SubheadlineEmphasized), WhiteMuted,
            TextStyles.SubheadlineEmphasized);
        var titleHeight = Typography.LineHeight(titleStyle);
        Typography.Draw(drawList, new Vector2(min.X + inset, subtitleTop - titleHeight),
            Typography.FitText(title, textWidth, titleStyle), White, titleStyle);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    private Vector2 VisibleWindow(float margin)
    {
        var top = ImGui.GetWindowPos().Y;
        return new Vector2(top - margin, top + ImGui.GetWindowSize().Y + margin);
    }

    private float DrawDayGrid(ImDrawListPtr drawList, Vector2 origin, float width, Rect content, float scale)
    {
        var columns = Columns;
        var gap = GridGap * scale;
        var cell = (width - gap * (columns - 1)) / columns;
        LayoutDayBands(cell, gap, columns, scale);
        var visible = VisibleWindow(cell + DayHeaderHeight * scale);
        for (var index = 0; index < dayBands.Count; index++)
        {
            var band = dayBands[index];
            var top = origin.Y + band.Top;
            if (top + band.Height < visible.X || top > visible.Y)
            {
                continue;
            }

            if (band.Header)
            {
                DrawDayHeader(drawList, content.Min.X, content.Max.X, top, band, scale);
                continue;
            }

            for (var column = 0; column < band.PhotoCount; column++)
            {
                var absolute = band.PhotoStart + column;
                var min = new Vector2(origin.X + column * (cell + gap), top);
                var max = new Vector2(min.X + cell, top + cell);
                var path = filteredPaths[absolute];
                if (DrawTile(drawList, min, max, path, false, scale) == TileHit.Open)
                {
                    ShowViewer(filteredPaths, absolute, false, true);
                }
            }
        }

        return dayBandsHeight;
    }

    private void LayoutDayBands(float cell, float gap, int columns, float scale)
    {
        if (dayBandsVersion == layoutVersion && MathF.Abs(dayBandsCell - cell) < 0.01f && dayBandsColumns == columns)
        {
            return;
        }

        dayBandsVersion = layoutVersion;
        dayBandsCell = cell;
        dayBandsColumns = columns;
        dayBands.Clear();
        var headerHeight = DayHeaderHeight * scale;
        var rowStride = cell + gap;
        var y = 0f;
        var index = 0;
        var end = filteredEntries.Length;
        while (index < end)
        {
            var day = filteredEntries[index].Taken.Date;
            var dayStart = index;
            var place = 0u;
            while (index < end && filteredEntries[index].Taken.Date == day)
            {
                if (place == 0)
                {
                    place = PlaceOf(filteredEntries[index].Path);
                }

                index++;
            }

            var dayCount = index - dayStart;
            dayBands.Add(new GridBand
            {
                Header = true,
                Day = filteredEntries[dayStart].Taken,
                DayCount = dayCount,
                Place = place,
                Top = y,
                Height = headerHeight,
            });
            y += headerHeight;
            var rows = (dayCount + columns - 1) / columns;
            for (var row = 0; row < rows; row++)
            {
                var rowStart = dayStart + row * columns;
                dayBands.Add(new GridBand
                {
                    PhotoStart = rowStart,
                    PhotoCount = Math.Min(columns, dayStart + dayCount - rowStart),
                    Top = y,
                    Height = cell,
                });
                y += rowStride;
            }
        }

        dayBandsHeight = y + GridBottomPad * scale;
    }

    private void DrawDayHeader(ImDrawListPtr drawList, float left, float right, float top, GridBand band, float scale)
    {
        var count = CountLabel(band.DayCount);
        var countSize = Typography.Measure(count, TextStyles.Footnote);
        var titleTop = top + DayHeaderTitleDrop * scale;
        var titleMax = MathF.Max(1f, right - left - countSize.X - Metrics.Space.Md * scale);
        var title = Typography.FitText(DayTitle(band.Day), titleMax, TextStyles.Headline);
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var placeName = PhotoPlaces.Name(band.Place);
        if (placeName.Length > 0)
        {
            titleTop -= Typography.LineHeight(TextStyles.Footnote) * 0.5f;
        }

        Typography.Draw(drawList, new Vector2(left, titleTop), title, ui.TitleInk, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(right - countSize.X, titleTop + (titleHeight - countSize.Y) * 0.5f), count,
            ui.MutedInk, TextStyles.Footnote);
        if (placeName.Length == 0)
        {
            return;
        }

        Typography.Draw(drawList, new Vector2(left, titleTop + titleHeight + DayHeaderSubtitleGap * scale),
            Typography.FitText(placeName, right - left, TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);
    }

    private void DrawLevelBar(Rect content, float scale)
    {
        if (levelLabels[0] is null)
        {
            levelLabels[(int)LibraryLevel.Years] = Loc.T(L.Photos.Years);
            levelLabels[(int)LibraryLevel.Months] = Loc.T(L.Photos.Months);
            levelLabels[(int)LibraryLevel.Days] = Loc.T(L.Photos.Days);
            levelLabels[(int)LibraryLevel.All] = Loc.T(L.Photos.AllPhotos);
        }

        var zone = TabBar.Zone(content, scale);
        var width = MathF.Min(LevelBarWidth * scale, content.Width);
        var bottom = zone.Min.Y - LevelBarGap * scale;
        var bar = new Rect(new Vector2(content.Center.X - width * 0.5f, bottom - LevelBarHeight * scale),
            new Vector2(content.Center.X + width * 0.5f, bottom));
        using var layer = ScreenLayer.Begin("photos.levels", bar, false);
        UiInteract.HoverOverlay(bar);
        var drawList = ImGui.GetWindowDrawList();
        var radius = bar.Height * 0.5f;
        Material.ThemedGlass(drawList, bar.Min, bar.Max, radius, scale, ui.BackdropColor, TabBar.GlassOpacity);
        var level = (int)Level;
        var segment = bar.Width / LevelCount;
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        if (!levelHighlightSettled)
        {
            levelHighlight.SnapTo(level);
            levelHighlightSettled = true;
        }

        var position = levelHighlight.Step(level, Motion.TabBar, delta);
        var inset = LevelHighlightInset * scale;
        var highlightMin = new Vector2(bar.Min.X + segment * position + inset, bar.Min.Y + inset);
        var highlightMax = new Vector2(highlightMin.X + segment - inset * 2f, bar.Max.Y - inset);
        Squircle.Fill(drawList, highlightMin, highlightMax, (highlightMax.Y - highlightMin.Y) * 0.5f,
            ImGui.GetColorU32(Core.Theme.Palette.WithAlpha(ui.Accent, LevelHighlightAlpha)));
        for (var index = 0; index < LevelCount; index++)
        {
            var cellMin = new Vector2(bar.Min.X + segment * index, bar.Min.Y);
            var cellMax = new Vector2(cellMin.X + segment, bar.Max.Y);
            UiAnchors.Report(LevelAnchor(index), new Rect(cellMin, cellMax));
            var active = index == level;
            var label = Typography.FitText(levelLabels[index], segment - inset * 2f, TextStyles.FootnoteEmphasized);
            Typography.DrawCentered(drawList, (cellMin + cellMax) * 0.5f, label, active ? ui.Accent : ui.TitleInk,
                TextStyles.FootnoteEmphasized);
            var hovered = !UiInteract.InputBlocked && UiInteract.HoverWindowOnly(cellMin, cellMax);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(cellMin, cellMax, hovered) && !active)
            {
                OpenLevel((LibraryLevel)index, 0);
            }
        }
    }

    private static string LevelAnchor(int index) => index switch
    {
        0 => "photos.level.years",
        1 => "photos.level.months",
        2 => "photos.level.days",
        _ => "photos.level.all",
    };

    private void DrawSortMenu(Rect screen)
    {
        if (!sortMenu.Open)
        {
            sortMenuPage = SortMenuPage.Main;
            return;
        }

        sortMenu.KeepOpen = true;
        switch (sortMenuPage)
        {
            case SortMenuPage.Filter:
                DrawFilterPage(screen);
                break;
            case SortMenuPage.View:
                DrawViewPage(screen);
                break;
            default:
                DrawSortPage(screen);
                break;
        }
    }

    private void DrawSortPage(Rect screen)
    {
        var key = SortKey;
        sortMenu.Header = string.Empty;
        for (var index = 0; index <= (int)PhotoSortKey.Dimensions; index++)
        {
            var rowKey = (PhotoSortKey)index;
            sortMenuItems[index] = new DropdownMenu.Item(rowKey == key ? ActiveSortLabel(key) : Loc.T(SortLabel(rowKey)),
                Selected: rowKey == key);
        }

        sortMenuItems[MenuRowFilter] = new DropdownMenu.Item(Loc.T(L.Photos.Filter), PhoneIcons.AdjustmentsHorizontal,
            Selected: Filter != PhotoFilter.All);
        sortMenuItems[MenuRowView] = new DropdownMenu.Item(Loc.T(L.Photos.ViewOptions), PhoneIcons.LayoutList);
        sortMenuItems[MenuRowFolder] = new DropdownMenu.Item(Loc.T(L.Photos.OpenFolder),
            IconGlyph.Of(FontAwesomeIcon.FolderOpen));
        var picked = sortMenu.Draw(screen, frameTheme, sortMenuItems);
        if (picked < 0)
        {
            return;
        }

        if (picked == MenuRowFilter)
        {
            sortMenuPage = SortMenuPage.Filter;
            return;
        }

        if (picked == MenuRowView)
        {
            sortMenuPage = SortMenuPage.View;
            return;
        }

        if (picked == MenuRowFolder)
        {
            sortMenu.Close();
            UrlActions.OpenFolder(library.DirectoryPath);
            return;
        }

        var pickedKey = (PhotoSortKey)picked;
        SetSort(pickedKey, pickedKey == key ? !configuration.PhotosSortAscending : DefaultAscending(pickedKey));
    }

    private void DrawFilterPage(Rect screen)
    {
        var filter = Filter;
        sortMenu.Header = Loc.T(L.Photos.Filter);
        sortMenuItems[PageRowBack] = new DropdownMenu.Item(Loc.T(L.Photos.MenuBack),
            IconGlyph.Of(FontAwesomeIcon.ChevronLeft));
        sortMenuItems[FilterRowAll] = new DropdownMenu.Item(Loc.T(L.Photos.FilterAll),
            Selected: filter == PhotoFilter.All);
        sortMenuItems[FilterRowFavorites] = new DropdownMenu.Item(Loc.T(L.Photos.Favorites),
            Selected: filter == PhotoFilter.Favorites);
        sortMenuItems[FilterRowNotInAlbum] = new DropdownMenu.Item(Loc.T(L.Photos.FilterNotInAlbum),
            Selected: filter == PhotoFilter.NotInAlbum);
        switch (sortMenu.Draw(screen, frameTheme, sortMenuItems.AsSpan(0, FilterRowCount)))
        {
            case PageRowBack:
                sortMenuPage = SortMenuPage.Main;
                break;
            case FilterRowAll:
                SetFilter(PhotoFilter.All);
                break;
            case FilterRowFavorites:
                SetFilter(PhotoFilter.Favorites);
                break;
            case FilterRowNotInAlbum:
                SetFilter(PhotoFilter.NotInAlbum);
                break;
        }
    }

    private void DrawViewPage(Rect screen)
    {
        sortMenu.Header = Loc.T(L.Photos.ViewOptions);
        sortMenuItems[PageRowBack] = new DropdownMenu.Item(Loc.T(L.Photos.MenuBack),
            IconGlyph.Of(FontAwesomeIcon.ChevronLeft));
        sortMenuItems[ViewRowZoomIn] = new DropdownMenu.Item(Loc.T(L.Photos.ZoomIn));
        sortMenuItems[ViewRowZoomOut] = new DropdownMenu.Item(Loc.T(L.Photos.ZoomOut));
        sortMenuItems[ViewRowAspect] = new DropdownMenu.Item(Loc.T(L.Photos.AspectRatioGrid),
            Selected: configuration.PhotosAspectGrid);
        switch (sortMenu.Draw(screen, frameTheme, sortMenuItems.AsSpan(0, ViewRowCount)))
        {
            case PageRowBack:
                sortMenuPage = SortMenuPage.Main;
                break;
            case ViewRowZoomIn:
                SetColumns(Columns - 1);
                break;
            case ViewRowZoomOut:
                SetColumns(Columns + 1);
                break;
            case ViewRowAspect:
                configuration.PhotosAspectGrid = !configuration.PhotosAspectGrid;
                configuration.Save();
                break;
        }
    }

    private static LocString SortLabel(PhotoSortKey key) => key switch
    {
        PhotoSortKey.Name => L.Photos.SortName,
        PhotoSortKey.Size => L.Photos.SortSize,
        PhotoSortKey.Dimensions => L.Photos.SortDimensions,
        _ => L.Photos.SortDate,
    };

    private string ActiveSortLabel(PhotoSortKey key)
    {
        var ascending = configuration.PhotosSortAscending;
        if (sortRowKey == (int)key && sortRowAscending == ascending)
        {
            return sortRowLabel;
        }

        sortRowKey = (int)key;
        sortRowAscending = ascending;
        sortRowLabel = string.Concat(Loc.T(SortLabel(key)), " · ",
            Loc.T(ascending ? AscendingLabel(key) : DescendingLabel(key)));
        return sortRowLabel;
    }

    private void SetFilter(PhotoFilter filter)
    {
        sortMenu.Close();
        if (Filter == filter)
        {
            return;
        }

        configuration.PhotosFilter = (int)filter;
        configuration.Save();
        ApplyFilter();
    }

    private void SetColumns(int columns)
    {
        var clamped = Math.Clamp(columns, MinColumns, MaxColumns);
        if (clamped == Columns)
        {
            return;
        }

        configuration.PhotosGridColumns = clamped;
        configuration.Save();
        InvalidateLayouts();
    }

    private static LocString DescendingLabel(PhotoSortKey key) => key switch
    {
        PhotoSortKey.Name => L.Photos.SortZToA,
        PhotoSortKey.Size => L.Photos.SortLargestFirst,
        PhotoSortKey.Dimensions => L.Photos.SortLargestFirst,
        _ => L.Photos.SortNewestFirst,
    };

    private static LocString AscendingLabel(PhotoSortKey key) => key switch
    {
        PhotoSortKey.Name => L.Photos.SortAToZ,
        PhotoSortKey.Size => L.Photos.SortSmallestFirst,
        PhotoSortKey.Dimensions => L.Photos.SortSmallestFirst,
        _ => L.Photos.SortOldestFirst,
    };

    private void SetSort(PhotoSortKey key, bool ascending)
    {
        if (SortKey == key && configuration.PhotosSortAscending == ascending)
        {
            return;
        }

        configuration.PhotosSortKey = (int)key;
        configuration.PhotosSortAscending = ascending;
        configuration.Save();
        Refresh();
    }

    private struct GridBand
    {
        public bool Header;
        public DateTime Day;
        public int DayCount;
        public uint Place;
        public int PhotoStart;
        public int PhotoCount;
        public float Top;
        public float Height;
    }
}
