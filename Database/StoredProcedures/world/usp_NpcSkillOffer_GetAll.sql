CREATE PROCEDURE world.usp_NpcSkillOffer_GetAll
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT NpcSkillOfferId,
           NpcId,
           ArrayKind,
           Tier,
           Dim2,
           Dim3,
           SlotIndex,
           SkillId
    FROM world.NpcSkillOffers
    ORDER BY NpcId, ArrayKind, Tier, Dim2, Dim3, SlotIndex;
END;
