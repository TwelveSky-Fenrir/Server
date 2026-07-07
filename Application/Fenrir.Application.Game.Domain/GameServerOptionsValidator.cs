using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Domain;

/// <summary>Validates <see cref="GameServerOptions" /> at startup (ValidateOnStart) before any connection is accepted.</summary>
public sealed class GameServerOptionsValidator : IValidateOptions<GameServerOptions>
{
    public ValidateOptionsResult Validate(string? name, GameServerOptions options)
    {
        var errors = new List<string>();

        if (options.Port is <= 0 or > 65535) errors.Add($"Game:Port must be between 1 and 65535 (was {options.Port}).");
        if (options.ShardId == 0) errors.Add("Game:ShardId must be non-zero.");
        if (string.IsNullOrWhiteSpace(options.PublicHost)) errors.Add("Game:PublicHost must not be empty.");
        if (options.TicketTtlSeconds <= 0)
            errors.Add($"Game:TicketTtlSeconds must be positive (was {options.TicketTtlSeconds}).");
        if (string.IsNullOrWhiteSpace(options.GameDataDirectory))
            errors.Add("Game:GameDataDirectory must not be empty.");

        // Deliberately does NOT check that {GameDataDirectory}/WORLD/*.WM navmesh data actually exists on disk.
        // Legacy fails a zone process's entire startup fatally when its own .WM load fails (ServerDocs/12_ts25zone/
        // 21_MyWorld_MySummon_Navmesh_Spawn.md §8.1), but Fenrir's Zone.TryLoadGeometry degrades gracefully instead
        // (Zone.cs remarks on the Geometry property) because .WM is a multi-hundred-MB external asset that is not
        // committed to this repo -- as of this writing, no Fenrir-side deployment (dev, CI, or otherwise) ships
        // committed navmesh data, so failing ValidateOnStart on its absence here would currently prevent every
        // GameServer instance from starting at all, not just catch a real misconfiguration. Whether to eventually
        // adopt legacy's fatal-on-missing-mesh posture (once real navmesh assets are provisioned for a deployment)
        // is an open product decision this validator does not make unilaterally -- see the monster-ai-aggro-pathing
        // finding this comment was added for.
        if (options.TickRateHz <= 0) errors.Add($"Game:TickRateHz must be positive (was {options.TickRateHz}).");
        if (options.AoiCellSize <= 0) errors.Add($"Game:AoiCellSize must be positive (was {options.AoiCellSize}).");
        if (options.MaxPlausibleSpeedPerSecond <= 0)
            errors.Add($"Game:MaxPlausibleSpeedPerSecond must be positive (was {options.MaxPlausibleSpeedPerSecond}).");
        if (options.HeartbeatIntervalSeconds <= 0)
            errors.Add($"Game:HeartbeatIntervalSeconds must be positive (was {options.HeartbeatIntervalSeconds}).");
        if (options.HeroRankingRolloverCheckIntervalMinutes <= 0)
            errors.Add(
                $"Game:HeroRankingRolloverCheckIntervalMinutes must be positive (was {options.HeroRankingRolloverCheckIntervalMinutes}).");
        if (options.Capacity <= 0) errors.Add($"Game:Capacity must be positive (was {options.Capacity}).");
        if (options.AccountSessionPollIntervalSeconds <= 0)
            errors.Add(
                $"Game:AccountSessionPollIntervalSeconds must be positive (was {options.AccountSessionPollIntervalSeconds}).");
        if (options.MutePollIntervalSeconds <= 0)
            errors.Add($"Game:MutePollIntervalSeconds must be positive (was {options.MutePollIntervalSeconds}).");
        if (options.TempRegistrationIdleSweepIntervalSeconds <= 0)
            errors.Add(
                $"Game:TempRegistrationIdleSweepIntervalSeconds must be positive (was {options.TempRegistrationIdleSweepIntervalSeconds}).");

        // Each singleton RvR scheduler is armed by "does this shard host the designated map", not ShardId --
        // an operator who flips the *Enabled flag on must also name which map arms it, or the scheduler is
        // silently inert cluster-wide with no boot-time signal beyond SingletonRvrSchedulerGuard's warning.
        if (options.VoteTribeEnabled && options.VoteTribeMapId == 0)
            errors.Add("Game:VoteTribeMapId must be configured (nonzero) when Game:VoteTribeEnabled is true.");
        if (options.HolyStoneBattleEnabled && options.TribeSymbolBattleMapId == 0)
            errors.Add(
                "Game:TribeSymbolBattleMapId must be configured (nonzero) when Game:HolyStoneBattleEnabled is true.");
        if (options.HolyStoneWarEnabled && options.HolyStoneMapId == 0)
            errors.Add("Game:HolyStoneMapId must be configured (nonzero) when Game:HolyStoneWarEnabled is true.");
        if (options.AllianceTribeEnabled && options.AllianceTribeMapId == 0)
            errors.Add(
                "Game:AllianceTribeMapId must be configured (nonzero) when Game:AllianceTribeEnabled is true.");

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
