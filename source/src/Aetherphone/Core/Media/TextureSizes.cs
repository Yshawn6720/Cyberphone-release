namespace Aetherphone.Core.Media;

internal static class TextureSizes
{
    public const int Native = 0;
    public const int Smallest = 32;
    public const int LevelCount = 5;

    public static int LevelFor(float drawnPixels)
    {
        var level = 1;
        var size = Smallest;
        while (size < drawnPixels && level < LevelCount)
        {
            size <<= 1;
            level++;
        }

        return level;
    }

    public static int SizeOf(int level)
    {
        if (level <= Native)
        {
            return 0;
        }

        return Smallest << (level - 1);
    }
}
