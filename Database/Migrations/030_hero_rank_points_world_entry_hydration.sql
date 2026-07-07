-- hero-rank-points-world-entry-hydration (Major): PlayerRuntimeState.HeroRankPoints always started at 0 for
-- every session instead of hydrating the character's persisted Current-period total at world entry, unlike
-- legacy's login-time load -- see Server/ts25login/S08_MyDB.cpp:1178-1188 (MyDB::GetHeroPoint, an
-- unconditional-zero-then-exactly-one-row-success-gate read of the SAME table the PvP-kill-reward accrual
-- path writes to -- corroborated against Server/ts25center/S08_MyDB.cpp:215) and
-- Server/ts25playuser/S07_MyGame01.cpp:1002 (the loaded value copied unconditionally into the zone's own
-- live per-character state, z->uStateInfo.mHeroPoint).
--
-- Legacy performs this read at DEMAND_ZONE_SERVER_INFO_SEND (Login-side, Server/ts25login/S04_MyWork02.cpp:
-- 1543-1622, call site :1606) and carries it through an internal Login -> ts25playuser -> Zone hand-off
-- (Server/ts25playuser/S07_MyGame01.cpp's RegisterUserForLogin_03). Fenrir's own Login->Zone hand-off (the
-- single-use runtime.SessionTickets row, ADR-0005) already has a shipped precedent for exactly this kind of
-- "a per-character fact Zone would otherwise need to independently re-read" dilemma:
-- ZoneHandshakeService.ConsumeTicketAsync's own remarks on Tribe explicitly document choosing an accepted,
-- once-per-session extra round trip on the Zone side over extending the ticket schema, specifically to keep
-- a fix like this one small and low-risk. This migration follows that same precedent for HeroRankPoints: the
-- read happens once, Zone-side, inside EnterWorldService.HandleAsync (alongside its existing
-- isMuted/guild/tribeRole/friends/mentor reads for the same character), not by extending
-- runtime.SessionTickets/ConsumedTicketDto. The OUTCOME the behavior contract asks for -- the character's
-- true persisted Current-period total seeded before the player is registered into the zone, not merely
-- points earned since the last world entry -- is unaffected by this timing choice: nothing can grant this
-- character a hero-rank point between the two reads, since the character isn't registered in any zone's
-- live state yet at either point.
--
-- game.HeroRankings already has a natural (CharacterId, PeriodKind) primary key (Tables/game/HeroRankings.sql),
-- so at most one row can ever match -- exactly the "exactly-one-row success gate" shape GetHeroPoint's own
-- query has. No accumulated row yet (character never earned a point) is a legitimate zero, indistinguishable
-- here from any other empty-result case, matching legacy's own collapsed failure semantics -- see
-- IHeroRankingRepository.GetPointsAsync's own remarks for the C# side of this contract.
--
-- Brand-new stored procedure (first appearance, never previously shipped) -- CREATE PROCEDURE, not CREATE OR
-- ALTER; no already-applied script is edited by this migration.
CREATE PROCEDURE game.usp_HeroRanking_GetPoints @CharacterId INT,
                                                @PeriodKind TINYINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT Points
    FROM game.HeroRankings
    WHERE CharacterId = @CharacterId
      AND PeriodKind = @PeriodKind;
END;
GO
