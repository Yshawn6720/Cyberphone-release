using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Apps.Casino;

internal enum TonightTone : byte
{
    Calm,
    Close,
    Reached,
}

internal readonly struct CasinoTonight
{
    public const float CloseFraction = 0.8f;

    public readonly long Limit;
    public readonly long NetLoss;
    public readonly long Headroom;

    public CasinoTonight(long limit, long netLoss, long headroom)
    {
        Limit = Math.Max(0, limit);
        NetLoss = netLoss;
        Headroom = Math.Clamp(headroom, 0, Limit);
    }

    public bool HasLimit => Limit > 0;

    public bool Reached => HasLimit && Headroom <= 0;

    public long Used => Limit - Headroom;

    public float Fraction => HasLimit ? Math.Clamp((float)Used / Limit, 0f, 1f) : 0f;

    public TonightTone Tone
    {
        get
        {
            if (Reached)
            {
                return TonightTone.Reached;
            }

            return Fraction >= CloseFraction ? TonightTone.Close : TonightTone.Calm;
        }
    }

    public static CasinoTonight From(CasinoStateDto state) =>
        new(state.LossLimit, state.NetLossToday, state.LossHeadroom);
}
