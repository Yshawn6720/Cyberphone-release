using System.Text;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Venues;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Venues;

internal sealed partial class VenuesApp
{
    private const float HoursRowHeight = 40f;
    private const float HoursChevron = 14f;

    private readonly List<VenueHoursSlot> hoursSlots = new();
    private readonly string[] hoursDayLabels = new string[VenueHours.DayCount];
    private readonly string[] hoursDayValues = new string[VenueHours.DayCount];
    private readonly bool[] hoursDayOpen = new bool[VenueHours.DayCount];
    private readonly bool[] hoursDayClosed = new bool[VenueHours.DayCount];
    private readonly StringBuilder hoursBuilder = new();
    private bool hoursAvailable;
    private bool hoursExpanded;

    private void RebuildHours(VenueEvent venue, DateTime nowUtc)
    {
        hoursAvailable = venue.Openings.Count > 0;
        if (!hoursAvailable)
        {
            return;
        }

        VenueHours.Collect(venue.Openings, nowUtc, TimeZoneInfo.Local, hoursSlots);
        var today = VenueHours.Today(nowUtc, TimeZoneInfo.Local);
        var culture = Loc.Culture;
        for (var day = 0; day < VenueHours.DayCount; day++)
        {
            hoursDayLabels[day] = day == 0
                ? Loc.T(L.Time.Today)
                : culture.TextInfo.ToTitleCase(today.AddDays(day).ToString("dddd", culture));
            hoursDayOpen[day] = false;
            hoursBuilder.Clear();
            for (var slotIndex = 0; slotIndex < hoursSlots.Count; slotIndex++)
            {
                var slot = hoursSlots[slotIndex];
                if (slot.Day != day)
                {
                    continue;
                }

                if (hoursBuilder.Length > 0)
                {
                    hoursBuilder.Append(", ");
                }

                hoursBuilder.Append(VenueFormat.Window(slot.StartLocal, slot.EndLocal));
                hoursDayOpen[day] |= slot.Now;
            }

            hoursDayClosed[day] = hoursBuilder.Length == 0;
            hoursDayValues[day] = hoursDayClosed[day] ? Loc.T(L.Venues.Closed) : hoursBuilder.ToString();
        }
    }

    private float DrawHours(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        if (!hoursAvailable)
        {
            return top;
        }

        var cardTop = Section(drawList, left, top, width, Loc.T(L.Venues.Hours), string.Empty, out _, scale);
        var rows = hoursExpanded ? VenueHours.DayCount : 1;
        var rowHeight = HoursRowHeight * scale;
        var card = new Rect(new Vector2(left, cardTop), new Vector2(left + width, cardTop + rows * rowHeight));
        ui.Card(drawList, card.Min, card.Max, Metrics.Radius.Grouped * scale);
        var hovered = VenuesArt.RowWash(drawList, ui, card, scale);
        var inset = CardInset * scale;
        for (var day = 0; day < rows; day++)
        {
            var rowTop = card.Min.Y + day * rowHeight;
            var centerY = rowTop + rowHeight * 0.5f;
            var labelStyle = day == 0 ? TextStyles.BodyEmphasized : TextStyles.Body;
            var labelHeight = Typography.LineHeight(labelStyle);
            var label = hoursDayLabels[day];
            Typography.Draw(drawList, new Vector2(card.Min.X + inset, centerY - labelHeight * 0.5f), label,
                day == 0 ? ui.TitleInk : ui.BodyInk, labelStyle);
            var valueRight = card.Max.X - inset - (day == 0 ? HoursChevron * scale + Metrics.Space.Sm * scale : 0f);
            var labelWidth = Typography.Measure(label, labelStyle).X;
            var valueWidth = MathF.Max(1f, valueRight - card.Min.X - inset - labelWidth - VenuesArt.TextGap * scale);
            var valueInk = hoursDayOpen[day] ? VenueCard.StatusTint(VenueStatusKind.Open, ui)
                : hoursDayClosed[day] ? ui.MutedInk : ui.TitleInk;
            var value = Typography.FitText(hoursDayValues[day], valueWidth, labelStyle);
            var valueSize = Typography.Measure(value, labelStyle);
            Typography.Draw(drawList, new Vector2(valueRight - valueSize.X, centerY - labelHeight * 0.5f), value,
                valueInk, labelStyle);
            if (day == 0)
            {
                PhoneIcon.Draw(drawList, new Vector2(card.Max.X - inset - HoursChevron * scale * 0.5f, centerY),
                    hoursExpanded ? PhoneIcons.ChevronUp : PhoneIcons.ChevronDown, ui.MutedInk, HoursChevron * scale);
            }

            if (day < rows - 1)
            {
                FeedCell.Hairline(drawList, card.Min.X + inset, card.Max.X - inset, rowTop + rowHeight, ui.Hairline);
            }
        }

        if (UiInteract.Click(card.Min, card.Max, hovered))
        {
            hoursExpanded = !hoursExpanded;
            UiFeedback.Play(hoursExpanded ? UiSound.ToggleOn : UiSound.ToggleOff);
        }

        var hintTop = card.Max.Y + Metrics.Space.Xs * scale;
        return hintTop + Typography.DrawWrappedLeft(new Vector2(left, hintTop), Loc.T(L.Venues.HoursLocalHint),
            ui.MutedInk, HintStyle, width);
    }
}
