using System.Buffers.Binary;
using System.Collections.Frozen;

namespace Fenrir.Application.Game.Stats;

/// <summary>
///     The routed-column choice a single (stat-type, gem-type, gem-value) triple resolves to inside the legacy
///     <c>GetSocketType</c> switch: read the effect-table row's primary value column
///     (<c>SOCKET_INFO.mValue03</c>), read its secondary value column (<c>mValue04</c>), or contribute nothing.
/// </summary>
public enum GemSocketColumn
{
    None = 0,
    Primary = 1,
    Secondary = 2
}

/// <summary>
///     The eight requested derived-stat codes any live-or-guarded caller passes to <c>GetSocketInfo</c> as its
///     <c>tType</c> selector. Codes 9/10 have no routing branch at all (always contribute nothing) and codes
///     11-19, while they have branches, have no live caller and are not transcribed here -- see
///     <see cref="StatCalculator" />'s gem-socket remarks.
/// </summary>
public enum GemSocketStatKind
{
    AttackPower = 1,
    Defense = 2,
    MaxLife = 3,
    MaxMana = 4,
    AttackSuccess = 5,
    AttackBlock = 6,
    ElementAttack = 7,
    ElementDefense = 8
}

/// <summary>One active socket's decoded (gem-type, gem-value) pair -- <c>SOCKET_GEM_V2</c>'s tN/gN byte fields.</summary>
public readonly record struct GemSocketSlot(byte GemType, byte GemValue);

public static partial class StatCalculator
{
    // ================================================================================================
    //  Workstream B13 -- gem-socket stat routing (the "which effect-table column does this gem read"
    //  decision the legacy GetSocketType switch makes), plus the SOCKET_GEM_V2 decode and per-item
    //  aggregation the AttackPower recompute folds in.
    //
    //  Réf. C++ :
    //   - Server/ts25zone/S07_MyGame03.cpp:10150-10464 -- GetSocketType, the full stat-type/gem-type/gem-value
    //     routing switch. :10159-10172 (stat type 1, Attack -- the ONLY branch live in the shipped ReleaseEU33
    //     build); :10173-10312 (stat types 2-8, dead under USE_SOCKET_GEM); :10313-10438 (stat types 11-19,
    //     no live caller -- NOT transcribed here, see Open questions); :10440-10461 (the shared column pick and
    //     the row lookup that returns mValue03/mValue04 or nothing).
    //   - Server/ts25zone/GameSystem/GameSystem_08_Socket.cpp:22-32 -- effect-table row lookup: reject a query
    //     whose type OR value is below 1, else return the first row matching BOTH type and value; the row's
    //     mValue01 field is loaded but is never a lookup key.
    //   - Server/ts25zone/S07_MyGame03.cpp:10107-10148 -- GetSocketInfo: packs 3 ints into SOCKET_GEM_V2, early
    //     -exits on a zero count, loops only the first `num` positions, skips a zero gem-type without a lookup.
    //   - Server/Header/Protocol/STRUCT.h:1882-1895 -- SOCKET_GEM_V2's 12-byte layout (unused byte, count byte,
    //     five (type,value) byte pairs); :1970-1977 -- SOCKET_INFO (type + mValue01..mValue04).
    //   - Server/Header/Protocol/MyFactor.cpp:4031-4035 -- the AttackPower GetSocketInfo(1,...) call site,
    //     unguarded (the only one of the eight not wrapped in USE_SOCKET_GEM).
    //   - Server/Header/Protocol/DEFINE.h:54 / :103,107 -- USE_SOCKET_GEM #define then #undef inside the LNW33
    //     block, so it is NOT defined in either shipped config (M33 / EU33): only the Attack-Power READ is live;
    //     both socket WRITE opcodes (98, 121) are unregistered and out of scope here.
    //
    //  Magnitudes are NOT hardcoded here (contract Open question 1: the per-row primary/secondary values are
    //  data-file-driven and unrecoverable from source). The routing decides only which COLUMN is read; the
    //  actual number comes from the injected world.GemSockets effect table (GemSocketRowDto.Value03/Value04),
    //  keyed by (Type, Value02). NEVER invent a magnitude in this file.
    // ================================================================================================

