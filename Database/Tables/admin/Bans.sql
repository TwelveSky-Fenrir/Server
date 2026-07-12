-- Legacy `baduserlist`. Was meant to be the source auth.Accounts.IsBanned syncs from, but no such sync
-- exists: usp_Ban_Create only inserts here, so IsBanned stays permanently 0 (dead flag, moyenne-severity
-- gap, unresolved pending a legacy-behavior-translator contract on the bit's exact set/clear conditions).
-- This table alone is the live source of truth today -- auth.usp_Account_Authenticate's caller ORs
-- admin.usp_Ban_GetActiveForAccount's result with IsBanned, and only the former ever returns true.
-- AccountId/CharacterId nullable per independent uUserIdx/uCharIdx targeting (legacy 0 = unset -> NULL).
-- ExpiresAtUtc NULL = permanent. Legacy tBanUntilDate/uBlockInfo is a YYYYMMDD decimal integer
-- (Server/Header/datetime.h:78-83,195-217 ReturnNowDate/ReturnAddDate: sprintf("%04d%02d%02d", Y, M, D)
-- then atoi), NEVER Unix epoch-seconds: zero must still import as NULL, nonzero imports as midnight UTC of
-- the decoded Y/M/D -- not DATEADD(SECOND, tBanUntilDate, '1970-01-01'), which was this comment's own prior
-- (wrong) formula. Whether legacy's zero specifically means "permanent" or "this BanUser call left
-- uBlockInfo untouched" is unresolved (Server/ts25playuser/S08_MyDB.cpp:284's `if (tBanUntilDate)` guard
-- reads either way) -- re-verify against Server/ before an importer leans on it.
CREATE TABLE admin.Bans
(
    BanId            INT IDENTITY (1,1) NOT NULL,
    AccountId        INT                NULL,
    CharacterId      INT                NULL,
    Reason           TINYINT            NOT NULL,
    ExpiresAtUtc     DATETIME2(3)       NULL,
    CreatedAtUtc     DATETIME2(3)       NOT NULL
        CONSTRAINT DF_Bans_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    ActorAccountId   INT                NULL, -- the GM who issued this ban (independently nullable like the target columns -- a system-imposed/legacy-import ban legitimately has no GM actor); deliberately NO FK, must survive deletion of the actor's own account/character
    ActorCharacterId INT                NULL,
    CONSTRAINT PK_Bans PRIMARY KEY CLUSTERED (BanId),
    CONSTRAINT CK_Bans_AccountOrCharacter CHECK (AccountId IS NOT NULL OR CharacterId IS NOT NULL),
    -- Cross-schema FK naming convention (repo-wide, established here): a constraint whose target lives in a
    -- DIFFERENT schema than the child table embeds that target schema right after the child table name --
    -- FK_<ChildTable>_<TargetSchema>_<Role> -- so `sys.foreign_keys`'s own name column alone tells you which
    -- schema boundary it crosses, with no need to open the definition. A bare FK_<ChildTable>_<Role> (no
    -- schema token) always means same-schema, e.g. game.Characters' own FK_Characters_TeacherCharacter.
    CONSTRAINT FK_Bans_Auth_Account FOREIGN KEY (AccountId) REFERENCES auth.Accounts (AccountId),
    CONSTRAINT FK_Bans_Game_Character FOREIGN KEY (CharacterId) REFERENCES game.Characters (CharacterId),
    -- Both INCLUDE lists cover admin.vw_ActiveBans' full select list (minus BanId, implicitly carried by
    -- every nonclustered index off this table's clustered key) so usp_Ban_GetActiveForAccount/
    -- GetActiveForCharacter -- the login and world-entry hot paths -- resolve as a covered seek with no
    -- Key Lookup back to the clustered index.
    INDEX IX_Bans_Account NONCLUSTERED (AccountId) INCLUDE (ExpiresAtUtc, Reason, CharacterId, CreatedAtUtc),
    INDEX IX_Bans_Character NONCLUSTERED (CharacterId) INCLUDE (ExpiresAtUtc, Reason, AccountId, CreatedAtUtc),
    -- Zero read consumers today (verified: IBanRepository.cs/BanRepository.cs only expose
    -- IsActiveForAccountAsync/IsActiveForCharacterAsync/CreateAsync -- none filter by actor). Kept rather than
    -- dropped: the WHERE filter already limits each index to the minority of rows a GM actually issued, so the
    -- write-time cost is small, and both anticipate a not-yet-built "who banned whom" GM audit screen. Whether
    -- that screen ships is a product-roadmap call, not a schema-layer one -- revisit dropping these if it never
    -- materializes.
    INDEX IX_Bans_ActorAccount NONCLUSTERED (ActorAccountId) INCLUDE (CreatedAtUtc, Reason) WHERE (ActorAccountId IS NOT NULL),
    INDEX IX_Bans_ActorCharacter NONCLUSTERED (ActorCharacterId) INCLUDE (CreatedAtUtc, Reason) WHERE (ActorCharacterId IS NOT NULL)
);
