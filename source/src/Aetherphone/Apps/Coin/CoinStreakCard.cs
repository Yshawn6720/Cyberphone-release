using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Coin;

internal sealed class CoinStreakCard
{
    private const int WeekDays = 7;
    private const float TileSize = 44f;
    private const float TileGlyph = 20f;
    private const float TileGap = 12f;
    private const float ButtonMinWidth = 96f;
    private const float RowGap = 14f;
    private const float DotRadius = 11f;
    private const float DotGlyph = 10f;
    private const float TrackThickness = 2f;
    private const float TrackAlpha = 0.10f;
    private const float FilledTrackAlpha = 0.55f;
    private const float PendingWashAlpha = 0.16f;
    private const float RestAlpha = 0.10f;
    private const float BreathBase = 0.45f;
    private const float BreathRange = 0.40f;
    private const float RingStroke = 1.8f;
    private const float FootGap = 10f;

    private CachedText title;

    public float Draw(AppSkin ui, Vector2 origin, float width, CoinWalletDto wallet, bool enabled, bool checking,
        out Rect buttonRect, out bool pressed)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var pad = Metrics.Space.Lg * scale;
        var tile = TileSize * scale;
        var footer = Loc.T(L.Coin.StreakGrace);
        var footerWidth = width - pad * 2f;
        var footerHeight = Typography.MeasureWrappedBlock(footer, TextStyles.Footnote, footerWidth).Y;
        var dotsHeight = DotRadius * 2f * scale;
        var height = pad + tile + RowGap * scale + dotsHeight + FootGap * scale + footerHeight + pad;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        CoinArt.Card(drawList, ui, min, max, scale);

        var accent = ui.Accent;
        var tileMin = new Vector2(min.X + pad, min.Y + pad);
        var tileMax = tileMin + new Vector2(tile, tile);
        IconTile.FillShaded(drawList, tileMin, tileMax, tile * Metrics.Radius.TileFactor, IconTile.Surface(accent));
        ProgressRing.CenterIcon(drawList, (tileMin + tileMax) * 0.5f, FontAwesomeIcon.Fire, AccentRing.Ink,
            TileGlyph * scale);

        var label = wallet.CheckInAvailable ? Loc.T(L.Coin.CheckIn) : Loc.T(L.Coin.CheckedIn);
        var buttonHeight = CoinArt.CapsuleHeight * scale;
        var buttonWidth = MathF.Min(width * 0.42f,
            MathF.Max(ButtonMinWidth * scale, CoinArt.CapsuleWidth(label, buttonHeight)));
        var rowCenterY = tileMin.Y + tile * 0.5f;
        buttonRect = new Rect(new Vector2(max.X - pad - buttonWidth, rowCenterY - buttonHeight * 0.5f),
            new Vector2(max.X - pad, rowCenterY + buttonHeight * 0.5f));
        var tone = wallet.CheckInAvailable ? CapsuleTone.Filled : CapsuleTone.Tinted;
        pressed = CoinArt.Capsule(drawList, ui, ImGui.GetID("coin.checkin"), buttonRect, label, tone,
            enabled && wallet.CheckInAvailable && !checking);

        var textLeft = tileMax.X + TileGap * scale;
        var textRight = buttonRect.Min.X - CoinArt.ValueGap * scale;
        var hint = wallet.CheckInAvailable ? Loc.T(L.Coin.StreakClaim) : Loc.T(L.Coin.StreakNext);
        CoinArt.Labels(drawList, textLeft, textRight, rowCenterY, Title(wallet.StreakDays), hint, ui.TitleInk,
            ui.MutedInk, scale);

        var dotsCenterY = tileMax.Y + RowGap * scale + dotsHeight * 0.5f;
        DrawWeek(drawList, wallet, accent, ui, min.X + pad, max.X - pad, dotsCenterY, scale);
        Typography.DrawWrappedLeft(new Vector2(min.X + pad, dotsCenterY + dotsHeight * 0.5f + FootGap * scale),
            footer, ui.MutedInk, TextStyles.Footnote, footerWidth);
        return max.Y;
    }

    private string Title(int streakDays)
    {
        return title.IsCurrent(streakDays)
            ? title.Value
            : title.Store(streakDays, Loc.T(L.Coin.StreakDays, NumberText.Group(streakDays)));
    }

    private static void DrawWeek(ImDrawListPtr drawList, CoinWalletDto wallet, Vector4 accent, AppSkin ui,
        float left, float right, float centerY, float scale)
    {
        var cycle = wallet.StreakDays % WeekDays;
        var filled = cycle == 0 && wallet.StreakDays > 0 ? WeekDays : cycle;
        var pending = wallet.CheckInAvailable && filled < WeekDays ? filled : -1;
        var radius = DotRadius * scale;
        var first = left + radius;
        var last = right - radius;
        var step = (last - first) / (WeekDays - 1);
        var restTrack = ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, TrackAlpha));
        var filledTrack = ImGui.GetColorU32(Palette.WithAlpha(accent, FilledTrackAlpha));
        for (var dayIndex = 0; dayIndex < WeekDays; dayIndex++)
        {
            var center = new Vector2(first + step * dayIndex, centerY);
            if (dayIndex < WeekDays - 1)
            {
                drawList.AddLine(new Vector2(center.X + radius, centerY), new Vector2(center.X + step - radius, centerY),
                    dayIndex + 1 < filled ? filledTrack : restTrack, TrackThickness * scale);
            }

            if (dayIndex < filled)
            {
                drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(accent), 32);
                ProgressRing.CenterIcon(drawList, center, FontAwesomeIcon.Check, CoinArt.White, DotGlyph * scale);
                continue;
            }

            if (dayIndex == pending)
            {
                var breath = BreathBase + BreathRange * Pulse.Wave(Pulse.Breath);
                drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.WithAlpha(accent, PendingWashAlpha)),
                    32);
                drawList.AddCircle(center, radius - RingStroke * scale * 0.5f,
                    ImGui.GetColorU32(Palette.WithAlpha(accent, breath)), 32, RingStroke * scale);
                continue;
            }

            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, RestAlpha)),
                32);
        }
    }
}
