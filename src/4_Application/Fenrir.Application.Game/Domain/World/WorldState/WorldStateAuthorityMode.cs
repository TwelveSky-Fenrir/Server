namespace Fenrir.Application.Game.Domain.World.WorldState;

/// <summary>
/// Qui, du shard ou du CenterServer, est l'écrivain autoritaire de l'état monde cross-zone (World/Tribe +
/// HeroRank). Interrupteur atomique unique porté par la config du shard (<c>Game:WorldStateAuthority</c>) : en
/// <see cref="Center"/>, le shard cesse d'écrire ces agrégats en DB, pousse ses events op33 au Center (au lieu du
/// DB-outbox) et reçoit le fan-out du Center ; en <see cref="Shard"/> (défaut), tout se comporte exactement comme
/// avant (shard-autoritaire, DB-outbox). Tower est <b>hors périmètre</b> (écriture partitionnée = shard-autoritaire
/// dans les deux modes — voir ADR-0005). La bascule est réversible par config seule.
/// </summary>
public enum WorldStateAuthorityMode : byte
{
    /// <summary>Le shard est l'écrivain autoritaire (comportement historique). Défaut.</summary>
    Shard = 0,

    /// <summary>Le CenterServer est l'écrivain autoritaire ; le shard devient un miroir alimenté par fan-out TCP.</summary>
    Center = 1
}