    /// <summary><c>SOCKET_GEM_V2</c>'s own cap -- t1..t5/g1..g5.</summary>
    public const int MaxSocketsPerItem = 5;

    // ---- gem type 1 ("attribute" gem): the (value) triples, grouped by the paired stat they feed. One attribute
    //      gem value routes to the PRIMARY column of one stat and the SECONDARY column of its partner (HP/MP,
    //      Hit/Dodge, ElementAtk/ElementDef), which is why the paired sets are shared, not duplicated. ----

    /// <summary>Attribute-gem values feeding Attack (primary). No paired stat. (S07_MyGame03.cpp:10159-10172)</summary>
    private static readonly FrozenSet<byte> AttributeAttackValues = new byte[] { 1, 6, 11 }.ToFrozenSet();

    /// <summary>Attribute-gem values feeding Defense (primary). (S07_MyGame03.cpp:10173-10193)</summary>
    private static readonly FrozenSet<byte> AttributeDefenseValues = new byte[] { 2, 7, 12 }.ToFrozenSet();

    /// <summary>Attribute-gem values feeding MaxLife (primary) AND MaxMana (secondary). (:10194-10235)</summary>
    private static readonly FrozenSet<byte> AttributeLifeManaValues = new byte[] { 3, 8, 13 }.ToFrozenSet();

    /// <summary>Attribute-gem values feeding Hit (primary) AND Dodge (secondary). (:10236-10277)</summary>
    private static readonly FrozenSet<byte> AttributeHitDodgeValues = new byte[] { 5, 10, 15 }.ToFrozenSet();

    /// <summary>Attribute-gem values feeding Element-Attack (primary) AND Element-Defense (secondary). (:10278-10312)</summary>
    private static readonly FrozenSet<byte> AttributeElementValues = new byte[] { 4, 9, 14 }.ToFrozenSet();

    // ---- gem types 2-29 (single/mixed gems): the gem-TYPE sets per (stat, column). gem-value is not consulted
    //      for the routing of these, only for the effect-table lookup key. ----

    private static readonly FrozenSet<byte> AttackPrimaryTypes = new byte[] { 2, 3, 4, 5, 6, 7, 8 }.ToFrozenSet();

    private static readonly FrozenSet<byte> DefensePrimaryTypes = new byte[] { 9, 10, 11, 12, 13, 14 }.ToFrozenSet();

    private static readonly FrozenSet<byte> LifePrimaryTypes = new byte[] { 15, 16, 17, 18, 19 }.ToFrozenSet();
    private static readonly FrozenSet<byte> LifeSecondaryTypes = new byte[] { 3, 9 }.ToFrozenSet();

    private static readonly FrozenSet<byte> ManaPrimaryTypes = new byte[] { 20, 21, 22, 23 }.ToFrozenSet();
    private static readonly FrozenSet<byte> ManaSecondaryTypes = new byte[] { 4, 10, 15 }.ToFrozenSet();

    private static readonly FrozenSet<byte> HitPrimaryTypes = new byte[] { 24, 25, 26 }.ToFrozenSet();
    private static readonly FrozenSet<byte> HitSecondaryTypes = new byte[] { 5, 11, 16, 20 }.ToFrozenSet();

    private static readonly FrozenSet<byte> DodgePrimaryTypes = new byte[] { 27, 28 }.ToFrozenSet();
    private static readonly FrozenSet<byte> DodgeSecondaryTypes = new byte[] { 6, 12, 17, 21, 24 }.ToFrozenSet();

    private static readonly FrozenSet<byte> ElementAttackSecondaryTypes =
        new byte[] { 7, 13, 18, 22, 25, 27 }.ToFrozenSet();

    private static readonly FrozenSet<byte> ElementDefenseSecondaryTypes =
        new byte[] { 8, 14, 19, 23, 26, 28, 29 }.ToFrozenSet();

