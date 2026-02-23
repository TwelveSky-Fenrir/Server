namespace Fenrir.LoginServer.Network.Metadata;

public record struct MessageMetadata(
    int Length,
    int SessionId,
    byte ProtocolId, // TODO: Why ProtocalId when we have enum?
    ReadOnlyMemory<byte> MessagePayload)
{
    public const int ByteSize = sizeof(int) + sizeof(int) + sizeof(byte);

    // TODO: Encrypted?
    // TODO: Should this just be for session data?
}