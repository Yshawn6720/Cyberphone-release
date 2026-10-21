using Aetherphone.Core.Shortcuts;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ShortcutRulesTests
{
    private static ShortcutEntry Entry(string name, params ShortcutStep[] steps)
    {
        var entry = new ShortcutEntry { Name = name, Tint = "5C8CFA" };
        entry.Steps.AddRange(steps);
        return entry;
    }

    private static ShortcutStep Command(string text) => new() { Kind = ShortcutStepKind.Command, Text = text };

    private static ShortcutStep Link(string text) => new() { Kind = ShortcutStepKind.OpenUrl, Text = text };

    private static ShortcutStep Wait(float seconds) => new() { Kind = ShortcutStepKind.Wait, Seconds = seconds };

    [Fact]
    public void ANamedShortcutWithACommand_CanBeSaved()
    {
        Assert.Equal(ShortcutDraftIssue.None, ShortcutRules.Check(Entry("Sit", Command("/groundsit"))));
    }

    [Fact]
    public void ABlankName_IsReportedFirst()
    {
        Assert.Equal(ShortcutDraftIssue.MissingName, ShortcutRules.Check(Entry("   ", Command("/sit"))));
    }

    [Fact]
    public void OnlyEmptyCommands_CountAsNoSteps()
    {
        Assert.Equal(ShortcutDraftIssue.NoSteps, ShortcutRules.Check(Entry("Sit", Command("  "))));
        Assert.Equal(ShortcutDraftIssue.NoSteps, ShortcutRules.Check(Entry("Sit")));
    }

    [Fact]
    public void AWaitAlone_IsStillRunnable()
    {
        Assert.True(ShortcutRules.HasRunnableStep(Entry("Pause", Wait(2f))));
    }

    [Fact]
    public void ALinkThatIsNotHttp_BlocksSaving()
    {
        var entry = Entry("Bad", Command("/sit"), Link("ftp://example.com"));

        Assert.Equal(ShortcutDraftIssue.BadLink, ShortcutRules.Check(entry));
        Assert.False(ShortcutRules.HasRunnableStep(entry));
    }

    [Fact]
    public void AnEmptyLink_IsIgnoredNotRejected()
    {
        Assert.Equal(ShortcutDraftIssue.None, ShortcutRules.Check(Entry("Ok", Command("/sit"), Link(""))));
    }

    [Fact]
    public void Pruning_DropsBlankTextStepsAndTrimsTheRest()
    {
        var entry = Entry("Mix", Command("  /sit  "), Command(""), Wait(1f), Link("  "));

        ShortcutRules.PruneEmptySteps(entry);

        Assert.Equal(2, entry.Steps.Count);
        Assert.Equal("/sit", entry.Steps[0].Text);
        Assert.Equal(ShortcutStepKind.Wait, entry.Steps[1].Kind);
    }

    [Fact]
    public void ACopy_HasTheSameContentDespiteANewId()
    {
        var entry = Entry("Ready", Command("/readycheck"), Wait(8f));
        var copy = entry.Copy();

        Assert.NotEqual(entry.Id, copy.Id);
        Assert.True(ShortcutRules.SameContent(entry, copy));
    }

    [Fact]
    public void ChangingAWait_IsAContentChange()
    {
        var entry = Entry("Ready", Wait(1f));
        var copy = entry.Copy();
        copy.Steps[0].Seconds = 1.5f;

        Assert.False(ShortcutRules.SameContent(entry, copy));
    }

    [Fact]
    public void ReorderingSteps_IsAContentChange()
    {
        var entry = Entry("Two", Command("/a"), Command("/b"));
        var copy = entry.Copy();
        ShortcutRules.Move(copy.Steps, 0, 1);

        Assert.False(ShortcutRules.SameContent(entry, copy));
        Assert.Equal("/b", copy.Steps[0].Text);
        Assert.Equal("/a", copy.Steps[1].Text);
    }

    [Fact]
    public void Move_ClampsTheTargetAndRejectsNoOps()
    {
        var steps = new List<ShortcutStep> { Command("/a"), Command("/b"), Command("/c") };

        Assert.False(ShortcutRules.Move(steps, 1, 1));
        Assert.False(ShortcutRules.Move(steps, 5, 0));
        Assert.True(ShortcutRules.Move(steps, 0, 99));
        Assert.Equal("/b", steps[0].Text);
        Assert.Equal("/a", steps[2].Text);
    }

    [Fact]
    public void NudgeWait_StepsByHalfSecondsWithinTheRunnerLimits()
    {
        Assert.Equal(1.5f, ShortcutRules.NudgeWait(1f, 1));
        Assert.Equal(0.5f, ShortcutRules.NudgeWait(1f, -1));
        Assert.Equal(ShortcutRunner.MinWaitSeconds, ShortcutRules.NudgeWait(0.3f, -1));
        Assert.Equal(ShortcutRunner.MaxWaitSeconds, ShortcutRules.NudgeWait(ShortcutRunner.MaxWaitSeconds, 1));
    }
}

public sealed class ShortcutTemplateTests
{
    [Fact]
    public void EveryTemplate_BuildsASavableShortcut()
    {
        for (var index = 0; index < ShortcutTemplates.All.Length; index++)
        {
            var entry = ShortcutTemplates.All[index].Build();

            Assert.Equal(ShortcutDraftIssue.None, ShortcutRules.Check(entry));
            Assert.InRange(entry.Name.Length, 1, ShortcutStore.NameMaxLength);
            Assert.InRange(entry.Steps.Count, 1, ShortcutStore.MaxSteps);
        }
    }

    [Fact]
    public void EveryTemplate_SurvivesAShareCodeRoundTrip()
    {
        for (var index = 0; index < ShortcutTemplates.All.Length; index++)
        {
            var entry = ShortcutTemplates.All[index].Build();

            Assert.True(ShortcutCode.TryDecode(ShortcutCode.Encode(entry), out var decoded, out _));
            Assert.True(ShortcutRules.SameContent(entry, decoded));
        }
    }

    [Fact]
    public void TemplateIds_AreUniqueAndEveryGroupHasTemplates()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < ShortcutTemplates.All.Length; index++)
        {
            Assert.True(ids.Add(ShortcutTemplates.All[index].Id));
        }

        for (var groupIndex = 0; groupIndex < ShortcutTemplates.Groups.Length; groupIndex++)
        {
            var group = ShortcutTemplates.Groups[groupIndex];
            Assert.Contains(ShortcutTemplates.All, template => template.Group == group);
        }
    }

    [Fact]
    public void BuildingATemplateTwice_GivesIndependentShortcuts()
    {
        var template = ShortcutTemplates.All[0];
        var first = template.Build();
        var second = template.Build();

        Assert.NotEqual(first.Id, second.Id);
        Assert.NotSame(first.Steps[0], second.Steps[0]);
    }
}
