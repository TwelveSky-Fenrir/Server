using Fenrir.Application.Game.Domain.Simulation;

namespace Fenrir.Application.Game.Domain.World.Loot;

public sealed record GroundItemEntity(
    int ServerIndex,
    uint UniqueNumber,
    int ItemId,
    int Quantity,
    int Value,
    int SerialNumber,
    float PosX,
    float PosY,
    float PosZ,
    string Master,
    string PartyName,
    int DropSort,
    TimeSpan CreatedAtZoneClock,
    int SocketGem1,
    int SocketGem2,
    int SocketGem3,
    int? InstanceId = null)
{
    public const int MonsterKillDropSort = 1;

    public const int ManualGroundDropSort = 2;

    public const int GmCreateItemDropSort = 13;

    public bool IsExpired(TimeSpan nowZoneClock)
    {
        return nowZoneClock - CreatedAtZoneClock >= SimulationClock.GroundItemLifetime;
    }

    public bool IsClaimableBy(string claimantName, string? claimantPartyName, TimeSpan nowZoneClock)
    {
        if (IsExpired(nowZoneClock) || nowZoneClock - CreatedAtZoneClock >= SimulationClock.GroundItemFreeForAllDelay)
            return true;

        if (string.IsNullOrEmpty(Master))
            return true;

        if (string.Equals(Master, claimantName, StringComparison.Ordinal))
            return true;

        if (DropSort == MonsterKillDropSort && !string.IsNullOrEmpty(claimantPartyName) &&
            string.Equals(PartyName, claimantPartyName, StringComparison.Ordinal) &&
            nowZoneClock - CreatedAtZoneClock >= SimulationClock.GroundItemPartyShareDelay)
            return true;

        if (DropSort == ManualGroundDropSort && !string.IsNullOrEmpty(claimantPartyName) &&
            string.Equals(Master, claimantPartyName, StringComparison.Ordinal))
            return true;

        return false;
    }
}
