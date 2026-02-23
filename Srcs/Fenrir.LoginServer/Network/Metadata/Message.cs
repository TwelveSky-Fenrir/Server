using System.Runtime.InteropServices;

namespace Fenrir.LoginServer.Network.Metadata;

// public abstract class BaseMessage
// {
//     public abstract void BaseMethod();
// }

public abstract class Message<TPacketType> //  : BaseMessage
{
    protected static readonly TPacketType Type = default!;

    public abstract TPacketType PacketType { get; }

    public abstract void Deserialize(BinaryReader reader);

    public abstract void Serialize(BinaryWriter writer);

    // TODO: Move this out.
    public MessageMetadata ToMetadata()
    {
        // TODO: Can we use span? To put it into buffer?
        
        // byte[] buffer = new byte[1024];
        // var span = buffer.AsSpan();
        //
        // var record = new MessageMetadata(
        //     (int)ms.Length + 9,
        //     0,
        //     0, //(byte)Type, // TODO: Why is this here?
        //     ms.ToArray()
        // );
        // MemoryMarshal.Write(span, record);
        // TODO: Read this https://learn.microsoft.com/en-us/archive/msdn-magazine/2018/january/csharp-all-about-span-exploring-a-new-net-mainstay
        using var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms))
        {
            Serialize(writer);
        }

        return new MessageMetadata(
            (int)ms.Length + 9,
            0,
            0, //(byte)Type, // TODO: Why is this here?
            ms.ToArray()
        );
    }
}