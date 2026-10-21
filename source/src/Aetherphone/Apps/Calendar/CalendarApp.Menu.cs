using Aetherphone.Core;
using Aetherphone.Core.Calendar;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Calendar;

internal enum CalendarMenu : byte
{
    Repeat,
    Group,
    Alert,
}

internal sealed partial class CalendarApp
{
    private const string MenuId = "calendar.editor.menu";

    private readonly DropdownMenu menu = new();
    private readonly DropdownMenu.Item[] repeatItems = new DropdownMenu.Item[CalendarReminder.RepeatOptionCount];
    private readonly DropdownMenu.Item[] alertItems =
        new DropdownMenu.Item[CalendarReminder.LeadOptionsMinutes.Length];
    private DropdownMenu.Item[] groupItems = Array.Empty<DropdownMenu.Item>();
    private CalendarMenu openMenu;

    private void OpenMenu(CalendarMenu kind, Rect row)
    {
        UiFeedback.Play(UiSound.Tap);
        openMenu = kind;
        menu.Header = string.Empty;
        menu.Toggle(MenuId, row);
    }

    private void DrawMenu()
    {
        if (!menu.Open)
        {
            return;
        }

        if (router.Current != CalendarScreen.EditEvent)
        {
            menu.Close();
            return;
        }

        switch (openMenu)
        {
            case CalendarMenu.Repeat:
                DrawRepeatMenu();
                return;
            case CalendarMenu.Group:
                DrawGroupMenu();
                return;
            default:
                DrawAlertMenu();
                return;
        }
    }

    private void DrawRepeatMenu()
    {
        for (var index = 0; index < repeatItems.Length; index++)
        {
            var repeat = (CalendarRepeat)index;
            repeatItems[index] = new DropdownMenu.Item(Loc.T(CalendarReminder.RepeatLabel(repeat)),
                Selected: repeat == editRepeat);
        }

        var picked = menu.Draw(frameScreen, theme, repeatItems);
        if (picked < 0)
        {
            return;
        }

        UiFeedback.Play(UiSound.Tap);
        editRepeat = (CalendarRepeat)picked;
    }

    private void DrawAlertMenu()
    {
        for (var index = 0; index < alertItems.Length; index++)
        {
            alertItems[index] = new DropdownMenu.Item(Loc.T(CalendarReminder.LeadLabelAt(index)),
                Selected: index == editLeadIndex);
        }

        var picked = menu.Draw(frameScreen, theme, alertItems);
        if (picked < 0)
        {
            return;
        }

        UiFeedback.Play(UiSound.Tap);
        editLeadIndex = picked;
    }

    private void DrawGroupMenu()
    {
        var groups = configuration.CalendarGroups;
        var count = groups.Count + 1;
        if (groupItems.Length < count)
        {
            groupItems = new DropdownMenu.Item[count];
        }

        groupItems[0] = new DropdownMenu.Item(Loc.T(L.Calendar.NoGroup), Selected: editGroupIndex == 0);
        for (var index = 0; index < groups.Count; index++)
        {
            groupItems[index + 1] = new DropdownMenu.Item(groups[index].Name, Selected: editGroupIndex == index + 1);
        }

        var picked = menu.Draw(frameScreen, theme, groupItems.AsSpan(0, count));
        if (picked < 0)
        {
            return;
        }

        UiFeedback.Play(UiSound.Tap);
        editGroupIndex = picked;
    }
}
