-- Single-row synchronous write to game.EventLog. The high-stakes audit path: call directly for a standalone
-- event (Session/AccountSecurity lifecycle, a GM command handler not already inside another mutation), or
-- have a caller's OWN procedure issue an equivalent EXEC of this procedure as one more statement inside its
-- existing transaction (e.g. a future usp_CharacterTrade_Execute/usp_Character_AdjustMoney revision), so the
-- audit row can never survive a rolled-back mutation, and vice versa. No THROW here -- an insert-only audit
-- write has no domain failure condition to reject; a bad EventCode/Category from the caller is an app bug,
-- not a registered admin.ErrorCatalog case.
CREATE PROCEDURE game.usp_EventLog_Insert
    @EventCode SMALLINT,
    @Category TINYINT,
    @ActorAccountId INT = NULL,
    @ActorCharacterId INT = NULL,
    @TargetAccountId INT = NULL,
    @TargetCharacterId INT = NULL,
    @ShardId SMALLINT = NULL,
    @DeltaMoney BIGINT = NULL,
    @DeltaBigMoney BIGINT = NULL,
    @ItemId INT = NULL,
    @Quantity INT = NULL,
    @Outcome TINYINT = NULL,
    @Payload NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    -- Single INSERT is already atomic; it naturally joins whatever ambient transaction the caller (if any)
    -- already opened -- no explicit BEGIN TRANSACTION needed, same carve-out as a single guarded UPDATE.
    INSERT INTO game.EventLog
    (EventCode, Category, ActorAccountId, ActorCharacterId, TargetAccountId, TargetCharacterId,
     ShardId, DeltaMoney, DeltaBigMoney, ItemId, Quantity, Outcome, Payload)
    VALUES
        (@EventCode, @Category, @ActorAccountId, @ActorCharacterId, @TargetAccountId, @TargetCharacterId,
         @ShardId, @DeltaMoney, @DeltaBigMoney, @ItemId, @Quantity, @Outcome, @Payload);
END;