    /// <summary>
    ///     True only for <see cref="GemSocketStatKind.AttackPower" />. In the shipped ReleaseEU33/ReleaseM33
    ///     build only the Attack-Power socket-aggregation loop is compiled (all seven siblings are wrapped in the
    ///     undefined-in-prod <c>USE_SOCKET_GEM</c> guard) -- so routing a socket contribution into any other stat
    ///     would replicate dead legacy code, not close a parity gap.
    /// </summary>
    public static bool IsGemSocketStatLiveInProduction(GemSocketStatKind statKind)
    {
        return statKind == GemSocketStatKind.AttackPower;
    }

    /// <summary>
    ///     Resolves which effect-table value column one gem contributes to a given derived stat, reproducing the
    ///     legacy <c>GetSocketType</c> switch. gem-type 1 is the "attribute" gem whose <paramref name="gemValue" />
    ///     (an attribute id) selects the branch; gem-types 2-29 route on <paramref name="gemType" /> alone.
    ///     Returns <see cref="GemSocketColumn.None" /> for any combination the legacy switch leaves unhandled
    ///     (including every stat kind whose branches are not transcribed here).
    /// </summary>
    public static GemSocketColumn ResolveGemSocketColumn(GemSocketStatKind statKind, byte gemType, byte gemValue)
    {
        return statKind switch
        {
            GemSocketStatKind.AttackPower => ResolveAttack(gemType, gemValue),
            GemSocketStatKind.Defense => ResolveDefense(gemType, gemValue),
            GemSocketStatKind.MaxLife => ResolveMaxLife(gemType, gemValue),
            GemSocketStatKind.MaxMana => ResolveMaxMana(gemType, gemValue),
            GemSocketStatKind.AttackSuccess => ResolveAttackSuccess(gemType, gemValue),
            GemSocketStatKind.AttackBlock => ResolveAttackBlock(gemType, gemValue),
            GemSocketStatKind.ElementAttack => ResolveElementAttack(gemType, gemValue),
            GemSocketStatKind.ElementDefense => ResolveElementDefense(gemType, gemValue),
            _ => GemSocketColumn.None
        };
    }

    // Stat type 1, Attack -- LIVE. Attribute triple {1,6,11} -> primary; single/mixed types 2-8 -> primary; else none.
    private static GemSocketColumn ResolveAttack(byte gemType, byte gemValue)
    {
        if (gemType == 1)
            return AttributeAttackValues.Contains(gemValue) ? GemSocketColumn.Primary : GemSocketColumn.None;
        return AttackPrimaryTypes.Contains(gemType) ? GemSocketColumn.Primary : GemSocketColumn.None;
    }

    // Stat type 2, Defense (dead). Attribute {2,7,12} -> primary; types 9-14 -> primary; type 2 -> secondary.
    private static GemSocketColumn ResolveDefense(byte gemType, byte gemValue)
    {
        if (gemType == 1)
            return AttributeDefenseValues.Contains(gemValue) ? GemSocketColumn.Primary : GemSocketColumn.None;
        if (DefensePrimaryTypes.Contains(gemType)) return GemSocketColumn.Primary;
        if (gemType == 2) return GemSocketColumn.Secondary;
        return GemSocketColumn.None;
    }

    // Stat type 3, MaxLife (dead). Attribute {3,8,13} -> primary; types 15-19 -> primary; types 3/9 -> secondary.
    private static GemSocketColumn ResolveMaxLife(byte gemType, byte gemValue)
    {
        if (gemType == 1)
            return AttributeLifeManaValues.Contains(gemValue) ? GemSocketColumn.Primary : GemSocketColumn.None;
        if (LifePrimaryTypes.Contains(gemType)) return GemSocketColumn.Primary;
        if (LifeSecondaryTypes.Contains(gemType)) return GemSocketColumn.Secondary;
        return GemSocketColumn.None;
    }

