CREATE PROCEDURE world.usp_RewardBundle_GetAll
    AS
BEGIN
    SET
NOCOUNT ON;

SELECT RewardBundleId
FROM world.RewardBundles
ORDER BY RewardBundleId;
END;
