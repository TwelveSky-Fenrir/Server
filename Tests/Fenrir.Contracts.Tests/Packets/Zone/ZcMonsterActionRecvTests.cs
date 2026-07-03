using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcMonsterActionRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(124, ZcMonsterActionRecv.PayloadSize);
        Assert.Equal(4 + 4 + ObjectForMonster.WireSize + 4, ZcMonsterActionRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.MonsterActionRecv, ZcMonsterActionRecv.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var data = WireTestKit.CreatePopulated<ObjectForMonster>(3);
        var packet = new ZcMonsterActionRecv
        {
            ServerIndex = 21,
            UniqueNumber = 42_000_111u,
            Data = data,
            CheckChangeActionState = 2
        };

        Span<byte> buffer = new byte[ZcMonsterActionRecv.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(ZcMonsterActionRecv.PayloadSize, written);
        Assert.Equal(21, BinaryPrimitives.ReadInt32LittleEndian(buffer));
        Assert.Equal(42_000_111u, BinaryPrimitives.ReadUInt32LittleEndian(buffer[4..]));

        var ok = ObjectForMonster.TryRead(buffer.Slice(8, ObjectForMonster.WireSize), out var dataBack);
        Assert.True(ok);
        WireTestKit.AssertDeepEqual(data, dataBack);

        Assert.Equal(2, BinaryPrimitives.ReadInt32LittleEndian(buffer[(8 + ObjectForMonster.WireSize)..]));
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var data = WireTestKit.CreatePopulated<ObjectForMonster>(9);
        var packet = new ZcMonsterActionRecv
        {
            ServerIndex = 99,
            UniqueNumber = 0xCAFEBABEu,
            Data = data,
            CheckChangeActionState = 1
        };

        var golden = new byte[ZcMonsterActionRecv.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(golden, 99);
        BinaryPrimitives.WriteUInt32LittleEndian(golden.AsSpan(4), 0xCAFEBABEu);
        var dataWritten = WireTestKit.EncodeObjectForMonster(golden.AsSpan(8, ObjectForMonster.WireSize), data);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(8 + ObjectForMonster.WireSize), 1);

        Assert.Equal(ObjectForMonster.WireSize, dataWritten);

        Span<byte> buffer = new byte[ZcMonsterActionRecv.PayloadSize];
        packet.Write(buffer);

        Assert.True(golden.AsSpan().SequenceEqual(buffer));
    }
}
