using Fenrir.Application.Login.Avatars;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Data.Characters;

namespace Fenrir.Application.Login.Tests.Avatars;

public class AvatarInfoFactoryTests
{
    [Fact]
    public void Zeroed_RoundTripsThroughWireFormat()
    {
        var avatar = AvatarInfoFactory.Zeroed;

        // Pin every [FixedArray] length to its declared attribute value (AvatarInfo.cs), one property at a time.
        // Write()/TryRead() alone only catch an OVERSIZED array (IndexOutOfRangeException while slicing the
        // fixed-offset destination); an UNDERSIZED one silently leaves trailing bytes as the buffer's pre-existing
        // zeros and round-trips fine byte-for-byte (Zeroed is all zero anyway), so it would never throw. These
        // explicit length assertions are what actually proves all ~30 arrays are sized correctly.
        Assert.Equal(52, avatar.Equip.Length);
        Assert.Equal(768, avatar.Inventory.Length);
        Assert.Equal(32, avatar.Trade.Length);
        Assert.Equal(224, avatar.StoreItem.Length);
        Assert.Equal(80, avatar.Skill.Length);
        Assert.Equal(126, avatar.HotKey.Length);
        Assert.Equal(5, avatar.QuestInfo.Length);
        Assert.Equal(10, avatar.Friend.Length);
        Assert.Equal(6, avatar.LogoutInfo.Length);
        Assert.Equal(10, avatar.Animal.Length);
        Assert.Equal(112, avatar.SaveItem.Length);
        Assert.Equal(5, avatar.PartyName.Length);
        Assert.Equal(10, avatar.Costume.Length);
        Assert.Equal(10, avatar.CostumeDate.Length);
        Assert.Equal(10, avatar.CostumeExpireDate.Length);
        Assert.Equal(16, avatar.AutoBuffSkill.Length);
        Assert.Equal(384, avatar.InvenSocket.Length);
        Assert.Equal(39, avatar.EquipSocket.Length);
        Assert.Equal(24, avatar.TradeSocket.Length);
        Assert.Equal(168, avatar.StoreSocket.Length);
        Assert.Equal(84, avatar.SaveSocket.Length);
        Assert.Equal(10, avatar.AnimalExpActivity.Length);
        Assert.Equal(10, avatar.AnimalPower.Length);
        Assert.Equal(20, avatar.PetBag.Length);
        Assert.Equal(10, avatar.Bottle.Length);
        Assert.Equal(10, avatar.BottleCount.Length);
        Assert.Equal(28, avatar.MixSkill.Length);
        Assert.Equal(10, avatar.StellarCore.Length);
        Assert.Equal(10, avatar.StellarCoreExpireDate.Length);
        Assert.Equal(128, avatar.InvenExpireDate.Length);
        Assert.Equal(13, avatar.EquipExpireDate.Length);
        Assert.Equal(8, avatar.TradeExpireDate.Length);
        Assert.Equal(56, avatar.StoreExpireDate.Length);
        Assert.Equal(28, avatar.SaveExpireDate.Length);
        Assert.Equal(4, avatar.RuneSystem.Length);
        Assert.Equal(4, avatar.RuneSystemStat.Length);
        Assert.Equal(16, avatar.AutoHunt.BuffStore.Length);
        Assert.Equal(4, avatar.AutoHunt.AttackType.Length);

        // No entry may be null: WriteFixedStringRows indexes every slot of the array unconditionally.
        Assert.All(avatar.Friend, Assert.NotNull);
        Assert.All(avatar.PartyName, Assert.NotNull);

        var buffer = new byte[AvatarInfo.WireSize];
        var written = avatar.Write(buffer);

        // A wrong (oversized) array length throws IndexOutOfRangeException inside Write() before reaching here.
        Assert.Equal(AvatarInfo.WireSize, written);
        Assert.True(AvatarInfo.TryRead(buffer, out var read));

        // The round-tripped value must itself still be a fully well-formed AvatarInfo (re-serializes to the same
        // total size); this is only reachable if TryRead's own array reconstruction didn't already blow up.
        Assert.Equal(AvatarInfo.WireSize, read.Write(new byte[AvatarInfo.WireSize]));
    }

    [Fact]
    public void CreateForCharacter_PopulatesIdentityFields()
    {
        var character = new CharacterWorldEntryDto(
            42,
            7,
            1,
            "Fenrir",
            2,
            1,
            3,
            4,
            12,
            101,
            1500.5f,
            -200.25f,
            30f,
            90f,
            850,
            1000,
            320,
            400,
            99L);

        var avatar = AvatarInfoFactory.CreateForCharacter(character);

        Assert.Equal("Fenrir", avatar.Name);
        Assert.Equal(2, avatar.Tribe);
        Assert.Equal(1, avatar.Gender);
        Assert.Equal(3, avatar.HeadType);
        Assert.Equal(4, avatar.FaceType);
        Assert.Equal(12, avatar.Level1);
        Assert.Equal(new[] { 101, 1500, -200, 30, 850, 320 }, avatar.LogoutInfo);

        // A field NOT covered by CreateForCharacter must stay at Zeroed's value -- proof the `with` expression
        // started from the shared Zeroed baseline rather than a freshly (half-)initialized AvatarInfo.
        Assert.Equal(0, avatar.Money);
    }
}
