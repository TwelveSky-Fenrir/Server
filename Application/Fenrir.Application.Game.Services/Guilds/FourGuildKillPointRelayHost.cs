using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Guilds;

public sealed class FourGuildKillPointRelayHost : BackgroundService, IFourGuildKillPointQueue
{

        public const int Capacity = 1024;

    private readonly Channel<int> _channel;
    private readonly ILogger<FourGuildKillPointRelayHost> _logger;
    private readonly FourGuildScoringService _scoring;

    public FourGuildKillPointRelayHost(FourGuildScoringService scoring, ILogger<FourGuildKillPointRelayHost> logger)
    {
        _scoring = scoring;
        _logger = logger;

        _channel = Channel.CreateBounded<int>(new BoundedChannelOptions(Capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.Wait
        });
    }

        public bool Enqueue(int guildId)
    {
        if (_channel.Writer.TryWrite(guildId))
            return true;

        _logger.LogWarning(
            "Four-guild kill-point relay queue full; point for guild {GuildId} dropped (sustained DB unavailability?)",
            guildId);
        return false;
    }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var guildId in _channel.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
                await _scoring.AccrueKillPointAsync(guildId, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }
}
