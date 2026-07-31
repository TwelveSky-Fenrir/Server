using Fenrir.Data.Abstractions.Progression;
using Microsoft.Extensions.Logging;

namespace Fenrir.Cluster.Center.WorldState;

public sealed class TowerStoreAuthority(ITowerRepository repository, ILogger<TowerStoreAuthority> logger)
{
    public const int TowerCount = 12;
    private readonly byte?[] _controllingTribe = new byte?[TowerCount];
    private readonly bool[] _dirty = new bool[TowerCount];

    private readonly Lock _lock = new();
    private readonly int[] _packedState = new int[TowerCount];
    private long _flushSequence;

    private bool _initialized;

    public async Task InitializeAsync(CancellationToken ct)
    {
        if (_initialized)
            throw new InvalidOperationException("TowerStoreAuthority.InitializeAsync must only run once, at boot.");

        await repository.EnsureInitializedAsync(ct).ConfigureAwait(false);
        var rows = await repository.GetAllAsync(ct).ConfigureAwait(false);

        lock (_lock)
        {
            foreach (var row in rows)
            {
                if (row.TowerIndex >= TowerCount)
                    continue;

                _packedState[row.TowerIndex] = row.Level > 0 ? row.Level * 100 + row.TowerType : 0;
                _controllingTribe[row.TowerIndex] = row.ControllingTribeId;
                _dirty[row.TowerIndex] = false;
            }

            _initialized = true;
        }

        logger.LogInformation("Center TowerStore loaded: {Count} tower rows", rows.Count);
    }

    public bool SetTowerState(int towerIndex, byte level, byte towerType, byte? controllingTribeId)
    {
        if (towerIndex < 0 || towerIndex >= TowerCount)
            return false;

        lock (_lock)
        {
            _packedState[towerIndex] = level > 0 ? level * 100 + towerType : 0;
            _controllingTribe[towerIndex] = controllingTribeId;
            _dirty[towerIndex] = true;
        }

        return true;
    }

    public int GetPackedState(int towerIndex)
    {
        lock (_lock)
        {
            return towerIndex >= 0 && towerIndex < TowerCount ? _packedState[towerIndex] : 0;
        }
    }

    public async ValueTask FlushDirtyAsync(CancellationToken ct)
    {
        for (var i = 0; i < TowerCount; i++)
        {
            int packed;
            byte? tribe;

            lock (_lock)
            {
                if (!_dirty[i])
                    continue;

                packed = _packedState[i];
                tribe = _controllingTribe[i];
            }

            var level = (byte)(packed < 1 ? 0 : packed / 100);
            var type = (byte)(packed < 1 ? 0 : packed % 100);

            try
            {
                await repository.SetProgressAsync((byte)i, level, type, tribe, ct).ConfigureAwait(false);

                lock (_lock)
                {
                    if (_packedState[i] == packed)
                        _dirty[i] = false;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Center TowerStore flush failed for tower {TowerIndex} -- retried next interval",
                    i);
            }
        }

        long sequence;
        lock (_lock)
        {
            sequence = ++_flushSequence;
        }

        logger.LogDebug("Center TowerStore flush #{Sequence} committed", sequence);
    }
}
