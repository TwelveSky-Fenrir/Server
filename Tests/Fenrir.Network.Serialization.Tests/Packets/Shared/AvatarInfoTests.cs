using System.Buffers.Binary;
using System.Text;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Tests.TestSupport;

namespace Fenrir.Network.Serialization.Tests.Packets.Shared;

public class AvatarInfoTests
{
    [Fact]
    public void WireSize_MatchesContract()
    {
        Assert.Equal(11168, AvatarInfo.WireSize);
    }

    // Property order matters here, not values; binary offsets are owned by the generator/[FenrirWireType].
    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var v = new SequentialValueFactory();

        var autoHunt = new AutoHunt
        {
            BuffType = v.NextInt(),
            BuffStore = v.NextIntArray(16),
            HuntType = v.NextInt(),
            AttackType = v.NextIntArray(4),
            MonNum = v.NextInt(),
            ItemType = v.NextInt(),
            InvenCmd = v.NextInt(),
            DeathCmd = v.NextInt(),
            AnimalPreyCmd = v.NextInt(),
            AnimalFoodCmd = v.NextInt()
        };

        var missionDate = new MissionDate
        {
            JoinWar = v.NextInt(),
            KillOtherTribe = v.NextInt(),
            KillMonster = v.NextInt(),
            PlayTime = v.NextInt()
        };

        var name = v.NextString(13);
        var tribe = v.NextInt();
        var guildName = v.NextString(13);
        var callName = v.NextString(5);
        var partyName = v.NextStringArray(5, 13);
        var premium = v.NextLong();

