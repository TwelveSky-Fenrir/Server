namespace Fenrir.Application.Game.Domain.Crafting;

/// <summary>
///     Single-position OVERWRITE encoders for a rune-core item's packed STR/DEX/VIT/INT value -- the
///     "*ValueRune" family consumed by the rune-stone-crafting write path. Each function replaces exactly
///     the one named position with a freshly supplied value and leaves the other three exactly as they
///     were.
/// </summary>
/// <remarks>
///     Réf. C++ (wave13/B5-rune-encoders contract): Server/Header/function.h:3348-3386
///     (<c>ChangeISValueRune</c>/<c>ChangeIUValueRune</c>/<c>ChangeIMValueRune</c>/<c>ChangeIZValueRune</c>).
///     Despite sharing the "Change" verb with <c>Fenrir.Application.Game.Domain.World.Loot.ItemStateEncoder</c>'s
///     additive family, these four are OVERWRITE, not additive -- the contract's own "Additive vs. overwrite
///     semantics are two structurally distinct families sharing similar names" edge case: conflating the two
///     would silently turn cumulative Enchant/Combine/Refine progression into destructive overwrite, or vice
///     versa. This family's positions also carry a wholly different, crafting-local meaning (STR/DEX/VIT/INT
///     stat-roll slots, in that fixed order) from the general Enchant/Combine/Refine/Socket layout
///     <see cref="RuneStoneStatCodec" />'s own remarks and the contract's "dual, unrelated semantics" edge
///     case both describe -- the storage mechanism (one packed 32-bit int, one signed byte per position) is
///     shared, the meaning is not.
///     <para>
///         <see cref="Fenrir.Application.Game.Domain.Crafting.RuneStoneCraftResolver" /> (already
///         implemented, see that type's own remarks) already achieves this exact overwrite behavior inline:
///         it decodes all four positions once up front via <see cref="RuneStoneStatCodec.Decode" /> and
///         calls <see cref="RuneStoneStatCodec.Encode" /> back with only one position replaced --
///         functionally identical to what each function below does. This type exists to give that same
///         operation an explicit, individually-named, independently-testable surface matching the legacy
///         catalog 1:1 (each function below is proven equivalent to the resolver's own inline computation in
///         this type's tests), without editing the resolver's own already-tested call sites.
///     </para>
/// </remarks>
public static class RuneStoneStatEncoder
{
    /// <summary><c>ChangeISValueRune</c> -- overwrites the STR position with <paramref name="newStr" />, preserving DEX/VIT/INT.</summary>
    public static int ChangeStrValueRune(int packed, sbyte newStr)
    {
        var (_, dex, vit, intel) = RuneStoneStatCodec.Decode(packed);
        return RuneStoneStatCodec.Encode(newStr, dex, vit, intel);
    }

    /// <summary><c>ChangeIUValueRune</c> -- overwrites the DEX position with <paramref name="newDex" />, preserving STR/VIT/INT.</summary>
    public static int ChangeDexValueRune(int packed, sbyte newDex)
    {
        var (str, _, vit, intel) = RuneStoneStatCodec.Decode(packed);
        return RuneStoneStatCodec.Encode(str, newDex, vit, intel);
    }

    /// <summary><c>ChangeIMValueRune</c> -- overwrites the VIT position with <paramref name="newVit" />, preserving STR/DEX/INT.</summary>
    public static int ChangeVitValueRune(int packed, sbyte newVit)
    {
        var (str, dex, _, intel) = RuneStoneStatCodec.Decode(packed);
        return RuneStoneStatCodec.Encode(str, dex, newVit, intel);
    }

    /// <summary><c>ChangeIZValueRune</c> -- overwrites the INT position with <paramref name="newInt" />, preserving STR/DEX/VIT.</summary>
    public static int ChangeIntValueRune(int packed, sbyte newInt)
    {
        var (str, dex, vit, _) = RuneStoneStatCodec.Decode(packed);
        return RuneStoneStatCodec.Encode(str, dex, vit, newInt);
    }
}
