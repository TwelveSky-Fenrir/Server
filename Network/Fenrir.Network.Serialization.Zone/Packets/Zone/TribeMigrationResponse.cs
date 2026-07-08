using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

/// <summary>
///     ZC_CHANGE_TO_TRIBE4_RECV, opcode 40 -- reply to <see cref="TribeMigrationRequest" />
///     (CZ_CHANGE_TO_TRIBE4_SEND, opcode 37). Carries a single, undifferentiated result code: on success
///     (<see cref="Fenrir.Application.Game.Domain.Tribes.TribeMigrationOutcome.Success" />), 0 with no
///     further payload; on the small subset of rejections that reply at all
///     (<see cref="Fenrir.Application.Game.Domain.Tribes.TribeMigrationOutcomeExtensions.RepliesWithFailure" />
///     -- feature disabled server-wide, outside the Saturday 16:00-18:59 window, or a tribe-1 character
///     attempting to join tribe 3), 1, a generic non-zero failure value that does not distinguish which
///     condition failed. Every other rejection tears the session down instead of sending any reply --
///     <see cref="Result" /> is never populated for those paths. Only three gates reply, not the legacy's
///     four -- the fourth (tribe-4 realm reachability, a per-tribe server-number check) has no Fenrir
///     equivalent, since Fenrir runs one shared, map-sharded world rather than one whole game instance per
///     tribe; see <c>TribeMigrationOutcome</c>'s own remarks.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/Header/Protocol/ZONE.h:589-592 (single <c>tResult</c> int, confirming this
///     struct's single-field shape is already byte-exact) and Server/ts25zone/S05_MyTransfer.cpp:720-724
///     (<c>B_CHANGE_TO_TRIBE4_RECV</c> builder). See the <c>legacy-behavior-translator</c> contract
///     "Fourth-tribe (Fujin) conversion and return" for the full gating/eligibility/response-code logic
///     this reply summarizes. This opcode was compiled but unreachable in every shipped legacy build --
///     its handler unconditionally disconnected the client as the very first statement, before any of that
///     logic ran (Server/ts25zone/S04_MyWork02.cpp:7567-7568) -- and is reachable for the first time via
///     <see cref="TribeMigrationRequest" />, handled by
///     <see cref="Fenrir.Application.Game.Handlers.Handlers.Tribes.TribeMigrationHandler" />.
/// </remarks>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.TribeMigration,
    ExpectedSize = 5)]
public readonly partial record struct TribeMigrationResponse : IOutgoingPacket
{
    public required int Result { get; init; }
}
