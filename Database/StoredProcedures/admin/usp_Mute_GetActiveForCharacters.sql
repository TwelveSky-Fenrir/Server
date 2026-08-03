CREATE PROCEDURE admin.usp_Mute_GetActiveForCharacters @CharacterIds admin.tvp_CharacterIdList READONLY
AS
BEGIN
    SET NOCOUNT ON;

    SELECT DISTINCT ids.CharacterId
    FROM @CharacterIds AS ids
             INNER JOIN game.Characters AS c ON c.CharacterId = ids.CharacterId
             INNER JOIN admin.Mutes AS m
                        ON (m.CharacterId = ids.CharacterId OR m.AccountId = c.AccountId)
                            AND m.LiftedAtUtc IS NULL
                            AND (m.ExpiresAtUtc IS NULL OR m.ExpiresAtUtc > SYSUTCDATETIME());
END;
