using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Casino.Tables;

internal sealed class BlackjackProjection
{
    private int epoch = -1;
    private long seq = -1;
    private int personalEpoch = -1;
    private long personalSeq = -1;
    private string accountId = string.Empty;

    public CasinoBlackjackRoomStateDto? Board { get; private set; }

    public CasinoBlackjackYouDto? Personal { get; private set; }

    public int Epoch => epoch;

    public long Seq => seq;

    public void Reset()
    {
        epoch = -1;
        seq = -1;
        personalEpoch = -1;
        personalSeq = -1;
        Board = null;
        Personal = null;
    }

    public void Watch(string signedInAccountId)
    {
        accountId = signedInAccountId;
    }

    public bool Apply(CasinoRoomState? state)
    {
        if (state?.Blackjack is null)
        {
            return false;
        }

        if (state.Epoch < epoch || (state.Epoch == epoch && state.Seq <= seq))
        {
            return false;
        }

        epoch = state.Epoch;
        seq = state.Seq;
        Board = state.Blackjack;
        return true;
    }

    public bool ApplyPersonal(CasinoRoomPrivate? held)
    {
        if (held?.Blackjack is null)
        {
            return false;
        }

        if (held.Epoch < personalEpoch || (held.Epoch == personalEpoch && held.Seq < personalSeq))
        {
            return false;
        }

        personalEpoch = held.Epoch;
        personalSeq = held.Seq;
        Personal = held.Blackjack;
        return true;
    }

    public int MySeat
    {
        get
        {
            var seats = Board?.Seats;
            if (seats is null || accountId.Length == 0)
            {
                return -1;
            }

            for (var index = 0; index < seats.Length; index++)
            {
                if (string.Equals(seats[index].UserId, accountId, StringComparison.Ordinal))
                {
                    return seats[index].SeatIndex;
                }
            }

            return -1;
        }
    }

    public int ActionsMask => Current?.ActionsMask ?? 0;

    public int ActiveHand => Current?.ActiveHand ?? -1;

    public int ActionCount => Current?.ActionCount ?? Board?.ActionCount ?? 0;

    public CasinoBlackjackSeatDto? SeatAt(int seatIndex)
    {
        var seats = Board?.Seats;
        if (seats is null)
        {
            return null;
        }

        for (var index = 0; index < seats.Length; index++)
        {
            if (seats[index].SeatIndex == seatIndex)
            {
                return seats[index];
            }
        }

        return null;
    }

    public CasinoBlackjackHandDto[] HandsAt(int seatIndex)
    {
        var mine = Current;
        if (mine is not null && mine.SeatIndex == seatIndex && mine.Hands is { Length: > 0 })
        {
            return mine.Hands;
        }

        return SeatAt(seatIndex)?.Hands ?? Array.Empty<CasinoBlackjackHandDto>();
    }

    public static int CardCount(CasinoBlackjackRoomStateDto? board)
    {
        if (board is null)
        {
            return 0;
        }

        var count = board.DealerCards?.Length ?? 0;
        var seats = board.Seats;
        if (seats is null)
        {
            return count;
        }

        for (var seatIndex = 0; seatIndex < seats.Length; seatIndex++)
        {
            var hands = seats[seatIndex].Hands;
            if (hands is null)
            {
                continue;
            }

            for (var handIndex = 0; handIndex < hands.Length; handIndex++)
            {
                count += hands[handIndex].Cards?.Length ?? 0;
            }
        }

        return count;
    }

    public int CardAt(int seatIndex, int splitIndex, int cardIndex, int publicCard)
    {
        if (PlayingCards.IsCard(publicCard))
        {
            return publicCard;
        }

        var mine = Current;
        if (mine is null || mine.SeatIndex != seatIndex)
        {
            return PlayingCards.FaceDown;
        }

        var hands = mine.Hands;
        if (hands is null || splitIndex < 0 || splitIndex >= hands.Length)
        {
            return PlayingCards.FaceDown;
        }

        var cards = hands[splitIndex].Cards;
        if (cards is null || cardIndex < 0 || cardIndex >= cards.Length)
        {
            return PlayingCards.FaceDown;
        }

        return PlayingCards.IsCard(cards[cardIndex]) ? cards[cardIndex] : PlayingCards.FaceDown;
    }

    private CasinoBlackjackYouDto? Current
    {
        get
        {
            var mine = Personal;
            var board = Board;
            if (mine is null || board is null || board.HandId.Length == 0 || personalEpoch != epoch)
            {
                return null;
            }

            return string.Equals(mine.HandId, board.HandId, StringComparison.Ordinal) ? mine : null;
        }
    }
}
