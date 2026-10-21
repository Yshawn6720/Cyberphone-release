using Dalamud.Game.Gui.NamePlate;

namespace Aetherphone.Core.Game;

internal static class NamePlateStripper
{
    public static void Strip(INamePlateUpdateHandler handler)
    {
        handler.RemoveName();
        handler.RemoveTitle();
        handler.RemoveFreeCompanyTag();
        handler.RemoveLevelPrefix();
        handler.RemoveStatusPrefix();
        handler.RemoveTargetSuffix();
        handler.MarkerIconId = 0;
        handler.NameIconId = -1;
    }
}
