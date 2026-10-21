namespace Aetherphone.Core.Calendar;

[Serializable]
internal sealed class CalendarCustomEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public DateTime When { get; set; }
    public bool Notified { get; set; }
    public Guid GroupId { get; set; }
    public int ReminderMinutesBefore { get; set; }
    public int DurationMinutes { get; set; }
    public CalendarRepeat Repeat { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DateTime LastNotifiedOccurrence { get; set; }

    public DateTime EndOf(DateTime occurrence) => occurrence.AddMinutes(Math.Max(0, DurationMinutes));
}
