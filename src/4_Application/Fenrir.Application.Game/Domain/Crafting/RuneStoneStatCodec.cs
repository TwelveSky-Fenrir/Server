namespace Fenrir.Application.Game.Domain.Crafting;

public static class RuneStoneStatCodec
{
    public static int Encode(sbyte str, sbyte dex, sbyte vit, sbyte intelligence)
    {
        return (str & 0xFF) | ((dex & 0xFF) << 8) | ((vit & 0xFF) << 16) | ((intelligence & 0xFF) << 24);
    }

    public static (sbyte Str, sbyte Dex, sbyte Vit, sbyte Int) Decode(int packedValue)
    {
        return (
            (sbyte)(packedValue & 0xFF),
            (sbyte)((packedValue >> 8) & 0xFF),
            (sbyte)((packedValue >> 16) & 0xFF),
            (sbyte)((packedValue >> 24) & 0xFF));
    }
}
