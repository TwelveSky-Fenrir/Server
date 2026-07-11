using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.Hotkeys;

public static class BotHotKeyResupplyPolicy
{

        public enum ResupplyCategory
    {

                None,

                Hp,

                Mp,

                HpMp,

                PetPrey,

                PetFood
    }

        public static ResupplyCategory ClassifyHpMpByPotionType(int potionType1)
    {
        return potionType1 switch
        {
            1 or 2 => ResupplyCategory.Hp,
            3 or 4 => ResupplyCategory.Mp,
            5 => ResupplyCategory.HpMp,
            _ => ResupplyCategory.None
        };
    }

        public static ImmutableArray<ResupplyMove> Resolve(
        BoundCategories boundCategories,
        IReadOnlyList<InventoryCandidate> inventoryCandidates,
        IReadOnlyList<HotkeyAddress> emptyHotkeySlots,
        bool animalPreyCmd, bool petEquipped,
        bool animalFoodCmd, bool animalPresent)
    {
        ArgumentNullException.ThrowIfNull(inventoryCandidates);
        ArgumentNullException.ThrowIfNull(emptyHotkeySlots);

        var moves = ImmutableArray.CreateBuilder<ResupplyMove>(4);
        var usedCandidates = 0;
        var nextEmptySlot = 0;

        if (!boundCategories.HasHp && !boundCategories.HasHpMp)
            TryRefill(ResupplyCategory.Hp, ResupplyCategory.HpMp);

        if (!boundCategories.HasMp && !boundCategories.HasHpMp)
            TryRefill(ResupplyCategory.Mp, ResupplyCategory.HpMp);

        if (!boundCategories.HasPetPrey && animalPreyCmd && petEquipped)
            TryRefill(ResupplyCategory.PetPrey, ResupplyCategory.PetPrey);

        if (!boundCategories.HasPetFood && animalFoodCmd && animalPresent)
            TryRefill(ResupplyCategory.PetFood, ResupplyCategory.PetFood);

        return moves.ToImmutable();

        void TryRefill(ResupplyCategory primary, ResupplyCategory alsoAccept)
        {
            if (nextEmptySlot >= emptyHotkeySlots.Count)
                return;

            for (var i = 0; i < inventoryCandidates.Count; i++)
            {
                if ((usedCandidates & (1 << i)) != 0)
                    continue;

                var candidate = inventoryCandidates[i];
                if (candidate.Category != primary && candidate.Category != alsoAccept)
                    continue;

                var destination = emptyHotkeySlots[nextEmptySlot];
                moves.Add(new ResupplyMove(candidate.Page, candidate.Slot, destination.Page, destination.Index,
                    candidate.ItemId, candidate.Quantity));
                usedCandidates |= 1 << i;
                nextEmptySlot++;
                return;
            }
        }
    }

        public readonly record struct BoundCategories(
        bool HasHp,
        bool HasMp,
        bool HasHpMp,
        bool HasPetPrey,
        bool HasPetFood);

        public readonly record struct InventoryCandidate(
        byte Page,
        byte Slot,
        int ItemId,
        int Quantity,
        ResupplyCategory Category);

        public readonly record struct HotkeyAddress(byte Page, byte Index);

        public readonly record struct ResupplyMove(
        byte SourcePage,
        byte SourceSlot,
        byte DestinationPage,
        byte DestinationIndex,
        int ItemId,
        int Quantity);
}
