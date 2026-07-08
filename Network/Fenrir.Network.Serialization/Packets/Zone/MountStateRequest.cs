using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

/// <summary>Case 120 (tier-2→3 upgrade) isn't compiled in EU33 (#ifndef LNW33); falls to default → Quit.</summary>
/// <remarks>
///     CZ_ANIMAL_STATE_SEND (op87). <c>Sort</c> selects the sub-operation: 1=Select, 2=Deselect, 3=Mount,
///     4=Dismount, 5=Delete Mount, 6=Convert Attribute, 7=Delete Rolled Attribute, 8=Transfer Rolled
///     Attribute are all in scope (120 is dead code in the EU33/LNW33 production build, see
///     <c>MountStateResolver</c>'s own remarks -- as are 6/8's own roll/transfer mechanics, pending a
///     dedicated probability-table/formula contract). <c>Value</c> is the garage-slot index for
///     Select/Deselect/Delete Mount, a stat-slot index (1-8) for Convert/Delete/Transfer Attribute, and is
///     otherwise unused for Mount/Dismount (which act on the already-selected/already-mounted index instead).
///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:11819-11866 (sorts 1-4 range validation and the
///     silent-no-op-on-failure pattern shared by every case), :11890-12044 (sorts 5-8).
/// </remarks>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.MountState, ExpectedSize = 17,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct MountStateRequest : IIncomingPacket<MountStateRequest>
{
    public required int Sort { get; init; }
    public required int Value { get; init; }
}
