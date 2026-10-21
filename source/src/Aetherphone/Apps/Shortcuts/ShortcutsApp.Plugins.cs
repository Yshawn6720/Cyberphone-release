using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Shortcuts;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Shortcuts;

internal sealed partial class ShortcutsApp
{
    private enum PickerPurpose : byte
    {
        AddStep,
        ReplaceStep,
        Icon,
    }

    private const float HeroTile = 64f;
    private const float HeroPad = 16f;
    private const float HeroLineGap = 2f;
    private const float CommandRowHeight = 56f;
    private const float PlusRadius = 14f;
    private const float PlusGlyph = 12f;
    private const float ChevronGlyph = 11f;

    private readonly Action<PluginEntry> openPluginDetail;
    private readonly Action<PluginEntry> pickStepPlugin;
    private readonly Action<PluginEntry> pickIconPlugin;
    private string pluginQuery = string.Empty;
    private string detailPlugin = string.Empty;
    private PickerPurpose pickerPurpose;
    private int pickerStepIndex = -1;
    private readonly Dictionary<int, string> commandCountLabels = new();
    private CachedText authorLabel;

    private void DrawPluginsTab(in PhoneContext context)
    {
        var navBar = AppHeader.BeginLargeTitle(context, false);
        DrawPluginBrowser(navBar.Body, openPluginDetail);
        AppHeader.EndLargeTitle(in navBar, context, "shortcuts.nav.plugins", Loc.T(L.Shortcuts.TabPlugins),
            NavBarStyle.From(ui), ReadOnlySpan<NavBarButton>.Empty);
    }

    private void OpenPluginPicker(PickerPurpose purpose, int stepIndex)
    {
        pluginQuery = string.Empty;
        pickerPurpose = purpose;
        pickerStepIndex = stepIndex;
        Push(ShortcutsRoute.PluginPicker, router.Current.Route == ShortcutsRoute.Appearance
            ? Loc.T(L.Shortcuts.Appearance)
            : EditorTitle());
    }

    private void DrawPluginPicker(in PhoneContext context, ShortcutsView view)
    {
        var navBar = AppHeader.BeginLargeTitle(context);
        DrawPluginBrowser(navBar.Body, pickerPurpose == PickerPurpose.Icon ? pickIconPlugin : pickStepPlugin);
        AppHeader.EndLargeTitle(in navBar, context, "shortcuts.nav.picker",
            Loc.T(pickerPurpose == PickerPurpose.Icon ? L.Shortcuts.ChooseIcon : L.Shortcuts.ChoosePlugin),
            NavBarStyle.From(ui), ReadOnlySpan<NavBarButton>.Empty, view.BackTitle, back);
    }

    private void DrawPluginBrowser(Rect body, Action<PluginEntry> onPick)
    {
        var scale = UiScale.Current;
        using (AppSurface.Begin(body))
        {
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var field = new Rect(origin, new Vector2(origin.X + width, origin.Y + GlassField.HeightUnits * scale));
            SearchBar.Surface(drawList, field, ControlInk.From(theme));
            GlassField.Search(drawList, field, "##shortcutsPluginSearch", Loc.T(L.Shortcuts.SearchPlugins),
                ref pluginQuery, theme, scale, SearchMaxLength, false);
            var cursorY = DrawPluginList(drawList, new Vector2(origin.X, field.Max.Y + SearchGap * scale), width,
                onPick, scale);
            ShortcutsArt.ReserveTo(origin, width, cursorY + ShortcutsArt.BottomPad * scale);
        }
    }

