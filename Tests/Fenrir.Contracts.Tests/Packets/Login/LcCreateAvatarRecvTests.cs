using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Login;
using Fenrir.Contracts.Packets.Shared;

namespace Fenrir.Contracts.Tests.Packets.Login;

public class LcCreateAvatarRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContractConstant()
    {
        // ExpectedSize=11173 (1-byte outbound header) -> 11172-byte payload (4 + AvatarInfo 11168 bytes).
        Assert.Equal(11172, LcCreateAvatarRecv.PayloadSize);
        Assert.Equal(11168, AvatarInfo.WireSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields_NoObfuscation()
    {
        var avatar = CreateSampleAvatarInfo();
        var packet = new LcCreateAvatarRecv
        {
            Result = 1,
            AvatarInfo = avatar
        };

        var buffer = new byte[LcCreateAvatarRecv.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(LcCreateAvatarRecv.PayloadSize, written);
        Assert.Equal(packet.Result, BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(0, 4)));

        Assert.True(AvatarInfo.TryRead(buffer.AsSpan(4, AvatarInfo.WireSize), out var decodedAvatar));
        AssertAvatarInfoEqual(avatar, decodedAvatar);
    }

    // Every scalar gets a unique, increasing value and every array a distinct sequence, so an
    // offset bug reading/writing the wrong region would shift values detectably (unlike an
    // all-zero fixture).
    private static AvatarInfo CreateSampleAvatarInfo()
    {
        var n = 0;

        int I()
        {
            return ++n;
        }

        int[] A(int count)
        {
            var array = new int[count];
            for (var i = 0; i < count; i++)
                array[i] = ++n;
            return array;
        }

        string[] Rows(int count)
        {
            var rows = new string[count];
            for (var i = 0; i < count; i++)
                rows[i] = $"Row{++n}";
            return rows;
        }

        return new AvatarInfo
        {
            VisibleState = I(),
            SpecialState = I(),
            PlayTime1 = I(),
            PlayTime2 = I(),
            KillOtherTribe = I(),
            Name = "AvatarHero",
            Tribe = I(),
            PreviousTribe = I(),
            Gender = I(),
            HeadType = I(),
            FaceType = I(),
            Level1 = I(),
            Level2 = I(),
            Exp1 = I(),
            Exp2 = I(),
            Vit = I(),
            Str = I(),
            Int = I(),
            Dex = I(),
            EatLifePotion = I(),
            EatManaPotion = I(),
            StatPoint = I(),
            SkillPoint = I(),
            Equip = A(52),
            InventoryDate = I(),
            Money = I(),
            Inventory = A(768),
            TradeMoney = I(),
            Trade = A(32),
            StoreDate = I(),
            StoreMoney = I(),
            StoreItem = A(224),
            Skill = A(80),
            HotKey = A(126),
            QuestInfo = A(5),
            Friend = Rows(10),
            Teacher = "TeacherOne",
            Student = "StudentOne",
            TeacherPoint = I(),
            GuildName = "GuildOne",
            GuildRole = I(),
            CallName = "Call",
            GuildMarkNum = I(),
            GuildMarkEffect = I(),
            LogoutInfo = A(6),
            ProtectForDeath = I(),
            ProtectForDestroy = I(),
            FightingGodForDestroy = I(),
            DoubleExpTime1 = I(),
            DoubleKillNumTime = I(),
            DoubleKillExpTime = I(),
            Zone175Time = I(),
            Zone101Time = I(),
            Zone125Time = I(),
            Zone126Time = I(),
            KillMonsterNum = I(),
            KillMonsterNum2 = I(),
            KillMonsterNum3 = I(),
            LevelZoneKeyNum = I(),
            SearchAndBuyDate = I(),
            LifePotionConvertNum = I(),
            ManaPotionConvertNum = I(),
            TribeVoteDate = I(),
            AutoLifeRatio = I(),
            AutoManaRatio = I(),
            EatStrPotion = I(),
            EatDexPotion = I(),
            Animal = A(10),
            AnimalIndex = I(),
            AnimalTime = I(),
            AddItemValue = I(),
            HighItemValue = I(),
            DropItemTime = I(),
            Title = I(),
            DoubleKillNumTime2 = I(),
            Halo = I(),
            BonusItemValue = I(),
            BonusItemLevel = I(),
            KillOtherTribeEvent = I(),
            TeacherPointEvent = I(),
            PlayTimeEvent = I(),
            ProtectForHalo = I(),
            UseOrnament = I(),
            SilverTime = I(),
            Zone234Time = I(),
            Zone235Time = I(),
            Zone236Time = I(),
            Zone237Time = I(),
            Zone238Time = I(),
            Zone239Time = I(),
            Zone240Time = I(),
            RebirthNum = I(),
            Zone241Time = I(),
            ChallengeNum = I(),
            GoldTime = I(),
            Zone234X2Time = I(),
            Zone235X2Time = I(),
            BattleEnterNum = I(),
            BattleEnterDate = I(),
            PlayTime3 = I(),
            BuffX2Time = I(),
            DoubleExpTime2 = I(),
            ReturnTribeNum = I(),
            PetExpX2Time = I(),
            GuildMoneyTime = I(),
            SaveMoney = I(),
            SaveItem = A(112),
            PartyName = Rows(5),
            Costume = A(10),
            CostumeDate = A(10),
            CostumeExpireDate = A(10),
            CostumeIndex = I(),
            DmgBoost = I(),
            HPBoost = I(),
            CriBoost = I(),
            AutoBuffTime = I(),
            AutoBuffSkill = A(16),
            DungeonEvent = I(),
            ImproveItemValue = I(),
            Zone270Score = I(),
            TimeEffectTime = I(),
            StateTimeEffect = I(),
            SelectTimeEffectType = I(),
            InvenSocket = A(384),
            EquipSocket = A(39),
            TradeSocket = A(24),
            StoreSocket = A(168),
            SaveSocket = A(84),
            AutoTime = I(),
            AutoTime2 = I(),
            AutoState = I(),
            AutoHunt = new AutoHunt
            {
                BuffType = I(),
                BuffStore = A(16),
                HuntType = I(),
                AttackType = A(4),
                MonNum = I(),
                ItemType = I(),
                InvenCmd = I(),
                DeathCmd = I(),
                AnimalPreyCmd = I(),
                AnimalFoodCmd = I()
            },
            BigMoney = I(),
            BigTradeMoney = I(),
            BigStoreMoney = I(),
            BigSaveMoney = I(),
            EatElePotion = I(),
            Zone050Time = I(),
            ProtectForRefine = I(),
            GoldenLakeTime = I(),
            PreventForSmelt = I(),
            BloodCoin = I(),
            HeavenlyTicket = I(),
            Tevushi = I(),
            MammothRoad = I(),
            Zone050Time2 = I(),
            FishingZoneDate = I(),
            ProxyShopDate = I(),
            B4GHPElixir = I(),
            B4GMPElixir = I(),
            B4GStrElixir = I(),
            B4GDexElixir = I(),
            KillCount = I(),
            KillCountTime = I(),
            RankPoint = I(),
            RankPointDate = I(),
            RankBuffType = I(),
            TribeNotifyNum = I(),
            SpeakerCount = I(),
            AnimalExpActivity = A(10),
            AnimalPower = A(10),
            AnimalDoubleExp = I(),
            EventValue1 = I(),
            EventValue2 = I(),
            EventValue3 = I(),
            EventValue4 = I(),
            AnimalAbsorbTime = I(),
            AnimalAbsorbState = I(),
            CapsuleCashPoint = I(),
            RageGauge = I(),
            RageBuffTime = I(),
            RageBuffState = I(),
            RageBuffPotion = I(),
            RageDoubleEffect = I(),
            WarriorScroll = I(),
            UseItemMonth = I(),
            UseItemOnce = I(),
            HeroPoint = I(),
            HeroPointDate = I(),
            KillMonsterNumForHeroPoint = I(),
            WarriorPill = I(),
            OllehEventPoint = I(),
            ProtectForWing = I(),
            TeacherPointDate = I(),
            ProtectForDestroy2 = I(),
            PetBagDate = I(),
            PetBag = A(20),
            MissionDate = new MissionDate
            {
                JoinWar = I(),
                KillOtherTribe = I(),
                KillMonster = I(),
                PlayTime = I()
            },
            CriticalSpeedTime = I(),
            DefenceUpNum = I(),
            Zone038Ticket = I(),
            HyunKyungMonster = I(),
            Bottle = A(10),
            BottleCount = A(10),
            BottleIndex = I(),
            BottleTime = I(),
            MixSkill = A(28),
            MixSkillBuffTime = I(),
            BywonjiTime = I(),
            UniqueSkill = I(),
            UniqueSkillBuffTime = I(),
            BackSoul = I(),
            Premium = 987654321012345L,
            ProtectForCombine = I(),
            PlayOnlineTime = I(),
            PlayOnlineTime2 = I(),
            ProtectForCostume = I(),
            WarPoint = I(),
            PopUpKillAvt = I(),
            PopUpKillMonster = I(),
            PopUpKillAvtWar = I(),
            StellarCore = A(10),
            StellarCoreExpireDate = A(10),
            StellarCoreIndex = I(),
            InvenExpireDate = A(128),
            EquipExpireDate = A(13),
            TradeExpireDate = A(8),
            StoreExpireDate = A(56),
            SaveExpireDate = A(28),
            WarKillCount = I(),
            HSBStoneRewardCheck = I(),
            GBox2249 = I(),
            GBox8114 = I(),
            GBox8115 = I(),
            GBox8111 = I(),
            RuneSystem = A(4),
            RuneSystemStat = A(4)
        };
    }

    // Deliberately explicit field-by-field comparison: default record equality compares array/
    // string[] members BY REFERENCE (never equal after a Write/TryRead round trip), so no
    // assertion below may use expected.Equals(actual)/== on a struct holding an array.
    private static void AssertAvatarInfoEqual(AvatarInfo expected, AvatarInfo actual)
    {
        Assert.Equal(expected.VisibleState, actual.VisibleState);
        Assert.Equal(expected.SpecialState, actual.SpecialState);
        Assert.Equal(expected.PlayTime1, actual.PlayTime1);
        Assert.Equal(expected.PlayTime2, actual.PlayTime2);
        Assert.Equal(expected.KillOtherTribe, actual.KillOtherTribe);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Tribe, actual.Tribe);
        Assert.Equal(expected.PreviousTribe, actual.PreviousTribe);
        Assert.Equal(expected.Gender, actual.Gender);
        Assert.Equal(expected.HeadType, actual.HeadType);
        Assert.Equal(expected.FaceType, actual.FaceType);
        Assert.Equal(expected.Level1, actual.Level1);
        Assert.Equal(expected.Level2, actual.Level2);
        Assert.Equal(expected.Exp1, actual.Exp1);
        Assert.Equal(expected.Exp2, actual.Exp2);
        Assert.Equal(expected.Vit, actual.Vit);
        Assert.Equal(expected.Str, actual.Str);
        Assert.Equal(expected.Int, actual.Int);
        Assert.Equal(expected.Dex, actual.Dex);
        Assert.Equal(expected.EatLifePotion, actual.EatLifePotion);
        Assert.Equal(expected.EatManaPotion, actual.EatManaPotion);
        Assert.Equal(expected.StatPoint, actual.StatPoint);
        Assert.Equal(expected.SkillPoint, actual.SkillPoint);
        Assert.True(expected.Equip.SequenceEqual(actual.Equip));
        Assert.Equal(expected.InventoryDate, actual.InventoryDate);
        Assert.Equal(expected.Money, actual.Money);
        Assert.True(expected.Inventory.SequenceEqual(actual.Inventory));
        Assert.Equal(expected.TradeMoney, actual.TradeMoney);
        Assert.True(expected.Trade.SequenceEqual(actual.Trade));
        Assert.Equal(expected.StoreDate, actual.StoreDate);
        Assert.Equal(expected.StoreMoney, actual.StoreMoney);
        Assert.True(expected.StoreItem.SequenceEqual(actual.StoreItem));
        Assert.True(expected.Skill.SequenceEqual(actual.Skill));
        Assert.True(expected.HotKey.SequenceEqual(actual.HotKey));
        Assert.True(expected.QuestInfo.SequenceEqual(actual.QuestInfo));
        Assert.True(expected.Friend.SequenceEqual(actual.Friend));
        Assert.Equal(expected.Teacher, actual.Teacher);
        Assert.Equal(expected.Student, actual.Student);
        Assert.Equal(expected.TeacherPoint, actual.TeacherPoint);
        Assert.Equal(expected.GuildName, actual.GuildName);
        Assert.Equal(expected.GuildRole, actual.GuildRole);
        Assert.Equal(expected.CallName, actual.CallName);
        Assert.Equal(expected.GuildMarkNum, actual.GuildMarkNum);
        Assert.Equal(expected.GuildMarkEffect, actual.GuildMarkEffect);
        Assert.True(expected.LogoutInfo.SequenceEqual(actual.LogoutInfo));
        Assert.Equal(expected.ProtectForDeath, actual.ProtectForDeath);
        Assert.Equal(expected.ProtectForDestroy, actual.ProtectForDestroy);
        Assert.Equal(expected.FightingGodForDestroy, actual.FightingGodForDestroy);
        Assert.Equal(expected.DoubleExpTime1, actual.DoubleExpTime1);
        Assert.Equal(expected.DoubleKillNumTime, actual.DoubleKillNumTime);
        Assert.Equal(expected.DoubleKillExpTime, actual.DoubleKillExpTime);
        Assert.Equal(expected.Zone175Time, actual.Zone175Time);
        Assert.Equal(expected.Zone101Time, actual.Zone101Time);
        Assert.Equal(expected.Zone125Time, actual.Zone125Time);
        Assert.Equal(expected.Zone126Time, actual.Zone126Time);
        Assert.Equal(expected.KillMonsterNum, actual.KillMonsterNum);
        Assert.Equal(expected.KillMonsterNum2, actual.KillMonsterNum2);
        Assert.Equal(expected.KillMonsterNum3, actual.KillMonsterNum3);
        Assert.Equal(expected.LevelZoneKeyNum, actual.LevelZoneKeyNum);
        Assert.Equal(expected.SearchAndBuyDate, actual.SearchAndBuyDate);
        Assert.Equal(expected.LifePotionConvertNum, actual.LifePotionConvertNum);
        Assert.Equal(expected.ManaPotionConvertNum, actual.ManaPotionConvertNum);
        Assert.Equal(expected.TribeVoteDate, actual.TribeVoteDate);
        Assert.Equal(expected.AutoLifeRatio, actual.AutoLifeRatio);
        Assert.Equal(expected.AutoManaRatio, actual.AutoManaRatio);
        Assert.Equal(expected.EatStrPotion, actual.EatStrPotion);
        Assert.Equal(expected.EatDexPotion, actual.EatDexPotion);
        Assert.True(expected.Animal.SequenceEqual(actual.Animal));
        Assert.Equal(expected.AnimalIndex, actual.AnimalIndex);
        Assert.Equal(expected.AnimalTime, actual.AnimalTime);
        Assert.Equal(expected.AddItemValue, actual.AddItemValue);
        Assert.Equal(expected.HighItemValue, actual.HighItemValue);
        Assert.Equal(expected.DropItemTime, actual.DropItemTime);
        Assert.Equal(expected.Title, actual.Title);
        Assert.Equal(expected.DoubleKillNumTime2, actual.DoubleKillNumTime2);
        Assert.Equal(expected.Halo, actual.Halo);
        Assert.Equal(expected.BonusItemValue, actual.BonusItemValue);
        Assert.Equal(expected.BonusItemLevel, actual.BonusItemLevel);
        Assert.Equal(expected.KillOtherTribeEvent, actual.KillOtherTribeEvent);
        Assert.Equal(expected.TeacherPointEvent, actual.TeacherPointEvent);
        Assert.Equal(expected.PlayTimeEvent, actual.PlayTimeEvent);
        Assert.Equal(expected.ProtectForHalo, actual.ProtectForHalo);
        Assert.Equal(expected.UseOrnament, actual.UseOrnament);
        Assert.Equal(expected.SilverTime, actual.SilverTime);
        Assert.Equal(expected.Zone234Time, actual.Zone234Time);
        Assert.Equal(expected.Zone235Time, actual.Zone235Time);
        Assert.Equal(expected.Zone236Time, actual.Zone236Time);
        Assert.Equal(expected.Zone237Time, actual.Zone237Time);
        Assert.Equal(expected.Zone238Time, actual.Zone238Time);
        Assert.Equal(expected.Zone239Time, actual.Zone239Time);
        Assert.Equal(expected.Zone240Time, actual.Zone240Time);
        Assert.Equal(expected.RebirthNum, actual.RebirthNum);
        Assert.Equal(expected.Zone241Time, actual.Zone241Time);
        Assert.Equal(expected.ChallengeNum, actual.ChallengeNum);
        Assert.Equal(expected.GoldTime, actual.GoldTime);
        Assert.Equal(expected.Zone234X2Time, actual.Zone234X2Time);
        Assert.Equal(expected.Zone235X2Time, actual.Zone235X2Time);
        Assert.Equal(expected.BattleEnterNum, actual.BattleEnterNum);
        Assert.Equal(expected.BattleEnterDate, actual.BattleEnterDate);
        Assert.Equal(expected.PlayTime3, actual.PlayTime3);
        Assert.Equal(expected.BuffX2Time, actual.BuffX2Time);
        Assert.Equal(expected.DoubleExpTime2, actual.DoubleExpTime2);
        Assert.Equal(expected.ReturnTribeNum, actual.ReturnTribeNum);
        Assert.Equal(expected.PetExpX2Time, actual.PetExpX2Time);
        Assert.Equal(expected.GuildMoneyTime, actual.GuildMoneyTime);
        Assert.Equal(expected.SaveMoney, actual.SaveMoney);
        Assert.True(expected.SaveItem.SequenceEqual(actual.SaveItem));
        Assert.True(expected.PartyName.SequenceEqual(actual.PartyName));
        Assert.True(expected.Costume.SequenceEqual(actual.Costume));
        Assert.True(expected.CostumeDate.SequenceEqual(actual.CostumeDate));
        Assert.True(expected.CostumeExpireDate.SequenceEqual(actual.CostumeExpireDate));
        Assert.Equal(expected.CostumeIndex, actual.CostumeIndex);
        Assert.Equal(expected.DmgBoost, actual.DmgBoost);
        Assert.Equal(expected.HPBoost, actual.HPBoost);
        Assert.Equal(expected.CriBoost, actual.CriBoost);
        Assert.Equal(expected.AutoBuffTime, actual.AutoBuffTime);
        Assert.True(expected.AutoBuffSkill.SequenceEqual(actual.AutoBuffSkill));
        Assert.Equal(expected.DungeonEvent, actual.DungeonEvent);
        Assert.Equal(expected.ImproveItemValue, actual.ImproveItemValue);
        Assert.Equal(expected.Zone270Score, actual.Zone270Score);
        Assert.Equal(expected.TimeEffectTime, actual.TimeEffectTime);
        Assert.Equal(expected.StateTimeEffect, actual.StateTimeEffect);
        Assert.Equal(expected.SelectTimeEffectType, actual.SelectTimeEffectType);
        Assert.True(expected.InvenSocket.SequenceEqual(actual.InvenSocket));
        Assert.True(expected.EquipSocket.SequenceEqual(actual.EquipSocket));
        Assert.True(expected.TradeSocket.SequenceEqual(actual.TradeSocket));
        Assert.True(expected.StoreSocket.SequenceEqual(actual.StoreSocket));
        Assert.True(expected.SaveSocket.SequenceEqual(actual.SaveSocket));
        Assert.Equal(expected.AutoTime, actual.AutoTime);
        Assert.Equal(expected.AutoTime2, actual.AutoTime2);
        Assert.Equal(expected.AutoState, actual.AutoState);

        Assert.Equal(expected.AutoHunt.BuffType, actual.AutoHunt.BuffType);
        Assert.True(expected.AutoHunt.BuffStore.SequenceEqual(actual.AutoHunt.BuffStore));
        Assert.Equal(expected.AutoHunt.HuntType, actual.AutoHunt.HuntType);
        Assert.True(expected.AutoHunt.AttackType.SequenceEqual(actual.AutoHunt.AttackType));
        Assert.Equal(expected.AutoHunt.MonNum, actual.AutoHunt.MonNum);
        Assert.Equal(expected.AutoHunt.ItemType, actual.AutoHunt.ItemType);
        Assert.Equal(expected.AutoHunt.InvenCmd, actual.AutoHunt.InvenCmd);
        Assert.Equal(expected.AutoHunt.DeathCmd, actual.AutoHunt.DeathCmd);
        Assert.Equal(expected.AutoHunt.AnimalPreyCmd, actual.AutoHunt.AnimalPreyCmd);
        Assert.Equal(expected.AutoHunt.AnimalFoodCmd, actual.AutoHunt.AnimalFoodCmd);

        Assert.Equal(expected.BigMoney, actual.BigMoney);
        Assert.Equal(expected.BigTradeMoney, actual.BigTradeMoney);
        Assert.Equal(expected.BigStoreMoney, actual.BigStoreMoney);
        Assert.Equal(expected.BigSaveMoney, actual.BigSaveMoney);
        Assert.Equal(expected.EatElePotion, actual.EatElePotion);
        Assert.Equal(expected.Zone050Time, actual.Zone050Time);
        Assert.Equal(expected.ProtectForRefine, actual.ProtectForRefine);
        Assert.Equal(expected.GoldenLakeTime, actual.GoldenLakeTime);
        Assert.Equal(expected.PreventForSmelt, actual.PreventForSmelt);
        Assert.Equal(expected.BloodCoin, actual.BloodCoin);
        Assert.Equal(expected.HeavenlyTicket, actual.HeavenlyTicket);
        Assert.Equal(expected.Tevushi, actual.Tevushi);
        Assert.Equal(expected.MammothRoad, actual.MammothRoad);
        Assert.Equal(expected.Zone050Time2, actual.Zone050Time2);
        Assert.Equal(expected.FishingZoneDate, actual.FishingZoneDate);
        Assert.Equal(expected.ProxyShopDate, actual.ProxyShopDate);
        Assert.Equal(expected.B4GHPElixir, actual.B4GHPElixir);
        Assert.Equal(expected.B4GMPElixir, actual.B4GMPElixir);
        Assert.Equal(expected.B4GStrElixir, actual.B4GStrElixir);
        Assert.Equal(expected.B4GDexElixir, actual.B4GDexElixir);
        Assert.Equal(expected.KillCount, actual.KillCount);
        Assert.Equal(expected.KillCountTime, actual.KillCountTime);
        Assert.Equal(expected.RankPoint, actual.RankPoint);
        Assert.Equal(expected.RankPointDate, actual.RankPointDate);
        Assert.Equal(expected.RankBuffType, actual.RankBuffType);
        Assert.Equal(expected.TribeNotifyNum, actual.TribeNotifyNum);
        Assert.Equal(expected.SpeakerCount, actual.SpeakerCount);
        Assert.True(expected.AnimalExpActivity.SequenceEqual(actual.AnimalExpActivity));
        Assert.True(expected.AnimalPower.SequenceEqual(actual.AnimalPower));
        Assert.Equal(expected.AnimalDoubleExp, actual.AnimalDoubleExp);
        Assert.Equal(expected.EventValue1, actual.EventValue1);
        Assert.Equal(expected.EventValue2, actual.EventValue2);
        Assert.Equal(expected.EventValue3, actual.EventValue3);
        Assert.Equal(expected.EventValue4, actual.EventValue4);
        Assert.Equal(expected.AnimalAbsorbTime, actual.AnimalAbsorbTime);
        Assert.Equal(expected.AnimalAbsorbState, actual.AnimalAbsorbState);
        Assert.Equal(expected.CapsuleCashPoint, actual.CapsuleCashPoint);
        Assert.Equal(expected.RageGauge, actual.RageGauge);
        Assert.Equal(expected.RageBuffTime, actual.RageBuffTime);
        Assert.Equal(expected.RageBuffState, actual.RageBuffState);
        Assert.Equal(expected.RageBuffPotion, actual.RageBuffPotion);
        Assert.Equal(expected.RageDoubleEffect, actual.RageDoubleEffect);
        Assert.Equal(expected.WarriorScroll, actual.WarriorScroll);
        Assert.Equal(expected.UseItemMonth, actual.UseItemMonth);
        Assert.Equal(expected.UseItemOnce, actual.UseItemOnce);
        Assert.Equal(expected.HeroPoint, actual.HeroPoint);
        Assert.Equal(expected.HeroPointDate, actual.HeroPointDate);
        Assert.Equal(expected.KillMonsterNumForHeroPoint, actual.KillMonsterNumForHeroPoint);
        Assert.Equal(expected.WarriorPill, actual.WarriorPill);
        Assert.Equal(expected.OllehEventPoint, actual.OllehEventPoint);
        Assert.Equal(expected.ProtectForWing, actual.ProtectForWing);
        Assert.Equal(expected.TeacherPointDate, actual.TeacherPointDate);
        Assert.Equal(expected.ProtectForDestroy2, actual.ProtectForDestroy2);
        Assert.Equal(expected.PetBagDate, actual.PetBagDate);
        Assert.True(expected.PetBag.SequenceEqual(actual.PetBag));

        Assert.Equal(expected.MissionDate.JoinWar, actual.MissionDate.JoinWar);
        Assert.Equal(expected.MissionDate.KillOtherTribe, actual.MissionDate.KillOtherTribe);
        Assert.Equal(expected.MissionDate.KillMonster, actual.MissionDate.KillMonster);
        Assert.Equal(expected.MissionDate.PlayTime, actual.MissionDate.PlayTime);

        Assert.Equal(expected.CriticalSpeedTime, actual.CriticalSpeedTime);
        Assert.Equal(expected.DefenceUpNum, actual.DefenceUpNum);
        Assert.Equal(expected.Zone038Ticket, actual.Zone038Ticket);
        Assert.Equal(expected.HyunKyungMonster, actual.HyunKyungMonster);
        Assert.True(expected.Bottle.SequenceEqual(actual.Bottle));
        Assert.True(expected.BottleCount.SequenceEqual(actual.BottleCount));
        Assert.Equal(expected.BottleIndex, actual.BottleIndex);
        Assert.Equal(expected.BottleTime, actual.BottleTime);
        Assert.True(expected.MixSkill.SequenceEqual(actual.MixSkill));
        Assert.Equal(expected.MixSkillBuffTime, actual.MixSkillBuffTime);
        Assert.Equal(expected.BywonjiTime, actual.BywonjiTime);
        Assert.Equal(expected.UniqueSkill, actual.UniqueSkill);
        Assert.Equal(expected.UniqueSkillBuffTime, actual.UniqueSkillBuffTime);
        Assert.Equal(expected.BackSoul, actual.BackSoul);
        Assert.Equal(expected.Premium, actual.Premium);
        Assert.Equal(expected.ProtectForCombine, actual.ProtectForCombine);
        Assert.Equal(expected.PlayOnlineTime, actual.PlayOnlineTime);
        Assert.Equal(expected.PlayOnlineTime2, actual.PlayOnlineTime2);
        Assert.Equal(expected.ProtectForCostume, actual.ProtectForCostume);
        Assert.Equal(expected.WarPoint, actual.WarPoint);
        Assert.Equal(expected.PopUpKillAvt, actual.PopUpKillAvt);
        Assert.Equal(expected.PopUpKillMonster, actual.PopUpKillMonster);
        Assert.Equal(expected.PopUpKillAvtWar, actual.PopUpKillAvtWar);
        Assert.True(expected.StellarCore.SequenceEqual(actual.StellarCore));
        Assert.True(expected.StellarCoreExpireDate.SequenceEqual(actual.StellarCoreExpireDate));
        Assert.Equal(expected.StellarCoreIndex, actual.StellarCoreIndex);
        Assert.True(expected.InvenExpireDate.SequenceEqual(actual.InvenExpireDate));
        Assert.True(expected.EquipExpireDate.SequenceEqual(actual.EquipExpireDate));
        Assert.True(expected.TradeExpireDate.SequenceEqual(actual.TradeExpireDate));
        Assert.True(expected.StoreExpireDate.SequenceEqual(actual.StoreExpireDate));
        Assert.True(expected.SaveExpireDate.SequenceEqual(actual.SaveExpireDate));
        Assert.Equal(expected.WarKillCount, actual.WarKillCount);
        Assert.Equal(expected.HSBStoneRewardCheck, actual.HSBStoneRewardCheck);
        Assert.Equal(expected.GBox2249, actual.GBox2249);
        Assert.Equal(expected.GBox8114, actual.GBox8114);
        Assert.Equal(expected.GBox8115, actual.GBox8115);
        Assert.Equal(expected.GBox8111, actual.GBox8111);
        Assert.True(expected.RuneSystem.SequenceEqual(actual.RuneSystem));
        Assert.True(expected.RuneSystemStat.SequenceEqual(actual.RuneSystemStat));
    }
}
