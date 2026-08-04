CREATE PROCEDURE game.usp_AccountVault_TransferItemWithCharacter @AccountId INT,
                                                                 @ExpectedVaultRevision BIGINT,
                                                                 @CharacterId INT = NULL,
                                                                 @Container TINYINT = NULL,
                                                                 @CharacterSlot TINYINT = NULL,
                                                                 @ExpectedCharacterItemId INT = NULL,
                                                                 @ExpectedCharacterQuantity INT = 0,
                                                                 @ExpectedCharacterEnchant TINYINT = 0,
                                                                 @ExpectedCharacterCombine TINYINT = 0,
                                                                 @ExpectedCharacterRefine TINYINT = 0,
                                                                 @ExpectedCharacterSocket TINYINT = 0,
                                                                 @ExpectedCharacterSocketGem1 INT = 0,
                                                                 @ExpectedCharacterSocketGem2 INT = 0,
                                                                 @ExpectedCharacterSocketGem3 INT = 0,
                                                                 @ExpectedCharacterExpireDate INT = 0,
                                                                 @ExpectedCharacterSerial INT = 0,
                                                                 @ExpectedCharacterXPos TINYINT = 0,
                                                                 @ExpectedCharacterYPos TINYINT = 0,
                                                                 @NewCharacterItemId INT = NULL,
                                                                 @NewCharacterQuantity INT = 0,
                                                                 @NewCharacterEnchant TINYINT = 0,
                                                                 @NewCharacterCombine TINYINT = 0,
                                                                 @NewCharacterRefine TINYINT = 0,
                                                                 @NewCharacterSocket TINYINT = 0,
                                                                 @NewCharacterSocketGem1 INT = 0,
                                                                 @NewCharacterSocketGem2 INT = 0,
                                                                 @NewCharacterSocketGem3 INT = 0,
                                                                 @NewCharacterExpireDate INT = 0,
                                                                 @NewCharacterSerial INT = 0,
                                                                 @NewCharacterXPos TINYINT = 0,
                                                                 @NewCharacterYPos TINYINT = 0,
                                                                 @Vault1Slot SMALLINT,
                                                                 @ExpectedVault1ItemId INT = NULL,
                                                                 @ExpectedVault1Quantity INT = 0,
                                                                 @ExpectedVault1SerialNumber INT = 0,
                                                                 @NewVault1ItemId INT = NULL,
                                                                 @NewVault1Quantity INT = 0,
                                                                 @NewVault1Value INT = 0,
                                                                 @NewVault1SerialNumber INT = 0,
                                                                 @NewVault1SocketData NVARCHAR(50) = NULL,
                                                                 @NewVault1SocketGem1 INT = 0,
                                                                 @NewVault1SocketGem2 INT = 0,
                                                                 @NewVault1SocketGem3 INT = 0,
                                                                 @NewVault1ExpireDate INT = 0,
                                                                 @Vault2Slot SMALLINT = NULL,
                                                                 @ExpectedVault2ItemId INT = NULL,
                                                                 @ExpectedVault2Quantity INT = 0,
                                                                 @ExpectedVault2SerialNumber INT = 0,
                                                                 @NewVault2ItemId INT = NULL,
                                                                 @NewVault2Quantity INT = 0,
                                                                 @NewVault2Value INT = 0,
                                                                 @NewVault2SerialNumber INT = 0,
                                                                 @NewVault2SocketData NVARCHAR(50) = NULL,
                                                                 @NewVault2SocketGem1 INT = 0,
                                                                 @NewVault2SocketGem2 INT = 0,
                                                                 @NewVault2SocketGem3 INT = 0,
                                                                 @NewVault2ExpireDate INT = 0
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @ExpectedVaultRevision IS NULL
        OR @ExpectedVaultRevision < 0
        OR @Vault1Slot IS NULL
        OR @Vault1Slot NOT BETWEEN 0 AND 27
        OR (@Vault2Slot IS NOT NULL AND @Vault2Slot NOT BETWEEN 0 AND 27)
        OR (@Vault2Slot IS NOT NULL AND @Vault2Slot = @Vault1Slot)
        OR (@CharacterId IS NULL AND (@Container IS NOT NULL OR @CharacterSlot IS NOT NULL))
        OR (@CharacterId IS NOT NULL AND (@Container IS NULL OR @CharacterSlot IS NULL))
        BEGIN
            SELECT CAST(0 AS BIT);

            RETURN;
        END;

    DECLARE @CurrentVaultRevision BIGINT;
    DECLARE @CharacterAccountId INT;
    DECLARE @CharacterItemExists BIT = 0;
    DECLARE @CurrentCharacterItemId INT;
    DECLARE @CurrentCharacterQuantity SMALLINT;
    DECLARE @CurrentCharacterEnchant TINYINT;
    DECLARE @CurrentCharacterCombine TINYINT;
    DECLARE @CurrentCharacterRefine TINYINT;
    DECLARE @CurrentCharacterSocket TINYINT;
    DECLARE @CurrentCharacterSocketGem1 INT;
    DECLARE @CurrentCharacterSocketGem2 INT;
    DECLARE @CurrentCharacterSocketGem3 INT;
    DECLARE @CurrentCharacterExpireDate INT;
    DECLARE @CurrentCharacterSerial INT;
    DECLARE @CurrentCharacterXPos TINYINT;
    DECLARE @CurrentCharacterYPos TINYINT;
    DECLARE @Vault1ItemExists BIT = 0;
    DECLARE @CurrentVault1ItemId INT;
    DECLARE @CurrentVault1Quantity INT;
    DECLARE @CurrentVault1SerialNumber INT;
    DECLARE @Vault2ItemExists BIT = 0;
    DECLARE @CurrentVault2ItemId INT;
    DECLARE @CurrentVault2Quantity INT;
    DECLARE @CurrentVault2SerialNumber INT;

    BEGIN TRANSACTION;

    IF NOT EXISTS (SELECT 1 FROM auth.Accounts WHERE AccountId = @AccountId)
        GOTO Conflict;

    IF @CharacterId IS NOT NULL
        BEGIN
            SELECT @CharacterAccountId = AccountId
            FROM game.Characters
            WITH (UPDLOCK, HOLDLOCK)
            WHERE CharacterId = @CharacterId;

            IF @CharacterAccountId IS NULL OR @CharacterAccountId <> @AccountId
                GOTO Conflict;
        END;

    SELECT @CurrentVaultRevision = Revision
    FROM game.AccountVault
    WITH (UPDLOCK, HOLDLOCK)
    WHERE AccountId = @AccountId;

    IF @CurrentVaultRevision IS NULL
        BEGIN
            IF @ExpectedVaultRevision <> 0
                GOTO Conflict;

            INSERT INTO game.AccountVault (AccountId) VALUES (@AccountId);

            SET @CurrentVaultRevision = 0;
        END;

    IF @CurrentVaultRevision <> @ExpectedVaultRevision
        GOTO Conflict;

    IF @CharacterId IS NOT NULL
        BEGIN
            SELECT @CharacterItemExists = 1,
                   @CurrentCharacterItemId = ItemId,
                   @CurrentCharacterQuantity = Quantity,
                   @CurrentCharacterEnchant = Enchant,
                   @CurrentCharacterCombine = Combine,
                   @CurrentCharacterRefine = Refine,
                   @CurrentCharacterSocket = Socket,
                   @CurrentCharacterSocketGem1 = SocketGem1,
                   @CurrentCharacterSocketGem2 = SocketGem2,
                   @CurrentCharacterSocketGem3 = SocketGem3,
                   @CurrentCharacterExpireDate = ExpireDate,
                   @CurrentCharacterSerial = Serial,
                   @CurrentCharacterXPos = XPos,
                   @CurrentCharacterYPos = YPos
            FROM game.CharacterItems
            WITH (UPDLOCK, HOLDLOCK)
            WHERE CharacterId = @CharacterId
              AND Container = @Container
              AND Slot = @CharacterSlot;

            IF @ExpectedCharacterItemId IS NULL
                BEGIN
                    IF @CharacterItemExists = 1
                        GOTO Conflict;
                END
            ELSE
                IF @CharacterItemExists = 0
                    OR @CurrentCharacterItemId <> @ExpectedCharacterItemId
                    OR @CurrentCharacterQuantity <> @ExpectedCharacterQuantity
                    OR @CurrentCharacterEnchant <> @ExpectedCharacterEnchant
                    OR @CurrentCharacterCombine <> @ExpectedCharacterCombine
                    OR @CurrentCharacterRefine <> @ExpectedCharacterRefine
                    OR @CurrentCharacterSocket <> @ExpectedCharacterSocket
                    OR @CurrentCharacterSocketGem1 <> @ExpectedCharacterSocketGem1
                    OR @CurrentCharacterSocketGem2 <> @ExpectedCharacterSocketGem2
                    OR @CurrentCharacterSocketGem3 <> @ExpectedCharacterSocketGem3
                    OR @CurrentCharacterExpireDate <> @ExpectedCharacterExpireDate
                    OR @CurrentCharacterSerial <> @ExpectedCharacterSerial
                    OR @CurrentCharacterXPos <> @ExpectedCharacterXPos
                    OR @CurrentCharacterYPos <> @ExpectedCharacterYPos
                    GOTO Conflict;
        END;

    SELECT @Vault1ItemExists = 1,
           @CurrentVault1ItemId = ItemId,
           @CurrentVault1Quantity = Quantity,
           @CurrentVault1SerialNumber = SerialNumber
    FROM game.AccountVaultItems
    WITH (UPDLOCK, HOLDLOCK)
    WHERE AccountId = @AccountId
      AND SlotIndex = @Vault1Slot;

    IF @ExpectedVault1ItemId IS NULL
        BEGIN
            IF @Vault1ItemExists = 1
                GOTO Conflict;
        END
    ELSE
        IF @Vault1ItemExists = 0
            OR @CurrentVault1ItemId <> @ExpectedVault1ItemId
            OR @CurrentVault1Quantity <> @ExpectedVault1Quantity
            OR @CurrentVault1SerialNumber <> @ExpectedVault1SerialNumber
            GOTO Conflict;

    IF @Vault2Slot IS NOT NULL
        BEGIN
            SELECT @Vault2ItemExists = 1,
                   @CurrentVault2ItemId = ItemId,
                   @CurrentVault2Quantity = Quantity,
                   @CurrentVault2SerialNumber = SerialNumber
            FROM game.AccountVaultItems
            WITH (UPDLOCK, HOLDLOCK)
            WHERE AccountId = @AccountId
              AND SlotIndex = @Vault2Slot;

            IF @ExpectedVault2ItemId IS NULL
                BEGIN
                    IF @Vault2ItemExists = 1
                        GOTO Conflict;
                END
            ELSE
                IF @Vault2ItemExists = 0
                    OR @CurrentVault2ItemId <> @ExpectedVault2ItemId
                    OR @CurrentVault2Quantity <> @ExpectedVault2Quantity
                    OR @CurrentVault2SerialNumber <> @ExpectedVault2SerialNumber
                    GOTO Conflict;
        END;

    IF @CharacterId IS NOT NULL
        BEGIN
            IF @NewCharacterItemId IS NULL
                DELETE
                FROM game.CharacterItems
                WHERE CharacterId = @CharacterId
                  AND Container = @Container
                  AND Slot = @CharacterSlot;
            ELSE
                IF @ExpectedCharacterItemId IS NULL
                    INSERT INTO game.CharacterItems (CharacterId, Container, Slot, ItemId, Quantity,
                                                     Enchant, Combine, Refine, Socket,
                                                     SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial, XPos, YPos)
                    VALUES (@CharacterId, @Container, @CharacterSlot, @NewCharacterItemId, @NewCharacterQuantity,
                            @NewCharacterEnchant, @NewCharacterCombine, @NewCharacterRefine, @NewCharacterSocket,
                            @NewCharacterSocketGem1, @NewCharacterSocketGem2, @NewCharacterSocketGem3,
                            @NewCharacterExpireDate, @NewCharacterSerial, @NewCharacterXPos, @NewCharacterYPos);
                ELSE
                    UPDATE game.CharacterItems
                    SET ItemId     = @NewCharacterItemId,
                        Quantity   = @NewCharacterQuantity,
                        Enchant    = @NewCharacterEnchant,
                        Combine    = @NewCharacterCombine,
                        Refine     = @NewCharacterRefine,
                        Socket     = @NewCharacterSocket,
                        SocketGem1 = @NewCharacterSocketGem1,
                        SocketGem2 = @NewCharacterSocketGem2,
                        SocketGem3 = @NewCharacterSocketGem3,
                        ExpireDate = @NewCharacterExpireDate,
                        Serial     = @NewCharacterSerial,
                        XPos       = @NewCharacterXPos,
                        YPos       = @NewCharacterYPos
                    WHERE CharacterId = @CharacterId
                      AND Container = @Container
                      AND Slot = @CharacterSlot;
        END;

    IF @NewVault1ItemId IS NULL
        DELETE
        FROM game.AccountVaultItems
        WHERE AccountId = @AccountId
          AND SlotIndex = @Vault1Slot;
    ELSE
        IF @ExpectedVault1ItemId IS NULL
            INSERT INTO game.AccountVaultItems (AccountId, SlotIndex, ItemId, Quantity, Value, SerialNumber, SocketData,
                                                SocketGem1, SocketGem2, SocketGem3, ExpireDate)
            VALUES (@AccountId, @Vault1Slot, @NewVault1ItemId, @NewVault1Quantity, @NewVault1Value,
                    @NewVault1SerialNumber, @NewVault1SocketData, @NewVault1SocketGem1, @NewVault1SocketGem2,
                    @NewVault1SocketGem3, @NewVault1ExpireDate);
        ELSE
            UPDATE game.AccountVaultItems
            SET ItemId       = @NewVault1ItemId,
                Quantity     = @NewVault1Quantity,
                Value        = @NewVault1Value,
                SerialNumber = @NewVault1SerialNumber,
                SocketData   = @NewVault1SocketData,
                SocketGem1   = @NewVault1SocketGem1,
                SocketGem2   = @NewVault1SocketGem2,
                SocketGem3   = @NewVault1SocketGem3,
                ExpireDate   = @NewVault1ExpireDate
            WHERE AccountId = @AccountId
              AND SlotIndex = @Vault1Slot;

    IF @Vault2Slot IS NOT NULL
        BEGIN
            IF @NewVault2ItemId IS NULL
                DELETE
                FROM game.AccountVaultItems
                WHERE AccountId = @AccountId
                  AND SlotIndex = @Vault2Slot;
            ELSE
                IF @ExpectedVault2ItemId IS NULL
                    INSERT INTO game.AccountVaultItems (AccountId, SlotIndex, ItemId, Quantity, Value, SerialNumber,
                                                        SocketData, SocketGem1, SocketGem2, SocketGem3, ExpireDate)
                    VALUES (@AccountId, @Vault2Slot, @NewVault2ItemId, @NewVault2Quantity, @NewVault2Value,
                            @NewVault2SerialNumber, @NewVault2SocketData, @NewVault2SocketGem1, @NewVault2SocketGem2,
                            @NewVault2SocketGem3, @NewVault2ExpireDate);
                ELSE
                    UPDATE game.AccountVaultItems
                    SET ItemId       = @NewVault2ItemId,
                        Quantity     = @NewVault2Quantity,
                        Value        = @NewVault2Value,
                        SerialNumber = @NewVault2SerialNumber,
                        SocketData   = @NewVault2SocketData,
                        SocketGem1   = @NewVault2SocketGem1,
                        SocketGem2   = @NewVault2SocketGem2,
                        SocketGem3   = @NewVault2SocketGem3,
                        ExpireDate   = @NewVault2ExpireDate
                    WHERE AccountId = @AccountId
                      AND SlotIndex = @Vault2Slot;
        END;

    UPDATE game.AccountVault
    SET Revision     = Revision + 1,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE AccountId = @AccountId;

    COMMIT TRANSACTION;

    SELECT CAST(1 AS BIT);

    RETURN;

    Conflict:
    ROLLBACK TRANSACTION;

    SELECT CAST(0 AS BIT);
END;