    private float DrawPluginList(ImDrawListPtr drawList, Vector2 origin, float width, Action<PluginEntry> onPick,
        float scale)
    {
        var entries = catalog.Entries;
        var matches = 0;
        for (var index = 0; index < entries.Count; index++)
        {
            if (Matches(entries[index], pluginQuery))
            {
                matches++;
            }
        }

        if (matches == 0)
        {
            return ShortcutsArt.State(drawList, ui, origin, width, FontAwesomeIcon.PuzzlePiece,
                Loc.T(L.Shortcuts.NoResults), Loc.T(L.Shortcuts.NoPluginsFound), string.Empty, string.Empty, out _,
                out _, scale);
        }

        var rowHeight = ShortcutsArt.RowHeight * scale;
        var max = new Vector2(origin.X + width, origin.Y + matches * rowHeight);
        ui.Card(drawList, origin, max, Metrics.Radius.Grouped * scale);
        var pad = Metrics.Space.Lg * scale;
        var iconSize = ShortcutsArt.IconSize * scale;
        var row = 0;
        PluginEntry? picked = null;
        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            if (!Matches(entry, pluginQuery))
            {
                continue;
            }

            var top = origin.Y + row * rowHeight;
            var rect = new Rect(new Vector2(origin.X, top), new Vector2(max.X, top + rowHeight));
            if (row == 0)
            {
                UiAnchors.Report("shortcuts.plugin.row", rect);
            }
            else
            {
                FeedCell.Hairline(drawList, origin.X + pad + iconSize + ShortcutsArt.TextGap * scale,
                    max.X - pad, top, ui.Hairline);
            }

            row++;
            if (ImGui.IsRectVisible(rect.Min, rect.Max) && DrawPluginRow(drawList, rect, entry, scale))
            {
                picked = entry;
            }
        }

        if (picked is not null)
        {
            onPick(picked);
        }

