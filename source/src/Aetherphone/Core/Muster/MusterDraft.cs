namespace Aetherphone.Core.Muster;

internal enum MusterDraftIssue : byte
{
    None,
    NeedDescription,
    DescriptionTooLong,
    NeedWhere,
    NeedDataCenter,
}

internal static class MusterDraft
{
    public const int DescriptionMaxLength = 200;
    public const int SpotMaxLength = 80;
    public const int MinAttendees = 2;
    public const int MaxAttendees = 200;
    public const int DefaultAttendees = 8;

    public static MusterDraftIssue Validate(string description, string spot, bool hasLocation, int dataCenterId)
    {
        if (TrimmedLength(description) == 0)
        {
            return MusterDraftIssue.NeedDescription;
        }

        if (description.Length > DescriptionMaxLength)
        {
            return MusterDraftIssue.DescriptionTooLong;
        }

        if (!hasLocation && TrimmedLength(spot) == 0)
        {
            return MusterDraftIssue.NeedWhere;
        }

        return dataCenterId == 0 ? MusterDraftIssue.NeedDataCenter : MusterDraftIssue.None;
    }

    public static int ClampAttendees(int value) => Math.Clamp(value, MinAttendees, MaxAttendees);

    public static int TrimmedLength(string value)
    {
        var start = 0;
        var end = value.Length - 1;
        while (start <= end && char.IsWhiteSpace(value[start]))
        {
            start++;
        }

        while (end >= start && char.IsWhiteSpace(value[end]))
        {
            end--;
        }

        return end - start + 1;
    }
}
