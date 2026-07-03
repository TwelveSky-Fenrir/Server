using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_GM_COMMAND_INFO (ZONE.h:936-940) — 4 + 100 bytes, reuses <c>MAX_TRIBE_WORK_SIZE = 100</c> (NOT
///     the 130-byte <c>MAX_BROADCAST_DATA_SIZE</c> of ZC 94). Builder S05_MyTransfer.cpp:1159; GM
///     command-relay channel, S04_MyWork04 (5 live emissions).
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.GmCommandInfo, ExpectedSize = 105)]
public readonly partial record struct ZcGmCommandInfo : IOutgoingPacket
{
    public required int Sort { get; init; }
    [FixedArray(100)] public required byte[] GmData { get; init; }
}
