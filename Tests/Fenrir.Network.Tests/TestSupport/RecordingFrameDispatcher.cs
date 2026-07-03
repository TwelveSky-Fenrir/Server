using System.Buffers;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Wire;

namespace Fenrir.Network.Tests.TestSupport;

/// <summary>
///     One recorded <see cref="IFrameDispatcher.DispatchAsync" /> call. <see cref="Payload" /> is materialized
///     (copied out of the <see cref="ReadOnlySequence{T}" />) because that sequence is only valid for the
///     duration of the call — assertions run well after it returns.
/// </summary>
internal readonly record struct DispatchRecord(FenrirServer Server, byte Opcode, byte[] Payload, long SessionId);

/// <summary>Test double for <see cref="IFrameDispatcher" />: records every call instead of routing to a real handler.</summary>
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
