using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_AUTO_CONFIG_SEND (CLIENT.h:412-416, <c>S_AUTO_CONFIG_SEND</c> CLIENT.h:687) — handler
///     <c>W_AUTO_CONFIG_SEND</c> (S04_MyWork02.cpp:13466). <c>tSort</c> must be 0 (disable) or 1
///     (enable), else <c>Quit()</c>. Enabling is refused (<c>Quit()</c>) in zones 38/319/320/321/322/323,
///     in zone 241, in level-gated battle zones, or without an equipped weapon / configured attack skill
///     (<c>attack_type[0][0]</c>/<c>[1][0]</c> null) — l.13508-13614. On success,
///     <see cref="AutoHunt" /> is copied verbatim (<c>CopyMemory</c>, NO content validation — anti-cheat
///     surface to handle server-side in Fenrir) and ZC 123 replies (<c>tAutoState = tSort</c>, live
///     call at l.13613).
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.AutoHuntToggle, ExpectedSize = 125,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct AutoHuntToggleRequest : IIncomingPacket<AutoHuntToggleRequest>
{
    public required int Sort { get; init; }
    public required AutoHunt AutoHunt { get; init; }
}
