using Fenrir.Core.Packets.Shared;

namespace Fenrir.Application.Game.Domain.Combat;

public readonly struct CombatCommand
{
    public required int AttackerCharacterId { get; init; }
    public required AttackForProtocol AttackInfo { get; init; }
}
