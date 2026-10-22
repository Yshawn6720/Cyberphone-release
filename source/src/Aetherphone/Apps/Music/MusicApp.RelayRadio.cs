using Aetherphone.Apps.Music.Components;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private const float RelayStationInset = 6f;

    private void DrawRelayRadioSection(float scale)
    {
        SectionHeader.Draw(ui, Loc.T(L.Music.RelayStations), false, 0f);
        var list = relayRadio.List;
        if (list.Stations.Length == 0)
        {
            DrawRelayRadioStatus(scale);
            return;
        }

        for (var index = 0; index < list.Stations.Length; index++)
        {
            DrawStationRow(scale, list.Stations, index, RelayStationInset, list.Stations[index].Tags,
                list.Homepages[index]);
        }
    }

    private void DrawRelayRadioStatus(float scale)
    {
        if (!relayRadio.Loading && relayRadio.Unsupported)
        {
            DrawRelayRadioUnsupported();
            return;
        }

        var retryable = !relayRadio.Loading && relayRadio.Failed;
        var label = relayRadio.Loading
            ? Loc.T(L.Common.Loading)
            : retryable
                ? Loc.T(L.Music.RelayStationsOffline)
                : Loc.T(L.Music.RelayStationsEmpty);
        if (DrawShelfStatusLine(scale, label, retryable))
        {
            relayRadio.Retry();
        }
    }

    private void DrawRelayRadioUnsupported()
    {
        var text = Loc.T(L.Music.RelayStationsUnsupported);
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var height = Typography.DrawWrappedLeft(origin, text, ui.MutedInk, TextStyles.Subheadline, width);
        ImGui.Dummy(new Vector2(width, height));
    }
}
