namespace Aetherphone.Core.Shortcuts;

internal enum ShortcutDraftIssue : byte
{
    None,
    MissingName,
    NoSteps,
    BadLink,
}

internal static class ShortcutRules
{
    public const float WaitNudgeSeconds = 0.5f;

    public static ShortcutDraftIssue Check(ShortcutEntry entry)
    {
        if (entry.Name.Trim().Length == 0)
        {
            return ShortcutDraftIssue.MissingName;
        }

        if (HasBadLink(entry))
        {
            return ShortcutDraftIssue.BadLink;
        }

        return HasRunnableStep(entry) ? ShortcutDraftIssue.None : ShortcutDraftIssue.NoSteps;
    }

    public static bool HasBadLink(ShortcutEntry entry)
    {
        for (var index = 0; index < entry.Steps.Count; index++)
        {
            if (IsBadLink(entry.Steps[index]))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsBadLink(ShortcutStep step) =>
        step.Kind == ShortcutStepKind.OpenUrl && step.Text.Trim().Length > 0 &&
        !ShortcutCommandText.IsWebUrl(step.Text);

    public static bool HasRunnableStep(ShortcutEntry entry)
    {
        var runnable = false;
        for (var index = 0; index < entry.Steps.Count; index++)
        {
            var step = entry.Steps[index];
            if (IsBadLink(step))
            {
                return false;
            }

            if (step.Kind is ShortcutStepKind.Wait or ShortcutStepKind.OpenPlugin || step.Text.Trim().Length > 0)
            {
                runnable = true;
            }
        }

        return runnable;
    }

    public static void PruneEmptySteps(ShortcutEntry entry)
    {
        for (var index = entry.Steps.Count - 1; index >= 0; index--)
        {
            var step = entry.Steps[index];
            if (step.Kind != ShortcutStepKind.Command && step.Kind != ShortcutStepKind.OpenUrl)
            {
                continue;
            }

            if (step.Text.Trim().Length == 0)
            {
                entry.Steps.RemoveAt(index);
                continue;
            }

            step.Text = step.Text.Trim();
        }
    }

    public static bool SameContent(ShortcutEntry first, ShortcutEntry second)
    {
        if (!string.Equals(first.Name, second.Name, StringComparison.Ordinal) || first.Glyph != second.Glyph ||
            !string.Equals(first.IconPlugin, second.IconPlugin, StringComparison.Ordinal) ||
            !string.Equals(first.IconImage, second.IconImage, StringComparison.Ordinal) ||
            !string.Equals(first.Tint, second.Tint, StringComparison.OrdinalIgnoreCase) ||
            first.Steps.Count != second.Steps.Count)
        {
            return false;
        }

        for (var index = 0; index < first.Steps.Count; index++)
        {
            var left = first.Steps[index];
            var right = second.Steps[index];
            if (left.Kind != right.Kind || !string.Equals(left.Text, right.Text, StringComparison.Ordinal) ||
                (left.Kind == ShortcutStepKind.Wait && MathF.Abs(left.Seconds - right.Seconds) > 0.001f))
            {
                return false;
            }
        }

        return true;
    }

    public static bool Move(List<ShortcutStep> steps, int from, int to)
    {
        if (from < 0 || from >= steps.Count)
        {
            return false;
        }

        var target = Math.Clamp(to, 0, steps.Count - 1);
        if (target == from)
        {
            return false;
        }

        var moving = steps[from];
        steps.RemoveAt(from);
        steps.Insert(target, moving);
        return true;
    }

    public static float NudgeWait(float seconds, int direction)
    {
        var next = MathF.Round((seconds + WaitNudgeSeconds * Math.Sign(direction)) * 10f) / 10f;
        return Math.Clamp(next, ShortcutRunner.MinWaitSeconds, ShortcutRunner.MaxWaitSeconds);
    }
}
