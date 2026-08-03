
IF OBJECT_ID('game.AccountDailyRewards', 'U') IS NULL
CREATE TABLE game.AccountDailyRewards
(
    AccountId       INT          NOT NULL,
    RewardClaimDay  TINYINT      NOT NULL
        CONSTRAINT DF_AccountDailyRewards_RewardClaimDay DEFAULT 0
        CONSTRAINT CK_AccountDailyRewards_RewardClaimDay CHECK (RewardClaimDay BETWEEN 0 AND 7),
    RewardClaimDate INT          NOT NULL
        CONSTRAINT DF_AccountDailyRewards_RewardClaimDate DEFAULT 0,
    UpdatedAtUtc    DATETIME2(3) NOT NULL
        CONSTRAINT DF_AccountDailyRewards_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_AccountDailyRewards PRIMARY KEY CLUSTERED (AccountId),
    CONSTRAINT FK_AccountDailyRewards_Auth_Account FOREIGN KEY (AccountId) REFERENCES auth.Accounts (AccountId)
);
GO

INSERT INTO game.AccountDailyRewards (AccountId, RewardClaimDay, RewardClaimDate, UpdatedAtUtc)
SELECT c.AccountId,
       MAX(c.RewardClaimDay),
       MAX(c.RewardClaimDate),
       SYSUTCDATETIME()
FROM game.Characters c
WHERE NOT EXISTS (SELECT 1
                  FROM game.AccountDailyRewards a
                  WHERE a.AccountId = c.AccountId)
GROUP BY c.AccountId
HAVING MAX(c.RewardClaimDay) <> 0
    OR MAX(c.RewardClaimDate) <> 0;
GO

CREATE OR ALTER PROCEDURE game.usp_AccountDailyReward_GetState @AccountId INT, @TodayDate INT
AS
BEGIN
    SET
        NOCOUNT ON;

    IF
        NOT EXISTS (SELECT 1
                    FROM game.Characters
                    WHERE AccountId = @AccountId)
        RETURN;

    DECLARE
        @TodayAsDate DATE = TRY_CONVERT(DATE, CAST(@TodayDate AS VARCHAR(8)), 112);
    DECLARE
        @Day TINYINT = 0;
    DECLARE
        @Date INT = 0;

    SELECT @Day = RewardClaimDay,
           @Date = RewardClaimDate
    FROM game.AccountDailyRewards
    WHERE AccountId = @AccountId;

    SELECT RewardClaimDay  = CAST(CASE
                                      WHEN @Date <> 0
                                          AND DATEDIFF(DAY, 0, @TodayAsDate) / 7 <>
                                              DATEDIFF(DAY, 0, TRY_CONVERT(DATE, CAST(@Date AS VARCHAR(8)), 112)) / 7
                                          THEN 0
                                      ELSE @Day
        END AS TINYINT),
           RewardClaimDate = @Date;
END;
GO

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
GO

CREATE OR ALTER PROCEDURE game.usp_AccountDailyReward_ResetAll @ClearWeeklyDayCounter BIT
AS
BEGIN
    SET
        NOCOUNT ON;

    UPDATE game.AccountDailyRewards
    SET RewardClaimDate = 0,
        RewardClaimDay  = CASE WHEN @ClearWeeklyDayCounter = 1 THEN 0 ELSE RewardClaimDay END,
        UpdatedAtUtc    = SYSUTCDATETIME()
    WHERE RewardClaimDate <> 0
       OR (@ClearWeeklyDayCounter = 1 AND RewardClaimDay <> 0);
END;
GO
