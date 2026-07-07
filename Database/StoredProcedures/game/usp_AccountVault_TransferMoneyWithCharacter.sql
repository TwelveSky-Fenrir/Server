-- Atomic transfer between a character's wallet (game.Characters.Money) and its account's shared vault/bank
-- (game.AccountVault.Money, legacy masterinfo.uSaveMoney) -- CZ_PROCESS_DATA_SEND tSort 231 (deposit,
-- DeltaCharacterMoney negative/DeltaVaultMoney positive) / 232 (withdraw, the reverse). Auto-creates the
-- AccountVault row on first use (same posture as usp_Gift_ClaimIntoVault) so a deposit never requires the
-- vault panel to have been opened first.
-- Réf. C++ : Server/ts25zone/S04_MyWork05.cpp:3275-3341 (ProcessForInventoryMoneyToSaveMoney/
-- ProcessForSaveMoneyToInventoryMoney) ; Server/Header/Protocol/DEFINE.h:365 (MAX_NUMBER_SIZE = 2,000,000,000).
CREATE PROCEDURE game.usp_AccountVault_TransferMoneyWithCharacter @CharacterId INT,
                                                                   @DeltaCharacterMoney BIGINT,
                                                                   @AccountId INT,
                                                                   @DeltaVaultMoney BIGINT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    BEGIN
        TRANSACTION;

    IF
        NOT EXISTS (SELECT 1 FROM game.AccountVault WHERE AccountId = @AccountId)
        INSERT INTO game.AccountVault (AccountId) VALUES (@AccountId);

    UPDATE game.Characters
    SET Money        = Money + @DeltaCharacterMoney,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE CharacterId = @CharacterId
      AND Money + @DeltaCharacterMoney BETWEEN 0 AND 2000000000;

    IF
        @@ROWCOUNT = 0
        THROW 50338, N'Unknown character or insufficient wallet balance for this vault transfer.', 1;

    UPDATE game.AccountVault
    SET Money        = Money + @DeltaVaultMoney,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE AccountId = @AccountId
      AND Money + @DeltaVaultMoney BETWEEN 0 AND 2000000000;

    IF
        @@ROWCOUNT = 0
        THROW 50339, N'Insufficient account vault balance for this transfer.', 1;

    COMMIT TRANSACTION;
END;
