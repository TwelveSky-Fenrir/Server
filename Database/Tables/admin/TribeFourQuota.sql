-- Singleton row (Id=1): the shared, global conversion quota behind the fourth-tribe (Fujin) conversion/return
-- behavior (CZ_CHANGE_TO_TRIBE4_SEND, op37) -- Fenrir's equivalent of the legacy's cross-process
-- MyDB::CheckUpdateSky check against ts25extra (Server/ts25extra/S08_MyDB.cpp:1193-1215), which has no
-- Fenrir-side "extra" process to consult (Fenrir targets exactly LoginServer/GameServer). An operator edits
-- MaxCount directly; CurrentCount is only ever advanced by
-- admin.usp_TribeFourQuota_TryConsume's own atomic check-and-increment. CK_TribeFourQuota_CurrentWithinMax
-- makes an operator's direct MaxCount edit fail immediately and legibly if it would drop MaxCount below
-- CurrentCount already consumed, instead of leaving the row in a silent paradoxical state until the next
-- TryConsume call.
--
-- MaxCount = 0 by default (see the seed script) is a fail-safe "no conversions grantable" starting state,
-- the same posture GameServerOptions uses for other newly-completed-but-never-shipped content
-- (HolyStoneMapId = 0, etc.) -- not a reproduction of any specific legacy limit, which the translating
-- behavior contract could not determine (see that contract's own Edge Cases entry on this quota's open
-- semantics). Unlike the legacy mechanism this survived from, Fenrir's Services-layer caller consumes this
-- quota only AFTER every character-specific eligibility gate has already passed -- see
-- Fenrir.Application.Game.Domain.Tribes.TribeMigrationOutcome.QuotaExhausted's own remarks for why that
-- ordering is a deliberate hardening deviation from the legacy's own quota-first bug.
CREATE TABLE admin.TribeFourQuota
(
    Id           TINYINT NOT NULL
        CONSTRAINT DF_TribeFourQuota_Id DEFAULT 1,
    MaxCount     INT     NOT NULL,
    CurrentCount INT     NOT NULL
        CONSTRAINT DF_TribeFourQuota_CurrentCount DEFAULT 0,
    CONSTRAINT PK_TribeFourQuota PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT CK_TribeFourQuota_Id CHECK (Id = 1),
    CONSTRAINT CK_TribeFourQuota_CurrentCount CHECK (CurrentCount >= 0),
    CONSTRAINT CK_TribeFourQuota_CurrentWithinMax CHECK (CurrentCount <= MaxCount)
);
