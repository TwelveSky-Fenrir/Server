using System.Buffers.Binary;
using System.Text;

namespace Fenrir.IntegrationTests.Wire;

/// <summary>
///     Hand-rolled write/read primitives for the top-level packet fields the bot builds/parses itself.
///     <c>IIncomingPacket</c> only ever generates <c>TryRead</c> (the real client never needs to serialize what
///     it receives) and <c>IOutgoingPacket</c> only ever generates <c>Write</c> (the server never needs to parse
///     what it sends) -- so a test bot playing BOTH roles has no generated code for the other half of each
///     packet, by design (see Generators/Fenrir.Generators.Protocol/Emitters/PacketEmitter.cs). Nested
///     <c>[FenrirWireType]</c> sub-structs (ActionInfo, AttackForProtocol, ObjectForMonster, ...) are not
///     affected -- those get both and are used directly via their own public Write/TryRead.
/// </summary>
internal static class WireScalars
{
    public static void WriteInt32(Span<byte> destination, int value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination, value);
    }

    public static void WriteUInt32(Span<byte> destination, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(destination, value);
    }

    public static int ReadInt32(ReadOnlySpan<byte> source)
    {
        return BinaryPrimitives.ReadInt32LittleEndian(source);
    }

    public static uint ReadUInt32(ReadOnlySpan<byte> source)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(source);
    }

    /// <summary>Null-terminated, zero-padded, Latin-1 -- mirrors the generated LegacyWireCodec.WriteFixedString exactly.</summary>
    public static void WriteFixedString(Span<byte> destination, string value)
    {
        destination.Clear();
        var count = Math.Min(value.Length, destination.Length);
        if (count > 0)
            Encoding.Latin1.GetBytes(value.AsSpan(0, count), destination);
    }

    public static string ReadFixedString(ReadOnlySpan<byte> source)
    {
        var nul = source.IndexOf((byte)0);
        return Encoding.Latin1.GetString(nul < 0 ? source : source[..nul]);
    }
}
