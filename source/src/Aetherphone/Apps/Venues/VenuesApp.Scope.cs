using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Venues;

internal sealed partial class VenuesApp
{
    private readonly GeoScopeScreen scopeScreen;

    private void DrawScopeScreen(in PhoneContext context, VenueRoute route)
    {
        CheckLanguage();
        var choice = scopeScreen.DrawPage(context, "venues.scope.page", Loc.T(L.Venues.ScopeTitle), route.BackTitle,
            back, CurrentWorld(), configuration.VenueScope, configuration.VenueScopeValue, true);
        if (!choice.Picked)
        {
            return;
        }

        UiFeedback.Play(UiSound.Tap);
        SetScope(choice.Kind, choice.Value);
        router.Pop();
    }
}
