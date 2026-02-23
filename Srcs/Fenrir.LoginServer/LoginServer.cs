using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using Fenrir.LoginServer.Handlers;
using Fenrir.LoginServer.Network;
using Fenrir.Network.Collections;
using Fenrir.Network.Framing;
using Fenrir.Network.Options;
using Fenrir.Network.Transport;
using Fenrir.Network.Util;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.LoginServer;

//using LoginServerSessionType = Session<MessageMetadata, Packet>;

public class LoginServer : FenrirServer
{
    private readonly ILogger<LoginServer> _logger;
    //private readonly IMessageDispatcher<PacketType, MessageMetadata, MessageMetadata> _messageDispatcher;
    //private readonly IServiceProvider _provider;
    // private readonly ISessionCollection<LoginSession> _sessions;
//         ISessionCollection<LoginSession> sessions
    // public LoginServer(
    //     //IMessageDispatcher<PacketType, MessageMetadata, Packet> messageDispatcher,
    //     //IServiceProvider provider
    //         IOptions<FenrirServerOptions> options,
    //         //IMessageDispatcher<TPacketType, TMessage> messageDispatcher,
    //         ILoggerFactory loggerFactory,
    //         IServiceProvider provider,
    //         IPacketCollection packetCollection
    //     ) : base(options, loggerFactory, provider, packetCollection)
    // {
    //     //_messageDispatcher = messageDispatcher;
    //     _logger = loggerFactory.CreateLogger<LoginServer>();
    //     //_provider = provider;
    // }
    
    public LoginServer(
        IOptions<FenrirServerOptions> options,
        ILoggerFactory loggerFactory,
        IServiceProvider provider,
        IPacketCollection packetCollection
    ) : base(options, loggerFactory, provider, packetCollection)
    {
        _logger = loggerFactory.CreateLogger<LoginServer>();
    }

    // protected override Session<PacketType, MessageMetadata, Packet> CreateSession(Socket socket,
    //     IMessageParser<MessageMetadata> messageParser,
    //     IMessageDispatcher<PacketType, MessageMetadata, Message<PacketType>> messageDispatcher,
    //     ILogger logger, FenrirServerOptions options)
    // {
    //     var session = new LoginSession(socket, messageParser, messageDispatcher, logger, options);
    //
    //     _logger.LogInformation($"Nouvelle session créée avec l'ID de session : {session.SessionId}");
    //
    //     return session;
    // }
    protected override IClient CreateClient(Socket socket)
    {
        // TODO: How to pass Socket, Session, Logger, Server, Options etc into here?
        throw new NotImplementedException();
        return new LoginClient();
    }

    protected override ISession CreateSession(string sessionId,
        //IMessageParser<MessageMetadata> messageParser,
        //IMessageDispatcher<PacketType, LoginSession, MessageMetadata> messageDispatcher,
        IPEndPoint endpoint,
        ILogger logger, FenrirServerOptions options)
    {
        // TODO: Factory?
        var session = new LoginSession(endpoint, logger, options);

        _logger.LogInformation($"New session created with session ID: {session.SessionId}");

        return session;
    }
    
    // protected override LoginSession CreateSession(Socket socket, IMessageParser<MessageMetadata> messageParser,
    //     IMessageDispatcher<PacketType, LoginSession, MessageMetadata> messageDispatcher, ILogger logger, FenrirServerOptions options)
    // {
    //     throw new NotImplementedException();
    // }

    protected override bool CanAddSession(ISession session)
    {
        // TODO: Fix?
        throw new NotImplementedException();
        // var canAdd = !_sessions.IsFull;
        // if (!canAdd) _logger.LogWarning("Cannot add session. The session collection is full.");
        // return canAdd;
    }

    protected override async Task OnSessionConnectedAsync(ISession session)
    {
        // TODO: Fix.
        throw new NotImplementedException();
        // _sessions.AddSession(session);
        // _logger.LogInformation("Client {SessionId} connected.", session.SessionId);
        // await base.OnSessionConnectedAsync(session);
    }

    protected override async Task OnSessionDisconnectedAsync(ISession session)
    {
        // TODO: Fix
        throw new NotImplementedException();
        // _sessions.RemoveSession(session.SessionId);
        // _logger.LogInformation("Client {SessionId} disconnected.", session.SessionId);
        // await base.OnSessionDisconnectedAsync(session);
    }

    // TODO: Put all this stuff into FenirServer.
    private async Task ListenServer()
    {
        var listenSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        listenSocket.Bind(new IPEndPoint(IPAddress.Loopback, 8087));

        Console.WriteLine("Listening on port 8087");

        listenSocket.Listen(120);

        while (true)
        {
            var socket = await listenSocket.AcceptAsync();
            _ = ProcessSocketReceiveAsync(socket);
        }
    }
    
    // Source: https://devblogs.microsoft.com/dotnet/system-io-pipelines-high-performance-io-in-net/
    private Task ProcessSocketReceiveAsync(Socket socket)
    {
        // TODO: See about that Duplex pipe? can we use it here?
        
        var pipe = new Pipe();
        Task writing = FillPipeAsync(socket, pipe.Writer);
        Task reading = ReadPipeAsync(pipe.Reader);

        return Task.WhenAll(reading, writing);
    }

