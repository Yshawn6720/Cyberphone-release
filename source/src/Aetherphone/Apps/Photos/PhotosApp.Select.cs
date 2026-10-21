using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Photos;

internal sealed partial class PhotosApp
{
    private enum SelectionScope : byte
    {
        Library,
        Album,
        Trash,
    }

    private const float ToolbarGlyph = 22f;
    private const float ToolbarHitRadius = 20f;
    private const float DisabledToolbarAlpha = 0.35f;

    private readonly HashSet<string> selection = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> selectionOrder = new();
    private bool selecting;
    private SelectionScope selectionScope;

    private void BeginSelect(SelectionScope scope)
    {
        selecting = true;
        selectionScope = scope;
        selection.Clear();
        selectionOrder.Clear();
    }

    private void EndSelect()
    {
        selecting = false;
        selection.Clear();
        selectionOrder.Clear();
    }

    private void ToggleSelected(string path)
    {
        if (selection.Remove(path))
        {
            selectionOrder.Remove(path);
            return;
        }

        selection.Add(path);
        selectionOrder.Add(path);
    }

    private string[] SelectionArray() => selectionOrder.ToArray();

    private string SelectionTitle() =>
        selection.Count > 0 ? SelectedLabel(selection.Count) : Loc.T(L.Photos.SelectPhotos);

    private bool AllSelectedFavorites()
    {
        if (selectionOrder.Count == 0)
        {
            return false;
        }

        for (var index = 0; index < selectionOrder.Count; index++)
        {
            if (!favorites.Contains(selectionOrder[index]))
            {
                return false;
            }
        }

        return true;
    }

    private void FavoriteSelection()
    {
        var remove = AllSelectedFavorites();
        for (var index = 0; index < selectionOrder.Count; index++)
        {
            if (remove)
            {
                favorites.Remove(selectionOrder[index]);
            }
            else
            {
                favorites.Add(selectionOrder[index]);
            }
        }

        BuildFavorites();
        SaveFavorites();
        ApplyFilter();
    }

    private void DrawSelectToolbar(Rect content)
    {
        var scale = UiScale.Current;
        var capsule = TabBarLayout.FullCapsule(content, scale, false);
        var zone = TabBar.Zone(content, scale);
        using var layer = ScreenLayer.Begin("photos.toolbar", zone, false);
        UiInteract.HoverOverlay(zone);
        var drawList = ImGui.GetWindowDrawList();
        Material.ThemedGlass(drawList, capsule.Min, capsule.Max, capsule.Height * 0.5f, scale, ui.BackdropColor,
            TabBar.GlassOpacity);
        var enabled = selection.Count > 0;
        if (selectionScope == SelectionScope.Trash)
        {
            if (ToolbarAction(drawList, ToolbarSlot(capsule, 0, 2), "photos.select.recover", PhoneIcons.ArrowBackUp,
                    Loc.T(L.Photos.Recover), ui.Accent, enabled, scale))
            {
                RecoverPhotos(SelectionArray());
            }

            if (ToolbarAction(drawList, ToolbarSlot(capsule, 1, 2), "photos.select.purge", PhoneIcons.Trash,
                    Loc.T(L.Photos.DeletePermanently), frameTheme.Danger, enabled, scale))
            {
                AskDeleteForever(SelectionArray());
            }

            return;
        }

        var allFavorites = AllSelectedFavorites();
        if (ToolbarAction(drawList, ToolbarSlot(capsule, 0, 3), "photos.select.favorite",
                allFavorites ? PhoneIcons.HeartFilled : PhoneIcons.Heart,
                Loc.T(allFavorites ? L.Photos.Unfavorite : L.Photos.Favorite), ui.Accent, enabled, scale))
        {
            FavoriteSelection();
        }

        if (ToolbarAction(drawList, ToolbarSlot(capsule, 1, 3), "photos.select.album", PhoneIcons.SquareRoundedPlus,
                Loc.T(L.Photos.AddToAlbum), ui.Accent, enabled, scale))
        {
            OpenAddToAlbum(SelectionArray());
        }

        if (ToolbarAction(drawList, ToolbarSlot(capsule, 2, 3), "photos.select.delete", PhoneIcons.Trash,
                Loc.T(L.Photos.Delete), frameTheme.Danger, enabled, scale))
        {
            AskDeletePhotos(SelectionArray());
        }
    }

    private static Vector2 ToolbarSlot(Rect bar, int index, int count) =>
        new(bar.Min.X + bar.Width / count * (index + 0.5f), bar.Center.Y);

    private static bool ToolbarAction(ImDrawListPtr drawList, Vector2 center, string id, string glyph, string tooltip,
        Vector4 ink, bool enabled, float scale)
    {
        var radius = ToolbarHitRadius * scale;
        var extent = new Vector2(radius, radius);
        var hovered = enabled && !UiInteract.InputBlocked && UiInteract.HoverWindowOnly(center - extent, center + extent);
        var down = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var grow = PressFx.Scale(ImGui.GetID(id), down, PressFx.IconPressedScale);
        var color = enabled ? ink : Palette.WithAlpha(ink, DisabledToolbarAlpha);
        PhoneIcon.Draw(drawList, center, glyph, color, ToolbarGlyph * scale * grow);
        if (!enabled)
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
}
