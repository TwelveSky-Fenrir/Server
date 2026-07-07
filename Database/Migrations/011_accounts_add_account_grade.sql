-- Fenrir's own account-grade fact (legacy uUserSort, Server/ts25login/S08_MyDB.cpp:244-245): loaded once at
-- authentication and carried forward through the session/ticket, never re-queried per action. This closes
-- the gap flagged in IGmAllowlistRepository's/ApplicationFirewall's own remarks ("auth.Accounts has no
-- account-grade/GM flag yet") for the first Fenrir feature that actually needs it (GM-BLOCK, legacy case
-- 519, Server/ts25zone/S04_MyWork04.cpp:1487-1515).
--
-- This is a schema change to an already-applied table/proc, so per the "never edit an already-applied
-- script" rule it is a new migration rather than an edit of Tables/auth/Accounts.sql or
-- StoredProcedures/auth/usp_Account_Authenticate.sql in place. usp_Account_Authenticate isn't
-- SCHEMABINDING/natively compiled, so CREATE OR ALTER is enough here -- no DROP needed first.

ALTER TABLE auth.Accounts
    ADD AccountGrade SMALLINT NOT NULL
        CONSTRAINT DF_Accounts_AccountGrade DEFAULT 0;
GO

-- Elevation is a strict "< 1 is not elevated" binary gate at every legacy call site that reads uUserSort
-- this way (case 519 and siblings 518/520/521, Server/ts25zone/S04_MyWork04.cpp). SMALLINT (not BIT) is
-- kept to match legacy's own field width and leave room for a future graduated grade, even though nothing
-- today distinguishes grade 1 from grade 2+.
CREATE OR ALTER PROCEDURE auth.usp_Account_Authenticate @LoginName NVARCHAR(64)
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT AccountId, PasswordHash, PasswordSalt, FailedLoginCount, LockoutUntilUtc, IsBanned, AccountGrade
    FROM auth.Accounts
    WHERE LoginName = @LoginName;
END;
GO
