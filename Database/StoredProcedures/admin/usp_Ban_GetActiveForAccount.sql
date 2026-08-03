CREATE PROCEDURE admin.usp_Ban_GetActiveForAccount @AccountId INT
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT BanId,
           AccountId,
           AccountLoginName,
           CharacterId,
           CharacterName,
           Reason,
           ExpiresAtUtc,
           CreatedAtUtc
    FROM admin.vw_ActiveBans
    WHERE AccountId = @AccountId;
END;
