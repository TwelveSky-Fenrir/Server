using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_AVATAR_CHANGE_INFO_2 (ZONE.h:453-460, 12-byte payload) — the self personal-stat update packet
///     (~180 live call sites: HP, MP, money, pet exp, hero point, ...). <c>USE_PREMIUM_LONGTIME</c> is ON in EU33
///     (DEFINE.h:63), so <c>tValue2</c> IS present (a build without it/<c>MANGOT5</c> would be 8 bytes payload,
///     not 12 — locked here). Unicast to self.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.AvatarStatUpdate, ExpectedSize = 13)]
public readonly partial record struct AvatarStatUpdateResponse : IOutgoingPacket
{
    /// <summary>Stat code (S010CHARACTER_HP, S011CHARACTER_MP, S019MONEY, S014PET_EXP, S904UPDATE_HERO_POINT, ...).</summary>
    public required int Sort { get; init; }

    public required int Value { get; init; }

    /// <summary>Present due to <c>USE_PREMIUM_LONGTIME</c>; 0 in most emissions.</summary>
    public required int Value2 { get; init; }
}
