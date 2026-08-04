using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Domain;

public sealed class GameServerOptionsValidator : IValidateOptions<GameServerOptions>
{
    public ValidateOptionsResult Validate(string? name, GameServerOptions options)
    {
        var errors = new List<string>();

        if (options.Port is <= 0 or > 65535) errors.Add($"Game:Port must be between 1 and 65535 (was {options.Port}).");
        if (options.ZoneBasePort is <= 0 or > 65000)
            errors.Add(
                $"Game:ZoneBasePort must be between 1 and 65000 (leaving headroom for ZoneBasePort + mapId; was {options.ZoneBasePort}).");
        if (options.ShardId == 0) errors.Add("Game:ShardId must be non-zero.");
        if (string.IsNullOrWhiteSpace(options.PublicHost)) errors.Add("Game:PublicHost must not be empty.");
        if (!GameRulesetRules.TryParse(options.Ruleset, out _))
            errors.Add(
                $"Game:Ruleset must be configured as Standard or ExtendedUpgrade (was '{options.Ruleset ?? "<null>"}').");
        if (options.TicketTtlSeconds <= 0)
            errors.Add($"Game:TicketTtlSeconds must be positive (was {options.TicketTtlSeconds}).");
        if (options.ShardReachabilityProbeTimeoutMilliseconds <= 0)
            errors.Add(
                $"Game:ShardReachabilityProbeTimeoutMilliseconds must be positive (was {options.ShardReachabilityProbeTimeoutMilliseconds}).");
        if (string.IsNullOrWhiteSpace(options.GameDataDirectory))
            errors.Add("Game:GameDataDirectory must not be empty.");

        if (options.TickRateHz <= 0) errors.Add($"Game:TickRateHz must be positive (was {options.TickRateHz}).");
        if (options.AoiCellSize <= 0) errors.Add($"Game:AoiCellSize must be positive (was {options.AoiCellSize}).");
        if (options.MaxPlausibleMoveDistance <= 0)
            errors.Add($"Game:MaxPlausibleMoveDistance must be positive (was {options.MaxPlausibleMoveDistance}).");
        if (options.HeartbeatIntervalSeconds <= 0)
            errors.Add($"Game:HeartbeatIntervalSeconds must be positive (was {options.HeartbeatIntervalSeconds}).");
        if (options.HeroRankingRolloverCheckIntervalMinutes <= 0)
            errors.Add(
                $"Game:HeroRankingRolloverCheckIntervalMinutes must be positive (was {options.HeroRankingRolloverCheckIntervalMinutes}).");
        if (options.Capacity <= 0) errors.Add($"Game:Capacity must be positive (was {options.Capacity}).");
        if (options.MaxConnectionsPerIp <= 0)
            errors.Add($"Game:MaxConnectionsPerIp must be positive (was {options.MaxConnectionsPerIp}).");
        if (options.MaxProtocolViolationsPerIpPerHour <= 0)
            errors.Add(
                $"Game:MaxProtocolViolationsPerIpPerHour must be positive (was {options.MaxProtocolViolationsPerIpPerHour}).");
        if (options.AccountSessionPollIntervalSeconds <= 0)
            errors.Add(
                $"Game:AccountSessionPollIntervalSeconds must be positive (was {options.AccountSessionPollIntervalSeconds}).");
        if (options.MutePollIntervalSeconds <= 0)
            errors.Add($"Game:MutePollIntervalSeconds must be positive (was {options.MutePollIntervalSeconds}).");
        if (options.TempRegistrationIdleSweepIntervalSeconds <= 0)
            errors.Add(
                $"Game:TempRegistrationIdleSweepIntervalSeconds must be positive (was {options.TempRegistrationIdleSweepIntervalSeconds}).");
        if (options.SessionLivenessSweepIntervalSeconds <= 0)
            errors.Add(
                $"Game:SessionLivenessSweepIntervalSeconds must be positive (was {options.SessionLivenessSweepIntervalSeconds}).");
        if (options.GuildTribeBroadcastPollIntervalSeconds <= 0)
            errors.Add(
                $"Game:GuildTribeBroadcastPollIntervalSeconds must be positive (was {options.GuildTribeBroadcastPollIntervalSeconds}).");
        if (options.GuildTribeBroadcastRetentionSeconds <= 0)
            errors.Add(
                $"Game:GuildTribeBroadcastRetentionSeconds must be positive (was {options.GuildTribeBroadcastRetentionSeconds}).");
        if (options.SocialCrossShardRelayPollIntervalSeconds <= 0)
            errors.Add(
                $"Game:SocialCrossShardRelayPollIntervalSeconds must be positive (was {options.SocialCrossShardRelayPollIntervalSeconds}).");
        if (options.SocialCrossShardRelayRetentionSeconds <= 0)
            errors.Add(
                $"Game:SocialCrossShardRelayRetentionSeconds must be positive (was {options.SocialCrossShardRelayRetentionSeconds}).");
        if (options.ChatCrossShardRelayPollIntervalSeconds <= 0)
            errors.Add(
                $"Game:ChatCrossShardRelayPollIntervalSeconds must be positive (was {options.ChatCrossShardRelayPollIntervalSeconds}).");
        if (options.ChatCrossShardRelayRetentionSeconds <= 0)
            errors.Add(
                $"Game:ChatCrossShardRelayRetentionSeconds must be positive (was {options.ChatCrossShardRelayRetentionSeconds}).");
        if (options.ProxyShopExpirationRelayPollIntervalSeconds <= 0)
            errors.Add(
                $"Game:ProxyShopExpirationRelayPollIntervalSeconds must be positive (was {options.ProxyShopExpirationRelayPollIntervalSeconds}).");
        if (options.ProxyShopExpirationRelayRetentionSeconds <= 0)
            errors.Add(
                $"Game:ProxyShopExpirationRelayRetentionSeconds must be positive (was {options.ProxyShopExpirationRelayRetentionSeconds}).");
        if (options.GuildBuffExpiryRelayPollIntervalSeconds <= 0)
            errors.Add(
                $"Game:GuildBuffExpiryRelayPollIntervalSeconds must be positive (was {options.GuildBuffExpiryRelayPollIntervalSeconds}).");
        if (options.GuildBuffExpiryRelayRetentionSeconds <= 0)
            errors.Add(
                $"Game:GuildBuffExpiryRelayRetentionSeconds must be positive (was {options.GuildBuffExpiryRelayRetentionSeconds}).");
        if (options.GuildStateRelayPollIntervalSeconds <= 0)
            errors.Add(
                $"Game:GuildStateRelayPollIntervalSeconds must be positive (was {options.GuildStateRelayPollIntervalSeconds}).");
        if (options.GuildStateRelayRetentionSeconds <= 0)
            errors.Add(
                $"Game:GuildStateRelayRetentionSeconds must be positive (was {options.GuildStateRelayRetentionSeconds}).");
        if (options.RvrSiegeEventRelayPollIntervalSeconds <= 0)
            errors.Add(
                $"Game:RvrSiegeEventRelayPollIntervalSeconds must be positive (was {options.RvrSiegeEventRelayPollIntervalSeconds}).");
        if (options.RvrSiegeEventRelayRetentionSeconds <= 0)
            errors.Add(
                $"Game:RvrSiegeEventRelayRetentionSeconds must be positive (was {options.RvrSiegeEventRelayRetentionSeconds}).");
        if (options.PartyResyncRelayPollIntervalSeconds <= 0)
            errors.Add(
                $"Game:PartyResyncRelayPollIntervalSeconds must be positive (was {options.PartyResyncRelayPollIntervalSeconds}).");
        if (options.PartyResyncRelayRetentionSeconds <= 0)
            errors.Add(
                $"Game:PartyResyncRelayRetentionSeconds must be positive (was {options.PartyResyncRelayRetentionSeconds}).");

        if (options.GuildTribeBroadcastRetentionSeconds <= options.GuildTribeBroadcastPollIntervalSeconds)
            errors.Add(
                $"Game:GuildTribeBroadcastRetentionSeconds ({options.GuildTribeBroadcastRetentionSeconds}) must be greater than Game:GuildTribeBroadcastPollIntervalSeconds ({options.GuildTribeBroadcastPollIntervalSeconds}).");
        if (options.SocialCrossShardRelayRetentionSeconds <= options.SocialCrossShardRelayPollIntervalSeconds)
            errors.Add(
                $"Game:SocialCrossShardRelayRetentionSeconds ({options.SocialCrossShardRelayRetentionSeconds}) must be greater than Game:SocialCrossShardRelayPollIntervalSeconds ({options.SocialCrossShardRelayPollIntervalSeconds}).");
        if (options.ChatCrossShardRelayRetentionSeconds <= options.ChatCrossShardRelayPollIntervalSeconds)
            errors.Add(
                $"Game:ChatCrossShardRelayRetentionSeconds ({options.ChatCrossShardRelayRetentionSeconds}) must be greater than Game:ChatCrossShardRelayPollIntervalSeconds ({options.ChatCrossShardRelayPollIntervalSeconds}).");
        if (options.ProxyShopExpirationRelayRetentionSeconds <= options.ProxyShopExpirationRelayPollIntervalSeconds)
            errors.Add(
                $"Game:ProxyShopExpirationRelayRetentionSeconds ({options.ProxyShopExpirationRelayRetentionSeconds}) must be greater than Game:ProxyShopExpirationRelayPollIntervalSeconds ({options.ProxyShopExpirationRelayPollIntervalSeconds}).");
        if (options.RvrSiegeEventRelayRetentionSeconds <= options.RvrSiegeEventRelayPollIntervalSeconds)
            errors.Add(
                $"Game:RvrSiegeEventRelayRetentionSeconds ({options.RvrSiegeEventRelayRetentionSeconds}) must be greater than Game:RvrSiegeEventRelayPollIntervalSeconds ({options.RvrSiegeEventRelayPollIntervalSeconds}).");
        if (options.GuildBuffExpiryRelayRetentionSeconds <= options.GuildBuffExpiryRelayPollIntervalSeconds)
            errors.Add(
                $"Game:GuildBuffExpiryRelayRetentionSeconds ({options.GuildBuffExpiryRelayRetentionSeconds}) must be greater than Game:GuildBuffExpiryRelayPollIntervalSeconds ({options.GuildBuffExpiryRelayPollIntervalSeconds}).");
        if (options.GuildStateRelayRetentionSeconds <= options.GuildStateRelayPollIntervalSeconds)
            errors.Add(
                $"Game:GuildStateRelayRetentionSeconds ({options.GuildStateRelayRetentionSeconds}) must be greater than Game:GuildStateRelayPollIntervalSeconds ({options.GuildStateRelayPollIntervalSeconds}).");
        if (options.PartyResyncRelayRetentionSeconds <= options.PartyResyncRelayPollIntervalSeconds)
            errors.Add(
                $"Game:PartyResyncRelayRetentionSeconds ({options.PartyResyncRelayRetentionSeconds}) must be greater than Game:PartyResyncRelayPollIntervalSeconds ({options.PartyResyncRelayPollIntervalSeconds}).");

        if (options.AlliancePostRadius <= 0)
            errors.Add($"Game:AlliancePostRadius must be positive (was {options.AlliancePostRadius}).");
        if (options.HolyStoneCaptureRadius <= 0)
            errors.Add($"Game:HolyStoneCaptureRadius must be positive (was {options.HolyStoneCaptureRadius}).");
        if (options.HolyStoneParticipationRadius <= 0)
            errors.Add(
                $"Game:HolyStoneParticipationRadius must be positive (was {options.HolyStoneParticipationRadius}).");
        if (options.HolyStoneParticipationRadius < options.HolyStoneCaptureRadius)
            errors.Add(
                $"Game:HolyStoneParticipationRadius ({options.HolyStoneParticipationRadius}) must be greater than or equal to Game:HolyStoneCaptureRadius ({options.HolyStoneCaptureRadius}).");

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
        if (options.MonsterSymbolAttackNotifyEnabled && options.MonsterSymbolAttackNotifyDelayMinutes <= 0)
            errors.Add(
                "Game:MonsterSymbolAttackNotifyDelayMinutes must be positive when Game:MonsterSymbolAttackNotifyEnabled is true.");
        if (options.HolyStoneBattleEnabled && !options.HolyStoneTestMode && options.HolyStoneBattleDays.Count == 0)
            errors.Add(
                "Game:HolyStoneBattleDays must be non-empty when Game:HolyStoneBattleEnabled is true and Game:HolyStoneTestMode is false (the schedule would otherwise never fire).");
        if (options.MonsterSymbolAttackNotifyEnabled)
        {
            var missingTribeIds = new List<byte>();
            for (byte tribeId = 0; tribeId < 4; tribeId++)
                if (!options.MonsterSymbolAttackNotifyMapIds.ContainsKey(tribeId))
                    missingTribeIds.Add(tribeId);
            if (missingTribeIds.Count > 0)
                errors.Add(
                    $"Game:MonsterSymbolAttackNotifyMapIds must contain an entry for every tribe id (0-3) when Game:MonsterSymbolAttackNotifyEnabled is true (missing: {string.Join(", ", missingTribeIds)}).");
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
