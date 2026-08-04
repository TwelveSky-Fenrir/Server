using System.Buffers.Binary;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Protocol.Game;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public readonly record struct TowerStatusRelaySnapshot(
    ImmutableArray<int> State1Tower,
    ImmutableArray<int> State2Tower)
{
    private const int SerializedStateByteCount = TowerWarState.TowerCount * sizeof(int) * 2;

    public static TowerStatusRelaySnapshot FromResponse(in TowerStatusResponse response)
    {
        if (!TryCreate(response.State1Tower, response.State2Tower, out var snapshot))
            throw new ArgumentException("Tower status response contains an invalid state snapshot.", nameof(response));

        return snapshot;
    }

    public static bool TryRead(ReadOnlySpan<byte> data, out TowerStatusRelaySnapshot snapshot)
    {
        snapshot = default;

        if (data.Length != ZoneCenterBroadcastIngestor.PayloadSize || !HasZeroSuffix(data))
            return false;

        var state1 = new int[TowerWarState.TowerCount];
        var state2 = new int[TowerWarState.TowerCount];

        for (var towerIndex = 0; towerIndex < TowerWarState.TowerCount; towerIndex++)
        {
            state1[towerIndex] = BinaryPrimitives.ReadInt32LittleEndian(data[(towerIndex * sizeof(int))..]);
            var attackStateOffset = (TowerWarState.TowerCount + towerIndex) * sizeof(int);
            state2[towerIndex] = BinaryPrimitives.ReadInt32LittleEndian(data[attackStateOffset..]);
        }

        return TryCreate(state1, state2, out snapshot);
    }

    public byte[] ToPayload()
    {
        var payload = new byte[ZoneCenterBroadcastIngestor.PayloadSize];

        for (var towerIndex = 0; towerIndex < TowerWarState.TowerCount; towerIndex++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(towerIndex * sizeof(int)), State1Tower[towerIndex]);
            BinaryPrimitives.WriteInt32LittleEndian(
                payload.AsSpan((TowerWarState.TowerCount + towerIndex) * sizeof(int)), State2Tower[towerIndex]);
        }

        return payload;
    }

    public TowerStatusResponse ToResponse()
    {
        return new TowerStatusResponse { State1Tower = [.. State1Tower], State2Tower = [.. State2Tower] };
    }

    public void ApplyTo(TowerWarState towerWar)
    {
        towerWar.ApplyStatusSnapshot(State1Tower.AsSpan(), State2Tower.AsSpan());
    }

    private static bool TryCreate(int[]? state1, int[]? state2, out TowerStatusRelaySnapshot snapshot)
    {
        snapshot = default;

        if (state1 is not { Length: TowerWarState.TowerCount } ||
            state2 is not { Length: TowerWarState.TowerCount })
            return false;

        for (var towerIndex = 0; towerIndex < TowerWarState.TowerCount; towerIndex++)
            if (!IsValidPackedState(state1[towerIndex]) || !IsValidAttackState(state2[towerIndex]))
                return false;

        snapshot = new TowerStatusRelaySnapshot([.. state1], [.. state2]);
        return true;
    }

    private static bool HasZeroSuffix(ReadOnlySpan<byte> data)
    {
        for (var offset = SerializedStateByteCount; offset < data.Length; offset++)
            if (data[offset] != 0)
                return false;

        return true;
    }

    private static bool IsValidPackedState(int packedState)
    {
        if (packedState == 0)
            return true;

        return TowerWarState.DecodeLevel(packedState) is 1 or 2 or 4 or 6 or 8 or 9 &&
               TowerWarState.DecodeType(packedState) is >= 1 and <= 3;
    }

    private static bool IsValidAttackState(int attackState)
    {
        return attackState is -1 or 0;
    }
}