    // Stat type 4, MaxMana (dead). Attribute {3,8,13} -> secondary (the HP+MP pair); types 20-23 -> primary;
    // types 4/10/15 -> secondary.
    private static GemSocketColumn ResolveMaxMana(byte gemType, byte gemValue)
    {
        if (gemType == 1)
            return AttributeLifeManaValues.Contains(gemValue) ? GemSocketColumn.Secondary : GemSocketColumn.None;
        if (ManaPrimaryTypes.Contains(gemType)) return GemSocketColumn.Primary;
        if (ManaSecondaryTypes.Contains(gemType)) return GemSocketColumn.Secondary;
        return GemSocketColumn.None;
    }

    // Stat type 5, Hit (dead). Attribute {5,10,15} -> primary; types 24-26 -> primary; types 5/11/16/20 -> secondary.
    private static GemSocketColumn ResolveAttackSuccess(byte gemType, byte gemValue)
    {
        if (gemType == 1)
            return AttributeHitDodgeValues.Contains(gemValue) ? GemSocketColumn.Primary : GemSocketColumn.None;
        if (HitPrimaryTypes.Contains(gemType)) return GemSocketColumn.Primary;
        if (HitSecondaryTypes.Contains(gemType)) return GemSocketColumn.Secondary;
        return GemSocketColumn.None;
    }

    // Stat type 6, Dodge (dead). Attribute {5,10,15} -> secondary (paired with Hit); types 27/28 -> primary;
    // types 6/12/17/21/24 -> secondary.
    private static GemSocketColumn ResolveAttackBlock(byte gemType, byte gemValue)
    {
        if (gemType == 1)
            return AttributeHitDodgeValues.Contains(gemValue) ? GemSocketColumn.Secondary : GemSocketColumn.None;
        if (DodgePrimaryTypes.Contains(gemType)) return GemSocketColumn.Primary;
        if (DodgeSecondaryTypes.Contains(gemType)) return GemSocketColumn.Secondary;
        return GemSocketColumn.None;
    }

    // Stat type 7, Element-Attack (dead). Attribute {4,9,14} -> primary; type 29 -> primary;
    // types 7/13/18/22/25/27 -> secondary.
    private static GemSocketColumn ResolveElementAttack(byte gemType, byte gemValue)
    {
        if (gemType == 1)
            return AttributeElementValues.Contains(gemValue) ? GemSocketColumn.Primary : GemSocketColumn.None;
        if (gemType == 29) return GemSocketColumn.Primary;
        if (ElementAttackSecondaryTypes.Contains(gemType)) return GemSocketColumn.Secondary;
        return GemSocketColumn.None;
    }

    // Stat type 8, Element-Defense (dead). Attribute {4,9,14} -> secondary (paired with Element-Attack);
    // types 8/14/19/23/26/28/29 -> secondary. No primary branch.
    private static GemSocketColumn ResolveElementDefense(byte gemType, byte gemValue)
    {
        if (gemType == 1)
            return AttributeElementValues.Contains(gemValue) ? GemSocketColumn.Secondary : GemSocketColumn.None;
        if (ElementDefenseSecondaryTypes.Contains(gemType)) return GemSocketColumn.Secondary;
        return GemSocketColumn.None;
    }

    /// <summary>
    ///     The composite key for indexing the world.GemSockets effect table by (gem type, gem value) -- i.e. the
    ///     legacy lookup keys (<c>SOCKET_INFO.Type</c>, <c>SOCKET_INFO.Value02</c>). Whatever builds the injected
    ///     <c>FrozenDictionary</c> MUST key it with this exact function, first-row-wins by GemSocketId to match the
    ///     legacy "first matching row" search.
    /// </summary>
    public static int GemSocketTypeValueKey(byte gemType, byte gemValue)
    {
        return (gemType << 8) | gemValue;
    }

