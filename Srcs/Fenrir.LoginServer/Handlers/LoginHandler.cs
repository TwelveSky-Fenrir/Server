using System.Runtime.InteropServices;
using System.Text;
using Fenrir.LoginServer.Network.Metadata;
using Fenrir.Network.Framing;
using Fenrir.Network.Helpers;
using Fenrir.Network.Transport;
using Microsoft.Extensions.Logging;

namespace Fenrir.LoginServer.Handlers;

public class LoginHandler(ILogger<LoginHandler> logger)
{
    // TODO: Can logger be DI?
    private readonly ILogger<LoginHandler> _logger = logger;

    // TODO: Can packets be structs?
    // TODO: Can use Builder pattern?

    // https://stackoverflow.com/questions/46279646/safe-fixed-size-array-in-struct-c-sharp
    
    
    // [Packet(PacketType.LoginRequest)]
    // [StructLayout(LayoutKind.Sequential, Pack = 1)]
    // public struct Ts2PacketCompressed : Ts2Packet
    // {
    //     public bool isCompressed;
    //     public uint compressedSize;
    //     public uint uncompressedSize;
    //     
    //     // Note: Decompression must make a copy of the data.
    //     [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(CompressedDataMarshaler))]
    //     public byte[] data;
    //     //
    //     // public Ts2Packet Decompress()
    //     // {
    //     //     if (!isCompressed)
    //     //     {
    //     //         return this;
    //     //     }
    //     //     
    //     //     byte[] decompressedData = CompressionHelper.Decompress(data, compressedSize, uncompressedSize);
    //     //     return new Ts2Packet
    //     //     {
    //     //         packetType = packetType,
    //     //         unknown1 = unknown1,
    //     //         unknown2 = unknown2,
    //     //         data = decompressedData
    //     //     };
    //     // }
    // }
    //
    // public class CompressedDataMarshaler : ICustomMarshaler
    // {
    //     private static readonly CompressedDataMarshaler _instance = new CompressedDataMarshaler();
    //
    //     public static ICustomMarshaler GetInstance(string cookie) => _instance;
    //
    //     public void CleanUpManagedData(object ManagedObj) { }
    //
    //     public void CleanUpNativeData(IntPtr pNativeData)
    //     {
    //         Marshal.FreeHGlobal(pNativeData);
    //     }
    //
    //     public int GetNativeDataSize() => -1;
    //
    //     public IntPtr MarshalManagedToNative(object ManagedObj)
    //     {
    //         if (ManagedObj is byte[] data)
    //         {
    //             IntPtr pNativeData = Marshal.AllocHGlobal(data.Length);
    //             Marshal.Copy(data, 0, pNativeData, data.Length);
    //             return pNativeData;
    //         }
    //         return IntPtr.Zero;
    //     }
    //
    //     public object MarshalNativeToManaged(IntPtr pNativeData)
    //     {
    //         // This method should not be used directly.
    //         throw new NotImplementedException();
    //     }
    // }
    //
    // [Packet(PacketType.LoginRequest)]
    // [StructLayout(LayoutKind.Sequential, Pack = 1)]
    // public unsafe struct LoginRequestPacket : IPacket
    // {
    //     //[MarshalAs(UnmanagedType.ByValArray, SizeConst = 13)]
    //     public fixed byte name[13];
    //
    //     //[MarshalAs(UnmanagedType.ByValArray, SizeConst = 13)]
    //     public fixed byte password[13];
    //
    //     public void SetUsername(string value)
    //     {
    //         if (value.Length > 12)
    //             throw new ArgumentException("Username cannot be longer than 12 characters.");
    //
    //         name ??= new byte[13];
    //         var bytes = Encoding.ASCII.GetBytes(value);
    //         Array.Clear(name, 0, 13);
    //         Array.Copy(bytes, name, bytes.Length);
    //     }
    //
    //     public void SetPassword(string value)
    //     {
    //         if (value.Length > 12)
    //             throw new ArgumentException("Password cannot be longer than 12 characters.");
    //
    //         password ??= new byte[13];
    //         var bytes = Encoding.ASCII.GetBytes(value);
    //         Array.Clear(password, 0, 13);
    //         Array.Copy(bytes, password, bytes.Length);
    //     }
    //
    //     public string GetUsername()
    //     {
    //         return Encoding.ASCII.GetString(name).TrimEnd('\0');
    //     }
    //
    //     public string GetPassword()
    //     {
    //         return Encoding.ASCII.GetString(password).TrimEnd('\0');
    //     }
    // }
    
