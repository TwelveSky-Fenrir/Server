using System.Buffers;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;
using System.Text;
using Fenrir.LoginServer;
using Fenrir.LoginServer.Handlers;
using Fenrir.LoginServer.Network.Dispatcher;
using Fenrir.LoginServer.Network.Framing;
using Fenrir.LoginServer.Network.Metadata;
using Fenrir.Network.Collections;
using Fenrir.Network.Dispatcher;
using Fenrir.Network.Framing;
using Fenrir.Network.Helpers;
using Fenrir.Network.Options;
using Fenrir.Network.Transport;
using Fenrir.Network.Util;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", false, true)
    .Build();

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddSingleton<ILoggerFactory, LoggerFactory>();

        // // services.AddSingleton<ISessionCollection<Session<PacketType, MessageMetadata, Packet>>, SessionCollection<MessageMetadata, Packet>>();
        // //
        // services.AddSingleton(_ => {
        //     var dispatcher = new MessageDispatcher();
        //     
        //     // PacketRegistration.RegisterPackets<PacketType, Packet>(dispatcher, "Fenrir.LoginServer.Handlers", "Fenrir.LoginServer.Packets");
        //     
        //     //var loginHandler = provider.GetRequiredService<LoginHandler>();
        //     // dispatcher.RegisterHandler(PacketType.LoginRequest, loginHandler.HandleLoginAsync);
        //     return dispatcher;
        // });

        services.AddSingleton(_ => {
            var packetCollection = new PacketCollection();
            PacketRegistration.RegisterPacketsToCollection(packetCollection, "Fenrir.LoginServer.Packets");
            PacketRegistration.RegisterPacketsToCollection(packetCollection, "Fenrir.LoginServer.Handlers");
            return packetCollection;
        });
        
        
        //services.AddSingleton<IMessageDispatcher<PacketType, MessageMetadata, Packet>, MessageDispatcher>();
        
        // TODO: is there a better way ?
        // services.AddSingleton(provider =>
        // {
        //     var dispatcher = provider.GetRequiredService<IMessageDispatcher<PacketType, MessageMetadata, Packet>>();
        //     var loginHandler = provider.GetRequiredService<LoginHandler>();
        //
        //     dispatcher.RegisterHandler(PacketType.LoginRequest, loginHandler.HandleLoginAsync);
        //     return dispatcher;
        // });


        // services.AddSingleton<IMessageParser<MessageMetadata>, MessageParser>();
        //
        // services.Configure<FenrirServerOptions>(options =>
        // {
        //     options.IpAddress = "127.0.0.1";
        //     options.Port = 11091;
        //     options.MaxConnections = 100;
        //     options.EnableKeepAlive = true;
        //     options.KeepAliveInterval = 10;
        //     options.EnableLogging = true;
        //     options.MaxConnectionsByIpAddress = 10;
        //     // TODO: Nagle?
        // });
        //
        // services.AddSingleton<LoginServer>();
    })
    .Build();

// Get logger factory
var loggerFactory = host.Services.GetRequiredService<ILoggerFactory>();
var logger = loggerFactory.CreateLogger("LoginServer");

logger.LogInformation("Starting Login Server");

// TODO: Add test that checks size of all packets recv and send.
// int size = Marshal.SizeOf<LoginHandler.LoginPacketData>();
// Console.WriteLine($"Size of LoginPacketData: {size} bytes");
var packetCollection = host.Services.GetRequiredService<PacketCollection>();
packetCollection.ForEach(packetInfo =>
{
    var size = Marshal.SizeOf(packetInfo.PacketType);
    
    // If Packet Id is in PacketType
    if (!Enum.IsDefined(typeof(PacketType), packetInfo.Id))
    {
        logger.LogWarning($"Packet Id {packetInfo.Id:X} is not defined in PacketType enum");
        return;
    }
    PacketType packetType = (PacketType) packetInfo.Id;
    
    logger.LogInformation($"{packetType.GetType().Name} {packetType:X} {packetType} {packetInfo.Name}: {size} bytes");
});



    // var deserializer = (Func<ReadOnlyMemory<byte>, object>)Delegate.CreateDelegate(typeof(Func<ReadOnlyMemory<byte>, object>), genericMethod);
    // var deserializer = (Func<ReadOnlySpan<byte>, object>)Delegate.CreateDelegate(typeof(Func<ReadOnlySpan<byte>, object>), genericMethod);

byte[] buffer = new byte[30];
Span<byte> span = buffer;
Memory<byte> memory = buffer;

LoginHandler.LoginRequestPacket loginRequestPacket = new();
loginRequestPacket.SetUsername("Liam");
loginRequestPacket.SetPassword("Secret");


// Write struct to buffer.
Marshaling.SerializeStructToMemory(loginRequestPacket, memory);

// Hex dump buffer to log.
logger.LogInformation(Utils.HexDump(buffer));


// private static Func<ReadOnlyMemory<byte>, object> GetDeserializer(Type type)
// {
// if (!_deserializers.TryGetValue(type, out var deserializer))
// {

