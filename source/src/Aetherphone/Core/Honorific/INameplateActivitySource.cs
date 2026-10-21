namespace Aetherphone.Core.Honorific;

internal interface INameplateActivitySource
{
    bool TryNameplateActivity(NameplateStatus status, out NameplateValues values);
}
