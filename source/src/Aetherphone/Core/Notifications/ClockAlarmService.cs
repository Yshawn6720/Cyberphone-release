using Aetherphone.Core.Clock;
using Aetherphone.Core.Game;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Runtime;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Notifications;

internal sealed class ClockAlarmService : IDisposable
{
    private const long TickIntervalMilliseconds = 1000;
    private static readonly Vector4 Accent = new(1.00f, 0.58f, 0.00f, 1f);
    private readonly Configuration configuration;
    private readonly FrameworkTicker ticker;
    private readonly NotificationService notifications;
    private readonly AlarmRinger ringer;

    public ClockAlarmService(Configuration configuration, IFramework framework, NotificationService notifications,
        AlarmRinger ringer, AppGate gate)
    {
        this.configuration = configuration;
        this.notifications = notifications;
        this.ringer = ringer;
        ticker = new FrameworkTicker(framework, TickIntervalMilliseconds, OnTick, gate);
    }

    public void Dispose()
    {
        ticker.Dispose();
    }

    private void OnTick()
    {
        ringer.Tick(DateTime.UtcNow);
        var dirty = CheckAlarms(DateTime.Now, DateTime.UtcNow);
        dirty |= CheckTimer(DateTime.UtcNow);
        if (dirty)
        {
            configuration.Save();
        }
    }

    private bool CheckAlarms(DateTime nowLocal, DateTime nowUtc)
    {
        var dirty = false;
        var alarms = configuration.Alarms;
        for (var index = 0; index < alarms.Count; index++)
        {
            var alarm = alarms[index];
            if (!alarm.Enabled || !AlarmSchedule.TryResolveDue(alarm, nowLocal, nowUtc, out var dueUtc, out var key))
            {
                continue;
            }

            if (alarm.LastFiredEpochMinute == key)
            {
                continue;
            }

            alarm.LastFiredEpochMinute = key;
            if (!alarm.Repeats)
            {
                alarm.Enabled = false;
            }

            dirty = true;
            var title = alarm.Label.Length > 0 ? alarm.Label : Loc.T(L.Clock.Alarm);
            var body = alarm.Eorzea
                ? Loc.T(L.Clock.EorzeaTimeOf, new EorzeaTime(alarm.Hour, alarm.Minute).Formatted)
                : TimeText.Clock(dueUtc.ToLocalTime());
            notifications.Notify(new PhoneNotification("clock", title, body, DateTime.Now, Accent)
            {
                Muted = true,
            });
            ringer.Ring(AlarmRingKind.Alarm, alarm.Label, nowUtc, alarm.SnoozeLength);
        }

        return dirty;
    }

    private bool CheckTimer(DateTime utcNow)
    {
        if (configuration.TimerEndsAtUtc is not { } end || configuration.TimerNotified || utcNow < end)
        {
            return false;
        }

        configuration.TimerNotified = true;
        var label = configuration.TimerLabel;
        notifications.Notify(new PhoneNotification("clock", label.Length > 0 ? label : Loc.T(L.Clock.TimerTitle),
            Loc.T(L.Clock.TimerFinished), DateTime.Now, Accent)
        {
            Muted = true,
        });
        ringer.Ring(AlarmRingKind.Timer, label, DateTime.UtcNow);
        return true;
    }
}
