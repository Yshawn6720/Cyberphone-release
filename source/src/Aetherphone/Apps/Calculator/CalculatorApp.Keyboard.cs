using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;

namespace Aetherphone.Apps.Calculator;

internal sealed partial class CalculatorApp
{
    private const char FirstPrintable = ' ';
    private static readonly CalcKey[] DigitKeys =
    {
        CalcKey.Zero, CalcKey.One, CalcKey.Two, CalcKey.Three, CalcKey.Four, CalcKey.Five, CalcKey.Six,
        CalcKey.Seven, CalcKey.Eight, CalcKey.Nine,
    };

    private static readonly VirtualKey[] KeyboardKeys =
    {
        VirtualKey.KEY_0, VirtualKey.KEY_1, VirtualKey.KEY_2, VirtualKey.KEY_3, VirtualKey.KEY_4,
        VirtualKey.KEY_5, VirtualKey.KEY_6, VirtualKey.KEY_7, VirtualKey.KEY_8, VirtualKey.KEY_9,
        VirtualKey.NUMPAD0, VirtualKey.NUMPAD1, VirtualKey.NUMPAD2, VirtualKey.NUMPAD3, VirtualKey.NUMPAD4,
        VirtualKey.NUMPAD5, VirtualKey.NUMPAD6, VirtualKey.NUMPAD7, VirtualKey.NUMPAD8, VirtualKey.NUMPAD9,
        VirtualKey.MULTIPLY, VirtualKey.ADD, VirtualKey.SUBTRACT, VirtualKey.DECIMAL, VirtualKey.DIVIDE,
        VirtualKey.OEM_PLUS, VirtualKey.OEM_MINUS, VirtualKey.OEM_PERIOD, VirtualKey.OEM_COMMA, VirtualKey.OEM_2,
        VirtualKey.X, VirtualKey.BACK, VirtualKey.DELETE, VirtualKey.RETURN, VirtualKey.ESCAPE,
    };

    private static readonly VirtualKey[] ClipboardKeys = [.. KeyboardKeys, VirtualKey.C, VirtualKey.V];

    private void HandleKeyboard(Rect screen)
    {
        var io = ImGui.GetIO();
        var claimedKeys = io.KeyCtrl ? ClipboardKeys : KeyboardKeys;
        if (!UiInteract.Hover(screen.Min, screen.Max) || !GameInput.Claim(claimedKeys))
        {
            return;
        }

        if (io.KeyCtrl)
        {
            if (ImGui.IsKeyPressed(ImGuiKey.C, false))
            {
                CopyResult();
            }
            else if (ImGui.IsKeyPressed(ImGuiKey.V, false))
            {
                PasteFromClipboard();
            }

            return;
        }

        if (ImGui.IsKeyPressed(ImGuiKey.Escape, false))
        {
            ClearFromKeyboard();
            return;
        }

        if (ImGui.IsKeyPressed(ImGuiKey.Backspace) || ImGui.IsKeyPressed(ImGuiKey.Delete))
        {
            if (engine.CanBackspace)
            {
                Press(CalcKey.Clear, true);
            }

            return;
        }

        if (ImGui.IsKeyPressed(ImGuiKey.Enter, false) || ImGui.IsKeyPressed(ImGuiKey.KeypadEnter, false))
        {
            Press(CalcKey.Equals, true);
            return;
        }

        var queue = io.InputQueueCharacters;
        for (var index = 0; index < queue.Size; index++)
        {
            if (TryKeyFor((char)queue[index], out var key))
            {
                Press(key, true);
            }
        }
    }

    private void ClearFromKeyboard()
    {
        if (menuOpen)
        {
            menuOpen = false;
            return;
        }

        keyPress[(int)CalcKey.Clear].SnapTo(1f);
        engine.AllClear();
        Play(UiSound.KeystrokeDelete);
    }

    private static bool TryKeyFor(char character, out CalcKey key)
    {
        key = CalcKey.Equals;
        if (character < FirstPrintable)
        {
            return false;
        }

        if (char.IsAsciiDigit(character))
        {
            key = DigitKeys[character - '0'];
            return true;
        }

        switch (character)
        {
            case '+':
                key = CalcKey.Add;
                return true;
            case '-':
                key = CalcKey.Subtract;
                return true;
            case '*':
            case 'x':
            case 'X':
            case '×':
                key = CalcKey.Multiply;
                return true;
            case '/':
            case '÷':
                key = CalcKey.Divide;
                return true;
            case '=':
                key = CalcKey.Equals;
                return true;
            case '%':
                key = CalcKey.Percent;
                return true;
            case '.':
            case ',':
                key = CalcKey.Decimal;
                return true;
            default:
                return false;
        }
    }
}
