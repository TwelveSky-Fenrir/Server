using Fenrir.Application.Game.Combat;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Packets.Shared;

namespace Fenrir.Application.Game.ZoneLifecycle.Services;

/// <summary>Business logic for CZ_PROCESS_ATTACK_SEND (op18) -- see <c>AttackHandler</c>'s remarks.</summary>
public interface IAttackService
{
    bool IsValidCase(int caseValue);

    void PostAttack(Zone zone, int characterId, in AttackForProtocol attackInfo);
}

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
