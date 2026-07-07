-- Batched sibling of usp_Mute_GetActiveForCharacter (left untouched -- still used by EnterWorldService's own
-- one-shot world-entry load). Identical character-OR-owning-account match, evaluated for every supplied
-- CharacterId in one round trip instead of one row at a time -- polled by MuteRefreshPollHost on a fixed
-- interval (GameServerOptions.MutePollIntervalSeconds) to apply a mid-session mute/unmute to each online
-- PlayerRuntimeState.IsMuted, since legacy re-checks live on every chat-send handler
-- (ZPP_ZONE_CHECK_MUTE_FOR_PLAYUSER_SEND) and a per-message SQL round trip is the wrong shape for an
-- IInlinePacketHandler<T> hot path. Returns only the subset that is currently muted; a supplied id with no
-- matching row is simply absent from the result set (caller treats "absent" as "not muted").
CREATE PROCEDURE admin.usp_Mute_GetActiveForCharacters @CharacterIds admin.tvp_CharacterIdList READONLY
AS
BEGIN
    SET NOCOUNT ON;

    SELECT DISTINCT ids.CharacterId
    FROM @CharacterIds AS ids
             INNER JOIN game.Characters AS c ON c.CharacterId = ids.CharacterId
             INNER JOIN admin.Mutes AS m
                        ON (m.CharacterId = ids.CharacterId OR m.AccountId = c.AccountId)
                            AND m.LiftedAtUtc IS NULL
                            AND (m.ExpiresAtUtc IS NULL OR m.ExpiresAtUtc > SYSUTCDATETIME());
END;
