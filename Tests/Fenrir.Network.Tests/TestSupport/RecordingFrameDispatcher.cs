using System.Buffers;
using Fenrir.Network.Abstractions;

namespace Fenrir.Network.Tests.TestSupport;

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
