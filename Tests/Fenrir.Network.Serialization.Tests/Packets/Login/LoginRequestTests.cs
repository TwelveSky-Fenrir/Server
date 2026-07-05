using System.Buffers.Binary;
using System.Text;
using Fenrir.Network.Serialization.Packets.Login;
using Fenrir.Network.Serialization.Packets.Shared;

namespace Fenrir.Network.Serialization.Tests.Packets.Login;

public class ClLoginSendTests
{
    [Fact]
    public void PayloadSize_MatchesContractConstant()
    {
        // ExpectedSize=457 (9-byte inbound header) -> 448-byte payload (255+33+4+156).
        Assert.Equal(448, LoginRequest.PayloadSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var adapter = new LoginAdapterInfo
        {
            AdapterName = "Intel(R) Ethernet Connection",
            PhysicalAddressLength = 6,
            PhysicalAddress = [0x02, 0x04, 0x06, 0x08, 0x0A, 0x0C, 0x00, 0x00],
            IPAddress = "10.0.0.42"
        };

        var buffer = new byte[LoginRequest.PayloadSize];
        WriteFixedString(buffer.AsSpan(0, 255), "PlayerOne");
        WriteFixedString(buffer.AsSpan(255, 33), "Sup3rSecret!");
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(288, 4), 733);
        adapter.Write(buffer.AsSpan(292, LoginAdapterInfo.WireSize));

        Assert.True(LoginRequest.TryRead(buffer, out var packet));

        Assert.Equal("PlayerOne", packet.Id);
        Assert.Equal("Sup3rSecret!", packet.Password);
        Assert.Equal(733, packet.Version);
        Assert.Equal(adapter.AdapterName, packet.Adapter.AdapterName);
        Assert.Equal(adapter.PhysicalAddressLength, packet.Adapter.PhysicalAddressLength);
        Assert.True(adapter.PhysicalAddress.SequenceEqual(packet.Adapter.PhysicalAddress));
        Assert.Equal(adapter.IPAddress, packet.Adapter.IPAddress);
    }

    [Fact]
    public void TryRead_BufferTooShort_Fails()
    {
        var buffer = new byte[LoginRequest.PayloadSize - 1];

        Assert.False(LoginRequest.TryRead(buffer, out _));
    }

    private static void WriteFixedString(Span<byte> destination, string value)
    {
        destination.Clear();
        Encoding.Latin1.GetBytes(value, destination);
    }
}
