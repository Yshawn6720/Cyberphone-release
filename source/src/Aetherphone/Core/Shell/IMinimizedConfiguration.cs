namespace Aetherphone.Core.Shell;

internal interface IMinimizedConfiguration
{
    MinimizedLayout? MinimizedLayout { get; set; }
    void Save();
}
