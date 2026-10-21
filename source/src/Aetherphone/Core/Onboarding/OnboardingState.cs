namespace Aetherphone.Core.Onboarding;

internal static class OnboardingState
{
    private static bool replayWelcomeRequested;
    private static string? requestedAppTour;
    public static bool Enabled => Plugin.Cfg.TutorialsEnabled;

    public static bool HasCompleted(string id, int version) =>
        Plugin.Cfg.OnboardingCompleted.TryGetValue(id, out var stored) && stored >= version;

    public static void SetEnabled(bool enabled)
    {
        if (Plugin.Cfg.TutorialsEnabled == enabled)
        {
            return;
        }

        Plugin.Cfg.TutorialsEnabled = enabled;
        Plugin.Cfg.Save();
    }

    public static void MarkCompleted(string id, int version)
    {
        Plugin.Cfg.OnboardingCompleted[id] = version;
        Plugin.Cfg.Save();
    }

    public static void ResetAll()
    {
        if (Plugin.Cfg.OnboardingCompleted.Count == 0)
        {
            return;
        }

        Plugin.Cfg.OnboardingCompleted.Clear();
        Plugin.Cfg.Save();
    }

    public static void Reset(string id)
    {
        if (!Plugin.Cfg.OnboardingCompleted.Remove(id))
        {
            return;
        }

        Plugin.Cfg.Save();
    }

    public static void RequestAppTour(string appId)
    {
        Reset(appId);
        requestedAppTour = appId;
    }

    public static void ClearAppTourRequest() => requestedAppTour = null;

    public static bool ConsumeAppTourRequest(out string appId)
    {
        appId = requestedAppTour ?? string.Empty;
        requestedAppTour = null;
        return appId.Length > 0;
    }

    public static void RequestReplayWelcome() => replayWelcomeRequested = true;

    public static bool ConsumeReplayWelcome()
    {
        if (!replayWelcomeRequested)
        {
            return false;
        }

        replayWelcomeRequested = false;
        return true;
    }
}
