namespace Aetherphone.Core.Coins;

internal sealed class CoinSavingGoal
{
    public string SkuId { get; set; } = string.Empty;
    public string CategoryId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public long Price { get; set; }

    public static float Fraction(long balance, long price)
    {
        if (price <= 0)
        {
            return 1f;
        }

        return Math.Clamp((float)((double)Math.Max(0L, balance) / price), 0f, 1f);
    }

    public static long Remaining(long balance, long price) => Math.Max(0L, price - Math.Max(0L, balance));
}
