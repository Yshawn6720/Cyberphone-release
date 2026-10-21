namespace Aetherphone.Core.Clock;

[Serializable]
internal sealed class AlarmEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int Hour { get; set; }
    public int Minute { get; set; }
    public byte RepeatDays { get; set; }
    public bool Enabled { get; set; } = true;
    public string Label { get; set; } = string.Empty;
    public long LastFiredEpochMinute { get; set; }
    public bool Eorzea { get; set; }
    public int SnoozeMinutes { get; set; } = AlarmSchedule.DefaultSnoozeMinutes;

    public bool Repeats => RepeatDays != 0;

    public int MinuteOfDay => Hour * 60 + Minute;

    public TimeSpan SnoozeLength => TimeSpan.FromMinutes(Math.Clamp(SnoozeMinutes, 0, AlarmSchedule.MaxSnoozeMinutes));

    public bool RepeatsOn(DayOfWeek day) => (RepeatDays & (1 << (int)day)) != 0;

    public void ToggleDay(DayOfWeek day)
    {
        var mask = (byte)(1 << (int)day);
        RepeatDays = (byte)(RepeatDays ^ mask);
    }
}
