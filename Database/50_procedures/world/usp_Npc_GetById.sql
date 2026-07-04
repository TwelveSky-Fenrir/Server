-- Returns 6 result sets in fixed order: Npc, NpcSpeeches, NpcMenuOptions, NpcShopItems, NpcSkillOffers, NpcGambleCosts.
CREATE PROCEDURE world.usp_Npc_GetById @NpcId INT
AS
BEGIN
    SET
NOCOUNT ON;

SELECT NpcId,
       Name,
       Tribe,
       Type,
       DataSortNumber2D,
       DataSortNumber3D,
       Size1,
       Size2,
       Size3
FROM world.Npcs
WHERE NpcId = @NpcId;

SELECT NpcId, SpeechGroup, SpeechIndex, Text
FROM world.NpcSpeeches
WHERE NpcId = @NpcId
ORDER BY SpeechGroup, SpeechIndex;

SELECT NpcId, SlotIndex, OptionId
FROM world.NpcMenuOptions
WHERE NpcId = @NpcId
ORDER BY SlotIndex;

SELECT NpcId, ShopPage, SlotIndex, ItemId
FROM world.NpcShopItems
WHERE NpcId = @NpcId
ORDER BY ShopPage, SlotIndex;

SELECT NpcSkillOfferId,
       NpcId,
       ArrayKind,
       Tier,
       Dim2,
       Dim3,
       SlotIndex,
       SkillId
FROM world.NpcSkillOffers
WHERE NpcId = @NpcId
ORDER BY ArrayKind, Tier, Dim2, Dim3, SlotIndex;

SELECT NpcId, GambleTier, CostIndex, Value
FROM world.NpcGambleCosts
WHERE NpcId = @NpcId
ORDER BY GambleTier, CostIndex;
END;
