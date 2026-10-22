using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Telephony.Audio;
using Aetherphone.Core.Telephony.Contracts;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Linkpearl;

internal sealed partial class LinkpearlApp
{
    private const float CallRowHeight = 60f;
    private const float CallAvatarRadius = 22f;
    private const float CallMonogramScale = 0.95f;
    private const int CallAvatarSegments = 32;
    private const float CallInviteFieldHeight = 52f;
    private const float CallButtonHeight = 40f;
    private const float CallTrailingGlyph = 18f;
    private const float CallCodeReserve = 24f;
    private const float CallSpeakingFloor = 0.03f;
    private const float CallAudioHeaderHeight = 40f;
    private const int CallInviteMaxLength = 512;
    private const int MicSilentWarningSeconds = 30;

    private static readonly Vector4 CallTileInk = new(1f, 1f, 1f, 1f);

    private readonly Sheet callAudioSheet = new();
    private string callInviteDraft = string.Empty;

    private void DrawCallsTab(Rect area)
    {
        var view = calls.Snapshot();
        using (AppSurface.BeginEdgeToEdge(area))
        {
            var drawList = ImGui.GetWindowDrawList();
            if (view.IsActive)
            {
                DrawCallRoom(drawList, view);
            }
            else
            {
                DrawCallLobby(drawList);
            }
        }
    }

    private void DrawCallLobby(ImDrawListPtr drawList)
    {
        chrome.DrawSectionLabel(Loc.T(L.Linkpearl.CallRoomSection));
        var startHint = Loc.T(calls.RelayReady ? L.Linkpearl.CallStartHint : L.Linkpearl.CallNeedsServer);
        if (chrome.DrawActionRow(drawList, "linkpearl.call.start", PhoneIcons.PhonePlus, Loc.T(L.Linkpearl.CallStart),
                startHint, separator: false))
        {
            ReportCallRoom(calls.StartRoom());
        }

        DrawCallNote(Loc.T(L.Linkpearl.CallLobbyNote), ink.MutedInk);
        chrome.DrawSectionLabel(Loc.T(L.Linkpearl.CallJoinSection));
        DrawCallInvite();
        chrome.DrawSectionLabel(Loc.T(L.Phone.Audio));
        if (chrome.DrawSettingRow(drawList, PhoneIcons.Volume, ChatListChrome.TintTeal, Loc.T(L.Phone.AudioTitle),
                separator: false))
        {
            callAudioSheet.Open();
        }
    }

