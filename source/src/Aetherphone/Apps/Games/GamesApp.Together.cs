using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Games;

internal sealed partial class GamesApp
{
    private const string TogetherNavId = "games.together.nav";

    private void DrawTogether(in PhoneContext context)
    {
        var navBar = AppHeader.BeginLargeTitle(context, false);
        using (var surface = AppSurface.Begin(navBar.Body))
        {
            onlineHub.Draw(navBar.Body, surface.Pull, surface.Dragging, ui, navigation);
        }

        AppHeader.EndLargeTitle(in navBar, context, TogetherNavId, Loc.T(L.Games.OnlineTitle),
            NavBarStyle.From(ui), ReadOnlySpan<NavBarButton>.Empty);
    }
}
