namespace Aetherphone.Core.Apps;

internal interface IResumableApp : IPhoneApp
{
    void OnResumed();
}
