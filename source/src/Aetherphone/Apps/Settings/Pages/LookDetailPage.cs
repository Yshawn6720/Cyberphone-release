using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class LookDetailPage : ISettingsPage
{
    private const int NameMaxLength = 40;

    public string Title => LooksPage.NameOf(look);
    public string Summary => string.Empty;
    public FontAwesomeIcon Icon => FontAwesomeIcon.Palette;
    public Vector4 Tint => new(0.55f, 0.45f, 0.95f, 1f);
    private readonly HomeLook look;
    private readonly HomeLookService looks;
    private readonly ISettingsNavigator navigator;
    private readonly ConfirmService confirm;
    private string editName;
    private bool deleted;

    public LookDetailPage(HomeLook look, HomeLookService looks, ISettingsNavigator navigator, ConfirmService confirm)
    {
        this.look = look;
        this.looks = looks;
        this.navigator = navigator;
        this.confirm = confirm;
        editName = look.Name;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        if (deleted)
        {
            deleted = false;
            navigator.Back();
            return;
        }

        var theme = context.Theme;
        using (AppSurface.Begin(body))
        {
            SettingsForm.Gap(Metrics.Space.Xs);
            DrawNameField(theme);
            SettingsForm.Gap(Metrics.Space.Lg);
            SettingsSection.Hint(Loc.T(L.Home.LookDetailHint), theme);
            if (!looks.CanDelete)
            {
                return;
            }

            SettingsForm.Gap(Metrics.Space.Lg);
            var card = GroupCard.Begin(theme, 1);
            if (SettingsRow.Action(card.NextRow(), Loc.T(L.Home.LookDelete), theme.Danger, theme))
            {
                AskDelete();
            }

            card.End();
        }
    }

    private void DrawNameField(PhoneTheme theme)
    {
        SettingsSection.Header(Loc.T(L.Home.LookName), theme);
        SettingsForm.TextField("##lookName", string.Empty, ref editName, theme, NameMaxLength,
            ImGuiInputTextFlags.None, out var active);
        if (active || string.Equals(editName, look.Name, StringComparison.Ordinal))
        {
            return;
        }

        looks.Rename(look, editName);
        editName = look.Name;
    }

    private void AskDelete()
    {
        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Home.LookDelete),
            Message = Loc.T(L.Home.LookDeleteMessage, LooksPage.NameOf(look)),
            ConfirmLabel = Loc.T(L.Home.LookDeleteConfirm),
            CancelLabel = Loc.T(L.Photos.DeleteCancel),
            Danger = true,
            Confirm = DeleteLook,
        });
    }

    private void DeleteLook()
    {
        deleted = looks.Delete(look.Id);
    }
}
