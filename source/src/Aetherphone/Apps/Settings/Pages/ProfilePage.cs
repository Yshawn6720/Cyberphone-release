using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Interface;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class ProfilePage : ISettingsPage, IDisposable
{
    public string Title => Loc.T(L.Profile.Title);
    public string Summary => SocialRegion.EffectiveCode(session, gameData);
    public FontAwesomeIcon Icon => FontAwesomeIcon.IdCard;
    public Vector4 Tint => new(0.42f, 0.58f, 0.86f, 1f);
    private readonly Configuration configuration;
    private readonly AethernetSession session;
    private readonly AccountClient client;
    private readonly GameData gameData;
    private readonly CancellationTokenSource cancellation = new();
    private bool initialSynced;

    public ProfilePage(Configuration configuration, AethernetSession session, AccountClient client, GameData gameData)
    {
        this.configuration = configuration;
        this.session = session;
        this.client = client;
        this.gameData = gameData;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var scale = UiScale.Current;
        var theme = context.Theme;
        using (AppSurface.Begin(body))
        {
            if (session.IsSignedIn && session.CurrentUser is not null && !initialSynced)
            {
                initialSynced = true;
                PushTimeZone(null);
            }

            DrawRegionSection(theme);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
            SettingsSection.Header(Loc.T(L.Profile.TimeZoneSection), theme, Loc.T(L.Profile.TimeZoneHelp));
            var share = session.CurrentUser?.ShareTimeZone ?? true;
            var shareCard = GroupCard.Begin(theme, 1);
            var nextShare = SettingsRow.Bool(shareCard.NextRow(), Loc.T(L.Profile.ShareTimeZoneLabel), share, theme);
            shareCard.End();
            if (nextShare != share && session.IsSignedIn)
            {
                PushTimeZone(nextShare);
            }

            if (!session.IsSignedIn)
            {
                ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
                SettingsSection.Hint(Loc.T(L.Profile.SignInToShare), theme);
            }

            if (nextShare)
            {
                ImGui.Dummy(new Vector2(0f, 12f * scale));
                var rowCount = configuration.TimeZoneManual ? 3 : 2;
                var card = GroupCard.Begin(theme, rowCount);
                var nextManual = SettingsRow.Bool(card.NextRow(), Loc.T(L.Profile.TimeZoneManualLabel),
                    configuration.TimeZoneManual, theme);
                if (configuration.TimeZoneManual)
                {
                    DrawOffsetStepper(card.NextRow(), theme);
                }

                SettingsRow.Info(card.NextRow(), Loc.T(L.Profile.YourTimeLabel),
                    SocialTimeZone.Describe(SocialTimeZone.EffectiveOffsetMinutes(configuration)), theme);
                card.End();
                if (nextManual != configuration.TimeZoneManual)
                {
                    configuration.TimeZoneManual = nextManual;
                    if (nextManual)
                    {
                        configuration.ManualUtcOffsetMinutes = SocialTimeZone.DeviceOffsetMinutes();
                    }

                    configuration.Save();
                    PushTimeZone(null);
                }
            }
        }
    }

    private void DrawRegionSection(PhoneTheme theme)
    {
        SettingsSection.Header(Loc.T(L.Profile.RegionSection), theme, Loc.T(L.Profile.RegionHelp));
        var autoCode = SocialRegion.AutoCode(session, gameData);
        if (!session.IsSignedIn)
        {
            var infoCard = GroupCard.Begin(theme, 1);
            SettingsRow.Info(infoCard.NextRow(), Loc.T(L.Profile.RegionAutomatic), autoCode, theme);
            infoCard.End();
            return;
        }

        var manual = session.ManualRegion;
        var card = GroupCard.Begin(theme, SocialRegion.Codes.Length + 1);
        var autoLabel = $"{Loc.T(L.Profile.RegionAutomatic)}  ({autoCode})";
        if (SettingsRow.Selectable(card.NextRow(), autoLabel, manual.Length == 0, theme) && manual.Length > 0)
        {
            ChooseRegion(string.Empty);
        }

        for (var index = 0; index < SocialRegion.Codes.Length; index++)
        {
            var code = SocialRegion.Codes[index];
            var selected = string.Equals(manual, code, StringComparison.Ordinal);
            if (SettingsRow.Selectable(card.NextRow(), code, selected, theme) && !selected)
            {
                ChooseRegion(code);
            }
        }

        card.End();
    }

    private void ChooseRegion(string code)
    {
        session.SetManualRegion(code);
        RegionSync.Push(session, client, gameData, cancellation.Token);
    }

    private void DrawOffsetStepper(Rect row, PhoneTheme theme)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var buttonSize = RoundButton.SmallRadius * 2f * scale;
        var plusMin = new Vector2(row.Max.X - buttonSize, row.Center.Y - buttonSize * 0.5f);
        var minusMin = new Vector2(plusMin.X - 96f * scale, row.Center.Y - buttonSize * 0.5f);
        var label = Typography.FitText(Loc.T(L.Profile.UtcOffsetLabel), minusMin.X - 12f * scale - row.Min.X,
            TextStyles.Body);
        var labelSize = Typography.Measure(label, TextStyles.Body);
        Typography.Draw(new Vector2(row.Min.X, row.Center.Y - labelSize.Y * 0.5f), label, theme.TextStrong,
            TextStyles.Body);
        if (StepperButton(drawList, minusMin, buttonSize, "-", theme))
        {
            AdjustOffset(-SocialTimeZone.StepMinutes);
        }

        if (StepperButton(drawList, plusMin, buttonSize, "+", theme))
        {
            AdjustOffset(SocialTimeZone.StepMinutes);
        }

        var value = SocialTimeZone.FormatOffset(SocialTimeZone.EffectiveOffsetMinutes(configuration));
        Typography.DrawCentered(new Vector2((minusMin.X + buttonSize + plusMin.X) * 0.5f, row.Center.Y), value,
            theme.TextStrong, TextStyles.Headline);
    }

    private static bool StepperButton(ImDrawListPtr drawList, Vector2 min, float size, string glyph, PhoneTheme theme)
    {
        var half = size * 0.5f;
        var center = min + new Vector2(half, half);
        var clicked = RoundButton.Draw(drawList, ImGui.GetID(glyph), center, half, ControlInk.From(theme),
            ButtonStyle.Gray, true, false, out var face);
        Typography.DrawCentered(drawList, center, glyph, face.LabelInk, TextStyles.Headline);
        return clicked;
    }


    private void AdjustOffset(int deltaMinutes)
    {
        var next = Math.Clamp(configuration.ManualUtcOffsetMinutes + deltaMinutes, SocialTimeZone.MinOffsetMinutes,
            SocialTimeZone.MaxOffsetMinutes);
        if (next == configuration.ManualUtcOffsetMinutes)
        {
            return;
        }

        configuration.ManualUtcOffsetMinutes = next;
        configuration.Save();
        PushTimeZone(null);
    }

    private void PushTimeZone(bool? share)
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        var request = new UpdateTimeZoneRequest(share, SocialTimeZone.EffectiveOffsetMinutes(configuration));
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var updated = await client.UpdateTimeZoneAsync(request, token).ConfigureAwait(false);
                if (updated is not null)
                {
                    session.SetUser(updated);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "Time zone update failed");
            }
        });
    }


    public void Dispose()
    {
        cancellation.Cancel();
        cancellation.Dispose();
    }
}
