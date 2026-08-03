CREATE PROCEDURE world.usp_TribeConversionCatalog_GetAll
AS
BEGIN
    SET NOCOUNT ON;

    SELECT GroupIndex, TribeId, SkillId
    FROM world.TribeSkillEquivalences
    ORDER BY GroupIndex, TribeId;

    SELECT GroupIndex, TribeId, ItemId
    FROM world.TribeItemEquivalences
    ORDER BY GroupIndex, TribeId;

    SELECT GroupIndex, TribeId, ItemId
    FROM world.TribeCostumeEquivalences
    ORDER BY GroupIndex, TribeId;
END;
