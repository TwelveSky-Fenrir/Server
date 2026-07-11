namespace Fenrir.Application.Game.Domain.World.Loot;

/// <summary>
///     Partial-update ("mirror-image encode direction") counterparts to <see cref="ItemValueCodec" />'s
///     full-rebuild <see cref="ItemValueCodec.Encode" />/<see cref="ItemValueCodec.Decode" /> pair -- one
///     function per legacy encoder in the Change/Reset/Set catalog, each touching exactly one of the four
///     packed byte positions (byte0 = Enchant/"IS", byte1 = Combine/"IU", byte2 = Refine/"IM", byte3 =
///     Socket-Etc/"IZ") and leaving the other three exactly as they were.
/// </summary>
/// <remarks>
///     Réf. C++ (wave13/B5-rune-encoders contract):
///     <para>
///         Server/Header/function.h:345-363 -- <c>ChangeISValue</c>/<c>ChangeIUValue</c>: additive,
///         delta-add-onto-existing-byte encoders for Enchant/Combine (see <see cref="ChangeEnchant" />/
///         <see cref="ChangeCombine" />). Used by the general cumulative Improve/Combine progression systems
///         -- NOT the same family as the rune-stone-crafting overwrite encoders (see
///         <c>Fenrir.Application.Game.Domain.Crafting.RuneStoneStatEncoder</c>'s own remarks for why the two
///         "Change"-named families must never be conflated: this family adds a delta onto the existing byte,
///         that family overwrites it outright).
///     </para>
///     <para>
///         Server/Header/function.h:365-393 -- <c>ResetISValue</c>/<c>ResetIUValue</c>/<c>ResetIMValue</c>:
///         zero-one-position, preserve-the-rest encoders (see <see cref="ResetEnchant" />/
///         <see cref="ResetCombine" />/<see cref="ResetRefine" />).
///     </para>
///     <para>
///         Server/Header/function.h:395-403 -- <c>SetIMValue</c>: direct-assign Refine encoder (see
///         <see cref="SetRefine" />). The contract's own finding reports zero call sites anywhere across the
///         nine executables -- kept here for catalog completeness only, not because a live caller exists.
///     </para>
///     <para>
///         Server/Header/function.h:405-413 -- <c>ChangeIMValue</c>: the additive Refine-position encoder
///         (see <see cref="ChangeRefine" />); its second parameter is named differently from
///         <c>SetIMValue</c>'s in the legacy source, confirmed cosmetic-only by matching byte position, not
///         behavior.
///     </para>
///     <para>
///         Server/Header/function.h:415-425 -- <c>SetISIUIMValue</c>: the full-rebuild encoder (see
///         <see cref="SetAll" />) used by the (legacy-only, see below) starter-equip stamping step. Its local
///         working buffer is default-zero-initialized and the prior packed value is never read into it at
///         all -- unlike every other encoder in this family, it is NOT a partial update.
///     </para>
///     <para>
///         Server/Header/function.h:427-447 -- <c>SetIZValue</c>/<c>ResetIZValue</c> (see
///         <see cref="SetSocket" />/<see cref="ResetSocket" />): the ONLY two encoders touching byte3
///         (Socket/Etc) anywhere in the traced source -- no additive <c>ChangeIZValue</c> exists in the
///         legacy catalog at all (a pre-existing asymmetry in the legacy function catalog, not an omission
///         here; do not add one). Per the contract's own finding, both of these, together with
///         <see cref="SetRefine" /> and <see cref="ResetRefine" />, are defined in the legacy source but have
///         no call site anywhere across any of the nine executables.
///     </para>
///     <para>
///         Round-trip-safe input range for every method here (and for each of <see cref="SetAll" />'s
///         discrete parameters): -128..127. A value outside it silently truncates to its low 8 bits on write
///         and decodes back as a different, sign-shifted number -- no method here validates or clamps this,
///         exactly mirroring every traced legacy call site (none of which was found supplying an
///         out-of-range value).
///     </para>
///     <para>
///         <b>Starter-equip stamping is deliberately NOT wired to any production call site by this change.</b>
///         The contract's own Trigger A describes the EU33 <c>USE_CUSTOME_CREATE</c> elite-gear stamp
///         (<c>SetISIUIMValue(45, 6, 0, 0)</c> on 6 equip slots, <c>SetISIUIMValue(40, 0, 0, 0)</c> on the
///         cosmetic wing slot -- Server/ts25login/S04_MyWork02.cpp:1100-1168). Current
///         <c>Fenrir.Application.Login.Services.CreateAvatar.CreateAvatarService</c> already replaced that
///         entire grant with an explicit, separately documented product decision (the
///         "character-creation-level1-redesign" workflow, see that class's own &lt;remarks&gt;) that a fresh
///         character starts unenchanted/uncombined instead. Reintroducing the elite stamp here would silently
///         reverse an already-shipped, deliberately-made product decision, so <see cref="SetAll" /> is
///         provided purely as a general-purpose reusable primitive (and is exercised against the exact legacy
///         literals in this type's own tests, for specification purposes only) -- it is not called from
///         <c>CreateAvatarService</c>.
///     </para>
/// </remarks>
public static class ItemStateEncoder
{
    /// <summary><c>ChangeISValue</c> -- adds <paramref name="delta" /> onto the existing Enchant byte, preserving Combine/Refine/Socket.</summary>
    public static int ChangeEnchant(int packed, int delta)
    {
        return SetByte(packed, 0, GetByte(packed, 0) + delta);
    }

