using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game;

/// <summary>Validates <see cref="GameServerOptions" /> at startup (ValidateOnStart) before any connection is accepted.</summary>
public sealed class GameServerOptionsValidator : IValidateOptions<GameServerOptions>
{
    public ValidateOptionsResult Validate(string? name, GameServerOptions options)
    {
        var errors = new List<string>();

        if (options.Port is <= 0 or > 65535) errors.Add($"Game:Port must be between 1 and 65535 (was {options.Port}).");
        if (options.ShardId == 0) errors.Add("Game:ShardId must be non-zero.");

        // One zone actor per entry (ADR-0012) -- an empty set means a GameServer hosting no world at all,
        // which is always a configuration mistake (the [1] default lives in appsettings.json, see the
        // GameServerOptions.Maps remarks for why it cannot live in code).
        if (options.Maps.Length == 0)
            errors.Add("Game:Maps must declare at least one map id (e.g. Game__Maps__0=1).");
        if (options.Maps.Any(mapId => mapId <= 0))
            errors.Add($"Game:Maps entries must all be positive (was [{string.Join(", ", options.Maps)}]).");
        if (options.Maps.Distinct().Count() != options.Maps.Length)
            errors.Add($"Game:Maps must not contain duplicates (was [{string.Join(", ", options.Maps)}]).");
        if (string.IsNullOrWhiteSpace(options.PublicHost)) errors.Add("Game:PublicHost must not be empty.");
        if (string.IsNullOrWhiteSpace(options.GameDataDirectory))
            errors.Add("Game:GameDataDirectory must not be empty.");
        if (options.TickRateHz <= 0) errors.Add($"Game:TickRateHz must be positive (was {options.TickRateHz}).");
        if (options.AoiCellSize <= 0) errors.Add($"Game:AoiCellSize must be positive (was {options.AoiCellSize}).");
        if (options.MaxPlausibleSpeedPerSecond <= 0)
            errors.Add($"Game:MaxPlausibleSpeedPerSecond must be positive (was {options.MaxPlausibleSpeedPerSecond}).");
        if (options.HeartbeatIntervalSeconds <= 0)
            errors.Add($"Game:HeartbeatIntervalSeconds must be positive (was {options.HeartbeatIntervalSeconds}).");
        if (options.Capacity <= 0) errors.Add($"Game:Capacity must be positive (was {options.Capacity}).");

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
