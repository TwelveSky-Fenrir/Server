CREATE OR ALTER PROCEDURE game.usp_AccountDailyReward_Claim @AccountId INT,
                                                            @CharacterId INT,
                                                            @TodayDate INT,
                                                            @Container TINYINT,
                                                            @Items game.tvp_CharacterItemSlot READONLY
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    DECLARE
        @TodayAsDate DATE = TRY_CONVERT(DATE, CAST(@TodayDate AS VARCHAR(8)), 112);

    IF
        NOT EXISTS (SELECT 1
                    FROM game.Characters
                    WHERE CharacterId = @CharacterId
                      AND AccountId = @AccountId)
        THROW 50359, N'Daily reward: character does not belong to the claiming account.', 1;

    BEGIN
        TRANSACTION;

    IF
        NOT EXISTS (SELECT 1
                    FROM game.AccountDailyRewards
                    WITH (UPDLOCK, HOLDLOCK)
                    WHERE AccountId = @AccountId)
        INSERT INTO game.AccountDailyRewards (AccountId)
        VALUES (@AccountId);

    DECLARE
        @IsNewWeek BIT;
    SELECT @IsNewWeek = CASE
                            WHEN RewardClaimDate = 0 THEN 0
                            WHEN DATEDIFF(DAY, 0, @TodayAsDate) / 7 <>
                                 DATEDIFF(DAY, 0, TRY_CONVERT(DATE, CAST(RewardClaimDate AS VARCHAR(8)), 112)) / 7
                                THEN 1
                            ELSE 0
        END
    FROM game.AccountDailyRewards
    WHERE AccountId = @AccountId;

    UPDATE game.AccountDailyRewards
    SET RewardClaimDay  = IIF(@IsNewWeek = 1, 1, RewardClaimDay + 1),
        RewardClaimDate = @TodayDate,
        UpdatedAtUtc    = SYSUTCDATETIME()
    WHERE AccountId = @AccountId
      AND RewardClaimDate <> @TodayDate
      AND (RewardClaimDay BETWEEN 0 AND 6 OR @IsNewWeek = 1);

    IF
        @@ROWCOUNT = 0
        THROW 50270, N'Daily reward already claimed today, fully claimed, or unknown account.', 1;

    DELETE
    FROM game.CharacterItems
    WHERE CharacterId = @CharacterId
      AND Container = @Container;

    INSERT INTO game.CharacterItems (CharacterId, Container, Slot, ItemId, Quantity, Enchant, Combine,
                                     Refine, Socket, SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial)
    SELECT @CharacterId,
           @Container,
           Slot,
           ItemId,
           Quantity,
           Enchant,
           Combine,
           Refine,
           Socket,
           SocketGem1,
           SocketGem2,
           SocketGem3,
           ExpireDate,
           Serial
    FROM @Items;

    COMMIT TRANSACTION;
END;
