CREATE TRIGGER game.tr_AccountVaultItems_BumpRevision
    ON game.AccountVaultItems
    AFTER INSERT, UPDATE, DELETE
    AS
BEGIN
    SET NOCOUNT ON;

    UPDATE vault
    SET Revision     = Revision + 1,
        UpdatedAtUtc = SYSUTCDATETIME()
    FROM game.AccountVault AS vault
             INNER JOIN
         (SELECT AccountId
          FROM inserted

          UNION

          SELECT AccountId
          FROM deleted) AS changed ON changed.AccountId = vault.AccountId;
END;
