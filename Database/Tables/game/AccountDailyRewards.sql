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
