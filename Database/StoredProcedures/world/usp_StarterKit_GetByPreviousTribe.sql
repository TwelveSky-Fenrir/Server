-- Everything CreateAvatarHandler needs to assemble op17's starter kit in one round trip. @MapId is the
-- caller-resolved spawn map (Tribe -> MapId is a fixed 4-entry mapping owned by the handler, mirroring
-- mapcheck.h::GetReturnBornInTownLocation) -- RS4 just looks up that map's default spawn point, already
-- seeded on world.Zones.
CREATE PROCEDURE world.usp_StarterKit_GetByPreviousTribe @PreviousTribe TINYINT, @MapId SMALLINT
AS
BEGIN
    SET
NOCOUNT ON;

SELECT EquipSlot, ItemId
FROM world.StarterKitEquipment
WHERE PreviousTribe = @PreviousTribe
ORDER BY EquipSlot, ItemId;

SELECT SlotIndex, ItemId, Quantity
FROM world.StarterKitInventory
ORDER BY SlotIndex;

SELECT SlotIndex, SkillId, Grade
FROM world.StarterKitSkills
WHERE PreviousTribe = @PreviousTribe
ORDER BY SlotIndex;

SELECT Page, KeyIndex, Sort, Value1, Value2
FROM world.StarterKitHotkeys
WHERE PreviousTribe = @PreviousTribe
ORDER BY Page, KeyIndex;

SELECT DefaultSpawnX, DefaultSpawnY, DefaultSpawnZ
FROM world.Zones
WHERE ZoneNumber = @MapId;
END;
