using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

/// <summary>
///     Re-read layer over CZ_PROCESS_DATA_SEND's (opcode 19, <see cref="GenericActionRequest" />) tData blob for
///     tSort 333, legacy's zone-335 FFA event start command (Server/ts25zone/S04_MyWork04.cpp:1097-1131). There
///     is no dedicated legacy wire opcode for this command -- it is multiplexed inside the same generic
///     envelope every other <see cref="GenericActionRequest" /> tSort uses (including sitting outside the
///     numeric block the other tier-10 sub-commands occupy, a source-organization detail with no behavioral
///     effect), so this is an embedded payload (<see cref="IFenrirWireType{TSelf}" />), never a standalone
///     <c>[FenrirPacket]</c>.
///     <para>
///         Single-field layout (Time at offset 0, no other fields) is inferred from the cited case body, which
///         reads exactly one value off the inline request struct before the idle-state guard and the
///         duration-to-ticks assignment (S04_MyWork04.cpp:1097-1131). Same "inferred from the accepted decode,
///         not independently byte-offset-confirmed" posture as <see cref="GmBlockAvatarPayload" />'s own remarks
///         -- flag for <c>cpp-protocol-header-analyst</c> re-check if a future change needs field-exact
///         confirmation.
///     </para>
/// </summary>
[FenrirWireType(4)]
public readonly record struct GmFfaEventStartPayload : IFenrirWireType<GmFfaEventStartPayload>
{
    /// <summary>
    ///     Requested duration in minutes. Any value (including zero/negative/unbounded-positive) is
    ///     syntactically accepted. Verified finding: this value has zero observable effect on the FFA event's
    ///     actual timing under any circumstance -- it is unconditionally overwritten before ever being read (see
    ///     the behavior contract's Side effects). Do not treat this as a real "set duration" input.
    /// </summary>
    public required int Time { get; init; }
}