    private void DrawCallInvite()
    {
        var scale = UiScale.Current;
        var pad = CellPadX * scale;
        var gap = Metrics.Space.Sm * scale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var fieldHeight = CallInviteFieldHeight * scale;
        var buttonHeight = CallButtonHeight * scale;
        var field = new Rect(new Vector2(origin.X + pad, origin.Y),
            new Vector2(origin.X + width - pad, origin.Y + fieldHeight));
        var submitted = SubmitField.Draw(field, "##linkpearlCallInvite", Loc.T(L.Linkpearl.CallInviteHint),
            ref callInviteDraft, frameTheme, CallInviteMaxLength, FontAwesomeIcon.Link);
        var top = field.Max.Y + gap;
        var half = (field.Width - gap) * 0.5f;
        var paste = new Rect(new Vector2(field.Min.X, top), new Vector2(field.Min.X + half, top + buttonHeight));
        var join = new Rect(new Vector2(field.Max.X - half, top), new Vector2(field.Max.X, top + buttonHeight));
        if (ui.GhostButton(paste, Loc.T(L.Linkpearl.CallPaste)))
        {
            callInviteDraft = (ImGui.GetClipboardText() ?? string.Empty).Trim();
        }

        var filled = !string.IsNullOrWhiteSpace(callInviteDraft);
        if ((ui.AccentPill(join, Loc.T(L.Linkpearl.CallJoin), filled, TextStyles.Headline) || submitted) && filled)
        {
            JoinCallRoom();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, fieldHeight + gap + buttonHeight + gap));
    }

    private void JoinCallRoom()
    {
        var outcome = calls.JoinRoom(callInviteDraft);
        if (outcome == CallRoomStart.Started)
        {
            callInviteDraft = string.Empty;
        }

        ReportCallRoom(outcome);
    }

    private static void ReportCallRoom(CallRoomStart outcome)
    {
        switch (outcome)
        {
            case CallRoomStart.NotReady:
                ShellToast.Show(Loc.T(L.Linkpearl.CallNotReady));
                return;
            case CallRoomStart.InvalidInvite:
                ShellToast.Show(Loc.T(L.Linkpearl.CallInvalidInvite));
                return;
            case CallRoomStart.Busy:
                ShellToast.Show(Loc.T(L.Linkpearl.CallBusy));
                return;
        }
    }

    private void DrawCallRoom(ImDrawListPtr drawList, in CallView view)
    {
        chrome.DrawSectionLabel(Loc.T(L.Phone.RoomTitle));
        DrawCallStatusRow(drawList, view);
        DrawCallRoomNote(view);
        DrawCallRequests(drawList);
        chrome.DrawSectionLabel(Loc.T(L.Linkpearl.CallPeople));
        var participants = view.Participants;
        var shown = 0;
        for (var index = 0; index < participants.Length; index++)
        {
            if (participants[index].State == ParticipantState.Active)
            {
                shown++;
            }
        }

        var drawn = 0;
        for (var index = 0; index < participants.Length; index++)
        {
            var participant = participants[index];
            if (participant.State != ParticipantState.Active)
            {
                continue;
            }

            drawn++;
            DrawCallParticipant(drawList, participant, view, drawn < shown);
        }

        chrome.DrawSectionLabel(Loc.T(L.Linkpearl.CallControls));
        var muted = chrome.DrawSwitchRow(drawList, PhoneIcons.MicrophoneOff, ChatListChrome.TintRed,
            Loc.T(L.Linkpearl.CallMute), view.Muted, "linkpearl.call.mute");
        if (muted != view.Muted)
        {
            calls.ToggleMute();
        }

        if (chrome.DrawSettingRow(drawList, PhoneIcons.Volume, ChatListChrome.TintTeal, Loc.T(L.Phone.AudioTitle)))
        {
            callAudioSheet.Open();
        }

        if (calls.RoomCode.Length > 0
            && chrome.DrawSettingRow(drawList, PhoneIcons.Copy, ChatListChrome.TintAzure,
                Loc.T(L.Linkpearl.CallCopyInvite), chevron: false))
        {
            ImGui.SetClipboardText(calls.ShareInvite());
            ShellToast.Show();
        }

        var leaveLabel = view.State == CallState.Connecting ? L.Common.Cancel : L.Linkpearl.CallLeave;
        if (chrome.DrawDangerRow(drawList, PhoneIcons.PhoneX, Loc.T(leaveLabel), false))
        {
            calls.Hangup();
        }

        DrawCallNote(Loc.T(L.Phone.UseHeadphones), ink.FaintInk);
    }

    private void DrawCallStatusRow(ImDrawListPtr drawList, in CallView view)
    {
        var scale = UiScale.Current;
        var code = calls.RoomCode;
        var codeReserve = code.Length > 0
            ? Typography.Measure(code, ChatListChrome.RoleTagStyle).X + CallCodeReserve * scale
            : 0f;
        var row = chrome.BeginPersonRow(drawList, CallRowHeight, CallAvatarRadius, codeReserve, false,
            out var tileCenter);
        var radius = CallAvatarRadius * scale;
        var live = view.State == CallState.Active && view.Connected;
        drawList.AddCircleFilled(tileCenter, radius,
            ImGui.GetColorU32(live ? ChatListChrome.TintGreen : ink.Accent), CallAvatarSegments);
        PhoneIcon.Draw(drawList, tileCenter, PhoneIcons.Phone, CallTileInk, radius);
        chrome.DrawRowTitleAndSub(drawList, new MarqueeId("linkpearl.call.status", view.PeerLabel), view.PeerLabel,
            CallStatusText.Label(view), row.TextLeft, row.TextRight, row.Bounds.Center.Y, ink.TitleInk, ink.MutedInk);
        if (code.Length > 0)
        {
            chrome.DrawRoleTag(drawList, row.Bounds.Max.X - CellPadX * scale, row.Bounds.Center.Y, code, out _);
        }

        chrome.EndPersonRow(drawList, row, false);
    }

    private void DrawCallRoomNote(in CallView view)
    {
        if (view.AwaitingApproval)
        {
            DrawCallNote(Loc.T(L.Linkpearl.CallAwaitingNote), ink.MutedInk);
            return;
        }

        if (view.State == CallState.Dialing)
        {
            DrawCallNote(Loc.T(L.Linkpearl.CallWaitingNote), ink.MutedInk);
            return;
        }

        var micSilent = view.State == CallState.Active
            && !view.Muted
            && view.Seconds >= MicSilentWarningSeconds
            && view.PeakMicLevel < AudioCapture.GateOpenRms;
        if (micSilent)
        {
            DrawCallNote(Loc.T(L.Phone.MicNotReaching), ink.Danger);
        }
    }

    private void DrawCallRequests(ImDrawListPtr drawList)
    {
        var requests = calls.Rooms.JoinRequests;
        if (requests.Length == 0)
        {
            return;
        }

        chrome.DrawSectionLabel(Loc.T(L.Linkpearl.CallRequestsSection));
        for (var index = 0; index < requests.Length; index++)
        {
            ImGui.PushID(index);
            DrawCallRequest(drawList, requests[index], index < requests.Length - 1);
            ImGui.PopID();
        }
    }

    private void DrawCallRequest(ImDrawListPtr drawList, ParticipantInfo request, bool separator)
    {
        var scale = UiScale.Current;
        var decisionRadius = RoundButton.SmallRadius * scale;
        var gap = Metrics.Space.Sm * scale;
        var row = chrome.BeginPersonRow(drawList, CallRowHeight, CallAvatarRadius, decisionRadius * 4f + gap, false,
            out var avatarCenter);
        var radius = CallAvatarRadius * scale;
        var name = calls.DisplayNameOf(request);
        AvatarView.Draw(drawList, avatarCenter, radius, ink.Accent, Initials.Of(name), CallMonogramScale,
            lodestone.Avatar(request.Name, request.World, radius * 2f), CallAvatarSegments);
        var denyCenter = new Vector2(row.Bounds.Max.X - CellPadX * scale - decisionRadius, row.Bounds.Center.Y);
        var admitCenter = new Vector2(denyCenter.X - decisionRadius * 2f - gap, row.Bounds.Center.Y);
        chrome.DrawRowTitleAndSub(drawList, new MarqueeId("linkpearl.call.request", request.UserId), name,
            request.World, row.TextLeft, row.TextRight, row.Bounds.Center.Y, ink.TitleInk, ink.MutedInk);
        if (RoundButton.Icon(drawList, admitCenter, decisionRadius, IconGlyph.Of(FontAwesomeIcon.Check),
                ui.Ink.WithAccent(AccentRing.Green), ButtonStyle.Tinted, Loc.T(L.Linkpearl.CallAdmit)))
        {
            calls.Rooms.Approve(request.UserId);
        }

        if (RoundButton.Icon(drawList, denyCenter, decisionRadius, IconGlyph.Of(FontAwesomeIcon.Times),
                ui.Ink.WithAccent(ink.Danger), ButtonStyle.Tinted, Loc.T(L.Linkpearl.CallDeny)))
        {
            calls.Rooms.Deny(request.UserId);
        }

        chrome.EndPersonRow(drawList, row, separator);
    }

    private static float SelfLevel(in CallView view) => view.Muted ? 0f : view.MicLevel;

    private void DrawCallParticipant(ImDrawListPtr drawList, ParticipantInfo participant, in CallView view,
        bool separator)
    {
        var scale = UiScale.Current;
        var self = participant.UserId == view.LocalUserId;
        var row = chrome.BeginPersonRow(drawList, CallRowHeight, CallAvatarRadius, CallTrailingGlyph * scale, !self,
            out var avatarCenter);
        var radius = CallAvatarRadius * scale;
        var level = self ? SelfLevel(view) : calls.LevelOf(participant);
        DrawCallSpeakingHalo(drawList, avatarCenter, radius, level, scale);
        var name = calls.DisplayNameOf(participant);
        AvatarView.Draw(drawList, avatarCenter, radius, ink.Accent, Initials.Of(name), CallMonogramScale,
            lodestone.Avatar(participant.Name, participant.World, radius * 2f), CallAvatarSegments);
        var subtitle = self ? Loc.T(L.Linkpearl.CallYou) : participant.World;
        chrome.DrawRowTitleAndSub(drawList, new MarqueeId("linkpearl.call.member", participant.UserId), name,
            subtitle, row.TextLeft, row.TextRight, row.Bounds.Center.Y, ink.TitleInk, ink.MutedInk);
        var silenced = self ? view.Muted : calls.PeerMuted(participant.UserId);
        if (silenced)
        {
            var glyphCenter = new Vector2(row.Bounds.Max.X - CellPadX * scale - CallTrailingGlyph * 0.5f * scale,
                row.Bounds.Center.Y);
            ProgressRing.CenterIcon(drawList, glyphCenter,
                self ? FontAwesomeIcon.MicrophoneSlash : FontAwesomeIcon.VolumeMute, ink.Danger,
                CallTrailingGlyph * scale);
        }

        chrome.EndPersonRow(drawList, row, separator);
        if (row.Tapped && !self)
        {
            callAudioSheet.Open();
        }
    }

    private static void DrawCallSpeakingHalo(ImDrawListPtr drawList, Vector2 center, float radius, float level,
        float scale)
    {
        if (level <= CallSpeakingFloor)
        {
            return;
        }

        var spread = (2f + 6f * Math.Clamp(level * 6f, 0f, 1f)) * scale;
        drawList.AddCircle(center, radius + spread, ImGui.GetColorU32(Palette.WithAlpha(ChatListChrome.OnlineDot, 0.6f)),
            CallAvatarSegments, 2f * scale);
    }

    private void DrawCallNote(string text, Vector4 color)
    {
        var scale = UiScale.Current;
        var pad = CellPadX * scale;
        var gap = Metrics.Space.Sm * scale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var height = Typography.DrawWrappedLeft(new Vector2(origin.X + pad, origin.Y + gap), text, color,
            TextStyles.Footnote, MathF.Max(1f, width - pad * 2f));
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + gap * 2f));
    }

    private void DrawCallAudioSheet(Rect screen)
    {
        if (callAudioSheet.IsOpen && (activeTab != MessagesTab.Calls || router.Current.Screen != LinkpearlScreen.Root))
        {
            callAudioSheet.Close();
        }

        if (!callAudioSheet.CapturesPointer)
        {
            return;
        }

        using var layer = ScreenLayer.Begin("linkpearl.callAudio", screen, false);
        var frame = callAudioSheet.Begin(ImGui.GetWindowDrawList(), screen, frameTheme,
            SheetDetents.Standard(screen.Height), SheetMetrics.AppVeil);
        if (!frame.Visible)
        {
            return;
        }

        var scale = UiScale.Current;
        var content = frame.Content;
        var headerHeight = CallAudioHeaderHeight * scale;
        Typography.DrawCentered(frame.DrawList, new Vector2(content.Center.X, content.Min.Y + headerHeight * 0.5f),
            Loc.T(L.Phone.AudioTitle), frame.Ink with { W = frame.Ink.W * frame.Opacity }, TextStyles.Headline);
        var rows = new Rect(new Vector2(content.Min.X, content.Min.Y + headerHeight),
            new Vector2(content.Max.X, content.Max.Y - Metrics.Size.HomeIndicatorInset * scale));
        var view = calls.Snapshot();
        using (var surface = AppSurface.Begin(rows))
        {
            if (callAudioPanel.Draw(frameTheme, view))
            {
                surface.CancelDrag();
                callAudioSheet.YieldPointer();
            }
        }

        callAudioSheet.End(in frame);
    }

    private bool ConsumeCallScreenRequest()
    {
        if (!CallHub.RelayMode || !calls.ConsumeCallScreenRequest())
        {
            return false;
        }

        regionMenu.Close();
        if (router.Current.Screen != LinkpearlScreen.Root)
        {
            router.Reset();
        }

        activeTab = MessagesTab.Calls;
        return true;
    }
}
