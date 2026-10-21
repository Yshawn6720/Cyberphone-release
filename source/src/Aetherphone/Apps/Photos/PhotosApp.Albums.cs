using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Photos;

internal sealed partial class PhotosApp
{
    private enum NameSheetMode : byte
    {
        Create,
        Rename,
    }

    private const int AlbumSheetItemCount = 3;
    private const int TrashSheetItemCount = 2;
    private const int MaxAlbumNameLength = 64;
    private const float TrashHintGap = 10f;
    private const float TrashHintTop = 6f;
    private const float AddRowHeight = 68f;
    private const float AddRowThumb = 48f;
    private const float AddRowThumbRounding = 10f;
    private const float AddRowGap = 12f;
    private const float AddRowTitleLift = 10f;
    private const float AddRowCountDrop = 11f;
    private const float SheetPadX = 20f;
    private const float SheetHeaderHeight = 48f;
    private const float SheetFieldGap = 14f;
    private const float SheetButtonHeight = 50f;
    private const float SheetHintGap = 10f;
    private const float SheetBottomPad = 24f;
    private const ImGuiWindowFlags SheetHostFlags = ImGuiWindowFlags.NoScrollbar |
                                                    ImGuiWindowFlags.NoScrollWithMouse |
                                                    ImGuiWindowFlags.NoBackground;

    private readonly List<CustomAlbum> customAlbums = new();
    private readonly List<string> customAlbumOrder = new();
    private readonly Dictionary<string, List<string>> customAlbumPhotos = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> customAlbumIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, string[]> cachedCustomAlbumPaths = new();
    private readonly List<string> pickerSelection = new();
    private readonly HashSet<string> pickerMembership = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> pickerSelectionOrder = new(StringComparer.OrdinalIgnoreCase);
    private readonly NavBarButton[] albumButtons = new NavBarButton[2];
    private readonly NavBarButton[] pickerButtons = new NavBarButton[1];
    private readonly ActionSheet albumSheet = new();
    private readonly ActionSheet.Item[] albumSheetItems = new ActionSheet.Item[AlbumSheetItemCount];
    private readonly ActionSheet photoSheet = new();
    private readonly ActionSheet.Item[] photoSheetItems = new ActionSheet.Item[1];
    private readonly ActionSheet trashSheet = new();
    private readonly ActionSheet.Item[] trashSheetItems = new ActionSheet.Item[TrashSheetItemCount];
    private readonly Sheet nameSheet = new();
    private readonly Action<Rect> drawNameSheet;
    private int nextCustomAlbumId = PhotoView.FirstCustomAlbumId;
    private int? pickerMembershipAlbumKey;
    private int albumSheetKey;
    private int photoSheetAlbumKey;
    private string photoSheetPath = string.Empty;
    private NameSheetMode nameSheetMode;
    private int nameSheetAlbumKey;
    private string[]? nameSheetPhotoPaths;
    private string nameDraft = string.Empty;
    private bool nameSheetFocus;
    private string[] addTargets = Array.Empty<string>();
    private readonly List<CustomAlbum> eligibleAlbums = new();
    private string[]? eligibleTargets;
    private int eligibleVersion = -1;
    private int albumsVersion;

    private readonly struct CustomAlbum
    {
        public readonly int Key;
        public readonly int Count;
        public readonly string Name;

        public CustomAlbum(int key, int count, string name)
        {
            Key = key;
            Count = count;
            Name = name;
        }
    }

    private readonly ref struct AlbumPage
    {
        public readonly string Title;
        public readonly string[] Paths;
        public readonly GridMode Mode;
        public readonly bool HasMenu;

        public AlbumPage(string title, string[] paths, GridMode mode, bool hasMenu)
        {
            Title = title;
            Paths = paths;
            Mode = mode;
            HasMenu = hasMenu;
        }
    }

