using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Abstractions.ZoneLifecycle;

/// <summary>Business logic for CZ_PROCESS_ATTACK_SEND (op18) -- see <c>AttackHandler</c>'s remarks.</summary>
public interface IAttackService
{
    public bool IsValidCase(int caseValue);

    public void PostAttack(Zone zone, int characterId, in AttackForProtocol attackInfo);
}
