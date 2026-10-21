using Aetherphone.Apps.Games.Online;
using Aetherphone.Core.Games;
using Xunit;

namespace Aetherphone.Tests;

public sealed class OnlineConnectFourTableTests
{
    private const int BottomLeft = (GameRoomWire.ConnectFourRows - 1) * GameRoomWire.ConnectFourColumns;

    [Fact]
    public void TheFirstObservationDoesNotAnimate()
    {
        var cells = BoardWith(BottomLeft, 0);

        Assert.False(OnlineConnectFourTable.IsNewDrop(OnlineConnectFourTable.NotObserved, BottomLeft, cells));
    }

    [Fact]
    public void AChangedIndexOnAnOccupiedCellAnimates()
    {
        var cells = BoardWith(BottomLeft, 0);
        cells[BottomLeft + 1] = 1;

        Assert.True(OnlineConnectFourTable.IsNewDrop(BottomLeft, BottomLeft + 1, cells));
    }

    [Fact]
    public void TheOpeningDropAnimatesAfterAnEmptyBoardWasSeen()
    {
        var cells = BoardWith(BottomLeft, 0);
        var emptyBoardIndex = -1 * GameRoomWire.ConnectFourColumns - 1;

        Assert.True(OnlineConnectFourTable.IsNewDrop(emptyBoardIndex, BottomLeft, cells));
    }

    [Fact]
    public void AnUnchangedIndexDoesNotAnimate()
    {
        var cells = BoardWith(BottomLeft, 0);

        Assert.False(OnlineConnectFourTable.IsNewDrop(BottomLeft, BottomLeft, cells));
    }

    [Fact]
    public void AChangedIndexOnAnEmptyCellDoesNotAnimate()
    {
        var cells = BoardWith(BottomLeft, 0);

        Assert.False(OnlineConnectFourTable.IsNewDrop(BottomLeft, BottomLeft + 1, cells));
    }

    [Theory]
    [InlineData(-8)]
    [InlineData(-1)]
    [InlineData(GameRoomWire.ConnectFourCellCount)]
    public void AnOutOfRangeIndexDoesNotAnimate(int lastIndex)
    {
        var cells = BoardWith(BottomLeft, 0);

        Assert.False(OnlineConnectFourTable.IsNewDrop(BottomLeft, lastIndex, cells));
    }

    [Fact]
    public void AShortCellArrayDoesNotAnimate()
    {
        Assert.False(OnlineConnectFourTable.IsNewDrop(BottomLeft, BottomLeft + 1, Array.Empty<int>()));
    }

    private static int[] BoardWith(int index, int seat)
    {
        var cells = new int[GameRoomWire.ConnectFourCellCount];
        Array.Fill(cells, -1);
        cells[index] = seat;
        return cells;
    }
}
