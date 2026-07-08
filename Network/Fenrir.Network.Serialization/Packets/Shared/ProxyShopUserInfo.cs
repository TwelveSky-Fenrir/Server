using Fenrir.Network.Serialization.Attributes;

namespace Fenrir.Network.Serialization.Packets.Shared;

[FenrirWireType(824)]
public readonly record struct ProxyShopUserInfo : IFenrirWireType<ProxyShopUserInfo>
{
    [FixedString(13)] public required string AvatarName { get; init; }

    // 3-byte pad after AvatarName (char[13]) before the int-aligned item array.
    [Reserved(3)] [FixedArray(25)] public required ProxyShopItem[] Items { get; init; }

    [FixedArray(75)] public required int[] Sockets { get; init; }

    public required int Money { get; init; }

    public required int BigMoney { get; init; }
}
