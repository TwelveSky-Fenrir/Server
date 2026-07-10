using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

/// <summary>
///     Re-read layer over CZ_PROCESS_DATA_SEND's (opcode 19, <see cref="GenericActionRequest" />) tData blob for
///     tSort 503, legacy's "[GM]-EXP" command (Server/ts25zone/S04_MyWork04.cpp:959-1005). There is no dedicated
///     legacy wire opcode for GM-EXP -- it is multiplexed inside the same generic envelope every other
///     <see cref="GenericActionRequest" /> tSort uses, so this is an embedded payload
///     (<see cref="IFenrirWireType{TSelf}" />), never a standalone <c>[FenrirPacket]</c>.
///     <para>
///         Field order (Type then Exp, back-to-back, no other fields) is inferred from the two values' read
///         order in the cited case body -- the tier gate at the top of the case runs before either field is
///         touched, then Type selects the branch and Exp supplies the amount, in that sequence
///         (S04_MyWork04.cpp:959-1005). The behavior contract backing this struct does not itself state a
///         byte-exact offset table for this inline request struct (only Server/Header/Protocol/STRUCT.h has
///         those for named, non-inline structs), so this is the same "inferred from the accepted decode order,
///         not independently re-derived" posture <see cref="GmBlockAvatarPayload" /> already documents for its
///         own inline tSort request shape -- flag for <c>cpp-protocol-header-analyst</c> re-check if a future
///         change needs field-exact confirmation rather than inferred-from-read-order.
///     </para>
/// </summary>
[FenrirWireType(8)]
public readonly partial record struct GmExpGrantPayload : IFenrirWireType<GmExpGrantPayload>
{
    /// <summary>
    ///     0 = general character-level experience; 1 = pet-experience (mislabeled "God 1 - Rebirth 12" in
    ///     the legacy source comment -- see the behavior contract's Side effects for why that label is wrong).
    ///     Any other value is syntactically legal on the wire but produces no effect.
    /// </summary>
    public required int Type { get; init; }

    /// <summary>
    ///     Full 32-bit signed range, including negative and overflow-sized values -- no wire-level bound.
    ///     Only an upper-bound clamp exists, and only inside the shared experience-grant routine this command
    ///     calls into (not enforced here).
    /// </summary>
    public required int Exp { get; init; }
}
