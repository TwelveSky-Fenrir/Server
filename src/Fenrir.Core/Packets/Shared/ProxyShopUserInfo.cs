using Fenrir.Core.Attributes;

namespace Fenrir.Core.Packets.Shared;

[FenrirWireType(824)]
public readonly partial record struct ProxyShopUserInfo : IFenrirWireType<ProxyShopUserInfo>
{
    [FixedString(13)] public required string AvatarName { get; init; }

    [Reserved(3)] [FixedArray(25)] public required ProxyShopItem[] Items { get; init; }

    [FixedArray(75)] public required int[] Sockets { get; init; }

    public required int Money { get; init; }

    public required int BigMoney { get; init; }
}