    private void DrawAlbum(in PhoneContext context, Rect area, int key)
    {
        if (!TryResolveAlbum(key, out var page))
        {
            router.Pop(false);
            return;
        }

        var scale = UiScale.Current;
        var navBar = AppHeader.BeginLargeTitle(context);
        var scope = page.Mode == GridMode.Trash ? SelectionScope.Trash : SelectionScope.Album;
        if (page.Paths.Length == 0)
        {
            using (AppSurface.Begin(navBar.Body))
            {
                DrawAlbumEmpty(key, VisibleBody(navBar.Body, scale));
            }
        }
        else
        {
            var bottomInset = selecting ? TabBar.ContentInset(scale) : 0f;
            var surfaceRect = new Rect(new Vector2(area.Min.X, navBar.Body.Min.Y),
                new Vector2(area.Max.X, context.Content.Max.Y));
            ImGui.PushID($"photos.album{key}");
            using (AppSurface.ReserveBottom(bottomInset))
            using (AppSurface.BeginEdgeToEdge(surfaceRect))
            {
                var origin = ImGui.GetCursorScreenPos();
                var drawList = ImGui.GetWindowDrawList();
                var height = 0f;
                if (page.Mode == GridMode.Trash)
                {
                    height += DrawTrashHint(drawList, new Vector2(context.Content.Min.X, origin.Y),
                        context.Content.Width, scale);
                }

                height += DrawFlatGrid(drawList, new Vector2(origin.X, origin.Y + height), area.Width, page.Paths,
                    page.Mode, key, scale);
                height += DrawFooter(drawList, new Vector2(origin.X, origin.Y + height), area.Width,
                    CountLabel(page.Paths.Length));
                ImGui.SetCursorScreenPos(origin);
                ImGui.Dummy(new Vector2(area.Width, height));
            }

            ImGui.PopID();
        }

        var buttons = AlbumButtons(page);
        var title = selecting ? SelectionTitle() : page.Title;
        var pressed = AppHeader.EndLargeTitle(in navBar, context, "photos.nav.album", title, NavBarStyle.From(ui),
            buttons, Loc.T(L.Photos.Collections), back);
        HandleAlbumButton(key, page, pressed, buttons.Length, scope);
        if (selecting)
        {
            DrawSelectToolbar(context.Content);
        }
    }

    private bool TryResolveAlbum(int key, out AlbumPage page)
    {
        page = default;
        if (PhotoView.IsCustomKey(key))
        {
            if (!TryFindCustomAlbum(key, out var album))
            {
                return false;
            }

            var paths = cachedCustomAlbumPaths.TryGetValue(key, out var cached) ? cached : Array.Empty<string>();
            page = new AlbumPage(album.Name, paths, GridMode.Album, true);
            return true;
        }

        if (PhotoView.IsPlaceKey(key))
        {
            if (!placeLookup.TryGetValue(PhotoView.TerritoryOf(key), out var place))
            {
                return false;
            }

            page = new AlbumPage(place.Name, place.Paths, GridMode.Plain, false);
            return true;
        }

        switch (key)
        {
            case PhotoView.FavoritesKey:
                page = new AlbumPage(Loc.T(L.Photos.Favorites), favoritePaths, GridMode.Plain, false);
                return true;
            case PhotoView.RecentsKey:
                page = new AlbumPage(Loc.T(L.Photos.Recents), recentPaths, GridMode.Plain, false);
                return true;
            case PhotoView.TrashKey:
                page = new AlbumPage(Loc.T(L.Photos.RecentlyDeleted), trashPaths, GridMode.Trash, trashPaths.Length > 0);
                return true;
        }

        if (!PhotoView.IsMonthKey(key) || !TryMonthPaths(key, out var monthPaths))
        {
            return false;
        }

        page = new AlbumPage(MonthTitle(key), monthPaths, GridMode.Plain, false);
        return true;
    }

    private bool TryMonthPaths(int key, out string[] paths)
    {
        for (var index = 0; index < monthAlbums.Length; index++)
        {
            if (monthAlbums[index].Key == key)
            {
                paths = monthAlbums[index].Paths;
                return true;
            }
        }

        paths = Array.Empty<string>();
        return false;
    }

    private ReadOnlySpan<NavBarButton> AlbumButtons(in AlbumPage page)
    {
        if (selecting)
        {
            selectingButtons[0] = new NavBarButton(PhoneIcons.X, Loc.T(L.Common.Cancel));
            return selectingButtons;
        }

        var count = 0;
        if (page.Paths.Length > 0)
        {
            albumButtons[count++] = new NavBarButton(PhoneIcons.CircleCheck, Loc.T(L.Photos.Select));
        }

        if (page.HasMenu)
        {
            albumButtons[count++] = new NavBarButton(PhoneIcons.Dots, Loc.T(L.Photos.AlbumOptions));
        }

        return albumButtons.AsSpan(0, count);
    }

    private void HandleAlbumButton(int key, in AlbumPage page, int pressed, int count, SelectionScope scope)
    {
        if (pressed < 0)
        {
            return;
        }

        if (selecting)
        {
            EndSelect();
            return;
        }

        var menu = page.HasMenu && pressed == count - 1;
        if (!menu)
        {
            BeginSelect(scope);
            return;
        }

        if (page.Mode == GridMode.Trash)
        {
            OpenTrashSheet();
            return;
        }

        OpenAlbumSheet(key);
    }

