namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public sealed record AlliancePostSite(
    short MapId,
    float Post0X,
    float Post0Z,
    float Post1X,
    float Post1Z,
    float Radius);

public static class AlliancePostOccupantScanner
{
    public static (AllianceCeremonyCandidate? PostOne, AllianceCeremonyCandidate? PostTwo) Scan(Zone zone,
        AlliancePostSite site)
    {
        var postOne = FindQualifyingLeader(zone, site.Post0X, site.Post0Z, site.Radius);
        var postTwo = FindQualifyingLeader(zone, site.Post1X, site.Post1Z, site.Radius);
        return (postOne, postTwo);
    }

    private static AllianceCeremonyCandidate? FindQualifyingLeader(Zone zone, float postX, float postZ,
        float radius)
    {
        var radiusSq = radius * radius;

        foreach (var player in zone.Players)
        {
            if (player.TribeRole != 1)
                continue;
            if (player.IsDead)
                continue;
            if (player.IsMovingZone)
                continue;
            if (DistanceSquared(player.PosX, player.PosZ, postX, postZ) > radiusSq)
                continue;

            return new AllianceCeremonyCandidate(player.CharacterId, player.Tribe);
        }

        return null;
    }

    private static float DistanceSquared(float x1, float z1, float x2, float z2)
    {
        var dx = x1 - x2;
        var dz = z1 - z2;
        return dx * dx + dz * dz;
    }
}