    /// <summary>
    /// Takes data received from the socket, and writes it to a pipe to be processed.
    /// </summary>
    /// <param name="socket"></param>
    /// <param name="writer"></param>
    async Task FillPipeAsync(Socket socket, PipeWriter writer)
    {
        // TODO: , byte decryptionKey
        // TODO: Find a suitable minimum buffer size (can this be configurable?) Server Options?
        const int minimumBufferSize = 512;

        while (true)
        {
            // Allocate at least 512 bytes from the PipeWriter
            Memory<byte> memory = writer.GetMemory(minimumBufferSize);
            try 
            {
                // TODO: Cancellation tokens?
                int bytesRead = await socket.ReceiveAsync(memory, SocketFlags.None);
                if (bytesRead == 0)
                {
                    break;
                }
                
                // TODO: is there a nice way to have child class do any encryption needed like a method they can override if wanting to do so?
                // client.decrypt(data);
                byte decryptionKey = 0x00;
                Utils.Xor(memory.Span.Slice(0, bytesRead), decryptionKey);

                // Tell the PipeWriter how much was read from the Socket
                writer.Advance(bytesRead);
            }
            catch (Exception ex)
            {
                // TODO: Logger.
                //LogError(ex);
                break;
            }

            // Make the data available to the PipeReader
            FlushResult result = await writer.FlushAsync(); // TODO: Cancellation token?

            if (result.IsCompleted)
            {
                break;
            }
        }

        // Tell the PipeReader that there's no more data coming
        writer.Complete();
    }

    async Task ReadPipeAsync(PipeReader reader)
    {
        while (true)
        {
            // TODO: Cancellation tokens
            ReadResult result = await reader.ReadAsync();

            ReadOnlySequence<byte> sequence = result.Buffer;
            SequencePosition consumed = sequence.Start;
            SequencePosition examined = sequence.Start;
            
            // If not enough data to read PacketHeader then continue to wait for more data.
            if (sequence.Length < 9)
            {
                examined = sequence.GetPosition(9);
                reader.AdvanceTo(consumed, examined);
                continue;
            }
            
            // TODO: Can it be writable to do decryption?
            
            var packetHeader = Marshaling.DeserializeStruct<PacketHeader>(sequence);
            examined = sequence.GetPosition(9);
            
            // It's illegal to use ReadOnlySequence<byte> after calling PipeReader.AdvanceTo.
            reader.AdvanceTo(consumed, examined);

            Console.WriteLine($"PacketHeader: {packetHeader.PacketType:X2} {packetHeader.Unknown1} {packetHeader.Unknown2}");
            Console.WriteLine($"PacketHeader Dump: {Utils.HexDump(packetHeader)}");
            
            PacketInfo packetInfo = _packetCollection.GetPacketInfo(packetHeader.PacketType);
            // var packetHeader = Marshaling.DeserializeStructFromReadOnlySequence<PacketHeader>(reader.UnreadSequence);
            // reader.Advance(Marshal.SizeOf<PacketHeader>()); // 9 bytes
            // Console.WriteLine($"PacketHeader: {packetHeader.PacketType:X2} {packetHeader.Unknown1} {packetHeader.Unknown2}");
            // Console.WriteLine($"PacketHeader Dump: {Utils.HexDump(packetHeader)}");

            
            // TODO: If packet requires authentication or some other pre-requisite then don't allow to process it?
            // TODO: Can we rate limit a spammy client? Or simulate lag?

            var expectedPacketSize = packetInfo.Size;
            if (expectedPacketSize > 0)
            {
                // var method = typeof(Marshaling).GetMethod(nameof(Marshaling.DeserializeStructFromSpan), new[] { typeof(ReadOnlyMemory<byte>) });
                // var genericMethod = method.MakeGenericMethod(packetInfo.PacketType);
                
//var deserializer = (Func<ReadOnlyMemory<byte>, object>)Delegate.CreateDelegate(typeof(Func<ReadOnlyMemory<byte>, object>), genericMethod);
// Please invoke the method.
// genericMethod.Invoke(null, [sequence]);

                Debug.Assert(packetInfo.Deserialize != null, "packetInfo.Deserialize != null");
                var deserializedPacket = packetInfo.Deserialize(sequence.Slice(expectedPacketSize)); 
                reader.AdvanceTo(sequence.GetPosition(expectedPacketSize));
                Utils.HexDump(deserializedPacket);

                //LoginHandler.LoginRequestPacket loginRequestPacket2 = (LoginHandler.LoginRequestPacket)oops;
                // logger.LogInformation($"Username: {loginRequestPacket2.GetUsername()} Password: {loginRequestPacket2.GetPassword()}");

                // TODO: How to get handler?
                // packetInfo.handlerNoData();
            }
            else
            {
                // TODO: How to get handler?
            }
            
            // TODO: Rate Limiting of client packets if they are spamming considerably?
            
            //
            // if (reader.Remaining < expectedPacketSize)
            // {
            //     reader.Rewind(9);
            //     return reader.Position;
            // }
            //
            // if (packetHeader.PacketType == 0x02)
            // {
            //     var loginRequestPacket =
            //         Marshaling.DeserializeStructFromReadOnlySequence<LoginHandler.LoginRequestPacket>(
            //             reader.UnreadSequence);
            //     Console.WriteLine($"LoginRequestPacket:\n{loginRequestPacket.GetUsername()}");
            //     Console.WriteLine($"LoginRequestPacket Dump:\n{Utils.HexDump(loginRequestPacket)}");
            // }
            //
            // // Marshal.SizeOf<LoginHandler.LoginRequestPacket>()
            // reader.Advance(expectedPacketSize);
            //
            //
            // // Tell the PipeReader how much of the buffer we have consumed
            // reader.AdvanceTo(buffer.Start, buffer.End);

            // Stop reading if there's no more data coming
            if (result.IsCompleted)
            {
                break;
            }
        }

        // Mark the PipeReader as complete
        reader.Complete();
    }
    
}