        var avatar = new AvatarInfo
        {
            VisibleState = v.NextInt(),
            SpecialState = v.NextInt(),
            PlayTime1 = v.NextInt(),
            PlayTime2 = v.NextInt(),
            KillOtherTribe = v.NextInt(),
            Name = name,
            Tribe = tribe,
            PreviousTribe = v.NextInt(),
            Gender = v.NextInt(),
            HeadType = v.NextInt(),
            FaceType = v.NextInt(),
            Level1 = v.NextInt(),
            Level2 = v.NextInt(),
            Exp1 = v.NextInt(),
            Exp2 = v.NextInt(),
            Vit = v.NextInt(),
            Str = v.NextInt(),
            Int = v.NextInt(),
            Dex = v.NextInt(),
            EatLifePotion = v.NextInt(),
            EatManaPotion = v.NextInt(),
            StatPoint = v.NextInt(),
            SkillPoint = v.NextInt(),
            Equip = v.NextIntArray(52),
            InventoryDate = v.NextInt(),
            Money = v.NextInt(),
            Inventory = v.NextIntArray(768),
            TradeMoney = v.NextInt(),
            Trade = v.NextIntArray(32),
            StoreDate = v.NextInt(),
            StoreMoney = v.NextInt(),
            StoreItem = v.NextIntArray(224),
            Skill = v.NextIntArray(80),
            HotKey = v.NextIntArray(126),
            QuestInfo = v.NextIntArray(5),
            Friend = v.NextStringArray(10, 13),
            Teacher = v.NextString(13),
            Student = v.NextString(13),
            TeacherPoint = v.NextInt(),
            GuildName = guildName,
            GuildRole = v.NextInt(),
            CallName = callName,
            GuildMarkNum = v.NextInt(),
            GuildMarkEffect = v.NextInt(),
            LogoutInfo = v.NextIntArray(6),
            ProtectForDeath = v.NextInt(),
            ProtectForDestroy = v.NextInt(),
            FightingGodForDestroy = v.NextInt(),
            DoubleExpTime1 = v.NextInt(),
            DoubleKillNumTime = v.NextInt(),
            DoubleKillExpTime = v.NextInt(),
            Zone175Time = v.NextInt(),
            Zone101Time = v.NextInt(),
            Zone125Time = v.NextInt(),
            Zone126Time = v.NextInt(),
            KillMonsterNum = v.NextInt(),
            KillMonsterNum2 = v.NextInt(),
            KillMonsterNum3 = v.NextInt(),
            LevelZoneKeyNum = v.NextInt(),
            SearchAndBuyDate = v.NextInt(),
            LifePotionConvertNum = v.NextInt(),
            ManaPotionConvertNum = v.NextInt(),
            TribeVoteDate = v.NextInt(),
            AutoLifeRatio = v.NextInt(),
            AutoManaRatio = v.NextInt(),
            EatStrPotion = v.NextInt(),
            EatDexPotion = v.NextInt(),
            Animal = v.NextIntArray(10),
            AnimalIndex = v.NextInt(),
            AnimalTime = v.NextInt(),
            AddItemValue = v.NextInt(),
            HighItemValue = v.NextInt(),
            DropItemTime = v.NextInt(),
            Title = v.NextInt(),
            DoubleKillNumTime2 = v.NextInt(),
            Halo = v.NextInt(),
            BonusItemValue = v.NextInt(),
            BonusItemLevel = v.NextInt(),
            KillOtherTribeEvent = v.NextInt(),
            TeacherPointEvent = v.NextInt(),
            PlayTimeEvent = v.NextInt(),
            ProtectForHalo = v.NextInt(),
            UseOrnament = v.NextInt(),
            SilverTime = v.NextInt(),
            Zone234Time = v.NextInt(),
            Zone235Time = v.NextInt(),
            Zone236Time = v.NextInt(),
            Zone237Time = v.NextInt(),
            Zone238Time = v.NextInt(),
            Zone239Time = v.NextInt(),
            Zone240Time = v.NextInt(),
            RebirthNum = v.NextInt(),
            Zone241Time = v.NextInt(),
            ChallengeNum = v.NextInt(),
            GoldTime = v.NextInt(),
            Zone234X2Time = v.NextInt(),
            Zone235X2Time = v.NextInt(),
            BattleEnterNum = v.NextInt(),
            BattleEnterDate = v.NextInt(),
            PlayTime3 = v.NextInt(),
            BuffX2Time = v.NextInt(),
            DoubleExpTime2 = v.NextInt(),
            ReturnTribeNum = v.NextInt(),
            PetExpX2Time = v.NextInt(),
            GuildMoneyTime = v.NextInt(),
            SaveMoney = v.NextInt(),
            SaveItem = v.NextIntArray(112),
            PartyName = partyName,
            Costume = v.NextIntArray(10),
            CostumeDate = v.NextIntArray(10),
            CostumeExpireDate = v.NextIntArray(10),
            CostumeIndex = v.NextInt(),
            DmgBoost = v.NextInt(),
            HPBoost = v.NextInt(),
            CriBoost = v.NextInt(),
            AutoBuffTime = v.NextInt(),
            AutoBuffSkill = v.NextIntArray(16),
            DungeonEvent = v.NextInt(),
            ImproveItemValue = v.NextInt(),
            Zone270Score = v.NextInt(),
            TimeEffectTime = v.NextInt(),
            StateTimeEffect = v.NextInt(),
            SelectTimeEffectType = v.NextInt(),
            InvenSocket = v.NextIntArray(384),
            EquipSocket = v.NextIntArray(39),
            TradeSocket = v.NextIntArray(24),
            StoreSocket = v.NextIntArray(168),
            SaveSocket = v.NextIntArray(84),
            AutoTime = v.NextInt(),
            AutoTime2 = v.NextInt(),
            AutoState = v.NextInt(),
            AutoHunt = autoHunt,
            BigMoney = v.NextInt(),
            BigTradeMoney = v.NextInt(),
            BigStoreMoney = v.NextInt(),
            BigSaveMoney = v.NextInt(),
            EatElePotion = v.NextInt(),
            Zone050Time = v.NextInt(),
            ProtectForRefine = v.NextInt(),
            GoldenLakeTime = v.NextInt(),
            PreventForSmelt = v.NextInt(),
            BloodCoin = v.NextInt(),
            HeavenlyTicket = v.NextInt(),
            Tevushi = v.NextInt(),
            MammothRoad = v.NextInt(),
            Zone050Time2 = v.NextInt(),
            FishingZoneDate = v.NextInt(),
            ProxyShopDate = v.NextInt(),
            B4GHPElixir = v.NextInt(),
            B4GMPElixir = v.NextInt(),
            B4GStrElixir = v.NextInt(),
            B4GDexElixir = v.NextInt(),
            KillCount = v.NextInt(),
            KillCountTime = v.NextInt(),
            RankPoint = v.NextInt(),
            RankPointDate = v.NextInt(),
            RankBuffType = v.NextInt(),
            TribeNotifyNum = v.NextInt(),
            SpeakerCount = v.NextInt(),
            AnimalExpActivity = v.NextIntArray(10),
            AnimalPower = v.NextIntArray(10),
            AnimalDoubleExp = v.NextInt(),
            EventValue1 = v.NextInt(),
            EventValue2 = v.NextInt(),
            EventValue3 = v.NextInt(),
            EventValue4 = v.NextInt(),
            AnimalAbsorbTime = v.NextInt(),
            AnimalAbsorbState = v.NextInt(),
            CapsuleCashPoint = v.NextInt(),
            RageGauge = v.NextInt(),
            RageBuffTime = v.NextInt(),
            RageBuffState = v.NextInt(),
            RageBuffPotion = v.NextInt(),
            RageDoubleEffect = v.NextInt(),
            WarriorScroll = v.NextInt(),
            UseItemMonth = v.NextInt(),
            UseItemOnce = v.NextInt(),
            HeroPoint = v.NextInt(),
            HeroPointDate = v.NextInt(),
            KillMonsterNumForHeroPoint = v.NextInt(),
            WarriorPill = v.NextInt(),
            OllehEventPoint = v.NextInt(),
            ProtectForWing = v.NextInt(),
            TeacherPointDate = v.NextInt(),
            ProtectForDestroy2 = v.NextInt(),
            PetBagDate = v.NextInt(),
            PetBag = v.NextIntArray(20),
            MissionDate = missionDate,
            CriticalSpeedTime = v.NextInt(),
            DefenceUpNum = v.NextInt(),
            Zone038Ticket = v.NextInt(),
            HyunKyungMonster = v.NextInt(),
            Bottle = v.NextIntArray(10),
            BottleCount = v.NextIntArray(10),
            BottleIndex = v.NextInt(),
            BottleTime = v.NextInt(),
            MixSkill = v.NextIntArray(28),
            MixSkillBuffTime = v.NextInt(),
            BywonjiTime = v.NextInt(),
            UniqueSkill = v.NextInt(),
            UniqueSkillBuffTime = v.NextInt(),
            BackSoul = v.NextInt(),
            Premium = premium,
            ProtectForCombine = v.NextInt(),
            PlayOnlineTime = v.NextInt(),
            PlayOnlineTime2 = v.NextInt(),
            ProtectForCostume = v.NextInt(),
            WarPoint = v.NextInt(),
            PopUpKillAvt = v.NextInt(),
            PopUpKillMonster = v.NextInt(),
            PopUpKillAvtWar = v.NextInt(),
            StellarCore = v.NextIntArray(10),
            StellarCoreExpireDate = v.NextIntArray(10),
            StellarCoreIndex = v.NextInt(),
            InvenExpireDate = v.NextIntArray(128),
            EquipExpireDate = v.NextIntArray(13),
            TradeExpireDate = v.NextIntArray(8),
            StoreExpireDate = v.NextIntArray(56),
            SaveExpireDate = v.NextIntArray(28),
            WarKillCount = v.NextInt(),
            HSBStoneRewardCheck = v.NextInt(),
            GBox2249 = v.NextInt(),
            GBox8114 = v.NextInt(),
            GBox8115 = v.NextInt(),
            GBox8111 = v.NextInt(),
            RuneSystem = v.NextIntArray(4),
            RuneSystemStat = v.NextIntArray(4)
        };

