using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_CHANGE_AUTO_INFO (CLIENT.h:386-390, <c>S_CHANGE_AUTO_INFO</c> CLIENT.h:661) — handler
///     <c>W_CHANGE_AUTO_INFO</c> (S04_MyWork02.cpp:11792). Any value outside 0..5 on either field →
///     <c>Quit()</c> (l.11794-11798). Stores <c>aAutoLifeRatio</c>/<c>aAutoManaRatio</c> (auto-potion HP/MP
///     thresholds). No ZC reply — silent handler.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.AutoPotionThreshold, ExpectedSize = 17,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct AutoPotionThresholdRequest : IIncomingPacket<AutoPotionThresholdRequest>
{
    public required int Value01 { get; init; }
    public required int Value02 { get; init; }
}