        return max.Y;
    }

    private static bool Matches(PluginEntry entry, string query)
    {
        var trimmed = query.AsSpan().Trim();
        if (trimmed.Length == 0)
        {
            return true;
        }

        if (entry.Name.AsSpan().Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
            entry.InternalName.AsSpan().Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
            entry.Punchline.AsSpan().Contains(trimmed, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        for (var index = 0; index < entry.Commands.Count; index++)
        {
            if (entry.Commands[index].Command.AsSpan().Contains(trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private bool DrawPluginRow(ImDrawListPtr drawList, Rect row, PluginEntry entry, float scale)
    {
        var hovered = ShortcutsArt.RowInteraction(drawList, ui, row, scale);
        var pad = Metrics.Space.Lg * scale;
        var iconSize = ShortcutsArt.IconSize * scale;
        DrawPluginTile(drawList, new Vector2(row.Min.X + pad + iconSize * 0.5f, row.Center.Y), iconSize, entry, scale);
        var chevronX = row.Max.X - pad - ChevronGlyph * scale * 0.5f;
        ProgressRing.CenterIcon(drawList, new Vector2(chevronX, row.Center.Y), FontAwesomeIcon.ChevronRight,
            Palette.WithAlpha(ui.MutedInk, 0.7f), ChevronGlyph * scale);
        var textLeft = row.Min.X + pad + iconSize + ShortcutsArt.TextGap * scale;
        Labels(drawList, textLeft, chevronX - ShortcutsArt.TextGap * scale, row.Center.Y, entry.Name,
            PluginSubtitle(entry), entry.Loaded ? ui.TitleInk : ui.MutedInk, ui.MutedInk, scale);
        return UiInteract.Click(row.Min, row.Max, hovered);
    }

    private static void Labels(ImDrawListPtr drawList, float left, float right, float centerY, string title,
        string subtitle, Vector4 titleInk, Vector4 subtitleInk, float scale)
    {
        var width = MathF.Max(1f, right - left);
        var fittedTitle = Typography.FitText(title, width, TextStyles.Headline);
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        if (subtitle.Length == 0)
        {
            Typography.Draw(drawList, new Vector2(left, centerY - titleHeight * 0.5f), fittedTitle, titleInk,
                TextStyles.Headline);
            return;
        }

        var fittedSubtitle = Typography.FitText(subtitle, width, TextStyles.Footnote);
        var subtitleHeight = Typography.LineHeight(TextStyles.Footnote);
        var top = centerY - (titleHeight + HeroLineGap * scale + subtitleHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(left, top), fittedTitle, titleInk, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(left, top + titleHeight + HeroLineGap * scale), fittedSubtitle,
            subtitleInk, TextStyles.Footnote);
    }

    private string PluginSubtitle(PluginEntry entry)
    {
        if (!entry.Loaded)
        {
            return Loc.T(L.Shortcuts.PluginDisabled);
        }

        if (entry.Commands.Count > 0)
        {
            return CommandCountLabel(entry.Commands.Count);
        }

        return entry.Punchline.Length > 0 ? entry.Punchline : entry.InternalName;
    }

    private string CommandCountLabel(int count)
    {
        if (!commandCountLabels.TryGetValue(count, out var label))
        {
            label = Loc.T(L.Shortcuts.PluginCommandCount, count);
            commandCountLabels[count] = label;
        }

        return label;
    }

    private string AuthorLabel(string author)
    {
        var key = (long)author.GetHashCode();
        return authorLabel.IsCurrent(key) ? authorLabel.Value : authorLabel.Store(key, Loc.T(L.Shortcuts.PluginBy, author));
    }

    private void DrawPluginTile(ImDrawListPtr drawList, Vector2 center, float size, PluginEntry entry, float scale)
    {
        var half = new Vector2(size * 0.5f);
        var min = center - half;
        var max = center + half;
        var radius = size * Metrics.Radius.TileFactor;
        var icon = catalog.Icon(entry.InternalName);
        if (icon is not null)
        {
            Squircle.FillImage(drawList, min, max, radius, icon.Handle, entry.Loaded ? 0xFFFFFFFFu : 0x80FFFFFFu);
            return;
        }

        IconTile.FillShaded(drawList, min, max, radius, IconTile.Surface(AccentFor(entry.InternalName)),
            entry.Loaded ? 1f : 0.55f);
        Material.EdgeSquircle(drawList, min, max, radius, scale);
        var monogram = ShortcutsArt.Monogram(entry.Name);
        var measured = Typography.Measure(monogram, TextStyles.Title2);
        var glyphScale = measured.Y > 0f ? size * 0.42f / measured.Y : 1f;
        Typography.DrawCentered(drawList, center, monogram, new Vector4(1f, 1f, 1f, 1f),
            TextStyles.Title2.Scale * glyphScale, FontWeight.SemiBold);
    }

    private static Vector4 AccentFor(string internalName)
    {
        var hash = 2166136261u;
        for (var index = 0; index < internalName.Length; index++)
        {
            hash = (hash ^ internalName[index]) * 16777619u;
        }

        return ShortcutPalette.Wheel[(int)(hash % (uint)ShortcutPalette.Wheel.Length)];
    }

    private void OpenPluginDetail(PluginEntry entry)
    {
        detailPlugin = entry.InternalName;
        Push(ShortcutsRoute.Plugin, Loc.T(L.Shortcuts.TabPlugins));
    }

    private void DrawPluginDetail(in PhoneContext context, ShortcutsView view)
    {
        var entry = catalog.Find(detailPlugin);
        var navBar = AppHeader.BeginLargeTitle(context);
        var scale = UiScale.Current;
        if (entry is not null)
        {
            using (AppSurface.Begin(navBar.Body))
            {
                var drawList = ImGui.GetWindowDrawList();
                var origin = ImGui.GetCursorScreenPos();
                var width = ScrollLayout.StableContentWidth();
                var cursorY = DrawPluginHero(drawList, origin, width, entry, scale);
                cursorY = DrawPluginActions(drawList, new Vector2(origin.X, cursorY + ShortcutsArt.TileGap * scale),
                    width, entry, scale);
                cursorY = DrawPluginCommands(drawList, new Vector2(origin.X, cursorY), width, entry, scale);
                ShortcutsArt.ReserveTo(origin, width, cursorY + ShortcutsArt.BottomPad * scale);
            }
        }

        AppHeader.EndLargeTitle(in navBar, context, "shortcuts.nav.plugin",
            entry?.Name ?? Loc.T(L.Shortcuts.TabPlugins), NavBarStyle.From(ui), ReadOnlySpan<NavBarButton>.Empty,
            view.BackTitle, back);
    }

    private float DrawPluginHero(ImDrawListPtr drawList, Vector2 origin, float width, PluginEntry entry, float scale)
    {
        var pad = HeroPad * scale;
        var tile = HeroTile * scale;
        var textLeft = origin.X + pad + tile + ShortcutsArt.TextGap * scale;
        var textWidth = MathF.Max(1f, origin.X + width - pad - textLeft);
        var nameHeight = Typography.LineHeight(TextStyles.Title3);
        var bylineHeight = entry.Author.Length > 0 ? Typography.LineHeight(TextStyles.Footnote) : 0f;
        var punchLines = entry.Punchline.Length > 0
            ? WidgetText.Clamp(entry.Punchline, TextStyles.Subheadline, textWidth, 3)
            : WidgetText.NoLines;
        var punchLine = Typography.LineHeight(TextStyles.Subheadline);
        var textHeight = nameHeight + bylineHeight + (punchLines.Length > 0 ? HeroLineGap * 3f * scale : 0f) +
                         punchLines.Length * punchLine;
        var height = MathF.Max(tile, textHeight) + pad * 2f;
        var max = new Vector2(origin.X + width, origin.Y + height);
        ui.Card(drawList, origin, max, Metrics.Radius.Grouped * scale);
        DrawPluginTile(drawList, new Vector2(origin.X + pad + tile * 0.5f, origin.Y + pad + tile * 0.5f), tile, entry,
            scale);
        var top = origin.Y + pad;
        Typography.Draw(drawList, new Vector2(textLeft, top), Typography.FitText(entry.Name, textWidth, TextStyles.Title3),
            ui.TitleInk, TextStyles.Title3);
        top += nameHeight;
        if (bylineHeight > 0f)
        {
            Typography.Draw(drawList, new Vector2(textLeft, top),
                Typography.FitText(AuthorLabel(entry.Author), textWidth, TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);
            top += bylineHeight;
        }

        if (punchLines.Length > 0)
        {
            WidgetText.Lines(drawList, punchLines, new Vector2(textLeft, top + HeroLineGap * 3f * scale),
                ui.BodyInk, TextStyles.Subheadline, punchLine);
        }

        return max.Y;
    }

    private float DrawPluginActions(ImDrawListPtr drawList, Vector2 origin, float width, PluginEntry entry,
        float scale)
    {
        var canOpen = entry.Loaded && entry.HasMainUi;
        var canConfigure = entry.Loaded && entry.HasConfigUi;
        var count = 1 + (canOpen ? 1 : 0) + (canConfigure ? 1 : 0);
        var gap = ShortcutsArt.ActionTileGap * scale;
        var tileWidth = (width - gap * (count - 1)) / count;
        var height = ShortcutsArt.ActionTileHeight * scale;
        var left = origin.X;
        var rect = new Rect(new Vector2(left, origin.Y), new Vector2(left + tileWidth, origin.Y + height));
        if (ShortcutsArt.ActionTile(drawList, ui, rect, "##pluginAddHome", FontAwesomeIcon.Home,
                Loc.T(L.Shortcuts.AddToHome), ui.Accent, scale))
        {
            CreateLauncherShortcut(entry);
        }

        left += tileWidth + gap;
        if (canOpen)
        {
            rect = new Rect(new Vector2(left, origin.Y), new Vector2(left + tileWidth, origin.Y + height));
            if (ShortcutsArt.ActionTile(drawList, ui, rect, "##pluginOpen", FontAwesomeIcon.ExternalLinkAlt,
                    Loc.T(L.Shortcuts.OpenPlugin), ShortcutsArt.PluginTint, scale))
            {
                PluginCatalog.TryOpenMainUi(entry.InternalName);
            }

            left += tileWidth + gap;
        }

        if (canConfigure)
        {
            rect = new Rect(new Vector2(left, origin.Y), new Vector2(left + tileWidth, origin.Y + height));
            if (ShortcutsArt.ActionTile(drawList, ui, rect, "##pluginSettings", FontAwesomeIcon.Cog,
                    Loc.T(L.Shortcuts.PluginSettings), ShortcutsArt.WaitTint, scale))
            {
                PluginCatalog.TryOpenConfigUi(entry.InternalName);
            }
        }

        return origin.Y + height;
    }

    private float DrawPluginCommands(ImDrawListPtr drawList, Vector2 origin, float width, PluginEntry entry,
        float scale)
    {
        var top = origin.Y + ShortcutsArt.SectionGap * scale * 0.5f;
        top += CardSectionHeader.Draw(drawList, new Vector2(origin.X, top), width, Loc.T(L.Shortcuts.Commands),
            ui.TitleInk) + ShortcutsArt.HeaderGap * scale;
        if (entry.Commands.Count == 0)
        {
            return top + Typography.DrawWrappedLeft(new Vector2(origin.X, top), Loc.T(L.Shortcuts.NoCommands),
                ui.MutedInk, TextStyles.Subheadline, width);
        }

        var rowHeight = CommandRowHeight * scale;
        var max = new Vector2(origin.X + width, top + entry.Commands.Count * rowHeight);
        ui.Card(drawList, new Vector2(origin.X, top), max, Metrics.Radius.Grouped * scale);
        var pad = Metrics.Space.Lg * scale;
        for (var index = 0; index < entry.Commands.Count; index++)
        {
            var rowTop = top + index * rowHeight;
            if (index > 0)
            {
                FeedCell.Hairline(drawList, origin.X + pad, max.X - pad, rowTop, ui.Hairline);
            }

            var row = new Rect(new Vector2(origin.X, rowTop), new Vector2(max.X, rowTop + rowHeight));
            if (ImGui.IsRectVisible(row.Min, row.Max))
            {
                DrawCommandRow(drawList, row, entry.Commands[index], scale);
            }
        }

        var bottom = max.Y + Metrics.Space.Sm * scale;
        return bottom + Typography.DrawWrappedLeft(new Vector2(origin.X, bottom), Loc.T(L.Shortcuts.CommandsHint),
            ui.MutedInk, TextStyles.Footnote, width);
    }

    private void DrawCommandRow(ImDrawListPtr drawList, Rect row, PluginCommand command, float scale)
    {
        var pad = Metrics.Space.Lg * scale;
        var radius = PlusRadius * scale;
        var plusCenter = new Vector2(row.Max.X - pad - radius, row.Center.Y);
        var hit = new Vector2(radius + 6f * scale);
        var hovered = UiInteract.Hover(plusCenter - hit, plusCenter + hit);
        drawList.AddCircleFilled(plusCenter, radius,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, hovered ? 0.3f : 0.16f)), 24);
        ProgressRing.CenterIcon(drawList, plusCenter, FontAwesomeIcon.Plus, ui.Accent, PlusGlyph * scale);
        Labels(drawList, row.Min.X + pad, plusCenter.X - radius - ShortcutsArt.TextGap * scale, row.Center.Y,
            command.Command, command.Help, ui.TitleInk, ui.MutedInk, scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        HoverTooltip.Show(new Rect(plusCenter - hit, plusCenter + hit), Loc.T(L.Shortcuts.NewFromCommand),
            HoverLabelSide.Above);
        if (UiInteract.Click(plusCenter - hit, plusCenter + hit, hovered))
        {
            CreateCommandShortcut(command);
        }
    }

    private void CreateLauncherShortcut(PluginEntry entry)
    {
        if (WarnIfFull())
        {
            return;
        }

        var launcher = new ShortcutEntry
        {
            Name = entry.Name.Length <= ShortcutStore.NameMaxLength
                ? entry.Name
                : entry.Name.Substring(0, ShortcutStore.NameMaxLength),
            IconPlugin = entry.InternalName,
            Tint = HexColor.ToDigits(AccentFor(entry.InternalName)),
        };
        launcher.Steps.Add(new ShortcutStep { Kind = ShortcutStepKind.OpenPlugin, Text = entry.InternalName });
        BeginDraft(launcher, Guid.Empty, true);
    }

    private void CreateCommandShortcut(PluginCommand command)
    {
        if (WarnIfFull())
        {
            return;
        }

        var name = command.Command.TrimStart('/');
        var entry = new ShortcutEntry
        {
            Name = name.Length <= ShortcutStore.NameMaxLength ? name : name.Substring(0, ShortcutStore.NameMaxLength),
            Tint = HexColor.ToDigits(ShortcutPalette.Wheel[0]),
        };
        entry.Steps.Add(new ShortcutStep { Kind = ShortcutStepKind.Command, Text = command.Command });
        BeginDraft(entry, Guid.Empty, false);
    }

    private void PickStepPlugin(PluginEntry entry)
    {
        if (draft is not null)
        {
            if (pickerPurpose == PickerPurpose.ReplaceStep && pickerStepIndex >= 0 &&
                pickerStepIndex < draft.Steps.Count && draft.Steps[pickerStepIndex].Kind == ShortcutStepKind.OpenPlugin)
            {
                draft.Steps[pickerStepIndex].Text = entry.InternalName;
            }
            else if (draft.Steps.Count < ShortcutStore.MaxSteps)
            {
                draft.Steps.Add(new ShortcutStep { Kind = ShortcutStepKind.OpenPlugin, Text = entry.InternalName });
                ResetBlockSprings();
            }
        }

        router.Pop();
    }

    private void UsePluginIcon(PluginEntry entry)
    {
        if (draft is not null)
        {
            var unsavedIcon = UnsavedIconOf(draft);
            if (unsavedIcon.Length > 0)
            {
                store.ReleaseIcon(unsavedIcon);
            }

            draft.IconPlugin = entry.InternalName;
            draft.IconImage = string.Empty;
        }

        router.Pop();
    }
}
