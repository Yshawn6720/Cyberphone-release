using Dalamud.Bindings.ImGui;
using ManagedDoom;
using ManagedDoom.UserInput;

namespace Aetherphone.Apps.Games.Doom;

internal enum DoomAction : byte
{
    Forward,
    Backward,
    StrafeLeft,
    StrafeRight,
    TurnLeft,
    TurnRight,
    Fire,
    Use,
    Count,
}

internal sealed class DoomInput : IUserInput
{
    private const int WeaponCount = 7;
    private const int MaxMouseSensitivityLevel = 15;
    private const float MouseTurnScale = 0.5f;
    private const int MouseTurnUnits = 0x8;
    private const int MaxPendingReleases = 8;
    private static readonly ImGuiKey[] EventKeys =
    {
        ImGuiKey.UpArrow, ImGuiKey.DownArrow, ImGuiKey.LeftArrow, ImGuiKey.RightArrow, ImGuiKey.Enter,
        ImGuiKey.KeypadEnter, ImGuiKey.Escape, ImGuiKey.Tab, ImGuiKey.Backspace, ImGuiKey.Y, ImGuiKey.N,
        ImGuiKey.Key1, ImGuiKey.Key2, ImGuiKey.Key3, ImGuiKey.Key4, ImGuiKey.Key5, ImGuiKey.Key6, ImGuiKey.Key7,
    };
    private static readonly DoomKey[] EventDoomKeys =
    {
        DoomKey.Up, DoomKey.Down, DoomKey.Left, DoomKey.Right, DoomKey.Enter,
        DoomKey.Enter, DoomKey.Escape, DoomKey.Tab, DoomKey.Backspace, DoomKey.Y, DoomKey.N,
        DoomKey.Num1, DoomKey.Num2, DoomKey.Num3, DoomKey.Num4, DoomKey.Num5, DoomKey.Num6, DoomKey.Num7,
    };

    private readonly Config config;
    private readonly bool[] held = new bool[(int)DoomAction.Count];
    private readonly bool[] weaponHeld = new bool[WeaponCount];
    private readonly bool[] keyWasDown = new bool[EventKeys.Length];
    private readonly DoomKey[] pendingReleases = new DoomKey[MaxPendingReleases];
    private int pendingReleaseCount;
    private int turnHeld;
    private float pendingTurn;

    public DoomInput(Config config)
    {
        this.config = config;
    }

    public void SetHeld(DoomAction action, bool value)
    {
        held[(int)action] = value;
    }

    public void SetWeapon(int index, bool value)
    {
        if (index >= 0 && index < WeaponCount)
        {
            weaponHeld[index] = value;
        }
    }

    public void AddTurn(float pixels)
    {
        pendingTurn += pixels;
    }

    public void PumpEvents(ManagedDoom.Doom doom, bool keyboardActive)
    {
        for (var index = 0; index < pendingReleaseCount; index++)
        {
            doom.PostEvent(new DoomEvent(EventType.KeyUp, pendingReleases[index]));
        }

        pendingReleaseCount = 0;
        for (var index = 0; index < EventKeys.Length; index++)
        {
            var down = keyboardActive && ImGui.IsKeyDown(EventKeys[index]);
            if (down == keyWasDown[index])
            {
                continue;
            }

            keyWasDown[index] = down;
            doom.PostEvent(new DoomEvent(down ? EventType.KeyDown : EventType.KeyUp, EventDoomKeys[index]));
        }
    }

    public void Tap(ManagedDoom.Doom doom, DoomKey key)
    {
        doom.PostEvent(new DoomEvent(EventType.KeyDown, key));
        if (pendingReleaseCount < MaxPendingReleases)
        {
            pendingReleases[pendingReleaseCount++] = key;
        }
    }

    public void ReleaseAll(ManagedDoom.Doom doom)
    {
        for (var index = 0; index < EventKeys.Length; index++)
        {
            if (!keyWasDown[index])
            {
                continue;
            }

            keyWasDown[index] = false;
            doom.PostEvent(new DoomEvent(EventType.KeyUp, EventDoomKeys[index]));
        }

        Array.Clear(held);
        Array.Clear(weaponHeld);
        pendingTurn = 0f;
    }

    public void BuildTicCmd(TicCmd cmd)
    {
        cmd.Clear();
        var speed = config.game_alwaysrun ? 1 : 0;
        var forward = 0;
        var side = 0;
        var turnLeft = held[(int)DoomAction.TurnLeft];
        var turnRight = held[(int)DoomAction.TurnRight];
        turnHeld = turnLeft || turnRight ? turnHeld + 1 : 0;
        var turnSpeed = turnHeld < PlayerBehavior.SlowTurnTics ? 2 : speed;
        if (turnRight)
        {
            cmd.AngleTurn -= (short)PlayerBehavior.AngleTurn[turnSpeed];
        }

        if (turnLeft)
        {
            cmd.AngleTurn += (short)PlayerBehavior.AngleTurn[turnSpeed];
        }

        if (held[(int)DoomAction.Forward])
        {
            forward += PlayerBehavior.ForwardMove[speed];
        }

        if (held[(int)DoomAction.Backward])
        {
            forward -= PlayerBehavior.ForwardMove[speed];
        }

        if (held[(int)DoomAction.StrafeLeft])
        {
            side -= PlayerBehavior.SideMove[speed];
        }

        if (held[(int)DoomAction.StrafeRight])
        {
            side += PlayerBehavior.SideMove[speed];
        }

        if (held[(int)DoomAction.Fire])
        {
            cmd.Buttons |= TicCmdButtons.Attack;
        }

        if (held[(int)DoomAction.Use])
        {
            cmd.Buttons |= TicCmdButtons.Use;
        }

        for (var index = 0; index < WeaponCount; index++)
        {
            if (!weaponHeld[index])
            {
                continue;
            }

            cmd.Buttons |= TicCmdButtons.Change;
            cmd.Buttons |= (byte)(index << TicCmdButtons.WeaponShift);
            break;
        }

        var mouseTurn = (int)MathF.Round(MouseTurnScale * config.mouse_sensitivity * pendingTurn);
        pendingTurn = 0f;
        cmd.AngleTurn -= (short)(mouseTurn * MouseTurnUnits);
        forward = Math.Clamp(forward, -PlayerBehavior.MaxMove, PlayerBehavior.MaxMove);
        side = Math.Clamp(side, -PlayerBehavior.MaxMove, PlayerBehavior.MaxMove);
        cmd.ForwardMove += (sbyte)forward;
        cmd.SideMove += (sbyte)side;
    }

    public void Reset()
    {
        pendingTurn = 0f;
    }

    public void GrabMouse()
    {
    }

    public void ReleaseMouse()
    {
    }

    public int MaxMouseSensitivity => MaxMouseSensitivityLevel;

    public int MouseSensitivity
    {
        get => config.mouse_sensitivity;
        set => config.mouse_sensitivity = value;
    }
}