    [Packet(PacketType.LoginRequest)]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct LoginRequestPacket : IPacket
    {
        private fixed byte name[13];
        private fixed byte password[13];

        public void SetUsername(string value)
        {
            // TODO: Can I make the setter a func or a custom type with a marshaler?
            if (value.Length > 12)
                throw new ArgumentException("Username cannot be longer than 12 characters.");

            fixed (byte* namePtr = name)
            {
                var bytes = Encoding.ASCII.GetBytes(value);
                for (int i = 0; i < 13; i++)
                {
                    namePtr[i] = i < bytes.Length ? bytes[i] : (byte)0;
                }
            }
        }

        public void SetPassword(string value)
        {
            if (value.Length > 12)
                throw new ArgumentException("Password cannot be longer than 12 characters.");

            fixed (byte* passwordPtr = password)
            {
                // TODO: Fix this
                var bytes = Encoding.ASCII.GetBytes(value);
                for (int i = 0; i < 13; i++)
                {
                    passwordPtr[i] = i < bytes.Length ? bytes[i] : (byte)0;
                }
            }
        }

        public string GetUsername()
        {
            // TODO: Can I make this get string a function
            // TODO: Can this be done without allocations?
            fixed (byte* namePtr = name)
            {
                Span<byte> nameSpan = new Span<byte>(namePtr, 13);
                return Encoding.ASCII.GetString(nameSpan).TrimEnd('\0');
                //return Encoding.ASCII.GetString(namePtr, 13).TrimEnd('\0');
            }
        }

        public string GetPassword()
        {
            fixed (byte* passwordPtr = password)
            {
                return Encoding.ASCII.GetString(passwordPtr, 13).TrimEnd('\0');
            }
        }
    }

    
    
    //
    // [Packet(PacketType.LoginRequest)]
    // [StructLayout(LayoutKind.Sequential, Pack = 1)]
    // public struct LoginRequestPacket : IPacket
    // {
    //     // Note; Try StringBuilder?
    //     [MarshalAs(UnmanagedType.ByValArray, SizeConst = 13)]
    //     public byte[] name;
    //
    //     [MarshalAs(UnmanagedType.ByValArray, SizeConst = 13)]
    //     public byte[] password;
    //
    //     // TODO: A better way to read/write these strings.
    //     // Unfortunately in C# char is not good here because it is UTF-16.
    //     // Game pretty much always uses ASCII but maps its self as needed into some other charset in specific language clients.
    //     // Although most I've seen have been custom sets and they generally have overlap with ASCII.
    //     
    //     public string Username
    //     {
    //         get => Encoding.ASCII.GetString(name).TrimEnd('\0');
    //         set
    //         {
    //             if (value.Length > 12)
    //                 throw new ArgumentException("Username cannot be longer than 12 characters.");
    //
    //             name ??= new byte[13];
    //             var bytes = Encoding.ASCII.GetBytes(value);
    //             Array.Clear(name, 0, name.Length);
    //             Array.Copy(bytes, name, bytes.Length);
    //         }
    //     }
    //
    //     public string Password
    //     {
    //         get => Encoding.ASCII.GetString(password).TrimEnd('\0');
    //         set
    //         {
    //             if (value.Length > 12)
    //                 throw new ArgumentException("Password cannot be longer than 12 characters.");
    //
    //             password ??= new byte[13];
    //             var bytes = Encoding.ASCII.GetBytes(value);
    //             Array.Clear(password, 0, password.Length);
    //             Array.Copy(bytes, password, bytes.Length);
    //         }
    //     }
    // }

    // public class LoginPacket : Packet
    // {
    //     public override PacketType PacketType => PacketType.LoginRequest;
    //     
    //     public string Username { get; set; }
    //     public string Password { get; set; }
    //
    //     public override void Deserialize(BinaryReader reader)
    //     {
    //         throw new NotImplementedException();
    //     }
    //
    //     public override void Serialize(BinaryWriter writer)
    //     {
    //         throw new NotImplementedException();
    //     }
    // }
    //
    // TODO: make a MySession Session
    // TODO: can this be handled better?
    // public ValueTask HandleLoginAsync(Session session, IPacket packet)
    // {
    //     return HandleLoginAsync(session, (LoginRequestPacket)packet);
    // }
    
    [PacketHandler]
    public ValueTask HandleLoginAsync(Session session, LoginRequestPacket packet)
    {
        // TODO: Validation? Throw if bad content?
        _logger.LogInformation("Handling login request for {Username}", packet.GetUsername());
        return ValueTask.CompletedTask;
    }
}