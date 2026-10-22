using Aetherphone.Apps.Music.Components;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private readonly NavBarButton[] radioButtons = new NavBarButton[1];

    private void DrawRadio(in PhoneContext context)
    {
        var scale = UiScale.Current;
        var stations = Array.Empty<CommunityStationDto>();
        var relayStations = false;
        if (HiddenApps.AethernetEnabled)
        {
            community.EnsureFresh(true);
            community.EnsureMine();
            stations = community.Stations;
            ApplyTagFilter(stations);
        }
        else
        {
            relayStations = relayRadio.EnsureFresh();
        }

        var frame = BeginPage(context);
        using (AppSurface.Begin(frame.Body))
        {
            if (HiddenApps.AethernetEnabled)
            {
                SectionHeader.Draw(ui, Loc.T(L.Music.CommunityRadio), false, 0f);
                if (stations.Length == 0)
                {
                    DrawCommunityShelfStatus(scale);
                }
                else
                {
                    DrawTagFilterRail(scale, stations);
                    DrawLiveGroup(scale, LiveGroup.OnAir, Loc.T(L.Music.OnAirSection));
                    DrawLiveGroup(scale, LiveGroup.Upcoming, Loc.T(L.Music.UpNextSection));
                    DrawLiveGroup(scale, LiveGroup.Followed, Loc.T(L.Music.FollowingSection));
                    DrawLiveGroup(scale, LiveGroup.Resting, Loc.T(L.Music.AllStationsSection));
                }
            }
            else if (relayStations)
            {
                DrawRelayRadioSection(scale);
            }

            DrawLiveDjsSection(scale);
            DrawWorldRadioShelf(scale);
            DrawFavoriteRadioStationsSection(scale);
            ImGui.Dummy(new Vector2(0f, 10f * scale));
        }

        if (!HiddenApps.AethernetEnabled || !community.OwnsStation)
        {
            EndPage(in frame, context, Loc.T(L.Music.TabRadio));
            return;
        }

        radioButtons[0] = new NavBarButton(IconGlyph.Of(FontAwesomeIcon.BroadcastTower), Loc.T(L.Music.MyStation));
        if (EndPage(in frame, context, Loc.T(L.Music.TabRadio), radioButtons) == 0)
        {
            OpenMyStation();
        }
    }
}
