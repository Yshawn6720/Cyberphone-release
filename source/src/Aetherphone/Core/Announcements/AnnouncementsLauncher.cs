using Aetherphone.Core.Apps;

namespace Aetherphone.Core.Announcements;

internal sealed class AnnouncementsLauncher
{
    private readonly LaunchIntent detail = new();

    public void RequestDetail(string announcementId) => detail.Request(announcementId);

    public bool TryConsumeDetail(out string announcementId) => detail.TryConsume(out announcementId);
}
