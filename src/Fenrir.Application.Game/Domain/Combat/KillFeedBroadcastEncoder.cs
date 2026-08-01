using System.Buffers.Binary;
using System.Text;

namespace Fenrir.Application.Game.Domain.Combat;

public static class KillFeedBroadcastEncoder
{
    public const int PayloadSize = 130;

    public const int NameFieldSize = 13;

    private const int RankedEntrySize = NameFieldSize + 1 + 4;

    private const int KillerNameOffset = 0;
    private const int KillerTribeOffset = KillerNameOffset + NameFieldSize;
    private const int VictimNameOffset = KillerTribeOffset + 1;
    private const int VictimTribeOffset = VictimNameOffset + NameFieldSize;
    private const int TopThreeOffset = VictimTribeOffset + 1;

    public static byte[] Encode(in KillFeedBroadcastPayload payload)
    {
        var data = new byte[PayloadSize];
        var span = data.AsSpan();

        WriteFixedName(span.Slice(KillerNameOffset, NameFieldSize), payload.KillerName);
        span[KillerTribeOffset] = payload.KillerTribe;

        WriteFixedName(span.Slice(VictimNameOffset, NameFieldSize), payload.VictimName);
        span[VictimTribeOffset] = payload.VictimTribe;

        for (var i = 0; i < 3; i++)
        {
            var entry = i < payload.TopThree.Length ? payload.TopThree[i] : KillFeedTopEntry.Blank;
            var entryOffset = TopThreeOffset + i * RankedEntrySize;

            WriteFixedName(span.Slice(entryOffset, NameFieldSize), entry.Name);
            span[entryOffset + NameFieldSize] = entry.Tribe;
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(entryOffset + NameFieldSize + 1, 4), entry.Kills);
        }

        return data;
    }

    private static void WriteFixedName(Span<byte> destination, string name)
    {
        destination.Clear();
        var sourceLength = Math.Min(name.Length, destination.Length);
        Encoding.ASCII.GetBytes(name.AsSpan(0, sourceLength), destination);
    }
}