    private void DrawAlbumEmpty(int key, Rect body)
    {
        if (PhotoView.IsCustomKey(key))
        {
            if (EmptyState.Draw(body, ui, PhoneIcons.Photo, Loc.T(L.Photos.EmptyAlbum), Loc.T(L.Photos.EmptyAlbumHint),
                    Loc.T(L.Photos.AddPhotos)))
            {
                OpenAlbumPicker(key);
            }

            return;
        }

        switch (key)
        {
            case PhotoView.FavoritesKey:
                EmptyState.Draw(body, ui, PhoneIcons.Heart, Loc.T(L.Photos.NoFavorites), Loc.T(L.Photos.NoFavoritesHint));
                return;
            case PhotoView.TrashKey:
                EmptyState.Draw(body, ui, PhoneIcons.Trash, Loc.T(L.Photos.TrashEmpty), Loc.T(L.Photos.TrashEmptyHint));
                return;
            default:
                DrawLibraryEmpty(body);
                return;
        }
    }

    private float DrawTrashHint(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var top = origin.Y + TrashHintTop * scale;
        var height = Typography.MeasureWrappedBlock(Loc.T(L.Photos.RecentlyDeletedHint), TextStyles.Footnote, width).Y;
        ImGui.SetCursorScreenPos(new Vector2(origin.X, top));
        Typography.DrawWrappedLeft(new Vector2(origin.X, top), Loc.T(L.Photos.RecentlyDeletedHint), ui.MutedInk,
            TextStyles.Footnote, width);
        return TrashHintTop * scale + height + TrashHintGap * scale;
    }

    private void DrawAlbumPicker(in PhoneContext context, Rect area, int key)
    {
        if (!TryFindCustomAlbum(key, out var album))
        {
            router.Pop(false);
            return;
        }

        var scale = UiScale.Current;
        var navBar = AppHeader.BeginLargeTitle(context);
        if (recentPaths.Length == 0)
        {
            using (AppSurface.Begin(navBar.Body))
            {
                DrawLibraryEmpty(navBar.Body);
            }
        }
        else
        {
            EnsurePickerMembership(key);
            var surfaceRect = new Rect(new Vector2(area.Min.X, navBar.Body.Min.Y),
                new Vector2(area.Max.X, context.Content.Max.Y));
            ImGui.PushID("photos.picker");
            using (AppSurface.BeginEdgeToEdge(surfaceRect))
            {
                var origin = ImGui.GetCursorScreenPos();
                var height = DrawFlatGrid(ImGui.GetWindowDrawList(), origin, area.Width, recentPaths, GridMode.Picker,
                    key, scale);
                ImGui.SetCursorScreenPos(origin);
                ImGui.Dummy(new Vector2(area.Width, height));
            }

            ImGui.PopID();
        }

        var selected = pickerSelection.Count;
        pickerButtons[0] = new NavBarButton(PhoneIcons.Check, Loc.T(L.Photos.Done));
        var buttons = selected > 0 ? pickerButtons.AsSpan() : Span<NavBarButton>.Empty;
        var title = selected > 0 ? SelectedLabel(selected) : Loc.T(L.Photos.AddPhotos);
        var pressed = AppHeader.EndLargeTitle(in navBar, context, "photos.nav.picker", title, NavBarStyle.From(ui),
            buttons, album.Name, back);
        if (pressed != 0)
        {
            return;
        }

        AddPhotosToCustomAlbum(key, pickerSelection.ToArray());
        UiFeedback.Play(UiSound.Success);
        router.Pop();
    }

    private void OpenAddToAlbum(string[] targets)
    {
        addTargets = targets;
        router.Push(PhotoView.AddToAlbum());
    }

