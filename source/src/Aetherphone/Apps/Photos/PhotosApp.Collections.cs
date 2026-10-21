using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Photos;

internal sealed partial class PhotosApp
{
    private const int AlbumColumns = 2;
    private const float AlbumGap = 12f;
    private const float AlbumRounding = 16f;
    private const float AlbumTextBlock = 46f;
    private const float AlbumTitleGap = 8f;
    private const float AlbumCountGap = 20f;
    private const float AlbumBadgeInset = 8f;
    private const float RailCardWidth = 132f;
    private const float RailGap = 10f;
    private const float SectionGap = 10f;
    private const float CollectionsBottomPad = 16f;
    private const float CollectionRowHeight = 52f;
    private const float CollectionTile = 30f;
    private const float CollectionTileGap = 12f;
    private const float CollectionTileGlyph = 0.58f;
    private const float ChevronSize = 7f;
    private const float RowWashAlpha = 0.06f;
    private const float RowPressAlpha = 0.11f;

    private readonly NavBarButton[] collectionButtons = new NavBarButton[1];
    private readonly PanRail monthsRail = new();
    private readonly PanRail placesRail = new();

    private void DrawCollections(in PhoneContext context, Rect area)
    {
        var scale = UiScale.Current;
        if (!configuration.PhotosMonthlyAlbumsAsked && entries.Length > 0)
        {
            AskMonthlyAlbums();
        }

        var navBar = AppHeader.BeginLargeTitle(context, false);
        using (TabBar.ReserveContent(scale))
        {
            ImGui.PushID("photos.collections");
            using (AppSurface.Begin(navBar.Body))
            {
                var origin = ImGui.GetCursorScreenPos();
                var width = ScrollLayout.StableContentWidth();
                var drawList = ImGui.GetWindowDrawList();
                var left = origin.X;
                var right = origin.X + width;
                var y = origin.Y;
                y = DrawPinnedSection(drawList, left, right, y, scale);
                y = DrawAlbumsSection(drawList, left, right, y, scale);
                if (places.Length > 0)
                {
                    y = DrawPlacesSection(drawList, left, width, y, scale);
                }

                if (configuration.PhotosMonthlyAlbums && monthAlbums.Length > 0)
                {
                    y = DrawMonthsSection(drawList, left, width, y, scale);
                }

                y = DrawUtilitiesSection(drawList, left, right, y, scale);
                ImGui.SetCursorScreenPos(origin);
                ImGui.Dummy(new Vector2(width, y - origin.Y + CollectionsBottomPad * scale));
            }

            ImGui.PopID();
        }

        collectionButtons[0] = new NavBarButton(PhoneIcons.Plus, Loc.T(L.Photos.CreateAlbum));
        var pressed = AppHeader.EndLargeTitle(in navBar, context, "photos.nav.collections", Loc.T(L.Photos.Collections),
            NavBarStyle.From(ui), collectionButtons);
        UiAnchors.Report("photos.albums.new", AppHeader.LargeTitleButtonRect(navBar, 0, 1));
        if (pressed == 0)
        {
            OpenCreateAlbumSheet(null);
        }
    }

    private float DrawSectionTitle(ImDrawListPtr drawList, float left, float right, float top, string title,
        float scale)
    {
        return top + CardSectionHeader.Draw(drawList, new Vector2(left, top), MathF.Max(1f, right - left), title,
            ui.TitleInk);
    }

    private float DrawPinnedSection(ImDrawListPtr drawList, float left, float right, float top, float scale)
    {
        var y = DrawSectionTitle(drawList, left, right, top, Loc.T(L.Photos.Pinned), scale);
        ImGui.SetCursorScreenPos(new Vector2(left, y));
        var card = GroupCard.Begin(ui, 2, CollectionRowHeight);
        card.SeparatorInset = CollectionTile + CollectionTileGap;
        var favoritesRow = card.NextRow();
        var recentsRow = card.NextRow();
        var bounds = card.Bounds;
        card.End();
        if (DrawCollectionRow(drawList, bounds, favoritesRow, true, false, PhoneIcons.HeartFilled, AccentRing.Rose,
                Loc.T(L.Photos.Favorites), favoritePaths.Length, scale))
        {
            OpenAlbum(PhotoView.FavoritesKey);
        }

        if (DrawCollectionRow(drawList, bounds, recentsRow, false, true, PhoneIcons.Photo, ui.Accent,
                Loc.T(L.Photos.Recents), entries.Length, scale))
        {
            OpenAlbum(PhotoView.RecentsKey);
        }

        return MathF.Max(y, bounds.Max.Y) + SectionGap * scale;
    }

