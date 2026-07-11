namespace Fenrir.Application.Game.Domain.Crafting;

public static class RuneStoneStatEncoder
{

        public static int ChangeStrValueRune(int packed, sbyte newStr)
    {
        var (_, dex, vit, intel) = RuneStoneStatCodec.Decode(packed);
        return RuneStoneStatCodec.Encode(newStr, dex, vit, intel);
    }

        public static int ChangeDexValueRune(int packed, sbyte newDex)
    {
        var (str, _, vit, intel) = RuneStoneStatCodec.Decode(packed);
        return RuneStoneStatCodec.Encode(str, newDex, vit, intel);
    }

        public static int ChangeVitValueRune(int packed, sbyte newVit)
    {
        var (str, dex, _, intel) = RuneStoneStatCodec.Decode(packed);
        return RuneStoneStatCodec.Encode(str, dex, newVit, intel);
    }

        public static int ChangeIntValueRune(int packed, sbyte newInt)
    {
        var (str, dex, vit, _) = RuneStoneStatCodec.Decode(packed);
        return RuneStoneStatCodec.Encode(str, dex, vit, newInt);
    }
}
