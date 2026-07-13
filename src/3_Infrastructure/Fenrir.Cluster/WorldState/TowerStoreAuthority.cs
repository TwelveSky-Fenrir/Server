using Fenrir.Data.Abstractions.Progression;
using Microsoft.Extensions.Logging;

namespace Fenrir.Cluster.WorldState;

/// <summary>
///     The CenterServer's single authoritative writer of the 12-tower primary-state aggregate. Preloads the
///     primary states at boot and flushes changed towers on the ~6s cadence and on stop.
/// </summary>
/// <remarks>
///     Reimplemented from the tower aggregate (Server/ts25center/S08_MyDB.cpp:469-513). Only the primary state
///     array (<c>mState1Tower[0..11]</c>) is persisted -- the legacy secondary array is filled with <c>-1</c> at
///     boot and never written back in the cited range (flagged in the contract for a re-check; not persisted
///     here either). Reuses the existing <see cref="ITowerRepository" /> procedures; no new schema.
///     <para>
///         The Center-side write source (who sets tower states) is a separate ingest path and is DORMANT this
///         lot; this store provides the authoritative storage, preload and flush so that path has a single
///         writer to target when Lot 5 flips the authority.
///     </para>
/// </remarks>
public sealed class TowerStoreAuthority(ITowerRepository repository, ILogger<TowerStoreAuthority> logger)
{
    public const int TowerCount = 12;

    private readonly Lock _lock = new();
    private readonly int[] _packedState = new int[TowerCount];
    private readonly byte?[] _controllingTribe = new byte?[TowerCount];
    private readonly bool[] _dirty = new bool[TowerCount];

    private bool _initialized;
    private long _flushSequence;

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

    /// <summary>Publishes a tower's authoritative primary state (packed level*100+type) and controller.</summary>
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
                logger.LogError(ex, "Center TowerStore flush failed for tower {TowerIndex} -- retried next interval", i);
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
