-- Upsert: serves both first-PIN creation and PIN replacement. "PIN already exists" / "no PIN to
-- change" preconditions are enforced by the caller before this proc runs.
--
-- The UPDATE-then-INSERT-if-0-rows shape has a TOCTOU window under concurrent callers for the SAME
-- @AccountId (e.g. a duplicated client request or a network retry racing itself): two concurrent calls
-- can both see 0 rows affected by the UPDATE (no PIN row exists yet) and both attempt the INSERT, so the
-- loser hits PK_AccountPins. This proc has no domain error of its own -- an unconditional upsert never
-- logically "fails" -- so rather than let that raw constraint-violation SqlException surface to the
-- caller, the CATCH retries the UPDATE: the request that lost the INSERT race ends up applying its own
-- PinHash/PinSalt/UpdatedAtUtc via UPDATE against the row the winner just created, so the final state
-- deterministically reflects whichever caller's write physically lands last -- consistent with the
-- unconditional-overwrite semantics this proc already promises, and it never logs or otherwise surfaces
-- either caller's PinHash/PinSalt value in the process.
CREATE PROCEDURE auth.usp_AccountPin_Set @AccountId INT,
                                         @PinHash VARBINARY(32),
                                         @PinSalt VARBINARY(16)
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    UPDATE auth.AccountPins
    SET PinHash      = @PinHash,
        PinSalt      = @PinSalt,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE AccountId = @AccountId;

    IF @@ROWCOUNT = 0
        BEGIN
            BEGIN TRY
                INSERT INTO auth.AccountPins (AccountId, PinHash, PinSalt)
                VALUES (@AccountId, @PinHash, @PinSalt);
            END TRY
            BEGIN CATCH
                IF ERROR_NUMBER() NOT IN (2627, 2601)
                    THROW;

                UPDATE auth.AccountPins
                SET PinHash      = @PinHash,
                    PinSalt      = @PinSalt,
                    UpdatedAtUtc = SYSUTCDATETIME()
                WHERE AccountId = @AccountId;
            END CATCH;
        END;
END;
