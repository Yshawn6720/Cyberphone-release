using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Sharing;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Photos;

internal sealed partial class PhotosApp
{
    private const float ViewerBarHeight = 52f;
    private const float ViewerControlRadius = 20f;
    private const float ViewerGlyph = 20f;
    private const float ViewerEdgeInset = 12f;
    private const float ViewerTitleGap = 2f;
    private const float ViewerTopScrim = 120f;
    private const float ViewerBottomScrim = 170f;
    private const float ToolbarHeight = 52f;
    private const float ToolbarSideInset = 12f;
    private const float ToolbarBottomInset = 10f;
    private const float EditPadX = 16f;
    private const float ScrubberHeight = 38f;
    private const float ScrubberGap = 10f;
    private const float ScrubberThumb = 26f;
    private const float ScrubberCurrent = 38f;
    private const float ScrubberSpacing = 2f;
    private const float ScrubberRounding = 4f;
    private const float PageGap = 18f;
    private const float SwipeSlop = 8f;
    private const float TapSlop = 6f;
    private const float VelocitySmoothing = 0.5f;
    private const float LoadingRadius = 13f;
    private const float InfoPadX = 20f;
    private const float InfoHeaderHeight = 72f;
    private const float InfoRowHeight = 46f;
    private const float InfoBottomPad = 24f;
    private const int ViewerMenuItemCount = 3;
    private const string ViewerMenuId = "photos.viewer.menu";
    private const long KilobyteBytes = 1024L;
    private const long MegabyteBytes = 1024L * 1024L;
    private static readonly Vector4 ViewerBackdrop = new(0f, 0f, 0f, 1f);

    private readonly PhotoZoomView zoomView = new();
    private readonly DropdownMenu viewerMenu = new();
    private readonly DropdownMenu.Item[] viewerMenuItems = new DropdownMenu.Item[ViewerMenuItemCount];
    private readonly Sheet infoSheet = new();
    private string[] viewerPaths = Array.Empty<string>();
    private int viewerIndex;
    private bool viewerInTrash;
    private Spring chrome = new(1f);
    private bool chromeVisible = true;
    private Spring pageOffset;
    private bool swipePressed;
    private bool swiping;
    private Vector2 swipeOrigin;
    private float swipeLastX;
    private float swipeVelocity;
    private float swipeStartOffset;
    private bool suppressTap;
    private string viewerTitlePath = string.Empty;
    private string viewerTitle = string.Empty;
    private string viewerSubtitle = string.Empty;
    private PhotoInfo info;

    private struct PhotoInfo
    {
        public string Path;
        public string Date;
        public string Time;
        public string Name;
        public string Dimensions;
        public string Size;
        public string Place;
        public string Albums;
        public int Rows;
    }

    private void ShowViewer(string[] paths, int index, bool inTrash, bool animate)
    {
        if (paths.Length == 0)
        {
            return;
        }

        viewerPaths = paths;
        viewerIndex = Math.Clamp(index, 0, paths.Length - 1);
        viewerInTrash = inTrash;
        chromeVisible = true;
        chrome.SnapTo(1f);
        ResetViewerMotion();
        router.Push(PhotoView.Viewer(), animate);
    }

    private void ResetViewerMotion()
    {
        zoomView.Reset();
        pageOffset.SnapTo(0f);
        swipePressed = false;
        swiping = false;
    }

    private void DrawViewer(Rect screen)
    {
        if (viewerPaths.Length == 0)
        {
            router.Pop(false);
            return;
        }

        var scale = UiScale.Current;
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        viewerIndex = Math.Clamp(viewerIndex, 0, viewerPaths.Length - 1);
        var safe = ContentWithin(screen);
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(screen.Min, screen.Max, ImGui.GetColorU32(ViewerBackdrop));
        var alpha = chrome.Step(chromeVisible ? 1f : 0f, Motion.Appear, delta);
        var live = router.Current.Route == PhotoRoute.Viewer && !router.IsTransitioning;
        if (live)
        {
            if (UiInteract.Hover(screen.Min, screen.Max))
            {
                UiInteract.ReportGestureSurface();
            }

            HandleViewerTap(screen, scale);
            HandleSwipe(screen, safe, scale, delta);
        }

        var path = viewerPaths[viewerIndex];
        var overControls = alpha > 0.5f && OverViewerControls(screen, safe, scale);
        DrawPages(drawList, screen, path, overControls, scale);
        if (alpha <= 0.01f)
        {
            return;
        }

        var interactive = alpha > 0.5f && live;
        PhotosChrome.TopScrim(drawList, screen.Min, screen.Max, ViewerTopScrim * scale, alpha);
        PhotosChrome.BottomScrim(drawList, screen.Min, screen.Max, ViewerBottomScrim * scale, alpha);
        DrawViewerTopBar(drawList, screen, safe, path, alpha, interactive, scale);
        var toolbar = ViewerToolbarRect(screen, safe, scale);
        if (viewerPaths.Length > 1 && !zoomView.IsZoomed)
        {
            DrawScrubber(drawList, screen, toolbar.Min.Y - (ScrubberGap + ScrubberHeight) * scale, alpha, interactive,
                scale);
        }

        DrawViewerToolbar(drawList, toolbar, path, alpha, interactive, scale);
    }

