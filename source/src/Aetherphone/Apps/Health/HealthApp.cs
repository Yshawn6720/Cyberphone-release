using Aetherphone.Apps.Health.Widgets;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Health;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Health;

internal sealed partial class HealthApp : IResumableApp, ITabRouteTarget
{
    public const string GoalsRoute = "health.tab.goals";

    private const float PressedCardScale = PressFx.CardPressedScale;

    public string Id => "health";
    public string DisplayName => Loc.T(L.Health.Title);
    public string Glyph => "He";
    public int BadgeCount => 0;

    private readonly HealthTracker tracker;
    private readonly ConfirmService confirm;
    private readonly AppSkin ui = new(AppPalettes.Health);
    private readonly HealthDigest digest = new();
    private readonly ViewRouter<HealthView> router;
    private readonly RouterDraw<HealthView> drawView;
    private readonly Action back;
    private PendingTab pendingTab;
    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;

    public HealthApp(HealthTracker tracker, ConfirmService confirm)
    {
        this.tracker = tracker;
        this.confirm = confirm;
        router = new ViewRouter<HealthView>(HealthView.Summary());
        drawView = DrawView;
        back = () => router.Pop();
    }

    private HealthProfile Profile => tracker.Profile;

    private HealthUnits Units => tracker.Profile.Units;

    public void OpenTab(string tab) => pendingTab.Request(tab);

    public void OnOpened()
    {
        router.Reset();
        weightSheet.CloseImmediately();
        goalSheet.CloseImmediately();
        digest.Invalidate();
        SnapFills();
        tracker.RefreshHeight();
    }

    public void OnResumed()
    {
        digest.Invalidate();
        tracker.RefreshHeight();
    }

    public void OnClosed()
    {
        weightSheet.CloseImmediately();
        goalSheet.CloseImmediately();
        tracker.SaveNow();
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = theme;
        var ready = tracker.IsTracking && Profile.SetupCompleted;
        if (ready)
        {
            ConsumePendingTab();
            digest.Refresh(tracker);
        }
        else
        {
            weightSheet.CloseImmediately();
            goalSheet.CloseImmediately();
        }

        if (!ready || router.Depth > 1)
        {
            TourHolds.Hold(Id);
        }
        else
        {
            TourHolds.Release(Id);
        }

        var scale = UiScale.Current;
        var screen = SceneChrome.ScreenFrom(context.Content, theme, scale);
        ui.Backdrop(screen);
        using (InputShield.Engage(weightSheet.CapturesPointer || goalSheet.CapturesPointer))
        {
            router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
        }

        if (!ready)
        {
            return;
        }

        DrawWeightSheet(screen);
        DrawGoalSheet(screen);
    }

    private void ConsumePendingTab()
    {
        if (pendingTab.Take(GoalsRoute))
        {
            OpenFresh(HealthView.Goals());
        }
        else if (pendingTab.Take(HydrationWidget.HydrationIntent))
        {
            OpenFresh(HealthView.Water());
        }
    }

    private void OpenFresh(HealthView view)
    {
        weightSheet.CloseImmediately();
        goalSheet.CloseImmediately();
        router.Reset();
        router.Push(view, false);
    }

    private void Open(HealthView view)
    {
        UiFeedback.Play(UiSound.Tap);
        router.Push(view);
    }

    private void DrawView(HealthView view, Rect area, int depth)
    {
        ui.Body(area);
        if (!tracker.IsTracking)
        {
            DrawSignedOut(area);
            return;
        }

        if (!Profile.SetupCompleted)
        {
            DrawWelcome(area);
            return;
        }

        switch (view.Kind)
        {
            case HealthViewKind.Water:
                DrawWater(area);
                break;
            case HealthViewKind.Metric:
                DrawMetric(area, view.Metric);
                break;
            case HealthViewKind.Weight:
                DrawWeight(area);
                break;
            case HealthViewKind.Goals:
                DrawGoals(area);
                break;
            case HealthViewKind.Settings:
                DrawSettings(area);
                break;
            default:
                DrawSummary(area);
                break;
        }
    }

    private void DrawSignedOut(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        var navBar = AppHeader.BeginLargeTitle(context, false);
        using (AppSurface.Begin(navBar.Body))
        {
            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var bottom = origin.Y + HealthArt.State(drawList, ui, origin, width, FontAwesomeIcon.Heartbeat, ui.Accent,
                Loc.T(L.Health.SignedOutTitle), Loc.T(L.Health.SignedOutBody), scale);
            ReserveTo(origin, width, bottom + HealthArt.BottomBreathing * scale);
        }

        AppHeader.EndLargeTitle(in navBar, context, "health.nav.signedOut", DisplayName, NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty);
    }

    private float SectionTop(ImDrawListPtr drawList, float left, float top, float width, string title, float scale)
    {
        var cursorY = top + HealthArt.SectionGap * scale;
        cursorY += CardSectionHeader.Draw(drawList, new Vector2(left, cursorY), width, title, ui.TitleInk);
        return cursorY + HealthArt.HeaderGap * scale;
    }

    private bool PressableCard(ImDrawListPtr drawList, Rect rect, string id, bool blocked, out Rect drawn)
    {
        var hovered = !blocked && UiInteract.Hover(rect.Min, rect.Max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var press = PressFx.Scale(id, pressed, PressedCardScale);
        var half = rect.Size * 0.5f * press;
        drawn = new Rect(rect.Center - half, rect.Center + half);
        var radius = Metrics.Radius.Grouped * UiScale.Current;
        ui.Card(drawList, drawn.Min, drawn.Max, radius);
        if (hovered)
        {
            Squircle.Fill(drawList, drawn.Min, drawn.Max, radius, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    private float Footnote(Vector2 origin, float width, string text, float scale) =>
        Typography.DrawWrappedLeft(new Vector2(origin.X, origin.Y + HealthArt.TileGap * scale), text, ui.MutedInk,
            TextStyles.Footnote, width) + HealthArt.TileGap * scale;

    private static void ReserveTo(Vector2 origin, float width, float bottom)
    {
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, MathF.Max(0f, bottom - origin.Y)));
    }

    public void Dispose()
    {
    }
}
