using System.Buffers;
using Fenrir.Network.Abstractions;

namespace Fenrir.Network.Tests.TestSupport;

// Payload is copied out of the ReadOnlySequence<byte> because that sequence is only valid for the
// duration of the DispatchAsync call, and assertions run after it returns.
internal readonly record struct DispatchRecord(FenrirServer Server, byte Opcode, byte[] Payload, long SessionId);

internal sealed class RecordingFrameDispatcher : IFrameDispatcher
{
    private readonly Lock _gate = new();
    private readonly List<DispatchRecord> _records = [];

    public IReadOnlyList<DispatchRecord> Records
    {
        get
        {
            lock (_gate)
            {
                return _records.ToArray();
            }
        }
    }

    public ValueTask DispatchAsync(FenrirServer server, byte opcode, ReadOnlySequence<byte> payload,
        IPacketSession session, CancellationToken cancellationToken)
    {
        var record = new DispatchRecord(server, opcode, payload.ToArray(), session.SessionId);

        lock (_gate)
        {
            _records.Add(record);
        }

        return ValueTask.CompletedTask;
    }
}
