using NAudio.CoreAudioApi;

namespace Aetherphone.Core.SystemMedia;

internal sealed class AppAudioVolume : IDisposable
{
    private const long RescanMilliseconds = 5000;
    private const int MaximumMatches = 8;

    private readonly AudioSessionControl?[] matches = new AudioSessionControl?[MaximumMatches];
    private MMDeviceEnumerator? enumerator;
    private MMDevice? device;
    private int matchCount;
    private string matchedAppId = string.Empty;
    private long scannedAt = long.MinValue;

    public float Read(string appId, long now)
    {
        Refresh(appId, now);
        if (matchCount == 0)
        {
            return MediaSessionSnapshot.NoVolume;
        }

        try
        {
            return Math.Clamp(matches[0]!.SimpleAudioVolume.Volume, 0f, 1f);
        }
        catch (Exception exception)
        {
            Invalidate(exception);
            return MediaSessionSnapshot.NoVolume;
        }
    }

    public void Set(string appId, float volume, long now)
    {
        Refresh(appId, now);
        var level = Math.Clamp(volume, 0f, 1f);
        try
        {
            for (var index = 0; index < matchCount; index++)
            {
                matches[index]!.SimpleAudioVolume.Volume = level;
            }
        }
        catch (Exception exception)
        {
            Invalidate(exception);
        }
    }

    public void Dispose()
    {
        ReleaseMatches();
        device?.Dispose();
        device = null;
        enumerator?.Dispose();
        enumerator = null;
    }

    private void Refresh(string appId, long now)
    {
        if (ReferenceEquals(appId, matchedAppId) && now - scannedAt < RescanMilliseconds)
        {
            return;
        }

        matchedAppId = appId;
        scannedAt = now;
        try
        {
            Scan(appId);
        }
        catch (Exception exception)
        {
            Invalidate(exception);
        }
    }

    private void Scan(string appId)
    {
        ReleaseMatches();
        device?.Dispose();
        device = null;
        if (appId.Length == 0)
        {
            return;
        }

        enumerator ??= new MMDeviceEnumerator();
        device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        var manager = device.AudioSessionManager;
        manager.RefreshSessions();
        var sessions = manager.Sessions;
        for (var index = 0; index < sessions.Count; index++)
        {
            var session = sessions[index];
            if (matchCount < MaximumMatches && !session.IsSystemSoundsSession
                                            && ProcessIdentity.Matches(session.GetProcessID, appId))
            {
                matches[matchCount++] = session;
                continue;
            }

            session.Dispose();
        }
    }

    private void Invalidate(Exception exception)
    {
        AepLog.Debug(exception, "[SystemMedia] app volume unavailable");
        ReleaseMatches();
        device?.Dispose();
        device = null;
    }

    private void ReleaseMatches()
    {
        for (var index = 0; index < matchCount; index++)
        {
            matches[index]?.Dispose();
            matches[index] = null;
        }

        matchCount = 0;
    }
}
