using Fenrir.Network.Abstractions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Network.Dispatch.Logging;

internal static partial class PacketLog
{
    [LoggerMessage(
        EventId = 4001,
        EventName = "PacketReceived",
        Level = LogLevel.Debug,
        Message =
            "Session {SessionId}: packet received, server {Server} opcode {Opcode} ({ByteSize} bytes, decoded in {DecodeMicroseconds:F1} us)")]
    public static partial void PacketReceived(
        this ILogger logger,
        long sessionId,
        FenrirServer server,
        byte opcode,
        int byteSize,
        double decodeMicroseconds);

    [LoggerMessage(
        EventId = 4002,
        EventName = "PacketSent",
        Level = LogLevel.Debug,
        Message = "Session {SessionId}: packet sent, opcode {Opcode} ({ByteSize} bytes)")]
    public static partial void PacketSent(this ILogger logger, long sessionId, byte opcode, int byteSize);

    [LoggerMessage(
        EventId = 4003,
        EventName = "PacketDispatched",
        Level = LogLevel.Debug,
        Message =
            "Session {SessionId}: packet dispatched, server {Server} opcode {Opcode} (handled in {DispatchMicroseconds:F1} us)")]
    public static partial void PacketDispatched(
        this ILogger logger,
        long sessionId,
        FenrirServer server,
        byte opcode,
        double dispatchMicroseconds);
}