    private void HandleViewerTap(Rect screen, float scale)
    {
        if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
        {
            suppressTap = true;
        }

        var hovered = UiInteract.Hover(screen.Min, screen.Max);
        if (!UiInteract.Click(screen.Min, screen.Max, hovered, false))
        {
            return;
        }

        if (suppressTap)
        {
            suppressTap = false;
            return;
        }

        if (swiping)
        {
            return;
        }

        var slop = TapSlop * scale;
        if (ImGui.GetIO().MouseDragMaxDistanceSqr[0] > slop * slop)
        {
            return;
        }

        chromeVisible = !chromeVisible;
    }

    private void HandleSwipe(Rect screen, Rect safe, float scale, float delta)
    {
        var hasPrevious = viewerIndex > 0;
        var hasNext = viewerIndex < viewerPaths.Length - 1;
        var mouse = ImGui.GetMousePos();
        var zone = new Rect(new Vector2(screen.Min.X, safe.Min.Y + ViewerBarHeight * scale),
            new Vector2(screen.Max.X, ViewerToolbarRect(screen, safe, scale).Min.Y));
        if (!swipePressed)
        {
            pageOffset.Step(0f, Motion.PageSettle, delta);
            if (zoomView.IsZoomed || viewerPaths.Length < 2 || !ImGui.IsMouseClicked(ImGuiMouseButton.Left) ||
                !UiInteract.Hover(zone.Min, zone.Max))
            {
                return;
            }

            swipePressed = true;
            swiping = false;
            swipeOrigin = mouse;
            swipeStartOffset = pageOffset.Value;
            swipeLastX = mouse.X;
            swipeVelocity = 0f;
            return;
        }

        if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            var travel = mouse - swipeOrigin;
            var slop = SwipeSlop * scale;
            if (!swiping && MathF.Abs(travel.X) > slop && MathF.Abs(travel.X) > MathF.Abs(travel.Y))
            {
                swiping = true;
                UiInteract.CancelPendingTap();
            }

            if (!swiping)
            {
                return;
            }

            UiInteract.BlockThisFrame();
            var instant = delta > 0f ? (mouse.X - swipeLastX) / delta : 0f;
            swipeVelocity += (instant - swipeVelocity) * VelocitySmoothing;
            swipeLastX = mouse.X;
            pageOffset.SnapTo(PhotoPaging.Resist(swipeStartOffset + travel.X, hasPrevious, hasNext));
            return;
        }

        swipePressed = false;
        if (!swiping)
        {
            return;
        }

        swiping = false;
        var page = screen.Width + PageGap * scale;
        var step = PhotoPaging.Settle(pageOffset.Value, swipeVelocity, screen.Width, hasPrevious, hasNext, scale);
        if (step == 0)
        {
            pageOffset.Velocity = swipeVelocity;
            return;
        }