    /// <summary><c>ChangeIUValue</c> -- adds <paramref name="delta" /> onto the existing Combine byte, preserving Enchant/Refine/Socket.</summary>
    public static int ChangeCombine(int packed, int delta)
    {
        return SetByte(packed, 1, GetByte(packed, 1) + delta);
    }

    /// <summary><c>ChangeIMValue</c> -- adds <paramref name="delta" /> onto the existing Refine byte, preserving Enchant/Combine/Socket.</summary>
    public static int ChangeRefine(int packed, int delta)
    {
        return SetByte(packed, 2, GetByte(packed, 2) + delta);
    }

    /// <summary><c>ResetISValue</c> -- zeroes the Enchant byte, preserving Combine/Refine/Socket.</summary>
    public static int ResetEnchant(int packed)
    {
        return SetByte(packed, 0, 0);
    }

    /// <summary><c>ResetIUValue</c> -- zeroes the Combine byte, preserving Enchant/Refine/Socket.</summary>
    public static int ResetCombine(int packed)
    {
        return SetByte(packed, 1, 0);
    }

    /// <summary><c>ResetIMValue</c> -- zeroes the Refine byte, preserving Enchant/Combine/Socket. No call site found anywhere in the traced source.</summary>
    public static int ResetRefine(int packed)
    {
        return SetByte(packed, 2, 0);
    }

    /// <summary><c>SetIMValue</c> -- overwrites the Refine byte outright with <paramref name="value" />, preserving Enchant/Combine/Socket. No call site found anywhere in the traced source.</summary>
    public static int SetRefine(int packed, int value)
    {
        return SetByte(packed, 2, value);
    }

    /// <summary><c>SetIZValue</c> -- overwrites the Socket/Etc byte outright with <paramref name="value" />, preserving Enchant/Combine/Refine. No call site found anywhere in the traced source.</summary>
    public static int SetSocket(int packed, int value)
    {
        return SetByte(packed, 3, value);
    }

    /// <summary><c>ResetIZValue</c> -- zeroes the Socket/Etc byte, preserving Enchant/Combine/Refine. No call site found anywhere in the traced source.</summary>
    public static int ResetSocket(int packed)
    {
        return SetByte(packed, 3, 0);
    }

    /// <summary>
    ///     <c>SetISIUIMValue</c> -- the full-rebuild encoder: discards whatever packed value previously
    ///     existed and builds a brand-new one from these four discrete values (identical in shape to
    ///     <see cref="ItemValueCodec.Encode" />, exposed under this family's own Change/Reset/Set naming for
    ///     callers reasoning about the catalog as a whole). <paramref name="socket" /> defaults to 0 when the
    ///     caller omits it, matching the legacy local working buffer's default-zero-initialization.
    /// </summary>
    public static int SetAll(int enchant, int combine, int refine, int socket = 0)
    {
        return ItemValueCodec.Encode((byte)enchant, (byte)combine, (byte)refine, (byte)socket);
    }

    private static sbyte GetByte(int packed, int position)
    {
        return (sbyte)(packed >> (position * 8));
    }

    private static int SetByte(int packed, int position, int rawValue)
    {
        var shift = position * 8;
        var clearMask = ~(0xFF << shift);
        return (packed & clearMask) | ((rawValue & 0xFF) << shift);
    }
}
