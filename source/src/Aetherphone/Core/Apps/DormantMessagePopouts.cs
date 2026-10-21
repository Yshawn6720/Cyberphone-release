using Aetherphone.Core.Message;
using Dalamud.Interface.Windowing;

namespace Aetherphone.Core.Apps;

// Stands in for MessagePopouts while the Aethernet Message app is hidden: no windows to restore from saved
// state, and it owns the inbox store the widget catalog still needs, so the store is disposed on unload.
internal sealed class DormantMessagePopouts(IDisposable inbox) : IMessagePopouts
{
    public IReadOnlyList<Window> Windows => Array.Empty<Window>();

    public Action<string>? OpenInPhone { get; set; }

    public void Restore()
    {
    }

    public void Dispose() => inbox.Dispose();
}
