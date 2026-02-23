using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using Fenrir.LoginServer.Network.Metadata;
using Fenrir.Network.Dispatcher;
using Fenrir.Network.Framing;
using Fenrir.Network.Options;
using Fenrir.Network.Transport;
using Microsoft.Extensions.Logging;

namespace Fenrir.LoginServer;

public sealed class LoginSession(
    IPEndPoint remoteEndPoint,
    //IMessageParser<Packet> messageParser,
    //IMessageDispatcher<PacketType, MessageMetadata, Packet> messageDispatcher,
    ILogger logger,
    FenrirServerOptions options)
    : Session(logger, options, "", remoteEndPoint) // TODO: Fix this.
{
    
    // TODO: Composition? Traits? (hasXorEncryption?)
    private const byte XorKey = 0x00; // TODO: Move somewhere else?
    public new int SessionId { get; set; }

    // public ValueTask SendAsync<TMessage>()
    //     where TMessage : Message<PacketType>, new()
    // {
    //     return SendAsync(new TMessage());
    // }

    // TODO: Sending should be done in parent class too?
    //
    // public ValueTask SendAsync(Message<PacketType> message)
    // {
    //     using var writer = new BinaryWriter(new MemoryStream());
    //     message.Serialize(writer);
    //
    //     var payload = ((MemoryStream)writer.BaseStream).ToArray();
    //
    //     for (var i = 0; i < payload.Length; i++)
    //         payload[i] ^= XorKey;
    //
    //     var metadata = new MessageMetadata(payload.Length, SessionId, (byte)message.PacketType, payload);
    //
    //     var buffer = _messageEncoder.EncodeMessage(metadata, true);
    //
    //     var vt = _pipe.Output.WriteAsync(buffer, SessionClosed);
    //
    //     return vt.IsCompletedSuccessfully
    //         ? ValueTask.CompletedTask
    //         : WaitAndFlush(vt);
    //
    //     static async ValueTask WaitAndFlush(ValueTask<FlushResult> vt)
    //     {
    //         try
    //         {
    //             await vt.ConfigureAwait(false);
    //         }
    //         catch
    //         {
    //             // ignored
    //         }
    //     }
    // }
}