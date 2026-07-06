-- Fix-forward for a fail-closed regression introduced by 018_character_previous_tribe_and_mount_readpath.sql
-- (not edited in place -- DbMigrator journals every script by content hash and refuses a changed re-apply).
--
-- 018 added game.Characters.PreviousTribe as NOT NULL DEFAULT 0. That DEFAULT applies retroactively to every
-- row that already existed at the moment the ALTER TABLE ran -- i.e. any character created before this
-- migration shipped. EnterWorldService.HandleAsync (Application/Fenrir.Application.Game.Services/
-- ZoneLifecycle/EnterWorldService.cs) now aborts the session outright (Server/ts25zone/S04_MyWork02.cpp:
-- 880-901's self-consistency check: a main-faction Tribe 0-2 requires PreviousTribe == Tribe) whenever the
-- two fields disagree. Combined, every already-created character with Tribe = 1 or Tribe = 2 -- the large
-- majority of any pre-existing population -- would silently receive PreviousTribe = 0 from the ALTER TABLE
-- DEFAULT and then be permanently unable to enter the world afterward (immediate Faulted disconnect on every
-- attempt), with no error surfaced beyond a generic disconnect. This is a fail-closed denial-of-service
-- against legitimate, already-existing accounts introduced purely by the migration's own DEFAULT, not
-- anything a player did.
--
-- Fix: backfill PreviousTribe = Tribe for every main-faction (Tribe IN (0,1,2)) row that hasn't already been
-- set to a non-default value (Tribe = 0 rows are already trivially consistent and are excluded to keep this
-- idempotent/side-effect-free on rows a later, PreviousTribe-aware write path may have already touched).
-- Tribe = 3 (fourth faction) rows are deliberately left untouched: PreviousTribe = 0 already satisfies the
-- self-consistency check for that branch (PreviousTribe in {0,1,2} is the whole legal set, no single "correct"
-- historical value can be reconstructed from data this migration never captured), so leaving the 018 DEFAULT
-- in place there is correct, not an oversight.
UPDATE game.Characters
SET PreviousTribe = Tribe
WHERE Tribe IN (1, 2)
  AND PreviousTribe <> Tribe;
GO
