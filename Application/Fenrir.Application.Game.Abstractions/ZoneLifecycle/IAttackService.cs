using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Abstractions.ZoneLifecycle;

public interface IAttackService
{
    public bool IsValidCase(int caseValue);

    public void PostAttack(Zone zone, int characterId, in AttackForProtocol attackInfo);
}
