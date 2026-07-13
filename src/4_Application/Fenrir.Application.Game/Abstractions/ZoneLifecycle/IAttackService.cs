using Fenrir.Application.Game.Domain.World;
using Fenrir.Core.Packets.Shared;

namespace Fenrir.Application.Game.Abstractions.ZoneLifecycle;

public interface IAttackService
{
    public bool IsValidCase(int caseValue);

    public void PostAttack(Zone zone, int characterId, in AttackForProtocol attackInfo);
}