        viewerIndex += step;
        pageOffset.Launch(pageOffset.Value + step * page, swipeVelocity);
        zoomView.Reset();
        UiFeedback.Play(UiSound.Tap);
    }

    private bool OverViewerControls(Rect screen, Rect safe, float scale)
    {
        var mouse = ImGui.GetMousePos();
        if (mouse.Y <= safe.Min.Y + ViewerBarHeight * scale)
        {
            return true;
        }

        var toolbar = ViewerToolbarRect(screen, safe, scale);
        var controlsTop = toolbar.Min.Y - (viewerPaths.Length > 1 ? (ScrubberGap + ScrubberHeight) * scale : 0f);
        return mouse.Y >= controlsTop;
    }

    private void DrawPages(ImDrawListPtr drawList, Rect screen, string path, bool overControls, float scale)
    {
        var offset = pageOffset.Value;
        var page = screen.Width + PageGap * scale;
        var stage = screen.Translate(new Vector2(offset, 0f));
        var texture = GetFull(path) ?? thumbnails.Get(path);
        if (texture is not null && overControls)
        {
            DrawSettled(drawList, stage, texture);
        }
        else if (texture is not null)
        {
            zoomView.Draw(stage, texture, frameTheme, 0f, false);
        }
        else
        {
            LoadingPulse.Draw(stage.Center, LoadingRadius * scale, ui.Accent, WhiteMuted, Loc.T(L.Common.Loading));
        }

        Prefetch(viewerIndex - 1);
        Prefetch(viewerIndex + 1);
        if (MathF.Abs(offset) < 0.5f)
        {
            return;
        }

        var neighbor = offset > 0f ? viewerIndex - 1 : viewerIndex + 1;
        if (neighbor < 0 || neighbor >= viewerPaths.Length)
        {
            return;
        }

        var neighborStage = stage.Translate(new Vector2(offset > 0f ? -page : page, 0f));
        var neighborPath = viewerPaths[neighbor];
        if ((GetFull(neighborPath) ?? GetThumbnail(neighborPath)) is not { } neighborTexture)
        {
            return;
        }

        var frame = ImageFit.CenteredRect(neighborStage, neighborTexture.Width / MathF.Max(1f, neighborTexture.Height));
        drawList.AddImage(neighborTexture.Handle, frame.Min, frame.Max);
    }

    private void DrawSettled(ImDrawListPtr drawList, Rect stage, IDalamudTextureWrap texture)
    {
        var drawn = texture.Size * PhotoZoomView.FitScale(stage, texture.Size) * zoomView.Zoom;
        var center = stage.Center + zoomView.Pan;
        drawList.PushClipRect(stage.Min, stage.Max, true);
        drawList.AddImage(texture.Handle, center - drawn * 0.5f, center + drawn * 0.5f);
        drawList.PopClipRect();
    }

    private void Prefetch(int index)
    {
        if (index < 0 || index >= viewerPaths.Length)
        {
            return;
        }

        GetFull(viewerPaths[index]);
    }

    private void DrawViewerTopBar(ImDrawListPtr drawList, Rect screen, Rect safe, string path, float alpha,
        bool interactive, float scale)
    {
        var rowCenterY = safe.Min.Y + ViewerBarHeight * scale * 0.5f;
        var radius = ViewerControlRadius * scale;
        var backCenter = new Vector2(screen.Min.X + ViewerEdgeInset * scale + radius, rowCenterY);
        if (ViewerControl(drawList, "photos.viewer.back", backCenter, PhoneIcons.ChevronLeft, Loc.T(L.Photos.MenuBack),
                White, alpha, interactive, HoverLabelSide.Below, scale))
        {
            router.Pop();
            return;
        }

        var right = screen.Max.X - ViewerEdgeInset * scale;
        var menuCenter = new Vector2(right - radius, rowCenterY);
        var reserve = radius * 2f + ViewerEdgeInset * scale;
        if (!viewerInTrash)
        {
            var menuRect = new Rect(menuCenter - new Vector2(radius, radius), menuCenter + new Vector2(radius, radius));
            if (ViewerControl(drawList, "photos.viewer.more", menuCenter, PhoneIcons.Dots, Loc.T(L.Photos.More), White,
                    alpha, interactive, HoverLabelSide.Below, scale))
            {
                OpenViewerMenu(menuRect);
            }

            var editLabel = Loc.T(L.Photos.Edit);
            var editWidth = Typography.Measure(editLabel, TextStyles.Headline).X + EditPadX * 2f * scale;
            var editRight = menuCenter.X - radius - Metrics.Space.Sm * scale;
            var editRect = new Rect(new Vector2(editRight - editWidth, rowCenterY - radius),
                new Vector2(editRight, rowCenterY + radius));
            UiAnchors.Report("photos.viewer.edit", editRect);
            if (GlassCapsule(drawList, editRect, editLabel, alpha, interactive, scale))
            {
                OpenEditor(path);
                return;
            }

            reserve = screen.Max.X - editRect.Min.X;
        }

        SyncViewerTitle(path);
        var titleWidth = MathF.Max(1f, screen.Width - MathF.Max(reserve, backCenter.X + radius - screen.Min.X) * 2f -
                                       Metrics.Space.Sm * scale);
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var subtitleHeight = Typography.LineHeight(TextStyles.Footnote);
        var blockTop = rowCenterY - (titleHeight + ViewerTitleGap * scale + subtitleHeight) * 0.5f;
        Typography.DrawCentered(drawList, new Vector2(screen.Center.X, blockTop + titleHeight * 0.5f),
            Typography.FitText(viewerTitle, titleWidth, TextStyles.Headline), White with { W = alpha },
            TextStyles.Headline);
        Typography.DrawCentered(drawList,
            new Vector2(screen.Center.X, blockTop + titleHeight + ViewerTitleGap * scale + subtitleHeight * 0.5f),
            Typography.FitText(viewerSubtitle, titleWidth, TextStyles.Footnote), WhiteMuted with { W = WhiteMuted.W * alpha },
            TextStyles.Footnote);
    }

    private void SyncViewerTitle(string path)
    {
        if (ReferenceEquals(viewerTitlePath, path))
        {
            return;
        }

        viewerTitlePath = path;
        var taken = ResolveTaken(path);
        viewerTitle = DayTitle(taken);
        var place = PhotoPlaces.Name(PlaceOf(path));
        var clock = TimeText.Clock(taken);
        viewerSubtitle = place.Length > 0 ? string.Concat(clock, " · ", place) : clock;
    }

    private bool ViewerControl(ImDrawListPtr drawList, string id, Vector2 center, string glyph, string tooltip,
        Vector4 ink, float alpha, bool interactive, HoverLabelSide side, float scale)
    {
        var radius = ViewerControlRadius * scale;
        var clicked = GlassCircle.Draw(drawList, ImGui.GetID(id), center, radius, scale, GlassTone.Dark,
            interactive ? tooltip : string.Empty, side, out var grow, null, true, alpha);
        PhoneIcon.Draw(drawList, center, glyph, ink with { W = ink.W * alpha }, ViewerGlyph * scale * grow);
        return interactive && clicked;
    }

    private bool GlassCapsule(ImDrawListPtr drawList, Rect rect, string label, float alpha, bool interactive,
        float scale)
    {
        var hovered = interactive && UiInteract.Hover(rect.Min, rect.Max);
        var down = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var grow = PressFx.Scale(ImGui.GetID("photos.viewer.capsule"), down, PressFx.ControlPressedScale);
        var half = rect.Size * 0.5f * grow;
        var min = rect.Center - half;
        var max = rect.Center + half;
        Material.LiquidGlass(drawList, min, max, half.Y, scale, GlassTone.Dark, 0f, alpha);
        Typography.DrawCentered(drawList, rect.Center, label, White with { W = alpha }, TextStyles.Headline);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return interactive && UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    private Rect ViewerToolbarRect(Rect screen, Rect safe, float scale)
    {
        var bottom = safe.Max.Y + frameTheme.BottomZoneHeight * scale * 0.25f - ToolbarBottomInset * scale;
        return new Rect(new Vector2(screen.Min.X + ToolbarSideInset * scale, bottom - ToolbarHeight * scale),
            new Vector2(screen.Max.X - ToolbarSideInset * scale, bottom));
    }

    private void DrawViewerToolbar(ImDrawListPtr drawList, Rect bar, string path, float alpha, bool interactive,
        float scale)
    {
        Material.LiquidGlass(drawList, bar.Min, bar.Max, bar.Height * 0.5f, scale, GlassTone.Dark, 0f, alpha);
        if (viewerInTrash)
        {
            if (ToolbarIcon(drawList, ToolbarSlot(bar, 0, 3), "photos.viewer.recover", PhoneIcons.ArrowBackUp,
                    Loc.T(L.Photos.Recover), White, alpha, interactive, scale))
            {
                RecoverPhotos(new[] { path });
                return;
            }

            if (ToolbarIcon(drawList, ToolbarSlot(bar, 1, 3), "photos.viewer.info", PhoneIcons.InfoCircle,
                    Loc.T(L.Photos.Info), White, alpha, interactive, scale))
            {
                OpenInfoSheet(path);
                return;
            }

            if (ToolbarIcon(drawList, ToolbarSlot(bar, 2, 3), "photos.viewer.purge", PhoneIcons.Trash,
                    Loc.T(L.Photos.DeletePermanently), frameTheme.Danger, alpha, interactive, scale))
            {
                AskDeleteForever(new[] { path });
            }

            return;
        }

        var canShare = share.CanShare(ShareKind.Photo, Id);
        var slots = canShare ? 4 : 3;
        var slot = 0;
        if (canShare)
        {
            if (ToolbarIcon(drawList, ToolbarSlot(bar, slot, slots), "photos.viewer.share", PhoneIcons.Share,
                    Loc.T(L.Share.Action), White, alpha, interactive, scale))
            {
                share.Offer(new ShareItem(ShareKind.Photo, path, Id));
                return;
            }

            slot++;
        }

        var favorite = IsFavorite(path);
        if (ToolbarIcon(drawList, ToolbarSlot(bar, slot, slots), "photos.viewer.favorite",
                favorite ? PhoneIcons.HeartFilled : PhoneIcons.Heart,
                Loc.T(favorite ? L.Photos.Unfavorite : L.Photos.Favorite), White, alpha, interactive, scale))
        {
            ToggleFavorite(path);
        }

        slot++;
        if (ToolbarIcon(drawList, ToolbarSlot(bar, slot, slots), "photos.viewer.info", PhoneIcons.InfoCircle,
                Loc.T(L.Photos.Info), White, alpha, interactive, scale))
        {
            OpenInfoSheet(path);
            return;
        }

        slot++;
        if (ToolbarIcon(drawList, ToolbarSlot(bar, slot, slots), "photos.viewer.delete", PhoneIcons.Trash,
                Loc.T(L.Photos.Delete), White, alpha, interactive, scale))
        {
            AskDeletePhotos(new[] { path });
        }
    }

    private static bool ToolbarIcon(ImDrawListPtr drawList, Vector2 center, string id, string glyph, string tooltip,
        Vector4 ink, float alpha, bool interactive, float scale)
    {
        var radius = ToolbarHeight * 0.5f * scale;
        var extent = new Vector2(radius, radius);
        var hovered = interactive && UiInteract.Hover(center - extent, center + extent);
        var down = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var grow = PressFx.Scale(ImGui.GetID(id), down, PressFx.IconPressedScale);
        PhoneIcon.Draw(drawList, center, glyph, ink with { W = ink.W * alpha }, ViewerGlyph * 1.1f * scale * grow);
        if (!interactive)
        {
            return false;
        }

        HoverTooltip.Show(new Rect(center - extent, center + extent), tooltip, HoverLabelSide.Above);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(center - extent, center + extent, hovered);
    }

    private void DrawScrubber(ImDrawListPtr drawList, Rect screen, float top, float alpha, bool interactive,
        float scale)
    {
        var centerX = screen.Center.X - pageOffset.Value / MathF.Max(1f, screen.Width) * ScrubberThumb * scale;
        var spacing = ScrubberSpacing * scale;
        var thumb = ScrubberThumb * scale;
        var current = ScrubberCurrent * scale;
        var rounding = ScrubberRounding * scale;
        var tint = ImGui.GetColorU32(White with { W = alpha });
        var placeholder = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.12f * alpha));
        var reach = (int)MathF.Ceiling(screen.Width * 0.5f / (thumb + spacing)) + 1;
        var first = Math.Max(0, viewerIndex - reach);
        var last = Math.Min(viewerPaths.Length - 1, viewerIndex + reach);
        var height = current;
        drawList.PushClipRect(new Vector2(screen.Min.X, top), new Vector2(screen.Max.X, top + height), true);
        var picked = -1;
        for (var index = first; index <= last; index++)
        {
            var distance = index - viewerIndex;
            float left;
            if (distance == 0)
            {
                left = centerX - current * 0.5f;
            }
            else if (distance < 0)
            {
                left = centerX - current * 0.5f - spacing + distance * (thumb + spacing);
            }
            else
            {
                left = centerX + current * 0.5f + spacing + (distance - 1) * (thumb + spacing);
            }

            var width = distance == 0 ? current : thumb;
            var min = new Vector2(left, top);
            var max = new Vector2(left + width, top + height);
            if (GetThumbnail(viewerPaths[index]) is { } texture)
            {
                var (uv0, uv1) = ImageFit.Cover(texture.Width, texture.Height, width, height);
                drawList.AddImageRounded(texture.Handle, min, max, uv0, uv1, tint, rounding, ImDrawFlags.RoundCornersAll);
            }
            else
            {
                drawList.AddRectFilled(min, max, placeholder, rounding);
            }

            if (!interactive || distance == 0)
            {
                continue;
            }

            var hovered = UiInteract.Hover(min, max);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(min, max, hovered))
            {
                picked = index;
            }
        }

        drawList.PopClipRect();
        if (picked < 0)
        {
            return;
        }

        viewerIndex = picked;
        ResetViewerMotion();
    }

    private void OpenViewerMenu(Rect anchor)
    {
        viewerMenu.Toggle(ViewerMenuId, anchor);
    }

    private void DrawViewerMenu(Rect screen)
    {
        if (!viewerMenu.Open)
        {
            return;
        }

        if (router.Current.Route != PhotoRoute.Viewer || viewerPaths.Length == 0)
        {
            viewerMenu.Close();
            return;
        }

        viewerMenuItems[0] = new DropdownMenu.Item(Loc.T(L.Photos.AddToAlbum), PhoneIcons.SquareRoundedPlus);
        viewerMenuItems[1] = new DropdownMenu.Item(Loc.T(L.Common.OpenInWindow), PhoneIcons.ExternalLink);
        viewerMenuItems[2] = new DropdownMenu.Item(Loc.T(L.Photos.ShowInFolder), IconGlyph.Of(FontAwesomeIcon.FolderOpen));
        var picked = viewerMenu.Draw(screen, frameTheme, viewerMenuItems);
        if (picked < 0)
        {
            return;
        }

        var path = viewerPaths[Math.Clamp(viewerIndex, 0, viewerPaths.Length - 1)];
        switch (picked)
        {
            case 0:
                OpenAddToAlbum(new[] { path });
                break;
            case 1:
                Plugin.PhotoWindow.Open(() => GetFull(path) ?? thumbnails.Get(path), this);
                break;
            case 2:
                UrlActions.OpenFolder(library.DirectoryPath);
                break;
        }
    }

    private void OpenInfoSheet(string path)
    {
        var taken = ResolveTaken(path);
        info.Path = path;
        info.Date = Capitalize(taken.ToString("D", Loc.Culture), Loc.Culture);
        info.Time = TimeText.Clock(taken);
        info.Name = Path.GetFileName(path);
        info.Dimensions = string.Empty;
        info.Size = string.Empty;
        info.Place = PhotoPlaces.Name(PlaceOf(path));
        info.Albums = AlbumsContaining(path);
        try
        {
            var (width, height) = ImageProcessor.IdentifyDimensions(path);
            info.Dimensions = string.Concat(width.ToString(Loc.Culture), " × ", height.ToString(Loc.Culture));
            info.Size = FormatBytes(new FileInfo(path).Length);
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, $"[Photos] could not read the details of {info.Name}");
        }

        info.Rows = RowCount(info.Place) + RowCount(info.Albums) + RowCount(info.Dimensions) + RowCount(info.Size) +
                    RowCount(info.Name);
        infoSheet.Open();
    }

    private static int RowCount(string value) => value.Length > 0 ? 1 : 0;

    private string AlbumsContaining(string path)
    {
        string? joined = null;
        for (var index = 0; index < customAlbums.Count; index++)
        {
            var album = customAlbums[index];
            if (!customAlbumPhotos.TryGetValue(album.Name, out var photos) || !ContainsOrdinalIgnoreCase(photos, path))
            {
                continue;
            }

            joined = joined is null ? album.Name : string.Concat(joined, ", ", album.Name);
        }

        return joined ?? string.Empty;
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= MegabyteBytes)
        {
            return Loc.T(L.Photos.SizeMegabytes, ((double)bytes / MegabyteBytes).ToString("0.0", Loc.Culture));
        }

        return Loc.T(L.Photos.SizeKilobytes, Math.Max(1L, bytes / KilobyteBytes).ToString(Loc.Culture));
    }

    private void DrawInfoSheet(Rect screen)
    {
        if (!infoSheet.CapturesPointer)
        {
            return;
        }

        if (infoSheet.IsOpen && router.Current.Route != PhotoRoute.Viewer)
        {
            infoSheet.Close();
        }

        var scale = UiScale.Current;
        var fitted = SheetMetrics.GrabberZone * scale +
                     (InfoHeaderHeight + info.Rows * InfoRowHeight + InfoBottomPad) * scale +
                     frameTheme.BottomZoneHeight * scale;
        ImGui.SetCursorScreenPos(screen.Min);
        using (ImRaii.Child("##photosInfoSheet", screen.Size, false, SheetHostFlags))
        {
            var frame = infoSheet.Begin(ImGui.GetWindowDrawList(), screen, frameTheme, SheetDetents.Fitted(fitted),
                SheetMetrics.AppVeil);
            if (!frame.Visible)
            {
                return;
            }

            DrawInfoBody(in frame, scale);
            infoSheet.End(in frame);
        }
    }

    private void DrawInfoBody(in SheetFrame frame, float scale)
    {
        var drawList = frame.DrawList;
        var ink = frame.Ink with { W = frame.Ink.W * frame.Opacity };
        var muted = ink with { W = ink.W * 0.62f };
        var padX = InfoPadX * scale;
        var left = frame.Content.Min.X + padX;
        var right = frame.Content.Max.X - padX;
        var width = right - left;
        var top = frame.Content.Min.Y;
        var dateHeight = Typography.LineHeight(TextStyles.Title3);
        Typography.Draw(drawList, new Vector2(left, top + Metrics.Space.Sm * scale),
            Typography.FitText(info.Date, width, TextStyles.Title3), ink, TextStyles.Title3);
        Typography.Draw(drawList, new Vector2(left, top + Metrics.Space.Sm * scale + dateHeight),
            Typography.FitText(info.Time, width, TextStyles.Subheadline), muted, TextStyles.Subheadline);
        var y = top + InfoHeaderHeight * scale;
        var rowHeight = InfoRowHeight * scale;
        var separator = ImGui.GetColorU32(ink with { W = ink.W * 0.12f });
        DrawInfoRow(drawList, left, right, ref y, rowHeight, PhoneIcons.MapPin, Loc.T(L.Photos.InfoPlace), info.Place,
            ink, muted, separator, scale);
        DrawInfoRow(drawList, left, right, ref y, rowHeight, PhoneIcons.LibraryPhoto, Loc.T(L.Photos.Albums),
            info.Albums, ink, muted, separator, scale);
        DrawInfoRow(drawList, left, right, ref y, rowHeight, PhoneIcons.Photo, Loc.T(L.Photos.InfoDimensions),
            info.Dimensions, ink, muted, separator, scale);
        DrawInfoRow(drawList, left, right, ref y, rowHeight, PhoneIcons.Download, Loc.T(L.Photos.InfoSize), info.Size,
            ink, muted, separator, scale);
        DrawInfoRow(drawList, left, right, ref y, rowHeight, PhoneIcons.FileText, Loc.T(L.Photos.InfoName), info.Name,
            ink, muted, separator, scale);
    }

    private static void DrawInfoRow(ImDrawListPtr drawList, float left, float right, ref float y, float rowHeight,
        string glyph, string label, string value, Vector4 ink, Vector4 muted, uint separator, float scale)
    {
        if (value.Length == 0)
        {
            return;
        }

        var centerY = y + rowHeight * 0.5f;
        var glyphSize = ViewerGlyph * scale;
        PhoneIcon.Draw(drawList, new Vector2(left + glyphSize * 0.5f, centerY), glyph, muted, glyphSize);
        var labelLeft = left + glyphSize + Metrics.Space.Md * scale;
        var labelSize = Typography.Measure(label, TextStyles.Subheadline);
        Typography.Draw(drawList, new Vector2(labelLeft, centerY - labelSize.Y * 0.5f), label, muted,
            TextStyles.Subheadline);
        var maxValue = MathF.Max(1f, right - labelLeft - labelSize.X - Metrics.Space.Md * scale);
        var fitted = Typography.FitText(value, maxValue, TextStyles.Body);
        var valueSize = Typography.Measure(fitted, TextStyles.Body);
        Typography.Draw(drawList, new Vector2(right - valueSize.X, centerY - valueSize.Y * 0.5f), fitted, ink,
            TextStyles.Body);
        drawList.AddLine(new Vector2(labelLeft, y), new Vector2(right, y), separator, Metrics.Stroke.Hairline);
        y += rowHeight;
    }
}
