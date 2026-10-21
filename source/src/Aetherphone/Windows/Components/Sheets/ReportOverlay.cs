using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Report;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal sealed class ReportOverlay
{
    private const ImGuiWindowFlags OverlayFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
                                                  ImGuiWindowFlags.NoBackground;

    private const string CategoryMenuId = "reportCategory";
    private const float MaxDim = 0.55f;
    private const float MinCardScale = 0.92f;
    private const float CardRounding = 24f;
    private const float CardPadding = 22f;
    private const float CardMaxWidth = 360f;
    private const float CardSideMargin = 24f;
    private const float FieldHeight = Button.LargeHeight;
    private const float FieldInset = 16f;
    private const float ChevronSize = 14f;
    private const float FieldGap = 10f;
    private const float SummaryGap = 8f;
    private const float TitleGap = 14f;
    private const float ButtonGap = 10f;
    private const float ButtonsTopGap = 20f;
    private const int ReasonMaxLength = 170;

    private readonly ReportService service;
    private readonly DropdownMenu categoryMenu = new();
    private readonly DropdownMenu.Item[] categoryItems = new DropdownMenu.Item[ReportCategories.All.Length];
    private Spring reveal;
    private Spring summaryReveal;
    private ReportPrompt? shown;
    private ReportPrompt? armedPrompt;
    private int openedFrame;

    public ReportOverlay(ReportService service)
    {
        this.service = service;
    }

    public bool CapturesPointer => service.Active is not null || !reveal.IsResting(0f, 0.001f, 0.005f);

    public void Dismiss() => service.Dismiss();

    public void Draw(Rect screen, PhoneTheme theme)
    {
        var active = service.Active;
        if (active is not null)
        {
            shown = active;
            if (!ReferenceEquals(active, armedPrompt))
            {
                armedPrompt = active;
                openedFrame = ImGui.GetFrameCount();
                summaryReveal.SnapTo(0f);
            }
        }
        else
        {
            armedPrompt = null;
            if (categoryMenu.Open)
            {
                categoryMenu.Close();
            }
        }

        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        reveal.Step(active is not null ? 1f : 0f, Motion.Sheet, delta);
        if (shown is null)
        {
            return;
        }

        if (active is null && reveal.IsResting(0f, 0.001f, 0.005f))
        {
            reveal.SnapTo(0f);
            shown = null;
            return;
        }

        var opacity = Math.Clamp(reveal.Value, 0f, 1f);
        var cardScale = MinCardScale + (1f - MinCardScale) * opacity;
        ImGui.SetCursorScreenPos(screen.Min);
        using (ImRaii.Child("##reportOverlay", screen.Size, false, OverlayFlags))
        {
            var drawList = ImGui.GetWindowDrawList();
            drawList.AddRectFilled(screen.Min, screen.Max,
                ImGui.GetColorU32(new Vector4(0f, 0f, 0f, MaxDim * opacity)));
            var menuWasOpen = categoryMenu.Open;
            categoryMenu.Gate();
            var interactive = active is not null && opacity > 0.5f && !menuWasOpen;
            var cardRect = DrawCard(screen, theme, shown, opacity, cardScale, interactive, delta);
            DrawCategoryMenu(screen, theme);
            if (active is null || opacity <= 0.5f || menuWasOpen)
            {
                return;
            }

            if (!service.Busy && ImGui.GetFrameCount() != openedFrame &&
                UiInteract.ClickedOutside(cardRect.Min, cardRect.Max))
            {
                service.Dismiss();
            }
        }
    }

    private Rect DrawCard(Rect screen, PhoneTheme theme, ReportPrompt prompt, float opacity, float cardScale,
        bool interactive, float delta)
    {
        var scale = UiScale.Current;
        var s = scale * cardScale;
        var drawList = ImGui.GetWindowDrawList();
        var pad = CardPadding * s;
        var available = screen.Width - CardSideMargin * 2f * scale;
        var cardWidth = MathF.Min(CardMaxWidth * scale, available) * cardScale;
        var innerWidth = cardWidth - pad * 2f;
        var titleStyle = Scaled(TextStyles.Title2, cardScale);
        var bodyStyle = Scaled(TextStyles.Callout, cardScale);
        var footnoteStyle = Scaled(TextStyles.Footnote, cardScale);
        var buttonHeight = Button.LargeHeight * s;

        var title = service.Sent ? Loc.T(L.Report.SentTitle) : prompt.Title;
        var titleHeight = Typography.MeasureWrappedBlock(title, titleStyle, innerWidth).Y;
        var summary = service.CategoryIndex >= 0 ? Loc.T(ReportCategories.All[service.CategoryIndex].Summary) : null;
        var summaryWidth = innerWidth - FieldInset * s * 2f;
        var summaryTarget = summary is null
            ? 0f
            : SummaryGap * s + Typography.MeasureWrappedBlock(summary, footnoteStyle, summaryWidth).Y;
        var summaryHeight = MathF.Max(0f, summaryReveal.Step(summaryTarget, Motion.Sheet, delta));
        float bodyHeight;
        if (service.Sent)
        {
            bodyHeight = Typography.MeasureWrappedBlock(Loc.T(L.Report.Sent), bodyStyle, innerWidth).Y +
                         ButtonsTopGap * s + buttonHeight;
        }
        else
        {
            var disclosureHeight = prompt.Disclosure is { Length: > 0 } disclosure
                ? Typography.MeasureWrappedBlock(disclosure, footnoteStyle, innerWidth).Y + TitleGap * s
                : 0f;
            var failedHeight = service.Failed
                ? FieldGap * s + Typography.Measure(Loc.T(L.Report.Failed), footnoteStyle).Y
                : 0f;
            bodyHeight = disclosureHeight + FieldHeight * s + summaryHeight + FieldGap * s + FieldHeight * s +
                         failedHeight + ButtonsTopGap * s + buttonHeight;
        }

        var cardHeight = pad + titleHeight + TitleGap * s + bodyHeight + pad;
        var cardMin = new Vector2(screen.Center.X - cardWidth * 0.5f, screen.Center.Y - cardHeight * 0.5f);
        var cardMax = cardMin + new Vector2(cardWidth, cardHeight);
        var cardRect = new Rect(cardMin, cardMax);
        Squircle.Fill(drawList, cardMin, cardMax, CardRounding * s,
            ImGui.GetColorU32(Palette.WithAlpha(theme.Surface, opacity)));
        Squircle.Stroke(drawList, cardMin, cardMax, CardRounding * s,
            ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, 0.08f * opacity)), 1f);

        var centerX = cardRect.Center.X;
        Typography.DrawWrappedCentered(new Vector2(centerX, cardMin.Y + pad), title,
            Palette.WithAlpha(theme.TextStrong, opacity), titleStyle, innerWidth);
        var y = cardMin.Y + pad + titleHeight + TitleGap * s;
        var left = cardMin.X + pad;
        if (service.Sent)
        {
            Typography.DrawWrappedCentered(new Vector2(centerX, y), Loc.T(L.Report.Sent),
                Palette.WithAlpha(theme.TextStrong, 0.88f * opacity), bodyStyle, innerWidth);
            var closeY = cardMax.Y - pad - buttonHeight;
            var closeRect = new Rect(new Vector2(left, closeY), new Vector2(left + innerWidth, closeY + buttonHeight));
            if (ConfirmDialog.DrawPillButton(closeRect, Loc.T(L.Common.Close), true, theme, cardScale, opacity,
                    ConfirmButtonTone.Primary, "report.close") && interactive)
            {
                service.Dismiss();
            }

            return cardRect;
        }

        if (prompt.Disclosure is { Length: > 0 } disclosureText)
        {
            y += Typography.DrawWrappedCentered(new Vector2(centerX, y), disclosureText,
                Palette.WithAlpha(theme.TextMuted, opacity), footnoteStyle, innerWidth) + TitleGap * s;
        }

        var ink = ControlInk.From(theme);
        var categoryRect = new Rect(new Vector2(left, y), new Vector2(left + innerWidth, y + FieldHeight * s));
        DrawCategoryField(categoryRect, theme, ink, s, opacity, cardScale, interactive);
        y += FieldHeight * s;
        var summaryVisible = summaryHeight - SummaryGap * s;
        if (summary is not null && summaryVisible > 0.5f)
        {
            DrawSummary(drawList, summary, new Vector2(left + FieldInset * s, y + SummaryGap * s), summaryWidth,
                summaryVisible, summaryTarget - SummaryGap * s, theme, footnoteStyle, opacity);
        }

        y += summaryHeight + FieldGap * s;
        var detailsRect = new Rect(new Vector2(left, y), new Vector2(left + innerWidth, y + FieldHeight * s));
        DrawDetailsField(detailsRect, theme, ink, s, opacity, cardScale, interactive);
        y += FieldHeight * s;
        if (service.Failed)
        {
            y += FieldGap * s;
            var failedText = Loc.T(L.Report.Failed);
            var failedHeight = Typography.Measure(failedText, footnoteStyle).Y;
            Typography.DrawCentered(drawList, new Vector2(centerX, y + failedHeight * 0.5f),
                Typography.FitText(failedText, innerWidth, footnoteStyle), Palette.WithAlpha(theme.Danger, opacity),
                footnoteStyle);
            y += failedHeight;
        }

        y += ButtonsTopGap * s;
        var buttonWidth = (innerWidth - ButtonGap * s) * 0.5f;
        var cancelRect = new Rect(new Vector2(left, y), new Vector2(left + buttonWidth, y + buttonHeight));
        var submitRect = new Rect(new Vector2(cancelRect.Max.X + ButtonGap * s, y),
            new Vector2(left + innerWidth, y + buttonHeight));
        if (ConfirmDialog.DrawPillButton(cancelRect, Loc.T(L.Common.Cancel), !service.Busy, theme, cardScale,
                opacity, ConfirmButtonTone.Neutral, "report.cancel") && interactive && !service.Busy)
        {
            service.Dismiss();
        }

        var canSubmit = !service.Busy && service.CategoryIndex >= 0;
        var submitLabel = Loc.T(service.Busy ? L.Report.Sending : L.Report.Submit);
        if (ConfirmDialog.DrawPillButton(submitRect, submitLabel, canSubmit, theme, cardScale, opacity,
                ConfirmButtonTone.Danger, "report.submit") && interactive && canSubmit)
        {
            service.Submit();
        }

        return cardRect;
    }

    private void DrawCategoryField(Rect rect, PhoneTheme theme, in ControlInk ink, float s, float opacity,
        float cardScale, bool interactive)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = interactive && !service.Busy && UiInteract.Hover(rect.Min, rect.Max);
        var menuOpen = categoryMenu.IsOpenFor(CategoryMenuId);
        var fill = Surfaces.Fill(ink, hovered || menuOpen ? FillLevel.Secondary : FillLevel.Tertiary);
        var radius = rect.Height * 0.5f;
        Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(fill with { W = fill.W * opacity }));

        var hasCategory = service.CategoryIndex >= 0;
        var label = hasCategory
            ? Loc.T(ReportCategories.All[service.CategoryIndex].Label)
            : Loc.T(L.Report.CategoryHint);
        var labelInk = hasCategory ? theme.TextStrong : theme.TextMuted;
        var labelStyle = Scaled(hasCategory ? TextStyles.BodyEmphasized : TextStyles.Body, cardScale);
        var chevronCenter = new Vector2(rect.Max.X - FieldInset * s - ChevronSize * s * 0.5f, rect.Center.Y);
        var labelLeft = rect.Min.X + FieldInset * s;
        var labelMaxWidth = chevronCenter.X - ChevronSize * s - labelLeft;
        var fittedLabel = Typography.FitText(label, labelMaxWidth, labelStyle);
        var labelSize = Typography.Measure(fittedLabel, labelStyle);
        Typography.Draw(drawList, new Vector2(labelLeft, rect.Center.Y - labelSize.Y * 0.5f), fittedLabel,
            Palette.WithAlpha(labelInk, opacity), labelStyle);
        PhoneIcon.Draw(drawList, chevronCenter, menuOpen ? PhoneIcons.ChevronUp : PhoneIcons.ChevronDown,
            Palette.WithAlpha(theme.TextMuted, opacity), ChevronSize * s);
        if (!hovered)
        {
            return;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (UiInteract.Click(rect.Min, rect.Max, hovered))
        {
            categoryMenu.Toggle(CategoryMenuId, rect);
        }
    }

    private static void DrawSummary(ImDrawListPtr drawList, string summary, Vector2 topLeft, float width,
        float visibleHeight, float fullHeight, PhoneTheme theme, in TextStyle style, float opacity)
    {
        var visibility = Math.Clamp(visibleHeight / MathF.Max(fullHeight, 0.0001f), 0f, 1f);
        drawList.PushClipRect(topLeft, new Vector2(topLeft.X + width, topLeft.Y + visibleHeight), true);
        Typography.DrawWrappedLeft(topLeft, summary, Palette.WithAlpha(theme.TextMuted, visibility * opacity), style,
            width);
        drawList.PopClipRect();
    }

    private void DrawDetailsField(Rect rect, PhoneTheme theme, in ControlInk ink, float s, float opacity,
        float cardScale, bool interactive)
    {
        var drawList = ImGui.GetWindowDrawList();
        var fill = Surfaces.Fill(ink, FillLevel.Tertiary);
        Squircle.Fill(drawList, rect.Min, rect.Max, rect.Height * 0.5f,
            ImGui.GetColorU32(fill with { W = fill.W * opacity }));
        if (!interactive)
        {
            var textLeft = rect.Min.X + FieldInset * s;
            var textMaxWidth = rect.Max.X - FieldInset * s - textLeft;
            var style = Scaled(TextStyles.Body, cardScale);
            var hasDraft = service.ReasonDraft.Length > 0;
            var shownText = hasDraft ? service.ReasonDraft : Loc.T(L.Report.DetailsHint);
            var textSize = Typography.Measure(shownText, style);
            Marquee.DrawLeft("reportoverlay.reason", shownText, textLeft, rect.Center.Y - textSize.Y * 0.5f,
                textMaxWidth, style, Palette.WithAlpha(hasDraft ? theme.TextStrong : theme.TextMuted, opacity),
                UiInteract.Hover(rect.Min, rect.Max));
            return;
        }

        var draft = service.ReasonDraft;
        var submitted = GlassField.Text(rect, "##reportDetails", Loc.T(L.Report.DetailsHint), ref draft, theme,
            UiScale.Current, ReasonMaxLength, false, ImGuiInputTextFlags.EnterReturnsTrue);
        service.ReasonDraft = draft;
        if (submitted)
        {
            service.Submit();
        }
    }

    private void DrawCategoryMenu(Rect screen, PhoneTheme theme)
    {
        if (!categoryMenu.IsOpenFor(CategoryMenuId))
        {
            return;
        }

        var visible = service.Visible;
        for (var index = 0; index < visible.Length; index++)
        {
            var categoryIndex = visible[index];
            categoryItems[index] = new DropdownMenu.Item(Loc.T(ReportCategories.All[categoryIndex].Label),
                Selected: categoryIndex == service.CategoryIndex);
        }

        var picked = categoryMenu.Draw(screen, theme, categoryItems.AsSpan(0, visible.Length));
        if (picked >= 0)
        {
            service.CategoryIndex = visible[picked];
        }
    }

    private static TextStyle Scaled(in TextStyle style, float cardScale) => style with { Scale = style.Scale * cardScale };
}
