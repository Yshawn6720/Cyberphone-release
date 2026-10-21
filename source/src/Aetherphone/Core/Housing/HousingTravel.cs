using Aetherphone.Core.Localization;
using Aetherphone.Core.Venues;

namespace Aetherphone.Core.Housing;

internal static class HousingTravel
{
    private const uint LimsaAetheryte = 8u;
    private const uint GridaniaAetheryte = 2u;
    private const uint UldahAetheryte = 9u;
    private const uint FoundationAetheryte = 70u;
    private const uint KuganeAetheryte = 111u;

    public static LifestreamOutcome Go(in HousingPlotKey key)
    {
        var city = CityAetheryte(key.DistrictId);
        return city == 0u
            ? LifestreamOutcome.CannotTeleportNow
            : LifestreamBridge.TravelToHousingPlot(key.WorldId, city, key.Ward, key.Plot);
    }

    public static string Command(in HousingPlotKey key, string worldName) =>
        LifestreamBridge.HousingCommand(worldName, HousingDistricts.Name(key.DistrictId), key.Ward, key.Plot);

    public static LocString Message(LifestreamOutcome outcome) => outcome switch
    {
        LifestreamOutcome.Busy => L.Housing.TravelBusy,
        LifestreamOutcome.NotAttuned => L.Housing.TravelNotAttuned,
        _ => L.Housing.TravelUnavailable,
    };

    private static uint CityAetheryte(uint districtId) => districtId switch
    {
        HousingDistricts.MistId => LimsaAetheryte,
        HousingDistricts.LavenderBedsId => GridaniaAetheryte,
        HousingDistricts.GobletId => UldahAetheryte,
        HousingDistricts.ShiroganeId => KuganeAetheryte,
        HousingDistricts.EmpyreumId => FoundationAetheryte,
        _ => 0u,
    };
}
