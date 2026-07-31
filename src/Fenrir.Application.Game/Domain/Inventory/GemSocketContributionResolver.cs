using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.Inventory;

public enum GemSocketStatRequest
{
    AttackPower = 1,
    Defense = 2,
    Life = 3,
    Mana = 4,
    AttackSuccess = 5,
    AttackBlock = 6,
    ElementalAttack = 7,
    ElementalDefense = 8
}

public static class GemSocketContributionResolver
{
    public const int MaxSocketsPerItem = 5;

    public static bool IsLiveInProduction(GemSocketStatRequest statType)
    {
        return statType == GemSocketStatRequest.AttackPower;
    }

    public static ImmutableArray<SocketEntry> UnpackActiveSockets(int packedSockets1, int packedSockets2,
        int packedSockets3)
    {
        Span<byte> bytes = stackalloc byte[12];
        BinaryPrimitives.WriteInt32LittleEndian(bytes[..4], packedSockets1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.Slice(4, 4), packedSockets2);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.Slice(8, 4), packedSockets3);

        var activeCount = Math.Min((int)bytes[1], MaxSocketsPerItem);
        if (activeCount == 0)
            return ImmutableArray<SocketEntry>.Empty;

        var builder = ImmutableArray.CreateBuilder<SocketEntry>(activeCount);
        for (var i = 0; i < activeCount; i++)
            builder.Add(new SocketEntry(bytes[2 + i * 2], bytes[3 + i * 2]));

        return builder.MoveToImmutable();
    }

    public static int GetSocketInfo(
        GemSocketStatRequest statType,
        int packedSockets1, int packedSockets2, int packedSockets3,
        Func<GemSocketStatRequest, SocketEntry, int> socketValueLookup)
    {
        var total = 0;
        foreach (var entry in UnpackActiveSockets(packedSockets1, packedSockets2, packedSockets3))
            if (entry.GemType != 0)
                total += socketValueLookup(statType, entry);

        return total;
    }

    public readonly record struct SocketEntry(byte GemType, byte GemTier);
}
