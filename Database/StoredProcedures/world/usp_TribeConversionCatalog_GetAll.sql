-- Boot-time bulk reader over world.Tribe{Skill,Item,Costume}Equivalences, same "world.* is read-mostly,
-- loaded once into an in-memory cache" posture as usp_Skill_GetAll/usp_Item_GetAll (see WorldDataRepository's
-- own header comment). Returns 3 result sets in fixed order: Skill, Item, Costume equivalences, each
-- ordered (GroupIndex, TribeId) so a caller can rebuild the 3-column legacy shape row-by-row without an
-- extra client-side sort.
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
