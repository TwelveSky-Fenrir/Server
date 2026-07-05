using Fenrir.Network.Serialization.Packets.Shared;

namespace Fenrir.Network.Serialization.Tests.Packets.Login;

public class LoginAdapterInfoTests
{
    [Fact]
    public void WireSize_MatchesContractConstant()
    {
        Assert.Equal(156, LoginAdapterInfo.WireSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var adapter = new LoginAdapterInfo
        {
            AdapterName = "Realtek PCIe GbE Family Controller",
            PhysicalAddressLength = 6,
            PhysicalAddress = [0x00, 0x1A, 0x2B, 0x3C, 0x4D, 0x5E, 0x00, 0x00],
            IPAddress = "192.168.1.100"
        };

        var buffer = new byte[LoginAdapterInfo.WireSize];
        var written = adapter.Write(buffer);

        Assert.Equal(LoginAdapterInfo.WireSize, written);
        Assert.True(LoginAdapterInfo.TryRead(buffer, out var decoded));

        Assert.Equal(adapter.AdapterName, decoded.AdapterName);
        Assert.Equal(adapter.PhysicalAddressLength, decoded.PhysicalAddressLength);
        Assert.True(adapter.PhysicalAddress.SequenceEqual(decoded.PhysicalAddress));
        Assert.Equal(adapter.IPAddress, decoded.IPAddress);
    }

    [Fact]
    public void TryRead_BufferTooShort_Fails()
    {
        var buffer = new byte[LoginAdapterInfo.WireSize - 1];

        Assert.False(LoginAdapterInfo.TryRead(buffer, out _));
    }
}