    /// <summary>
    ///     Resolves one gem's whole-number contribution to <paramref name="statKind" />: picks the column via
    ///     <see cref="ResolveGemSocketColumn" />, then reads that column (<c>Value03</c> primary / <c>Value04</c>
    ///     secondary) from the matching <c>world.GemSockets</c> row. Returns 0 when nothing is routed, when the
    ///     legacy below-1 type/value guard rejects the query, or when the (type,value) row is absent from
    ///     <paramref name="effectTable" />.
    /// </summary>
    /// <remarks>
    ///     An absent (type,value) row is an unguarded null-pointer dereference (a crash) in the legacy server when
    ///     a column WAS routed (S07_MyGame03.cpp:10440-10461); this deliberately does not reproduce that crash --
    ///     it returns 0 instead. That is a documented hardening deviation from legacy parity, not an oversight;
    ///     route the underlying robustness defect to security review rather than replicating it.
    /// </remarks>
    public static int ResolveGemSocketValue(GemSocketStatKind statKind, byte gemType, byte gemValue,
        FrozenDictionary<int, GemSocketRowDto> effectTable)
    {
        var column = ResolveGemSocketColumn(statKind, gemType, gemValue);
        if (column == GemSocketColumn.None) return 0;

        // Legacy GSOCKET::Search rejects a query whose type or value is below 1 before matching any row.
        if (gemType < 1 || gemValue < 1) return 0;

        if (!effectTable.TryGetValue(GemSocketTypeValueKey(gemType, gemValue), out var row))
            return 0;

        return column == GemSocketColumn.Primary ? row.Value03 : row.Value04;
    }

    /// <summary>
    ///     Decodes a <c>SOCKET_GEM_V2</c> record (12 bytes packed little-endian into 3 ints: an unused byte, an
    ///     active-count byte, then five (type,value) byte pairs) into <paramref name="destination" /> and returns
    ///     the active-socket count. The count is capped at <see cref="MaxSocketsPerItem" /> (the legacy read path
    ///     itself does NOT clamp and over-reads its own working array when the count exceeds 5 --
    ///     S07_MyGame03.cpp:10111-10117,10139 -- a robustness defect deliberately not reproduced here).
    ///     <paramref name="destination" /> must be at least <see cref="MaxSocketsPerItem" /> long.
    /// </summary>
    public static int DecodeSocketGemV2(int packed1, int packed2, int packed3, Span<GemSocketSlot> destination)
    {
        Span<byte> bytes = stackalloc byte[12];
        BinaryPrimitives.WriteInt32LittleEndian(bytes[..4], packed1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.Slice(4, 4), packed2);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.Slice(8, 4), packed3);

        var count = Math.Min((int)bytes[1], MaxSocketsPerItem);
        for (var i = 0; i < count; i++)
            destination[i] = new GemSocketSlot(bytes[2 + i * 2], bytes[3 + i * 2]);

        return count;
    }

    /// <summary>
    ///     Sums one equipped item's gem-socket contribution to <paramref name="statKind" />: decodes its packed
    ///     <c>SOCKET_GEM_V2</c> blob and folds <see cref="ResolveGemSocketValue" /> across the first <c>num</c>
    ///     active sockets, skipping any socket whose gem type is zero without a lookup (mirroring the legacy
    ///     <c>if (a2[i][0]) val += GetSocketType(...)</c> guard). A zero count contributes 0.
    /// </summary>
    /// <remarks>
    ///     Only <see cref="GemSocketStatKind.AttackPower" /> is <see cref="IsGemSocketStatLiveInProduction" />;
    ///     callers must not fold any other stat kind's result into a live stat (see this class's gem-socket
    ///     remarks / <c>StatCalculator.GemSocketContribution.cs</c>).
    /// </remarks>
    public static int SumGemSocketContribution(GemSocketStatKind statKind, int packed1, int packed2, int packed3,
        FrozenDictionary<int, GemSocketRowDto> effectTable)
    {
        Span<GemSocketSlot> slots = stackalloc GemSocketSlot[MaxSocketsPerItem];
        var count = DecodeSocketGemV2(packed1, packed2, packed3, slots);

        var total = 0;
        for (var i = 0; i < count; i++)
        {
            var slot = slots[i];
            if (slot.GemType == 0) continue;
            total += ResolveGemSocketValue(statKind, slot.GemType, slot.GemValue, effectTable);
        }

        return total;
    }
}
