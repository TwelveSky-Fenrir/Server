using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Domain.World;

public static class ZoneTransfer
{
    private static readonly byte[] AllContainers =
    [
        ContainerMatrix.InventoryPage0, ContainerMatrix.InventoryPage1, ContainerMatrix.Equipment,
        ContainerMatrix.StorePage0, ContainerMatrix.StorePage1
    ];

    public static bool Request(Zone source, Zone target, int characterId)
    {
        return source.Post(ZoneCommand.Leave(characterId, target));
    }

    public static PlayerEnterData CreateEnterData(PlayerRuntimeState state, short targetMapId,
        (float X, float Y, float Z)? position = null)
    {
        var (posX, posY, posZ) = position ?? (state.PosX, state.PosY, state.PosZ);

        List<CharacterItemSlotDto> items = [];
        foreach (var container in AllContainers)
        foreach (var (slot, stack) in state.Inventory.GetContainer(container))
            items.Add(stack.ToRow(container, slot));

        List<CharacterSkillDto> skills = [];
        foreach (var (slot, learned) in state.LearnedSkills)
            skills.Add(new CharacterSkillDto(slot, learned.SkillId, learned.Grade));

        List<CharacterHotkeyDto> hotkeys = [];
        foreach (var ((page, index), slot) in state.Hotkeys)
            hotkeys.Add(new CharacterHotkeyDto(page, index, slot.Value1, slot.Value2, (int)slot.Kind));

        return new PlayerEnterData(
            state.Session,
            state.Name,
            state.Tribe,
            state.Gender,
            state.HeadType,
            state.FaceType,
            state.Level,
            targetMapId,
            posX,
            posY,
            posZ,
            state.Heading,
            state.Life,
            state.MaxLife,
            state.Mana,
            state.MaxMana,
            state.FlushSequence + 1,
            state.IsDead,
            items,
            state.Stats,
            state.IsMuted,
            state.GuildId,
            state.GuildName,
            state.GuildRoleDb,
            state.TribeRole,
            state.Friends,
            skills,
            state.TeacherCharacterId,
            state.StudentCharacterId,
            GuildCallName: state.GuildCallName,
            GuildBuffType: state.GuildBuffType,
            GuildBuffActive: state.GuildBuffActive,
            PreviousTribe: state.PreviousTribe,
            StatVit: state.StatVit,
            StatStr: state.StatStr,
            StatInt: state.StatInt,
            StatDex: state.StatDex,
            StatPoints: state.StatPoints,
            Title: state.Title,
            Halo: state.Halo,
            RebirthCount: state.RebirthCount,
            Experience: state.Experience,
            ContributionPoints: state.ContributionPoints,
            TeacherPoint: state.TeacherPoint,
            Level2: state.Level2,
            Exp2: state.Exp2,
            Zone241Time: state.Zone241Time,
            KnownCashCatalogVersion: state.KnownCashCatalogVersion,
            TicksSinceDeath: state.TicksSinceDeath,
            ReviveHackFlag: state.ReviveHackFlag,
            CanUseConsumables: state.CanUseConsumables,
            DeathSubCounter: state.DeathSubCounter,
            DungeonInstanceRoundsRemaining: state.DungeonInstanceRoundsRemaining,
            HeroRankPoints: state.HeroRankPoints,
            EatLifePotion: state.EatLifePotion,
            EatManaPotion: state.EatManaPotion,
            EatStrPotion: state.EatStrPotion,
            EatDexPotion: state.EatDexPotion,
            EatElePotion: state.EatElePotion,
            DropItemTime: state.DropItemTime,
            WarPoint: state.WarPoint,
            QuestProgress: new QuestProgress(state.QuestStepPermanent, state.QuestActiveFlag, state.QuestSort,
                state.QuestTargetPhase, state.QuestKillCounter),
            MissionJoinWar: state.MissionJoinWar,
            MissionKillOtherTribe: state.MissionKillOtherTribe,
            MissionKillMonster: state.MissionKillMonster,
            MissionPlayTime: state.MissionPlayTime,
            AutoHuntEnabled: state.AutoHuntEnabled,
            AutoHuntConfig: state.AutoHuntConfig,
            AutoLifeRatio: state.AutoLifeRatio,
            AutoManaRatio: state.AutoManaRatio,
            PetGrowth: state.PetGrowth,
            PetActivity: state.PetActivity,
            PremiumExpireUtc: state.PremiumExpireUtc,
            BuffX2Time: state.BuffX2Time,
            Buffs: ZoneTransferBuffRules.Resolve(state.Buffs, targetMapId),
            PetExpX2Time: state.PetExpX2Time,
            Hotkeys: hotkeys,
            StoreMoney: state.StoreMoney,
            BigMoney: state.BigMoney,
            InventoryDate: state.InventoryDate,
            StoreDate: state.StoreDate,
            PetBagDate: state.PetBagDate,
            M15PetLuckyBoxPity: state.M15PetLuckyBoxPity,
            SourceIp: state.SourceIp,
            RuneSystem: state.RuneSystem,
            RuneSystemStat: state.RuneSystemStat);
    }
}

public static class ZoneTransferBuffRules
{
    public const short BuffClearDestinationZoneId = 124;

    public static BuffInfo Resolve(BuffInfo liveBuffs, short targetMapId)
    {
        return targetMapId == BuffClearDestinationZoneId
            ? new BuffInfo { Buff = new int[70] }
            : new BuffInfo { Buff = (int[])liveBuffs.Buff.Clone() };
    }
}
