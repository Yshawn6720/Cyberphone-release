using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;

namespace Aetherphone.Core.Honorific;

internal sealed class HonorificBridge : IDisposable
{
    public const string ProjectUrl = "https://github.com/Caraxi/Honorific";
    private const string InternalName = "Honorific";
    private const uint SupportedMajorVersion = 3;
    private const int LocalPlayerIndex = 0;

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICallGateSubscriber<(uint, uint)> apiVersionGate;
    private readonly ICallGateSubscriber<int, string, object> setTitleGate;
    private readonly ICallGateSubscriber<int, object> clearTitleGate;
    private readonly ICallGateSubscriber<string> localTitleGate;
    private readonly ICallGateSubscriber<object> readyGate;
    private readonly ICallGateSubscriber<object> disposingGate;
    private volatile bool readySignaled;
    private volatile bool disposingSignaled;

    public HonorificBridge(IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;
        apiVersionGate = pluginInterface.GetIpcSubscriber<(uint, uint)>($"{InternalName}.ApiVersion");
        setTitleGate = pluginInterface.GetIpcSubscriber<int, string, object>($"{InternalName}.SetCharacterTitle");
        clearTitleGate = pluginInterface.GetIpcSubscriber<int, object>($"{InternalName}.ClearCharacterTitle");
        localTitleGate = pluginInterface.GetIpcSubscriber<string>($"{InternalName}.GetLocalCharacterTitle");
        readyGate = pluginInterface.GetIpcSubscriber<object>($"{InternalName}.Ready");
        disposingGate = pluginInterface.GetIpcSubscriber<object>($"{InternalName}.Disposing");
        readyGate.Subscribe(OnReady);
        disposingGate.Subscribe(OnDisposing);
    }

    public bool ConsumeReady()
    {
        if (!readySignaled)
        {
            return false;
        }

        readySignaled = false;
        return true;
    }

    public bool ConsumeDisposing()
    {
        if (!disposingSignaled)
        {
            return false;
        }

        disposingSignaled = false;
        return true;
    }

    public bool Probe()
    {
        if (!IsLoaded())
        {
            return false;
        }

        try
        {
            var (major, _) = apiVersionGate.InvokeFunc();
            return major == SupportedMajorVersion;
        }
        catch (Exception exception)
        {
            AepLog.Warning($"Honorific API version check failed: {exception.Message}");
            return false;
        }
    }

    public bool TrySetLocalTitle(string titleJson)
    {
        try
        {
            setTitleGate.InvokeAction(LocalPlayerIndex, titleJson);
            return true;
        }
        catch (Exception exception)
        {
            AepLog.Warning($"Honorific SetCharacterTitle failed: {exception.Message}");
            return false;
        }
    }

    public bool TryClearLocalTitle()
    {
        try
        {
            clearTitleGate.InvokeAction(LocalPlayerIndex);
            return true;
        }
        catch (Exception exception)
        {
            AepLog.Warning($"Honorific ClearCharacterTitle failed: {exception.Message}");
            return false;
        }
    }

    public bool TryGetLocalTitle(out string titleJson)
    {
        try
        {
            titleJson = localTitleGate.InvokeFunc() ?? string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            AepLog.Warning($"Honorific GetLocalCharacterTitle failed: {exception.Message}");
            titleJson = string.Empty;
            return false;
        }
    }

    public void Dispose()
    {
        readyGate.Unsubscribe(OnReady);
        disposingGate.Unsubscribe(OnDisposing);
    }

    private bool IsLoaded()
    {
        foreach (var plugin in pluginInterface.InstalledPlugins)
        {
            if (string.Equals(plugin.InternalName, InternalName, StringComparison.Ordinal) && plugin.IsLoaded)
            {
                return true;
            }
        }

        return false;
    }

    private void OnReady() => readySignaled = true;

    private void OnDisposing() => disposingSignaled = true;
}
