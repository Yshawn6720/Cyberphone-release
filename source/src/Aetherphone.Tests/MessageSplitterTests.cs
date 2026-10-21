using System.Text;
using Aetherphone.Core.GameChat;
using Xunit;

namespace Aetherphone.Tests;

public sealed class MessageSplitterTests
{
    private const string Mark = ">>";

    private static List<string> Split(string text, int budget, string indicator)
    {
        var parts = new List<string>();
        MessageSplitter.Split(text, budget, indicator, parts);
        return parts;
    }

    private static string Repeat(string unit, int times)
    {
        var builder = new StringBuilder(unit.Length * times);
        for (var index = 0; index < times; index++)
        {
            builder.Append(unit);
        }

        return builder.ToString();
    }

    private static void AssertWithinBudget(List<string> parts, int budget)
    {
        for (var index = 0; index < parts.Count; index++)
        {
            Assert.True(Encoding.UTF8.GetByteCount(parts[index]) <= budget,
                $"part {index} is {Encoding.UTF8.GetByteCount(parts[index])} bytes, budget is {budget}");
        }
    }

    [Fact]
    public void ShortMessageStaysOnePartWithoutTheMark()
    {
        var parts = Split("just one line", 100, Mark);

        Assert.Single(parts);
        Assert.Equal("just one line", parts[0]);
    }

    [Fact]
    public void CountsBytesRatherThanCharacters()
    {
        var word = Repeat("あ", 10);
        var text = string.Concat(word, " ", word, " ", word);

        Assert.Equal(32, text.Length);
        Assert.Equal(92, Encoding.UTF8.GetByteCount(text));

        var parts = Split(text, 70, string.Empty);

        Assert.Equal(2, parts.Count);
        AssertWithinBudget(parts, 70);
    }

    [Fact]
    public void NeverSplitsInsideAWord()
    {
        var text = "alpha bravo charlie delta echo foxtrot golf hotel india juliett";

        var parts = Split(text, 24, string.Empty);

        Assert.True(parts.Count > 2);
        Assert.Equal(text, string.Join(' ', parts));
        AssertWithinBudget(parts, 24);
    }

    [Fact]
    public void MarkRidesEveryPartButTheLast()
    {
        var text = "alpha bravo charlie delta echo foxtrot golf hotel india juliett kilo lima";

        var parts = Split(text, 32, Mark);

        Assert.True(parts.Count > 2);
        for (var index = 0; index < parts.Count - 1; index++)
        {
            Assert.EndsWith(Mark, parts[index], StringComparison.Ordinal);
        }

        Assert.DoesNotContain(Mark, parts[parts.Count - 1], StringComparison.Ordinal);
        AssertWithinBudget(parts, 32);
    }

    [Fact]
    public void MarkIsDroppedWhenItWouldLeaveNoRoom()
    {
        var parts = Split("alpha bravo charlie delta", 10, "----------");

        Assert.True(parts.Count > 1);
        for (var index = 0; index < parts.Count; index++)
        {
            Assert.DoesNotContain("----------", parts[index], StringComparison.Ordinal);
        }

        AssertWithinBudget(parts, 10);
    }

    [Fact]
    public void EveryPartFitsTheChannelLineWithItsPrefix()
    {
        Assert.True(GameChannels.TryByKey(GameChannels.SayKey, out var say));
        Assert.True(GameChannels.TryByKey(GameChannels.TellKey, out var tell));

        const string target = "Aria Nightsong@Siren";
        var sayBudget = ChatSend.Budget(say, string.Empty);
        var tellBudget = ChatSend.Budget(tell, target);

        Assert.Equal(ChatSend.MaxBytes - Encoding.UTF8.GetByteCount(say.Command) - 1, sayBudget);
        Assert.Equal(ChatSend.MaxBytes - Encoding.UTF8.GetByteCount(tell.Command) -
                     Encoding.UTF8.GetByteCount(target) - 2, tellBudget);

        const string word = "alpha ";
        var text = Repeat(word, sayBudget / Encoding.UTF8.GetByteCount(word)).Trim();
        var length = Encoding.UTF8.GetByteCount(text);

        Assert.True(length <= sayBudget);
        Assert.True(length > tellBudget);
        Assert.Single(Split(text, sayBudget, Mark));

        var tellParts = Split(text, tellBudget, Mark);

        Assert.True(tellParts.Count > 1);
        for (var index = 0; index < tellParts.Count; index++)
        {
            var line = string.Concat(tell.Command, " ", target, " ", tellParts[index]);
            Assert.True(Encoding.UTF8.GetByteCount(line) <= ChatSend.MaxBytes);
        }
    }

    [Fact]
    public void WordLongerThanAWholePartIsCutAtACharacterBoundary()
    {
        var word = Repeat("x", 120);

        var parts = Split(word, 40, string.Empty);

        Assert.Equal(3, parts.Count);
        Assert.Equal(word, string.Concat(parts));
        AssertWithinBudget(parts, 40);
    }

    [Fact]
    public void NeverCutsThroughAMultiByteSequence()
    {
        var text = Repeat("\U0001F600", 20);

        var parts = Split(text, 30, string.Empty);

        Assert.True(parts.Count > 1);
        for (var index = 0; index < parts.Count; index++)
        {
            var part = parts[index];
            Assert.Equal(part, Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(part)));
            Assert.DoesNotContain('�', part);
        }

        Assert.Equal(text, string.Concat(parts));
        AssertWithinBudget(parts, 30);
    }

    [Fact]
    public void ALineBreakStartsItsOwnPart()
    {
        var parts = Split("first line\nsecond line", 200, string.Empty);

        Assert.Equal(2, parts.Count);
        Assert.Equal("first line", parts[0]);
        Assert.Equal("second line", parts[1]);
    }

    [Fact]
    public void NothingComesOutOfAnEmptyBudget()
    {
        Assert.Empty(Split("alpha bravo", 0, Mark));
    }

    [Fact]
    public void CapacityLeavesRoomForTheMarkOnEveryPart()
    {
        var capacity = MessageSplitter.Capacity(100, Mark);

        Assert.Equal((100 - Encoding.UTF8.GetByteCount(" >>")) * MessageSplitter.MaxParts, capacity);
        Assert.Equal(100 * MessageSplitter.MaxParts, MessageSplitter.Capacity(100, string.Empty));
    }
}
