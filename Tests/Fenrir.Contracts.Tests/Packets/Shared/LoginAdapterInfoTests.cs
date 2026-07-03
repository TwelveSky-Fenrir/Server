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
}