    private float DrawUtilitiesSection(ImDrawListPtr drawList, float left, float right, float top, float scale)
    {
        var y = DrawSectionTitle(drawList, left, right, top, Loc.T(L.Photos.Utilities), scale);
        ImGui.SetCursorScreenPos(new Vector2(left, y));
        var card = GroupCard.Begin(ui, 1, CollectionRowHeight);
        var row = card.NextRow();
        var bounds = card.Bounds;
        card.End();
        UiAnchors.Report("photos.albums.trash", bounds);
        if (DrawCollectionRow(drawList, bounds, row, true, true, PhoneIcons.Trash, AccentRing.Slate,
                Loc.T(L.Photos.RecentlyDeleted), trashPaths.Length, scale))
        {
            OpenAlbum(PhotoView.TrashKey);
        }

        return bounds.Max.Y;
    }

    private bool DrawCollectionRow(ImDrawListPtr drawList, Rect card, Rect row, bool first, bool last, string glyph,
        Vector4 tint, string label, int count, float scale)
    {
        var washMin = new Vector2(card.Min.X, row.Min.Y);
        var washMax = new Vector2(card.Max.X, row.Max.Y);
        var hovered = UiInteract.Hover(washMin, washMax);
        if (hovered)
        {
            var down = ImGui.IsMouseDown(ImGuiMouseButton.Left);
            RowWash(drawList, washMin, washMax, first, last, down ? RowPressAlpha : RowWashAlpha, scale);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var tile = CollectionTile * scale;
        var tileMin = new Vector2(row.Min.X, row.Center.Y - tile * 0.5f);
        var tileMax = tileMin + new Vector2(tile, tile);
        IconTile.FillShaded(drawList, tileMin, tileMax, tile * Metrics.Radius.TileFactor, IconTile.Surface(tint));
        PhoneIcon.Draw(drawList, (tileMin + tileMax) * 0.5f, glyph, new Vector4(1f, 1f, 1f, 1f),
            tile * CollectionTileGlyph);
        var countText = NumberLabel(count);
        var countWidth = Typography.Measure(countText, TextStyles.Body).X;
        var chevronX = row.Max.X - ChevronSize * scale;
        var countX = chevronX - Metrics.Space.Md * scale - countWidth;
        var labelLeft = tileMax.X + CollectionTileGap * scale;
        var lineHeight = Typography.LineHeight(TextStyles.Body);
        var fitted = Typography.FitText(label, MathF.Max(1f, countX - labelLeft - Metrics.Space.Sm * scale),
            TextStyles.Body);
        Typography.Draw(drawList, new Vector2(labelLeft, row.Center.Y - lineHeight * 0.5f), fitted, ui.TitleInk,
            TextStyles.Body);
        Typography.Draw(drawList, new Vector2(countX, row.Center.Y - lineHeight * 0.5f), countText, ui.MutedInk,
            TextStyles.Body);
        DrawChevron(drawList, new Vector2(chevronX, row.Center.Y), ui.MutedInk, scale);
        return UiInteract.Click(washMin, washMax, hovered);
    }

    private void RowWash(ImDrawListPtr drawList, Vector2 min, Vector2 max, bool first, bool last, float alpha,
        float scale)
    {
        var color = ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, alpha));
        var radius = Metrics.Radius.Grouped * scale;
        if (first && last)
        {
            Squircle.Fill(drawList, min, max, radius, color);
            return;
        }

