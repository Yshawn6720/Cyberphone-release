using Aetherphone.Core.Game;
using Dalamud.Game.Gui.NamePlate;

namespace Aetherphone.Core.Video;

internal sealed class ScreenController : IDisposable
{
    private readonly Func<bool> hideNameplates;
    private const float NameplateHideRadius = 3.0f;

    internal MediaDependencies Dependencies { get; }
    internal VideoEngine Engine { get; }
    internal VideoEngine EngineB { get; }

    public ScreenController(Func<bool> hideNameplates)
    {
        this.hideNameplates = hideNameplates;
        Dependencies = new MediaDependencies();
        DxHandler.Initialise(Plugin.PluginInterface);
        Engine = new VideoEngine(Dependencies);
        EngineB = new VideoEngine(Dependencies, Engine);
        Plugin.NamePlateGui.OnNamePlateUpdate += OnNamePlateUpdate;
    }

    internal VideoEngine EngineFor(VideoChannel channel) => channel == VideoChannel.B ? EngineB : Engine;

    internal bool BothActive => Engine.IsActive && EngineB.IsActive;

    internal bool PlaceScreenWall(ScreenWallLayout layout)
    {
        if (!BothActive || !ScreenPainter.TryScreenWallPoses(layout, out var first, out var second))
        {
            return false;
        }

        Engine.PlaceFlat(first);
        EngineB.PlaceFlat(second);
        return true;
    }

    private void OnNamePlateUpdate(INamePlateUpdateContext context, IReadOnlyList<INamePlateUpdateHandler> handlers)
    {
        var nearA = Engine.IsActive;
        var nearB = EngineB.IsActive;
        if (!hideNameplates() || (!nearA && !nearB))
        {
            return;
        }

        foreach (var handler in handlers)
        {
            var gameObject = handler.GameObject;
            if (gameObject is null)
            {
                continue;
            }

            var position = gameObject.Position;
            if ((nearA && Vector3.Distance(position, Engine.ScreenPosition) <= NameplateHideRadius)
                || (nearB && Vector3.Distance(position, EngineB.ScreenPosition) <= NameplateHideRadius))
            {
                NamePlateStripper.Strip(handler);
                handler.VisibilityFlags = 0;
            }
        }
    }

    public void Dispose()
    {
        Plugin.NamePlateGui.OnNamePlateUpdate -= OnNamePlateUpdate;
        EngineB.Dispose();
        Engine.Dispose();
        Dependencies.Dispose();
    }
}
