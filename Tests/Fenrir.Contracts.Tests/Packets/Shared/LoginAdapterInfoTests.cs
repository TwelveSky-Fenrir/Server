using System.Buffers.Binary;
using System.Text;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Tests.TestSupport;

namespace Fenrir.Contracts.Tests.Packets.Shared;

public class LoginAdapterInfoTests
{
    [Fact]
    public void WireSize_MatchesContract()
    {
        Assert.Equal(156, LoginAdapterInfo.WireSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var v = new SequentialValueFactory();
        var adapter = new LoginAdapterInfo
        {
            AdapterName = v.NextString(128),
            PhysicalAddressLength = v.NextUInt(),
            PhysicalAddress = v.NextByteArray(8),
            IPAddress = v.NextString(16)
        };

        var buffer = new byte[LoginAdapterInfo.WireSize];
        var written = adapter.Write(buffer);
        Assert.Equal(LoginAdapterInfo.WireSize, written);

        Assert.True(LoginAdapterInfo.TryRead(buffer, out var roundTripped));
        StructuralAssert.DeepEqual(adapter, roundTripped);
    }

    [Fact]
    public void TryRead_DecodesGoldenBytes()
    {
        var buffer = new byte[LoginAdapterInfo.WireSize];
        Encoding.Latin1.GetBytes("eth0", buffer.AsSpan(0, 128));
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(128, 4), 6u);
        new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x01, 0x02, 0x00, 0x00 }.CopyTo(buffer.AsSpan(132, 8));
        Encoding.Latin1.GetBytes("10.0.0.5", buffer.AsSpan(140, 16));

        var ok = LoginAdapterInfo.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal("eth0", packet.AdapterName);
        Assert.Equal(6u, packet.PhysicalAddressLength);
        Assert.Equal(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x01, 0x02, 0x00, 0x00 }, packet.PhysicalAddress);
        Assert.Equal("10.0.0.5", packet.IPAddress);
    }
}