        if (first || last)
        {
            Squircle.FillCap(drawList, min, max, radius, color, first);
            return;
        }

        drawList.AddRectFilled(min, max, color);
    }

    private static void DrawChevron(ImDrawListPtr drawList, Vector2 center, Vector4 color, float scale)
    {
        var half = ChevronSize * scale * 0.5f;
        var stroke = ImGui.GetColorU32(color);
        var thickness = Metrics.Stroke.Ring * scale;
        drawList.AddLine(new Vector2(center.X - half * 0.5f, center.Y - half), new Vector2(center.X + half * 0.5f,
            center.Y), stroke, thickness);
        drawList.AddLine(new Vector2(center.X + half * 0.5f, center.Y), new Vector2(center.X - half * 0.5f,
            center.Y + half), stroke, thickness);
    }

    private float DrawAlbumsSection(ImDrawListPtr drawList, float left, float right, float top, float scale)
    {
        var y = DrawSectionTitle(drawList, left, right, top, Loc.T(L.Photos.Albums), scale);
        var gap = AlbumGap * scale;
        var tile = (right - left - gap) / AlbumColumns;
        var cardHeight = tile + AlbumTextBlock * scale;
        var total = customAlbums.Count + 1;
        for (var index = 0; index < total; index++)
        {
            var column = index % AlbumColumns;
            var rowIndex = index / AlbumColumns;
            var min = new Vector2(left + column * (tile + gap), y + rowIndex * (cardHeight + gap));
            var coverMax = new Vector2(min.X + tile, min.Y + tile);
            if (index == customAlbums.Count)
            {
                if (PhotosChrome.NewAlbumTile(drawList, min, coverMax, AlbumRounding * scale,
                        Loc.T(L.Photos.CreateAlbum), ui, scale))
                {
                    OpenCreateAlbumSheet(null);
                }

                continue;
            }

            var rect = new Rect(min, new Vector2(coverMax.X, min.Y + cardHeight));
            DrawCustomAlbumCard(drawList, rect, customAlbums[index], tile, scale);
        }

        var rows = (total + AlbumColumns - 1) / AlbumColumns;
        return y + rows * cardHeight + (rows - 1) * gap + SectionGap * scale;
    }

    private void DrawCustomAlbumCard(ImDrawListPtr drawList, Rect rect, CustomAlbum album, float tile, float scale)
    {
        var coverMax = new Vector2(rect.Min.X + tile, rect.Min.Y + tile);
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var coverPath = cachedCustomAlbumPaths.TryGetValue(album.Key, out var paths) && paths.Length > 0
            ? paths[0]
            : null;
        PhotosChrome.Cover(drawList, coverPath is null ? null : GetThumbnail(coverPath), rect.Min, coverMax,
            AlbumRounding * scale, ui, scale, hovered);
        DrawAlbumCaption(drawList, rect, coverMax.Y, new MarqueeId("photos.album.", album.Key), album.Name, album.Count,
            hovered, scale);
        var overBadge = false;
        if (hovered || (albumSheet.IsOpen && albumSheetKey == album.Key))
        {
            var badgeOffset = (PhotosChrome.BadgeRadius + AlbumBadgeInset) * scale;
            var badgeCenter = new Vector2(coverMax.X - badgeOffset, rect.Min.Y + badgeOffset);
            var extent = new Vector2(PhotosChrome.BadgeRadius * scale, PhotosChrome.BadgeRadius * scale);
            overBadge = UiInteract.Hover(badgeCenter - extent, badgeCenter + extent);
            if (PhotosChrome.CoverBadge(drawList, badgeCenter, Loc.T(L.Photos.AlbumOptions), scale))
            {
                OpenAlbumSheet(album.Key);
                return;
            }
        }

        if (hovered && ImGui.IsMouseReleased(ImGuiMouseButton.Right))
        {
            OpenAlbumSheet(album.Key);
            return;
        }

        if (hovered && !overBadge)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(rect.Min, rect.Max, hovered && !overBadge))
        {
            OpenAlbum(album.Key);
        }
    }

    private void DrawAlbumCaption(ImDrawListPtr drawList, Rect rect, float coverBottom, MarqueeId id, string title,
        int count, bool hovered, float scale)
    {
        var textTop = coverBottom + AlbumTitleGap * scale;
        Marquee.DrawLeft(drawList, id, title, rect.Min.X, textTop, rect.Width, TextStyles.SubheadlineEmphasized,
            ui.TitleInk, hovered);
        Typography.Draw(drawList, new Vector2(rect.Min.X, textTop + AlbumCountGap * scale), CountLabel(count),
            ui.MutedInk, TextStyles.Footnote);
    }

    private float DrawPlacesSection(ImDrawListPtr drawList, float left, float width, float top, float scale)
    {
        var y = DrawSectionTitle(drawList, left, left + width, top, Loc.T(L.Photos.Places), scale);
        var cardWidth = RailCardWidth * scale;
        var cardHeight = cardWidth + AlbumTextBlock * scale;
        var gap = RailGap * scale;
        var count = places.Length;
        var row = new Rect(new Vector2(left, y), new Vector2(left + width, y + cardHeight));
        UiAnchors.Report("photos.places", row);
        placesRail.Begin(row, count * cardWidth + (count - 1) * gap);
        var x = row.Min.X - placesRail.Offset;
        for (var index = 0; index < count; index++)
        {
            var min = new Vector2(x, y);
            x += cardWidth + gap;
            if (min.X + cardWidth < row.Min.X || min.X > row.Max.X)
            {
                continue;
            }

            var place = places[index];
            var rect = new Rect(min, new Vector2(min.X + cardWidth, y + cardHeight));
            if (DrawRailCard(drawList, placesRail, rect, place.Paths[0], new MarqueeId("photos.place.", (int)place.Territory),
                    place.Name, place.Paths.Length, cardWidth, scale))
            {
                OpenAlbum(PhotoView.PlaceKey(place.Territory));
            }
        }

        placesRail.End();
        return row.Max.Y + SectionGap * scale;
    }

    private float DrawMonthsSection(ImDrawListPtr drawList, float left, float width, float top, float scale)
    {
        var y = DrawSectionTitle(drawList, left, left + width, top, Loc.T(L.Photos.Months), scale);
        var cardWidth = RailCardWidth * scale;
        var cardHeight = cardWidth + AlbumTextBlock * scale;
        var gap = RailGap * scale;
        var count = monthAlbums.Length;
        var row = new Rect(new Vector2(left, y), new Vector2(left + width, y + cardHeight));
        monthsRail.Begin(row, count * cardWidth + (count - 1) * gap);
        var x = row.Min.X - monthsRail.Offset;
        for (var index = 0; index < count; index++)
        {
            var min = new Vector2(x, y);
            x += cardWidth + gap;
            if (min.X + cardWidth < row.Min.X || min.X > row.Max.X)
            {
                continue;
            }

            var month = monthAlbums[index];
            var rect = new Rect(min, new Vector2(min.X + cardWidth, y + cardHeight));
            if (DrawRailCard(drawList, monthsRail, rect, month.Paths[0], new MarqueeId("photos.month.", month.Key),
                    MonthTitle(month.Key), month.Paths.Length, cardWidth, scale))
            {
                OpenAlbum(month.Key);
            }
        }

        monthsRail.End();
        return row.Max.Y + SectionGap * scale;
    }

    private bool DrawRailCard(ImDrawListPtr drawList, PanRail rail, Rect rect, string coverPath, MarqueeId id,
        string title, int count, float tile, float scale)
    {
        var coverMax = new Vector2(rect.Min.X + tile, rect.Min.Y + tile);
        var hovered = rail.Hover(rect.Min, rect.Max);
        PhotosChrome.Cover(drawList, GetThumbnail(coverPath), rect.Min, coverMax, AlbumRounding * scale, ui, scale,
            hovered);
        DrawAlbumCaption(drawList, rect, coverMax.Y, id, title, count, hovered, scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return rail.Tapped(rect.Min, rect.Max, hovered);
    }
}