    private void DrawAddToAlbumPage(in PhoneContext context)
    {
        if (addTargets.Length == 0)
        {
            router.Pop(false);
            return;
        }

        var scale = UiScale.Current;
        var navBar = AppHeader.BeginLargeTitle(context);
        RefreshEligibleAlbums();
        using (AppSurface.Begin(navBar.Body))
        {
            var width = ScrollLayout.StableContentWidth();
            var available = eligibleAlbums.Count;

            var card = GroupCard.Begin(ui, available + 1, AddRowHeight);
            card.SeparatorInset = AddRowThumb + AddRowGap;
            var bounds = card.Bounds;
            var picked = int.MinValue;
            var newRow = card.NextRow();
            if (DrawAddRow(bounds, newRow, true, available == 0, null, Loc.T(L.Photos.CreateAlbum), string.Empty, scale))
            {
                picked = 0;
            }

            for (var index = 0; index < eligibleAlbums.Count; index++)
            {
                var album = eligibleAlbums[index];
                var row = card.NextRow();
                var cover = cachedCustomAlbumPaths.TryGetValue(album.Key, out var paths) && paths.Length > 0
                    ? paths[0]
                    : null;
                if (DrawAddRow(bounds, row, false, index == available - 1, cover, album.Name, CountLabel(album.Count),
                        scale))
                {
                    picked = album.Key;
                }
            }

            card.End();
            if (available == 0 && customAlbums.Count > 0)
            {
                ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
                var origin = ImGui.GetCursorScreenPos();
                var height = Typography.DrawWrappedLeft(origin, Loc.T(L.Photos.AlreadyInAllAlbums), ui.MutedInk,
                    TextStyles.Footnote, width);
                ImGui.SetCursorScreenPos(origin);
                ImGui.Dummy(new Vector2(width, height));
            }

            if (picked == 0)
            {
                OpenCreateAlbumSheet(addTargets);
            }
            else if (picked != int.MinValue)
            {
                AddPhotosToCustomAlbum(picked, addTargets);
                UiFeedback.Play(UiSound.Success);
                EndSelect();
                router.Pop();
            }
        }

        AppHeader.EndLargeTitle(in navBar, context, "photos.nav.addToAlbum", Loc.T(L.Photos.AddToAlbum),
            NavBarStyle.From(ui), ReadOnlySpan<NavBarButton>.Empty, Loc.T(L.Common.Cancel), popOnly);
    }

    private void RefreshEligibleAlbums()
    {
        if (ReferenceEquals(eligibleTargets, addTargets) && eligibleVersion == albumsVersion)
        {
            return;
        }

        eligibleTargets = addTargets;
        eligibleVersion = albumsVersion;
        eligibleAlbums.Clear();
        for (var index = 0; index < customAlbums.Count; index++)
        {
            if (!AlbumContainsAll(customAlbums[index], addTargets))
            {
                eligibleAlbums.Add(customAlbums[index]);
            }
        }
    }

