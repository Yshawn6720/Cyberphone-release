using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Calculator;

internal enum CalcKey : byte
{
    Clear,
    Negate,
    Percent,
    Divide,
    Seven,
    Eight,
    Nine,
    Multiply,
    Four,
    Five,
    Six,
    Subtract,
    One,
    Two,
    Three,
    Add,
    Zero,
    Decimal,
    Equals,
}

internal sealed partial class CalculatorApp
{
    private const int KeyCount = 19;
    private const int OperatorColumn = 3;
    private const float HoverLighten = 0.08f;
    private const float PressLighten = 0.30f;
    private static readonly byte[] KeyRows = { 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4 };
    private static readonly byte[] KeyColumns = { 0, 1, 2, 3, 0, 1, 2, 3, 0, 1, 2, 3, 0, 1, 2, 3, 0, 2, 3 };
    private static readonly sbyte[] KeyDigits = { -1, -1, -1, -1, 7, 8, 9, -1, 4, 5, 6, -1, 1, 2, 3, -1, 0, -1, -1 };
    private static readonly string[] DigitLabels = { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" };
    private static readonly Vector4 DigitFill = new(0.200f, 0.200f, 0.212f, 1f);
    private static readonly Vector4 FunctionFill = new(0.361f, 0.361f, 0.376f, 1f);
    private static readonly TextStyle DigitStyle = new(1.90f, FontWeight.Regular);
    private static readonly TextStyle FunctionStyle = new(1.45f, FontWeight.Medium);
    private const string AllClearLabel = "AC";
    private const string PercentLabel = "%";

    private readonly Spring[] keyPress = new Spring[KeyCount];

    private void DrawKeypad(Rect keypad, float button, float gap, float scale)
    {
        UiAnchors.Report("calculator.keypad", keypad);
        var drawList = ImGui.GetWindowDrawList();
        var active = engine.ActiveOperator;
        for (var keyIndex = 0; keyIndex < KeyCount; keyIndex++)
        {
            var key = (CalcKey)keyIndex;
            var rect = KeyRect(keypad, key, button, gap);
            if (DrawKey(drawList, key, rect, active, scale))
            {
                Press(key, false);
            }
        }
    }

    private static Rect KeyRect(Rect keypad, CalcKey key, float button, float gap)
    {
        var index = (int)key;
        var left = keypad.Min.X + KeyColumns[index] * (button + gap);
        var top = keypad.Min.Y + KeyRows[index] * (button + gap);
        var width = key == CalcKey.Zero ? button * 2f + gap : button;
        return new Rect(new Vector2(left, top), new Vector2(left + width, top + button));
    }

    private bool DrawKey(ImDrawListPtr drawList, CalcKey key, Rect rect, CalcOp active, float scale)
    {
        var index = (int)key;
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var down = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        ref var spring = ref keyPress[index];
        if (down)
        {
            spring.Step(1f, Motion.PressIn, delta);
        }
        else
        {
            spring.Step(0f, Motion.Release, delta);
        }

        var press = Math.Clamp(spring.Value, 0f, 1f);
        var grow = 1f - (1f - PressFx.ControlPressedScale) * press;
        var half = rect.Size * 0.5f * grow;
        var center = rect.Center;
        var face = new Rect(center - half, center + half);
        var op = OperatorFor(key);
        var highlighted = op != CalcOp.None && op == active;
        KeyColors(key, highlighted, out var fill, out var ink);
        var lift = (hovered ? HoverLighten : 0f) + PressLighten * press;
        Squircle.Fill(drawList, face.Min, face.Max, face.Height * 0.5f,
            ImGui.GetColorU32(Palette.Mix(fill, White, lift)));
        DrawKeyLabel(drawList, key, face, ink, grow, scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(rect.Min, rect.Max, hovered, false);
    }

    private void KeyColors(CalcKey key, bool highlighted, out Vector4 fill, out Vector4 ink)
    {
        var index = (int)key;
        if (KeyColumns[index] == OperatorColumn)
        {
            fill = highlighted ? White : ui.Accent;
            ink = highlighted ? ui.Accent : White;
            return;
        }

        fill = KeyRows[index] == 0 ? FunctionFill : DigitFill;
        ink = White;
    }

    private void DrawKeyLabel(ImDrawListPtr drawList, CalcKey key, Rect face, Vector4 ink, float grow, float scale)
    {
        var digit = KeyDigits[(int)key];
        if (digit >= 0)
        {
            var center = key == CalcKey.Zero ? new Vector2(face.Min.X + face.Height * 0.5f, face.Center.Y) : face.Center;
            Typography.DrawCentered(drawList, center, DigitLabels[digit], ink, DigitStyle);
            return;
        }

        var glyph = face.Height * CalculatorGlyphs.SizeFraction;
        switch (key)
        {
            case CalcKey.Clear:
                if (engine.CanBackspace)
                {
                    CalculatorGlyphs.Backspace(drawList, face.Center, glyph, ink, scale * grow);
                }
                else
                {
                    Typography.DrawCentered(drawList, face.Center, AllClearLabel, ink, FunctionStyle);
                }

                return;
            case CalcKey.Negate:
                CalculatorGlyphs.PlusMinus(drawList, face.Center, glyph, ink, scale * grow);
                return;
            case CalcKey.Percent:
                Typography.DrawCentered(drawList, face.Center, PercentLabel, ink, FunctionStyle);
                return;
            case CalcKey.Decimal:
                Typography.DrawCentered(drawList, face.Center, decimalLabel, ink, DigitStyle);
                return;
            case CalcKey.Equals:
                CalculatorGlyphs.EqualsSign(drawList, face.Center, glyph, ink, scale * grow);
                return;
            default:
                CalculatorGlyphs.Operator(drawList, OperatorFor(key), face.Center, glyph, ink, scale * grow);
                return;
        }
    }

    private static CalcOp OperatorFor(CalcKey key) => key switch
    {
        CalcKey.Add => CalcOp.Add,
        CalcKey.Subtract => CalcOp.Subtract,
        CalcKey.Multiply => CalcOp.Multiply,
        CalcKey.Divide => CalcOp.Divide,
        _ => CalcOp.None,
    };

    private void Press(CalcKey key, bool fromKeyboard)
    {
        if (fromKeyboard)
        {
            keyPress[(int)key].SnapTo(1f);
        }

        menuOpen = false;
        var digit = KeyDigits[(int)key];
        if (digit >= 0)
        {
            engine.InputDigit(digit);
            Play(UiSound.Keystroke);
            return;
        }

        switch (key)
        {
            case CalcKey.Clear:
                if (engine.CanBackspace)
                {
                    engine.Backspace();
                }
                else
                {
                    engine.AllClear();
                }

                Play(UiSound.KeystrokeDelete);
                return;
            case CalcKey.Negate:
                engine.Negate();
                break;
            case CalcKey.Percent:
                engine.Percent();
                break;
            case CalcKey.Decimal:
                engine.InputDecimal();
                break;
            case CalcKey.Equals:
                engine.Equals();
                break;
            default:
                engine.SetOperator(OperatorFor(key));
                break;
        }

        Play(engine.IsError ? UiSound.Blocked : UiSound.Keystroke);
    }
}
