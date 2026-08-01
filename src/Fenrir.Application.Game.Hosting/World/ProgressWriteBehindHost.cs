using Fenrir.Application.Game.Domain.Costumes;
using Fenrir.Application.Game.Domain.Mounts;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Data.WriteBehind;

namespace Fenrir.Application.Game.Hosting.World;

public sealed class ProgressWriteBehindHost(ZoneRegistry zones, ICharacterRepository characters)
{
    private const DirtyFlags ProgressFlags = DirtyFlags.Vitals | DirtyFlags.Progression;

    public async ValueTask<IReadOnlySet<int>> FlushAsync(IReadOnlyDictionary<int, DirtyFlags> dirty,
        CancellationToken ct)
    {
        var rows = new List<CharacterProgressTvp>(dirty.Count);
        var costumes = new List<CharacterCostumeSlotTvp>();
        var claimed = new HashSet<int>();
        List<(PlayerRuntimeState State, int WarPoint, int BloodCoin)>? credited = null;

        foreach (var (characterId, flags) in dirty)
        {
            if ((flags & ProgressFlags) == 0)
                continue;

            if (!zones.TryGetPlayer(characterId, out var state))
                continue;

            // WarPoint/BloodCoin ship as a delta, never a balance -- the baseline only advances once SQL
            // accepted the credit, so a failed batch replays the same grant instead of losing it.
            var warPoint = state.WarPoint;
            var bloodCoin = state.BloodCoin;
            if (warPoint != state.PersistedWarPoint || bloodCoin != state.PersistedBloodCoin)
                (credited ??= []).Add((state, warPoint, bloodCoin));

            rows.Add(new CharacterProgressTvp(characterId, state.FlushSequence, state.Level, state.Level2,
                state.Experience, state.Life, state.MaxLife, state.Mana, state.MaxMana, state.StatVit,
                state.StatStr, state.StatInt, state.StatDex, state.StatPoints, state.SkillPoints,
                state.ContributionPoints, state.Exp2, state.RebirthCount, state.EatLifePotion, state.EatManaPotion,
                state.EatStrPotion, state.EatDexPotion, state.EatElePotion, state.DropItemTime,
                state.M15PetLuckyBoxPity,
                state.MountGarage[MountPersistenceCodec.PersistedGarageSlot],
                MountPersistenceCodec.EncodeExpActivity(state.MountActivity, state.MountAccumulatedExp),
                MountPersistenceCodec.EncodePower(state.MountRolledAttributes),
                state.AnimalIndex, state.AnimalTime,
                state.VisibleState, state.SpecialState, state.UseOrnament ? 1 : 0,
                state.Title, state.Halo, state.TeacherPoint,
                warPoint - state.PersistedWarPoint, bloodCoin - state.PersistedBloodCoin,
                state.PetExpX2Time, state.AnimalAbsorbTime, state.AnimalAbsorbState, state.CostumeIndex,
                // Nommes a partir d'ici: le TVP est en append continu par plusieurs lots, un ajout positionnel
                // decale silencieusement tout ce qui suit vers le mauvais parametre.
                ProtectForHalo: state.ProtectForHalo,
                BonusItemLevel: state.BonusItemLevel,
                BonusItemValue: state.BonusItemValue,
                TribeNotifyScrollCount: state.TribeNotifyScrollCount,
                TribeFourReturnAllowance: state.TribeFourReturnAllowance,
                BottleSlots: BottleSlotsCodec.Encode(state.BottleSlots),
                DrunkBottleIndex: state.DrunkBottleIndex,
                AutoBuffTime: state.AutoBuffTime,
                AutoBuffSkill: AutoBuffSkillCodec.Encode(state.AutoBuffSkill),
                RankPointDate: state.RankPointDate,
                RankBuffType: state.RankBuffType,
                AutoTime: state.AutoHuntPaidDayBudget,
                AutoTime2: state.AutoHuntPaidMinuteBudget,
                BuffX2Time: state.BuffX2Time,
                PremiumExpireUtc: state.PremiumExpireUtc,
                PetGrowth: state.PetGrowth,
                PetActivity: state.PetActivity,
                RankPoint: state.RankPoint,
                CloakLuckyBoxPity: state.CloakLuckyBoxPity,
                CloakVariantBoxPity: state.CloakVariantBoxPity,
                MountVariantBoxPity: state.MountVariantBoxPity,
                ImproveItemValue: state.ImproveItemValue,
                AddItemValue: state.AddItemValue,
                HighItemValue: state.HighItemValue,
                TaiyanKeyTimer: state.TaiyanKeyTimer,
                ProtectForRefine: state.ProtectForRefine,
                ProtectForDestroy: state.ProtectForDestroy,
                ProtectForCostume: state.ProtectForCostume,
                ProtectForDestroy2: state.ProtectForDestroy2,
                LodRounds: state.LodRounds,
                StellarCoreExpireDate: StellarCoreExpireDateCodec.Encode(state.StellarCoreExpireDate),
                EliteDungeonTime: state.EliteDungeonTime,
                DungeonKeyTime: state.DungeonKeyTime,
                IvyHallTicketTime: state.IvyHallTicketTime,
                ScrollOfSeekersTime: state.ScrollOfSeekersTime,
                FightingGodForDestroy: state.FightingGodForDestroy,
                PetBagDate: state.PetBagDate,
                PlayTime1: state.PlayTime1,
                PlayTime3: state.PlayTime3,
                HsbStoneRewardClaimed: state.HsbStoneRewardClaimed ? 1 : 0,
                TowerCpMilestoneCounter: state.TowerCpMilestoneCounter,
                InventoryDate: state.InventoryDate,
                StoreDate: state.StoreDate,
                WarriorPill: state.WarriorPill,
                WarriorScroll: state.WarriorScroll));

            // Penderie COMPLETE de chaque personnage emis: la procedure remplace, elle ne fusionne pas.
            CostumePersistenceCodec.AppendOccupiedSlots(costumes, characterId, state);

            claimed.Add(characterId);
        }

        await characters.PersistProgressAsync(rows, costumes, ct).ConfigureAwait(false);

        if (credited is not null)
            foreach (var (state, warPoint, bloodCoin) in credited)
            {
                state.PersistedWarPoint = warPoint;
                state.PersistedBloodCoin = bloodCoin;
            }

        return claimed;
    }
}