    private bool DrawAddRow(Rect card, Rect row, bool first, bool last, string? coverPath, string title,
        string subtitle, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var washMin = new Vector2(card.Min.X, row.Min.Y);
        var washMax = new Vector2(card.Max.X, row.Max.Y);
        var hovered = UiInteract.Hover(washMin, washMax);
        if (hovered)
        {
            var down = ImGui.IsMouseDown(ImGuiMouseButton.Left);
            RowWash(drawList, washMin, washMax, first, last, down ? RowPressAlpha : RowWashAlpha, scale);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var thumb = AddRowThumb * scale;
        var thumbMin = new Vector2(row.Min.X, row.Center.Y - thumb * 0.5f);
        var thumbMax = thumbMin + new Vector2(thumb, thumb);
        var rounding = AddRowThumbRounding * scale;
        if (first)
        {
            Squircle.Fill(drawList, thumbMin, thumbMax, rounding,
                ImGui.GetColorU32(Core.Theme.Palette.WithAlpha(ui.Accent, 0.16f)));
            PhoneIcon.Draw(drawList, (thumbMin + thumbMax) * 0.5f, PhoneIcons.Plus, ui.Accent, thumb * 0.5f);
        }
        else
        {
            PhotosChrome.Cover(drawList, coverPath is null ? null : GetThumbnail(coverPath), thumbMin, thumbMax,
                rounding, ui, scale, false);
        }

        var textLeft = thumbMax.X + AddRowGap * scale;
        var textWidth = MathF.Max(1f, row.Max.X - textLeft);
        var titleInk = first ? ui.Accent : ui.TitleInk;
        if (subtitle.Length == 0)
        {
            var lineHeight = Typography.LineHeight(TextStyles.Body);
            Typography.Draw(drawList, new Vector2(textLeft, row.Center.Y - lineHeight * 0.5f),
                Typography.FitText(title, textWidth, TextStyles.Body), titleInk, TextStyles.Body);
        }
        else
        {
            var titleHeight = Typography.LineHeight(TextStyles.Body);
            Typography.Draw(drawList, new Vector2(textLeft, row.Center.Y - AddRowTitleLift * scale - titleHeight * 0.5f),
                Typography.FitText(title, textWidth, TextStyles.Body), titleInk, TextStyles.Body);
            var subtitleHeight = Typography.LineHeight(TextStyles.Footnote);
            Typography.Draw(drawList,
                new Vector2(textLeft, row.Center.Y + AddRowCountDrop * scale - subtitleHeight * 0.5f),
                Typography.FitText(subtitle, textWidth, TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);
        }

        return UiInteract.Click(washMin, washMax, hovered);
    }

    private void OpenAlbum(int key) => router.Push(PhotoView.Album(key));

    private void OpenAlbumPicker(int key)
    {
        pickerSelection.Clear();
        pickerSelectionOrder.Clear();
        InvalidatePickerMembership();
        router.Push(PhotoView.AlbumPicker(key));
    }

    private void OpenCreateAlbumSheet(string[]? photoPaths)
    {
        nameSheetMode = NameSheetMode.Create;
        nameSheetAlbumKey = 0;
        nameSheetPhotoPaths = photoPaths;
        nameDraft = string.Empty;
        nameSheetFocus = true;
        nameSheet.Open();
    }

    private void OpenRenameSheet(int key)
    {
        if (!TryFindCustomAlbum(key, out var album))
        {
            return;
        }

        nameSheetMode = NameSheetMode.Rename;
        nameSheetAlbumKey = key;
        nameSheetPhotoPaths = null;
        nameDraft = album.Name;
        nameSheetFocus = true;
        nameSheet.Open();
    }

    private void DrawNameSheet(Rect area)
    {
        if (!nameSheet.CapturesPointer)
        {
            return;
        }

        var scale = UiScale.Current;
        var fitted = SheetMetrics.GrabberZone * scale + (SheetHeaderHeight + GlassField.HeightUnits + SheetFieldGap +
                     SheetButtonHeight + SheetHintGap + SheetBottomPad) * scale +
                     Typography.LineHeight(TextStyles.Footnote);
        var screen = new Rect(new Vector2(area.Min.X, frameScreen.Min.Y), frameScreen.Max);
        ImGui.SetCursorScreenPos(screen.Min);
        using (ImRaii.Child("##photosNameSheet", screen.Size, false, SheetHostFlags))
        {
            var frame = nameSheet.Begin(ImGui.GetWindowDrawList(), screen, frameTheme, SheetDetents.Fitted(fitted),
                SheetMetrics.AppVeil);
            if (!frame.Visible)
            {
                return;
            }

            if (frame.Interactive)
            {
                drawNameSheet(frame.Content);
            }

            DrawNameSheetHeader(in frame, scale);
            nameSheet.End(in frame);
        }
    }

    private void DrawNameSheetHeader(in SheetFrame frame, float scale)
    {
        var title = Loc.T(nameSheetMode == NameSheetMode.Rename ? L.Photos.Rename : L.Photos.CreateAlbum);
        var center = new Vector2(frame.Content.Center.X, frame.Content.Min.Y + SheetHeaderHeight * scale * 0.5f);
        Typography.DrawCentered(frame.DrawList, center, title, frame.Ink with { W = frame.Ink.W * frame.Opacity },
            TextStyles.Headline);
    }

    private void DrawNameSheetContent(Rect content)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var padX = SheetPadX * scale;
        var top = content.Min.Y + SheetHeaderHeight * scale;
        var field = new Rect(new Vector2(content.Min.X + padX, top),
            new Vector2(content.Max.X - padX, top + GlassField.HeightUnits * scale));
        SearchBar.Surface(drawList, field, ControlInk.From(frameTheme));
        var focus = nameSheetFocus;
        nameSheetFocus = false;
        var submitted = GlassField.Text(field, "##photos.albumName", Loc.T(L.Photos.AlbumName), ref nameDraft,
            frameTheme, scale, MaxAlbumNameLength, focus, ImGuiInputTextFlags.EnterReturnsTrue);
        var trimmed = nameDraft.AsSpan().Trim();
        var excludeKey = nameSheetMode == NameSheetMode.Rename ? nameSheetAlbumKey : 0;
        var duplicate = trimmed.Length > 0 && IsAlbumNameTaken(trimmed, excludeKey);
        var canCommit = trimmed.Length > 0 && !duplicate;
        var buttonTop = field.Max.Y + SheetFieldGap * scale;
        var button = new Rect(new Vector2(field.Min.X, buttonTop),
            new Vector2(field.Max.X, buttonTop + SheetButtonHeight * scale));
        var label = Loc.T(nameSheetMode == NameSheetMode.Rename ? L.Photos.Rename : L.Photos.CreateAlbumButton);
        var pressed = ui.AccentPill(button, label, canCommit, TextStyles.Headline);
        if ((pressed || submitted) && canCommit)
        {
            CommitNameSheet(trimmed.ToString());
            return;
        }

        if (!duplicate)
        {
            return;
        }

        var hintCenterY = button.Max.Y + SheetHintGap * scale + Typography.LineHeight(TextStyles.Footnote) * 0.5f;
        Typography.DrawCentered(drawList, new Vector2(content.Center.X, hintCenterY),
            Typography.FitText(Loc.T(L.Photos.AlbumExists), field.Width, TextStyles.Footnote), frameTheme.Danger,
            TextStyles.Footnote);
    }

    private void CommitNameSheet(string name)
    {
        nameSheet.Close();
        if (nameSheetMode == NameSheetMode.Rename)
        {
            RenameCustomAlbum(nameSheetAlbumKey, name);
            return;
        }

        var key = CreateCustomAlbum(name);
        if (key == 0)
        {
            return;
        }

        UiFeedback.Play(UiSound.Success);
        if (nameSheetPhotoPaths is { } paths)
        {
            nameSheetPhotoPaths = null;
            AddPhotosToCustomAlbum(key, paths);
            EndSelect();
            if (router.Current.Route == PhotoRoute.AddToAlbum)
            {
                router.Pop();
            }

            return;
        }

        OpenAlbum(key);
    }

    private bool IsAlbumNameTaken(ReadOnlySpan<char> name, int excludeKey)
    {
        for (var index = 0; index < customAlbums.Count; index++)
        {
            var album = customAlbums[index];
            if (album.Key != excludeKey && name.Equals(album.Name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void OpenAlbumSheet(int key)
    {
        albumSheetKey = key;
        albumSheetItems[0] = new ActionSheet.Item(Loc.T(L.Photos.AddPhotos), PhoneIcons.Plus);
        albumSheetItems[1] = new ActionSheet.Item(Loc.T(L.Photos.Rename), PhoneIcons.Pencil);
        albumSheetItems[2] = new ActionSheet.Item(Loc.T(L.Photos.DeleteAlbum), PhoneIcons.Trash, Danger: true);
        albumSheet.Open();
    }

    private void DrawAlbumSheet(Rect screen)
    {
        if (!albumSheet.CapturesPointer)
        {
            return;
        }

        var title = TryFindCustomAlbum(albumSheetKey, out var album) ? album.Name : string.Empty;
        var picked = albumSheet.Draw(screen, ActionSheetStyle.From(ui), albumSheetItems, Loc.T(L.Common.Cancel), false,
            title);
        switch (picked)
        {
            case 0:
                OpenAlbumPicker(albumSheetKey);
                break;
            case 1:
                OpenRenameSheet(albumSheetKey);
                break;
            case 2:
                AskDeleteAlbum(albumSheetKey);
                break;
        }
    }

    private void OpenPhotoSheet(int albumKey, string path)
    {
        photoSheetAlbumKey = albumKey;
        photoSheetPath = path;
        photoSheetItems[0] = new ActionSheet.Item(Loc.T(L.Photos.RemoveFromAlbum), PhoneIcons.Trash, Danger: true);
        photoSheet.Open();
    }

    private void DrawPhotoSheet(Rect screen)
    {
        if (!photoSheet.CapturesPointer)
        {
            return;
        }

        var picked = photoSheet.Draw(screen, ActionSheetStyle.From(ui), photoSheetItems, Loc.T(L.Common.Cancel), false);
        if (picked != 0)
        {
            return;
        }

        AskRemoveFromAlbum(photoSheetAlbumKey, photoSheetPath);
    }

    private void OpenTrashSheet()
    {
        trashSheetItems[0] = new ActionSheet.Item(Loc.T(L.Photos.RecoverAll), PhoneIcons.ArrowBackUp);
        trashSheetItems[1] = new ActionSheet.Item(Loc.T(L.Photos.DeleteAll), PhoneIcons.Trash, Danger: true);
        trashSheet.Open();
    }

    private void DrawTrashSheet(Rect screen)
    {
        if (!trashSheet.CapturesPointer)
        {
            return;
        }

        var picked = trashSheet.Draw(screen, ActionSheetStyle.From(ui), trashSheetItems, Loc.T(L.Common.Cancel), false,
            Loc.T(L.Photos.RecentlyDeleted));
        switch (picked)
        {
            case 0:
                RecoverPhotos(trashPaths);
                break;
            case 1:
                AskDeleteForever(trashPaths);
                break;
        }
    }

    private void AskRemoveFromAlbum(int albumKey, string path)
    {
        if (!TryFindCustomAlbum(albumKey, out var album))
        {
            return;
        }

        confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Photos.RemoveFromAlbumConfirm, album.Name),
            ConfirmLabel = Loc.T(L.Photos.RemoveFromAlbum),
            CancelLabel = Loc.T(L.Common.Cancel),
            Sheet = true,
            Confirm = () => RemovePhotoFromCustomAlbum(albumKey, path),
        });
    }

    private void AskMonthlyAlbums()
    {
        configuration.PhotosMonthlyAlbumsAsked = true;
        configuration.Save();
        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Photos.MonthlyAlbums),
            Message = Loc.T(L.Photos.MonthlyAlbumsPrompt),
            ConfirmLabel = Loc.T(L.Photos.MonthlyAlbumsOn),
            CancelLabel = Loc.T(L.Photos.MonthlyAlbumsNotNow),
            Danger = false,
            Sheet = true,
            Confirm = () =>
            {
                configuration.PhotosMonthlyAlbums = true;
                configuration.Save();
            },
        });
    }

    private void AskDeleteAlbum(int key)
    {
        if (!TryFindCustomAlbum(key, out var album))
        {
            return;
        }

        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Photos.DeleteAlbumConfirm, album.Name),
            Message = Loc.T(L.Photos.DeleteAlbumBody),
            ConfirmLabel = Loc.T(L.Photos.DeleteAlbum),
            CancelLabel = Loc.T(L.Common.Cancel),
            Sheet = true,
            Confirm = () =>
            {
                DeleteCustomAlbum(key);
                if (router.Current.Route == PhotoRoute.Album && router.Current.AlbumKey == key)
                {
                    router.Pop();
                }
            },
        });
    }

    private void LoadCustomAlbums()
    {
        customAlbumOrder.Clear();
        customAlbumOrder.AddRange(configuration.CustomAlbumOrder);
        customAlbumPhotos.Clear();
        foreach (var entry in configuration.CustomAlbumPhotos)
        {
            customAlbumPhotos[entry.Key] = new List<string>(entry.Value);
        }

        customAlbumIds.Clear();
        nextCustomAlbumId = PhotoView.FirstCustomAlbumId;
    }

    private void SaveCustomAlbums()
    {
        configuration.CustomAlbumOrder = new List<string>(customAlbumOrder);
        configuration.CustomAlbumPhotos = new Dictionary<string, List<string>>(customAlbumPhotos,
            StringComparer.OrdinalIgnoreCase);
        configuration.SaveNow();
    }

    private bool PruneCustomAlbumPaths(HashSet<string> validPaths)
    {
        var removedAny = false;
        for (var albumIndex = 0; albumIndex < customAlbumOrder.Count; albumIndex++)
        {
            if (!customAlbumPhotos.TryGetValue(customAlbumOrder[albumIndex], out var photos))
            {
                continue;
            }

            for (var index = photos.Count - 1; index >= 0; index--)
            {
                if (validPaths.Contains(photos[index]))
                {
                    continue;
                }

                photos.RemoveAt(index);
                removedAny = true;
            }
        }

        return removedAny;
    }

    private void BuildCustomAlbums()
    {
        albumsVersion++;
        customAlbums.Clear();
        cachedCustomAlbumPaths.Clear();
        for (var albumIndex = 0; albumIndex < customAlbumOrder.Count; albumIndex++)
        {
            var name = customAlbumOrder[albumIndex];
            if (!customAlbumPhotos.TryGetValue(name, out var photos))
            {
                continue;
            }

            var key = -GetOrAssignCustomAlbumId(name);
            customAlbums.Add(new CustomAlbum(key, photos.Count, name));
            var sorted = photos.ToArray();
            Array.Sort(sorted, comparePaths);
            cachedCustomAlbumPaths[key] = sorted;
        }
    }

    private bool TryFindCustomAlbum(int key, out CustomAlbum result)
    {
        for (var index = 0; index < customAlbums.Count; index++)
        {
            if (customAlbums[index].Key == key)
            {
                result = customAlbums[index];
                return true;
            }
        }

        result = default;
        return false;
    }

    private int CreateCustomAlbum(string name)
    {
        name = name.Trim();
        if (name.Length == 0 || ContainsOrdinalIgnoreCase(customAlbumOrder, name))
        {
            return 0;
        }

        customAlbumOrder.Add(name);
        customAlbumPhotos[name] = new List<string>();
        BuildCustomAlbums();
        SaveCustomAlbums();
        return -GetOrAssignCustomAlbumId(name);
    }

    private void DeleteCustomAlbum(int key)
    {
        if (!TryFindCustomAlbum(key, out var found))
        {
            return;
        }

        customAlbumOrder.Remove(found.Name);
        customAlbumPhotos.Remove(found.Name);
        customAlbumIds.Remove(found.Name);
        BuildCustomAlbums();
        SaveCustomAlbums();
        ApplyFilter();
    }

    private void RenameCustomAlbum(int key, string newName)
    {
        newName = newName.Trim();
        if (newName.Length == 0 || !TryFindCustomAlbum(key, out var found) ||
            string.Equals(found.Name, newName, StringComparison.OrdinalIgnoreCase) ||
            ContainsOrdinalIgnoreCase(customAlbumOrder, newName) ||
            !customAlbumPhotos.TryGetValue(found.Name, out var photos))
        {
            return;
        }

        if (customAlbumIds.TryGetValue(found.Name, out var id))
        {
            customAlbumIds.Remove(found.Name);
            customAlbumIds[newName] = id;
        }

        customAlbumOrder[customAlbumOrder.IndexOf(found.Name)] = newName;
        customAlbumPhotos.Remove(found.Name);
        customAlbumPhotos[newName] = photos;
        BuildCustomAlbums();
        SaveCustomAlbums();
    }

    private void AddPhotosToCustomAlbum(int key, string[] paths)
    {
        if (!TryFindCustomAlbum(key, out var found) || !customAlbumPhotos.TryGetValue(found.Name, out var photos))
        {
            return;
        }

        for (var index = 0; index < paths.Length; index++)
        {
            if (!ContainsOrdinalIgnoreCase(photos, paths[index]))
            {
                photos.Add(paths[index]);
            }
        }

        BuildCustomAlbums();
        SaveCustomAlbums();
        ApplyFilter();
        InvalidatePickerMembership();
    }

    private void RemovePhotoFromCustomAlbum(int key, string path)
    {
        if (!TryFindCustomAlbum(key, out var found) || !customAlbumPhotos.TryGetValue(found.Name, out var photos))
        {
            return;
        }

        photos.Remove(path);
        BuildCustomAlbums();
        SaveCustomAlbums();
        ApplyFilter();
        InvalidatePickerMembership();
    }

    private bool AlbumContainsAll(CustomAlbum album, string[] paths)
    {
        if (!customAlbumPhotos.TryGetValue(album.Name, out var photos) || paths.Length == 0)
        {
            return false;
        }

        var members = new HashSet<string>(photos, StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < paths.Length; index++)
        {
            if (!members.Contains(paths[index]))
            {
                return false;
            }
        }

        return true;
    }

    private void EnsurePickerMembership(int albumKey)
    {
        if (pickerMembershipAlbumKey == albumKey)
        {
            return;
        }

        pickerMembership.Clear();
        if (TryFindCustomAlbum(albumKey, out var album) && customAlbumPhotos.TryGetValue(album.Name, out var existing))
        {
            pickerMembership.UnionWith(existing);
        }

        pickerMembershipAlbumKey = albumKey;
    }

    private void InvalidatePickerMembership() => pickerMembershipAlbumKey = null;

    private void AddToPickerSelection(string path)
    {
        pickerSelection.Add(path);
        pickerSelectionOrder[path] = pickerSelection.Count;
    }

    private void RemoveFromPickerSelection(string path)
    {
        if (!pickerSelection.Remove(path))
        {
            return;
        }

        pickerSelectionOrder.Remove(path);
        for (var index = 0; index < pickerSelection.Count; index++)
        {
            pickerSelectionOrder[pickerSelection[index]] = index + 1;
        }
    }

    private int GetOrAssignCustomAlbumId(string name)
    {
        if (customAlbumIds.TryGetValue(name, out var id))
        {
            return id;
        }

        id = nextCustomAlbumId++;
        customAlbumIds[name] = id;
        return id;
    }

    private static bool ContainsOrdinalIgnoreCase(List<string> values, string value)
    {
        for (var index = 0; index < values.Count; index++)
        {
            if (string.Equals(values[index], value, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
