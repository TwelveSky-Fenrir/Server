using System.Buffers;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Network.Tests.Sessions;

/// <summary>Confirms <c>ClientSession</c>'s two send paths put the exact expected bytes on the wire.</summary>
public class ClientSessionSendTests
{
    // ZoneGreetingResponse declares no Obfuscation, so FrameWriter takes the no-XOR path.
    [Fact]
    public async Task Send_WritesOpcodeAndPayload_ForPacketWithoutObfuscation()
    {
        var pipe = new FakeDuplexPipe();
        var session = new ZoneClientSession(1, pipe);
        var packet = new ZoneGreetingResponse { RandomNumber = 0x12345678 };

        session.Send(packet);

        var result = await pipe.SessionToPeer.ReadAsync();
        var bytes = result.Buffer.ToArray();
        pipe.SessionToPeer.AdvanceTo(result.Buffer.End);

        Assert.Equal(new byte[] { 0x00, 0x78, 0x56, 0x34, 0x12 }, bytes);
    }

    // SendRaw is the pre-built-frame path (LZ4/ZPACKET) -- it must never reinterpret the caller's bytes.
    [Fact]
    public async Task SendRaw_WritesSuppliedBytesUnchanged()
    {
        var pipe = new FakeDuplexPipe();
        var session = new ZoneClientSession(1, pipe);
        byte[] raw = [0xDE, 0xAD, 0xBE, 0xEF, 0x01];

        session.SendRaw(raw);

        var result = await pipe.SessionToPeer.ReadAsync();
        var bytes = result.Buffer.ToArray();
        pipe.SessionToPeer.AdvanceTo(result.Buffer.End);

        Assert.Equal(raw, bytes);
    }
}
