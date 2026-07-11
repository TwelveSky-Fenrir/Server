using Fenrir.Application.Login.Domain;
using Fenrir.Application.Login.Domain.Avatars;
using Fenrir.Data.Abstractions.Characters;

namespace Fenrir.Application.Login.Tests;

public class LoginTrainLogoutInfoTests
{
    [Fact]
    public void OccupiedSlot_PopulatesLogoutInfoFromPersistedPlacement_ZonePositionLifeMana()
    {
        var entry = EntryFor(RosterCharacter(mapId: 101, posX: 1500f, posY: -200f, posZ: 30f, life: 850,
            mana: 320));

        var slots = LoginTrain.BuildAvatarSlots([entry]);

        Assert.Equal(6, slots[0].LogoutInfo.Length);
        Assert.Equal(new[] { 101, 1500, -200, 30, 850, 320 }, slots[0].LogoutInfo);
    }

    [Fact]
    public void OccupiedSlot_TruncatesFractionalPositionTowardZero()
    {
        var entry = EntryFor(RosterCharacter(mapId: 3, posX: 6.9f, posY: 0.4f, posZ: -7.9f, life: 100,
            mana: 50));

        var slots = LoginTrain.BuildAvatarSlots([entry]);

        Assert.Equal(3, slots[0].LogoutInfo[0]);
        Assert.Equal(6, slots[0].LogoutInfo[1]);
        Assert.Equal(0, slots[0].LogoutInfo[2]);
        Assert.Equal(-7, slots[0].LogoutInfo[3]);
    }

    [Fact]
    public void OccupiedSlot_AppliesLoginTailVitalsFloor_LifeFlooredToOne_ManaFlooredToZero()
    {
        var entry = EntryFor(RosterCharacter(mapId: 5, posX: 0f, posY: 0f, posZ: 0f, life: 0, mana: -5));

        var slots = LoginTrain.BuildAvatarSlots([entry]);

        Assert.Equal(1, slots[0].LogoutInfo[4]);
        Assert.Equal(0, slots[0].LogoutInfo[5]);
    }

    [Fact]
    public void OccupiedSlot_HealthyVitals_AreCarriedThroughUnchangedByTheFloor()
    {
        var entry = EntryFor(RosterCharacter(mapId: 5, posX: 0f, posY: 0f, posZ: 0f, life: 850, mana: 320));

        var slots = LoginTrain.BuildAvatarSlots([entry]);

        Assert.Equal(850, slots[0].LogoutInfo[4]);
        Assert.Equal(320, slots[0].LogoutInfo[5]);
    }

    [Fact]
    public void EmptySlot_LeavesLogoutInfoAtTheAllZeroTemplate()
    {
        var slots = LoginTrain.BuildAvatarSlots([EntryFor(RosterCharacter(mapId: 7, posX: 1f, posY: 2f,
            posZ: 3f, life: 10, mana: 20, slot: 0))]);

        Assert.Equal(6, slots[1].LogoutInfo.Length);
        Assert.All(slots[1].LogoutInfo, value => Assert.Equal(0, value));
    }

    private static AvatarRosterEntry EntryFor(CharacterRosterDto character)
    {
        return new AvatarRosterEntry(character, [], "", new Dictionary<byte, string>(), "", "");
    }

    private static CharacterRosterDto RosterCharacter(short mapId, float posX, float posY, float posZ, int life,
        int mana, byte slot = 0)
    {
        return new CharacterRosterDto(
            1000, slot, "Hero", 0, 0, 1, 2, 1, 12, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, mapId, posX, posY, posZ, life, mana);
    }
}
