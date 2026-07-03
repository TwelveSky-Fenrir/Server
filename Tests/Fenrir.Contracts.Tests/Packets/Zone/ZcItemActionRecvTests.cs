using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcItemActionRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(96, ZcItemActionRecv.PayloadSize);
        Assert.Equal(4 + 4 + ObjectForItem.WireSize + 4, ZcItemActionRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.ItemActionRecv, ZcItemActionRecv.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var data = WireTestKit.CreatePopulated<ObjectForItem>(4);
        var packet = new ZcItemActionRecv
        {
            ServerIndex = 8,
            UniqueNumber = 77_000_222u,
            Data = data,
            CheckChangeActionState = 1
        };

        Span<byte> buffer = new byte[ZcItemActionRecv.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(ZcItemActionRecv.PayloadSize, written);
        Assert.Equal(8, BinaryPrimitives.ReadInt32LittleEndian(buffer));
        Assert.Equal(77_000_222u, BinaryPrimitives.ReadUInt32LittleEndian(buffer[4..]));

        var ok = ObjectForItem.TryRead(buffer.Slice(8, ObjectForItem.WireSize), out var dataBack);
        Assert.True(ok);
        WireTestKit.AssertDeepEqual(data, dataBack);

        Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(buffer[(8 + ObjectForItem.WireSize)..]));
    }

    [Fact]
    public void Write_ProducesGoldenBytes_WithInternalPaddingZeroed()
    {
        var data = new ObjectForItem
        {
            Index = 1,
            Quantity = 2,
            Value = 3,
            SerialNumber = 4,
            Location = [10f, 0f, 20f],
            Master = "Odin",
            PartyName = "Valhalla",
            DropSort = 5,
            CreateTime = 100u,
            PresentTime = 200u,
            CreateState = 6,
            SocketGem = [0, 0, 0]
        };
        var packet = new ZcItemActionRecv
        {
            ServerIndex = 12,
            UniqueNumber = 0xFEEDFACEu,
            Data = data,
            CheckChangeActionState = 2
        };

        var golden = new byte[ZcItemActionRecv.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(golden, 12);
        BinaryPrimitives.WriteUInt32LittleEndian(golden.AsSpan(4), 0xFEEDFACEu);
        var dataWritten = WireTestKit.EncodeObjectForItem(golden.AsSpan(8, ObjectForItem.WireSize), data);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(8 + ObjectForItem.WireSize), 2);

        Assert.Equal(ObjectForItem.WireSize, dataWritten);
        // Padding bytes (payload offsets 62-63, after PartyName) must be zeroed, not left uninitialized.
        Assert.Equal(0, golden[8 + 54]);
        Assert.Equal(0, golden[8 + 55]);

        Span<byte> buffer = new byte[ZcItemActionRecv.PayloadSize];
        packet.Write(buffer);

        Assert.True(golden.AsSpan().SequenceEqual(buffer));
    }
}
