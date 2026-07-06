namespace Fenrir.Application.Game.Domain.Crafting;

/// <summary>
///     Packs/unpacks a rune-core item's 4 sub-stat components into/from one packed int, one byte per
///     component, ordered first=STR/second=DEX/third=VIT/fourth=INT (little-endian: byte0=STR).
/// </summary>
/// <remarks>
///     Réf. C++ : Server/Header/function.h:317-343 (the 4 "return" accessors establishing this byte order) ;
///     :3348-3386 (the matching mutators).
///     <para>
///         Components are modeled as <see cref="sbyte" />, not <see cref="byte" />, because
///         <see cref="RuneStoneCraftResolver" />'s 92298 branch checks a stat's raw stored value against
///         exactly <c>0</c> in a way that implies the value can go negative in pre-existing/legacy state (see
///         that type's own remarks on the "== 0 vs &lt;= 0" inconsistency between branches) -- an unsigned
///         byte could never represent that.
///     </para>
///     <para>
///         Where this packed value physically lives on the destination item's persisted row (which
///         <c>ItemStack</c>/<c>CharacterItemSlotDto</c>/<c>Tvp</c> column, if any) is an open integration
///         question -- see <c>Fenrir.Application.Game.Services.ItemModification.RuneStoneCraftService</c>'s
///         remarks in the Services layer. This codec is deliberately storage-agnostic: it only knows how to
///         pack/unpack the bare <see cref="int" />.
///     </para>
/// </remarks>
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
