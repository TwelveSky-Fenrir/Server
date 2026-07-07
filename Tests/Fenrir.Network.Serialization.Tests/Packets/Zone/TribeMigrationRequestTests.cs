using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class CzChangeToTribe4SendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        // ExpectedSize=9 (inbound header only) -> 0-byte payload: empty struct. The character undergoing
        // conversion is resolved from the session, never from anything client-supplied.
        Assert.Equal(0, TribeMigrationRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.TribeMigration, TribeMigrationRequest.Opcode);
    }

    [Fact]
    public void RoundTrip_EmptyPayload_AlwaysSucceeds()
    {
        Assert.True(TribeMigrationRequest.TryRead(ReadOnlySpan<byte>.Empty, out _));
    }

    [Fact]
    public void RoundTrip_TrailingBytesAreIgnored()
    {
        byte[] buffer = [0xAA, 0xBB, 0xCC];

        Assert.True(TribeMigrationRequest.TryRead(buffer, out _));
    }
}
