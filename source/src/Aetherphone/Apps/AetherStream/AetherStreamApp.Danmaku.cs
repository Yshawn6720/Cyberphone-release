using Aetherphone.Apps.Music.Components;
using Aetherphone.Apps.Music.Radio.Live;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Video;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.AetherStream;

internal sealed partial class AetherStreamApp
{
    private readonly SoftWrapEditor danmakuEditor = new();
    private readonly RadioCountLabel danmakuCounter = new();
    private readonly RadioFittedText danmakuLockFit = new();

    private void DrawDanmakuComposer(float scale)
    {
        if (!ReactionsAvailable || !watchAlong.ServerSupportsDanmaku)
        {
            return;
        }

        Gap(Metrics.Space.Sm);
        var open = watchAlong.RoomDanmakuEnabled;
        var growth = open ? danmakuEditor.Growth(LiveChatTranscript.ComposerMaxLines) : 0f;
        var bar = BeginBlock(LiveChatTranscript.ComposerBaseHeight * scale + growth);
        var label = Loc.T(L.AetherStream.PartySettingsDanmaku);
        if (open)
        {
            var send = LiveChatTranscript.DrawComposerField(ui, bar, bar.Min.X, "##aetherstreamDanmakuComposer", label,
                danmakuEditor, PartyDanmaku.MaxTextLength, watchAlong.CanSendDanmaku, danmakuCounter, scale);
            if (send && watchAlong.SendDanmaku(danmakuEditor.Text, screenTabsShown ? watchScreen : null)
                == DanmakuRefusal.None)
            {
                danmakuEditor.Adopt(string.Empty);
                UiFeedback.Play(UiSound.MessageSent);
            }
        }
        else
        {
            var inset = LiveChatTranscript.ComposerFieldInsetY * scale;
            LiveChatTranscript.DrawComposerLock(ui, ImGui.GetWindowDrawList(),
                new Rect(new Vector2(bar.Min.X, bar.Min.Y + inset), new Vector2(bar.Max.X, bar.Max.Y - inset)), label,
                danmakuLockFit, scale);
        }

        EndBlock();
        DrawDanmakuNote(Loc.T(open ? L.AetherStream.DanmakuComposerHint : L.AetherStream.DanmakuOffByHost), scale);
    }

    private void DrawDanmakuNote(string text, float scale)
    {
        var width = ScrollLayout.StableContentWidth() - PadX * 2f * scale;
        var height = Typography.MeasureWrappedBlock(text, TextStyles.Footnote, width).Y;
        var block = BeginBlock(height);
        Typography.DrawWrappedLeft(block.Min, text, Ink.MutedInk, TextStyles.Footnote, width);
        EndBlock();
    }
}
