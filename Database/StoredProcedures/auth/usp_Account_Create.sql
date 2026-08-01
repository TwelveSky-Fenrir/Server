-- Not idempotent: a second call with the same @LoginName raises 50101 instead of creating a duplicate.
--
-- The IF EXISTS check below is a cheap common-case rejection, not the actual concurrency guarantee: under
-- RCSI a plain SELECT takes no lock that would stop a second concurrent Create call for the same
-- @LoginName from also passing it before either INSERT commits (classic check-then-act TOCTOU). The
-- TRY/CATCH around the INSERT is the real backstop -- it translates the UQ_Accounts_LoginName violation
-- (error 2627, or 2601 if the engine ever reports it via the index instead of the constraint) into the
-- same domain error 50101 instead of letting a raw, uncataloged SqlException surface to the caller when
-- two Create calls race. No explicit BEGIN TRANSACTION is needed: each execution path (fast-path THROW,
-- successful INSERT, or CATCH-translated THROW) contains exactly one terminal write statement, already
-- atomic on its own, and XACT_ABORT ON is not carrying an open explicit transaction here for TRY/CATCH to
-- interact badly with (verified against Microsoft Learn: XACT_ABORT ON only dooms a transaction that was
-- opened with an explicit BEGIN TRANSACTION -- a single autocommit statement's failure just rolls back
-- that one statement, leaving the CATCH block free to run further statements).
CREATE PROCEDURE auth.usp_Account_Create @LoginName NVARCHAR(64),
                                         @PasswordHash VARBINARY(32),
                                         @PasswordSalt VARBINARY(16)
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    IF
        EXISTS (SELECT 1 FROM auth.Accounts WHERE LoginName = @LoginName)
        THROW 50101, N'Account login name already taken.', 1;

    BEGIN TRY
        INSERT INTO auth.Accounts (LoginName, PasswordHash, PasswordSalt)
        VALUES (@LoginName, @PasswordHash, @PasswordSalt);
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() NOT IN (2627, 2601)
            THROW;

        THROW 50101, N'Account login name already taken.', 1;
    END CATCH;

    SELECT CAST(SCOPE_IDENTITY() AS INT) AS AccountId;
END;
