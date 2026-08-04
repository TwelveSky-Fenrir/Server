using System.Collections.Concurrent;
using System.Threading.Channels;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Hosting.World;

public sealed class PvpKillCooldownClaimHost : BackgroundService, IPvpKillCooldownClaimQueue
{
    private const int Capacity = 4096;

    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(1);

    private readonly Channel<PvpKillCooldownClaim> _claims = Channel.CreateBounded<PvpKillCooldownClaim>(
        new BoundedChannelOptions(Capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.Wait
        });

    private readonly IPvpKillCooldownRepository _cooldowns;
    private readonly ILogger<PvpKillCooldownClaimHost> _logger;
    private readonly ConcurrentDictionary<Guid, PvpKillCooldownClaim> _retrying = new();
    private readonly ZoneRegistry _zones;

    public PvpKillCooldownClaimHost(
        ZoneRegistry zones,
        IPvpKillCooldownRepository cooldowns,
        ILogger<PvpKillCooldownClaimHost> logger)
    {
        _zones = zones;
        _cooldowns = cooldowns;
        _logger = logger;
    }

    public bool TryEnqueue(in PvpKillCooldownClaim claim)
    {
        return claim.IsValid && _claims.Writer.TryWrite(claim);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(RetryInterval);
        var reader = _claims.Reader;
        var readReady = reader.WaitToReadAsync(stoppingToken).AsTask();
        var timerTick = timer.WaitForNextTickAsync(stoppingToken).AsTask();

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var completed = await Task.WhenAny(readReady, timerTick).ConfigureAwait(false);

                if (completed == readReady)
                {
                    while (reader.TryRead(out var claim))
                        await ProcessAsync(claim, stoppingToken).ConfigureAwait(false);

                    readReady = reader.WaitToReadAsync(stoppingToken).AsTask();
                }

                if (completed != timerTick)
                    continue;

                await RetryPendingAsync(stoppingToken).ConfigureAwait(false);
                timerTick = timer.WaitForNextTickAsync(stoppingToken).AsTask();
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task RetryPendingAsync(CancellationToken ct)
    {
        foreach (var claim in _retrying.Values)
            await ProcessAsync(claim, ct).ConfigureAwait(false);
    }

    private async Task ProcessAsync(PvpKillCooldownClaim claim, CancellationToken ct)
    {
        try
        {
            var result = await _cooldowns.TryClaimAsync(claim.ClaimId, claim.AttackerCharacterId,
                claim.DefenderCharacterId, ct).ConfigureAwait(false);

            if (!result.WasAccepted)
            {
                _retrying.TryRemove(claim.ClaimId, out _);
                return;
            }

            if (!_zones.TryGet(claim.MapId, out var zone) || !zone.Post(ZoneCommand.ApplyPvpKillRewardClaim(claim)))
            {
                _retrying[claim.ClaimId] = claim;
                return;
            }

            _retrying.TryRemove(claim.ClaimId, out _);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _retrying[claim.ClaimId] = claim;
            _logger.LogError(ex,
                "PvP kill cooldown claim {ClaimId} for attacker {AttackerCharacterId} and defender {DefenderCharacterId} " +
                "could not be resolved; it will be retried before any actor reward mutation",
                claim.ClaimId, claim.AttackerCharacterId, claim.DefenderCharacterId);
        }
    }
}
