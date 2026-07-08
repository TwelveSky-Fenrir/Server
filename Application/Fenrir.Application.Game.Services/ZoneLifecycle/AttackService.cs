using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Services.ZoneLifecycle;

public sealed class AttackService : IAttackService
{
    public bool IsValidCase(int caseValue)
    {
        return caseValue is >= 1 and <= 6;
    }

    public void PostAttack(Zone zone, int characterId, in AttackForProtocol attackInfo)
    {
        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = characterId, AttackInfo = attackInfo });
    }
}