Type type = typeof(LoginHandler.LoginRequestPacket);

//var method = typeof(Marshaling).GetMethod(nameof(Marshaling.DeserializeStructFromSpan), [typeof(ReadOnlyMemory<byte>)]); // , [type]
//var genericMethod = method.MakeGenericMethod(type);

//var deserializer = (Func<ReadOnlyMemory<byte>, object>)Delegate.CreateDelegate(typeof(Func<ReadOnlyMemory<byte>, object>), genericMethod);


var method = typeof(Marshaling).GetMethod(nameof(Marshaling.DeserializeStruct), new[] { typeof(ReadOnlyMemory<byte>) });
var genericMethod = method.MakeGenericMethod(type);
//var deserializer = (Func<ReadOnlyMemory<byte>, object>)Delegate.CreateDelegate(typeof(Func<ReadOnlyMemory<byte>, object>), genericMethod);

// Please invoke the method.
ReadOnlyMemory<byte> readOnlyMemory = memory;

var oops = genericMethod.Invoke(null, new object[] { readOnlyMemory });
LoginHandler.LoginRequestPacket loginRequestPacket2 = (LoginHandler.LoginRequestPacket)oops;
logger.LogInformation($"Username: {loginRequestPacket2.GetUsername()} Password: {loginRequestPacket2.GetPassword()}");

//var obj = deserializer();




    //     _deserializers[type] = deserializer;
    // }
//     return deserializer;
// }

// var loginServer = host.Services.GetRequiredService<LoginServer>();
//
// await loginServer.StartAsync();


// TODO: Consider if a ReadOnlySequence is even a good fit?
// We can just use Memory<T> if we pre-allocate buffer that is large enough for all clients to spam packets into.
// However, using a ReadOnlySequence<T> allows us to handle packets that are larger than the buffer size and they would be discarded if no longer needed.
// Allowing us to handle bursting of data and have a smaller initial buffer size.
// However, we would need to keep track of memory use still if each client is sending large packets, or if it happens too much throttle or boot them.

/*
 * Make a new byte buffer and set the first byte to 0x02
 * Set the subsequent 4 bytes to an int value of 1,
 * then the next 4 bytes to an int value of 2
 * then write a string of 13 bytes length terminated with a null byte 00
 */
byte[] readBuffer = new byte[36];
readBuffer[0] = 0x02;
BitConverter.TryWriteBytes(readBuffer.AsSpan(1, 4), 1);
BitConverter.TryWriteBytes(readBuffer.AsSpan(5, 4), 2);
string str = "Hello,World!!";
//Encoding.UTF8.GetBytes(str, readBuffer.AsSpan(9, 12));
Encoding.UTF8.GetBytes(str.AsSpan(0, Math.Min(str.Length, 12)), readBuffer.AsSpan(9, 21));
readBuffer[21] = 0x00; // Null terminator

logger.LogInformation(Utils.HexDump(readBuffer));

// ReadOnlySequence<byte> sequence = new ReadOnlySequence<byte>(readBuffer);
// WubaSequenceReader.ReadItems(sequence);

// public class WubaSequenceReader
// {
//     public static SequencePosition ReadItems(in ReadOnlySequence<byte> sequence)
//     {
//         var reader = new SequenceReader<byte>(sequence);
//
//         // TODO: Decryption on pipeline write after socket read.
//
//         // Loop until we have read the entire sequence.
//         while (!reader.End)
//         {
//             if (reader.Remaining < 9)
//             {
//                 return reader.Position;
//             }
//
//             var packetHeader = Marshaling.DeserializeStructFromReadOnlySequence<PacketHeader>(reader.UnreadSequence);
//             reader.Advance(Marshal.SizeOf<PacketHeader>()); // 9 bytes
//             Console.WriteLine($"PacketHeader: {packetHeader.PacketType:X2} {packetHeader.Unknown1} {packetHeader.Unknown2}");
//             Console.WriteLine($"PacketHeader Dump: {Utils.HexDump(packetHeader)}");
//         
//             // TODO: Get packet info
//             // TODO: Read on packet.
//
//             var expectedPacketSize = 26;
//             if (reader.Remaining < expectedPacketSize)
//             {
//                 reader.Rewind(9);
//                 return reader.Position;
//             }
//             
//             if (packetHeader.PacketType == 0x02)
//             {
//                 var loginRequestPacket =
//                     Marshaling.DeserializeStructFromReadOnlySequence<LoginHandler.LoginRequestPacket>(
//                         reader.UnreadSequence);
//                 Console.WriteLine($"LoginRequestPacket:\n{loginRequestPacket.GetUsername()}");
//                 Console.WriteLine($"LoginRequestPacket Dump:\n{Utils.HexDump(loginRequestPacket)}");
//             }
//             
//             // Marshal.SizeOf<LoginHandler.LoginRequestPacket>()
//             reader.Advance(expectedPacketSize);
//         }
//
//         return reader.Position;
//     }
//
// }