        var buffer = new byte[AvatarInfo.WireSize];
        var written = avatar.Write(buffer);
        Assert.Equal(AvatarInfo.WireSize, written);

        Assert.True(AvatarInfo.TryRead(buffer, out var roundTripped));
        StructuralAssert.DeepEqual(avatar, roundTripped);

        // Re-checked individually: these fields sit next to padding (offset 10056), most exposed to an offset bug.
        Assert.Equal(name, roundTripped.Name);
        Assert.Equal(tribe, roundTripped.Tribe);
        Assert.Equal(guildName, roundTripped.GuildName);
        Assert.Equal(callName, roundTripped.CallName);
        Assert.Equal(partyName, roundTripped.PartyName);
        Assert.Equal(premium, roundTripped.Premium);
    }

    // Offsets independently computed from the [FixedString]/[Reserved]/[FixedArray] layout in AvatarInfo.cs,
    // spanning the Name/Tribe padding boundary (offset 33-35), the Premium long-alignment boundary (offset
    // 10056) and the trailing RuneSystemStat array (tail ends exactly at WireSize).
    [Fact]
    public void TryRead_DecodesGoldenBytes()
    {
        var buffer = new byte[AvatarInfo.WireSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(0, 4), 11);
        Encoding.Latin1.GetBytes("Hero1", buffer.AsSpan(20, 13));
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(36, 4), 22);
        Encoding.Latin1.GetBytes("Cal1", buffer.AsSpan(5452, 5));
        BinaryPrimitives.WriteInt64LittleEndian(buffer.AsSpan(10056, 8), 123_456_789_012L);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(11152 + 3 * 4, 4), 33);

        var ok = AvatarInfo.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal(11, packet.VisibleState);
        Assert.Equal("Hero1", packet.Name);
        Assert.Equal(22, packet.Tribe);
        Assert.Equal("Cal1", packet.CallName);
        Assert.Equal(123_456_789_012L, packet.Premium);
        Assert.Equal(33, packet.RuneSystemStat[3]);
    }
}
