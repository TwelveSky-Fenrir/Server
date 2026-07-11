using Fenrir.Application.Game.Domain.World;
using Fenrir.Data.WriteBehind;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Hosting.World;

public interface ICharacterWriteBehindFlusher : IWriteBehindFlusher
{

        public ValueTask FlushCharacterNowAsync(int characterId, CancellationToken ct);
}

public sealed class PositionWriteBehindHost : BackgroundService, ICharacterWriteBehindFlusher
{
    private readonly ICharacterRepository _characters;
    private readonly WriteBehindFlusher<int> _flusher;

    private readonly SemaphoreSlim _flushGate = new(1, 1);
    private readonly ILogger<PositionWriteBehindHost> _logger;
    private readonly ZoneRegistry _zones;

    public PositionWriteBehindHost(ZoneRegistry zones, DirtyTracker<int> dirtyTracker, ICharacterRepository characters,
        ProgressWriteBehindHost progress, ILogger<PositionWriteBehindHost> logger)
    {
        _zones = zones;
        _characters = characters;
        _logger = logger;

        _flusher = new WriteBehindFlusher<int>(
            dirtyTracker,
            async (dirty, ct) =>
            {
                await _flushGate.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    var claimedByProgress = await progress.FlushAsync(dirty, ct).ConfigureAwait(false);

                    var rows = new List<CharacterPositionTvp>(dirty.Count);

                    foreach (var (characterId, flags) in dirty)
                    {
                        if ((flags & DirtyFlags.Position) == 0)
                            continue;

                        if (claimedByProgress.Contains(characterId))
                        {
                            dirtyTracker.MarkDirty(characterId, DirtyFlags.Position);
                            continue;
                        }

                        if (zones.TryGetPlayer(characterId, out var state))
                            rows.Add(new CharacterPositionTvp(characterId, state.FlushSequence, state.MapId, state.PosX,
                                state.PosY, state.PosZ, state.Heading));
                    }

                    await characters.PersistPositionsAsync(rows, ct).ConfigureAwait(false);
                }
                finally
                {
                    _flushGate.Release();
                }
            },
            onFlushError: ex => logger.LogError(ex, "Character write-behind flush failed (position and/or progress)"));
    }

    public void RequestImmediateFlush()
    {
        _flusher.RequestImmediateFlush();
    }

        public async ValueTask FlushCharacterNowAsync(int characterId, CancellationToken ct)
    {
        await _flushGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!_zones.TryGetPlayer(characterId, out var state))
            {
                _logger.LogDebug(
                    "FlushCharacterNowAsync({CharacterId}): character not found in any zone's live registry -- nothing to persist",
                    characterId);
                return;
            }

            var progressRow = new CharacterProgressTvp(characterId, state.FlushSequence, state.Level, state.Level2,
                state.Experience, state.Life, state.MaxLife, state.Mana, state.MaxMana, state.StatVit, state.StatStr,
                state.StatInt, state.StatDex, state.StatPoints, state.SkillPoints, state.ContributionPoints,
                state.Exp2, state.RebirthCount, state.EatLifePotion, state.EatManaPotion, state.EatStrPotion,
                state.EatDexPotion, state.EatElePotion, state.DropItemTime, state.M15PetLuckyBoxPity);
            await _characters.PersistProgressAsync([progressRow], ct).ConfigureAwait(false);

            var positionRow = new CharacterPositionTvp(characterId, state.FlushSequence + 1, state.MapId, state.PosX,
                state.PosY, state.PosZ, state.Heading);
            await _characters.PersistPositionsAsync([positionRow], ct).ConfigureAwait(false);
        }
        finally
        {
            _flushGate.Release();
        }
    }

        public async ValueTask DisposeAsync()
    {
        await _flusher.DisposeAsync().ConfigureAwait(false);
        _flushGate.Dispose();
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return _flusher.RunAsync(stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
        await _flusher.DisposeAsync().ConfigureAwait(false);
    }
}
