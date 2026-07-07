-- uppercom-playuser-extra-relay-opcodes (Major, legacy-behavior-translator contract): legacy re-checks a
-- sender's mute status live on every chat-send handler (ZPP_ZONE_CHECK_MUTE_FOR_PLAYUSER_SEND, opcode 45,
-- called from GENERAL_CHAT_SEND/GENERAL_SHOUT_SEND/GUILD_CHAT_SEND/TRIBE_CHAT_SEND/WORLD_CHAT_SEND --
-- Server/ts25zone/S04_MyWork02.cpp:7762-7784,8075-8096,10736-10759,11522-11541,15467-15484), so a GM
-- mute/unmute takes effect on the player's very next chat attempt, no reconnect required. Fenrir instead
-- loaded admin.Mutes exactly once at world entry (admin.usp_Mute_GetActiveForCharacter) and cached the
-- result for the rest of the session (PlayerRuntimeState.IsMuted) -- a mid-session mute/unmute had no
-- effect until the character's next world entry.
--
-- Legacy's own recheck is essentially free (an in-memory ts25playuser session-registry lookup); Fenrir's
-- IMuteRepository.IsActiveForCharacterAsync is a genuine SQL round trip, and every one of the five chat
-- send handlers this check is reached from is an IInlinePacketHandler<T> (Network/Fenrir.Network.
-- Abstractions/IInlinePacketHandler.cs's own contract: "never SQL or a contended lock" on that synchronous
-- session-loop path) -- so a literal per-message live requery is the wrong shape here, not a legacy-accurate
-- one. Instead, this migration adds a BATCHED read (one round trip for every online character on a shard)
-- that Application/Fenrir.Application.Game.Hosting/MuteRefreshPollHost.cs polls on a fixed interval
-- (GameServerOptions.MutePollIntervalSeconds, default 15s) and applies in place to each online
-- PlayerRuntimeState.IsMuted -- a bounded-staleness approximation of the legacy live recheck (both a
-- newly-applied and a newly-lifted mute now take effect within one poll interval instead of only at the
-- character's next world entry), chosen over per-message re-querying per the behavior contract's own
-- explicitly-listed "add a bounded-staleness periodic refresh" option.
--
-- admin.tvp_CharacterIdList mirrors runtime.tvp_AccountIdList's existing single-column shape
-- (Schemas/Types/runtime/tvp_AccountIdList.sql, batched input for usp_AccountSession_RefreshAndGetKicked) --
-- same "plain disk-based table type, not natively compiled" rationale.
CREATE TYPE admin.tvp_CharacterIdList AS TABLE
(
    CharacterId INT NOT NULL
);
GO

-- Batched sibling of the already-shipped admin.usp_Mute_GetActiveForCharacter (StoredProcedures/admin/
-- usp_Mute_GetActiveForCharacter.sql, left untouched -- still used by EnterWorldService's own one-shot
-- world-entry load) -- identical character-OR-owning-account match, evaluated for every supplied
-- CharacterId in one round trip instead of one row at a time. Returns only the subset that is currently
-- muted; a supplied id with no matching row is simply absent from the result set (caller treats "absent"
-- as "not muted").
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
GO
