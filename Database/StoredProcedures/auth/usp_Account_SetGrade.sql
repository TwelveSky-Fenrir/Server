-- Operational tool support: there was no way for an operator to grant an account GM privilege
-- (AccountGrade, legacy uUserSort) anywhere in this repo -- not in a seed script, not in
-- Tools/Fenrir.Tools.AccountProvisioning, not documented -- which made the GM command tier work
-- (Application/Fenrir.Application.Game.Services/Gm/*) unreachable/untestable end-to-end. This proc
-- is the missing write path; Tools/Fenrir.Tools.AccountProvisioning's "grant-gm" subcommand is the
-- CLI surface over it. Idempotent by design (a plain overwrite of the current value, not a delta).
CREATE PROCEDURE auth.usp_Account_SetGrade @LoginName NVARCHAR(64),
                                            @AccountGrade SMALLINT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    UPDATE auth.Accounts
    SET AccountGrade = @AccountGrade
    WHERE LoginName = @LoginName;

    IF @@ROWCOUNT = 0
        THROW 50102, N'Account login name not found.', 1;
END;
