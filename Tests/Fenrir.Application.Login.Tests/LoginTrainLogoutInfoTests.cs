using Fenrir.Application.Login.Domain;
using Fenrir.Application.Login.Domain.Avatars;
using Fenrir.Data.Abstractions.Characters;

namespace Fenrir.Application.Login.Tests;

public class LoginTrainLogoutInfoTests
{
    [Fact]
    public void OccupiedSlot_PopulatesLogoutInfoFromPersistedPlacement_ZonePositionLifeMana()
    {
        var entry = EntryFor(RosterCharacter(2, 1500f, -200f, 30f, 850,
            320));

        var slots = LoginTrain.BuildAvatarSlots([entry]);

        Assert.Equal(6, slots[0].LogoutInfo.Length);
        Assert.Equal(new[] { 2, 1500, -200, 30, 850, 320 }, slots[0].LogoutInfo);
    }

    [Fact]
    public void OccupiedSlot_PersistedZoneNotOwnedByOwnTribe_SelfHealsToHometown()
    {
        var entry = EntryFor(RosterCharacter(101, 1500f, -200f, 30f, 850, 320));

        var slots = LoginTrain.BuildAvatarSlots([entry]);

        Assert.Equal(new[] { 1, 6, 0, -7, 850, 320 }, slots[0].LogoutInfo);
    }

    [Fact]
    public void OccupiedSlot_PersistedZoneOutOfRange_SelfHealsToHometown()
    {
        var entry = EntryFor(RosterCharacter(500, 1500f, -200f, 30f, 850, 320));

        var slots = LoginTrain.BuildAvatarSlots([entry]);

        Assert.Equal(new[] { 1, 6, 0, -7, 850, 320 }, slots[0].LogoutInfo);
    }

    [Fact]
    public void OccupiedSlot_TruncatesFractionalPositionTowardZero()
    {
        var entry = EntryFor(RosterCharacter(3, 6.9f, 0.4f, -7.9f, 100,
            50));

        var slots = LoginTrain.BuildAvatarSlots([entry]);

        Assert.Equal(3, slots[0].LogoutInfo[0]);
        Assert.Equal(6, slots[0].LogoutInfo[1]);
        Assert.Equal(0, slots[0].LogoutInfo[2]);
        Assert.Equal(-7, slots[0].LogoutInfo[3]);
    }

    [Fact]
    public void OccupiedSlot_AppliesLoginTailVitalsFloor_LifeFlooredToOne_ManaFlooredToZero()
    {
        var entry = EntryFor(RosterCharacter(5, 0f, 0f, 0f, 0, -5));

        var slots = LoginTrain.BuildAvatarSlots([entry]);

        Assert.Equal(1, slots[0].LogoutInfo[4]);
        Assert.Equal(0, slots[0].LogoutInfo[5]);
    }

    [Fact]
    public void OccupiedSlot_HealthyVitals_AreCarriedThroughUnchangedByTheFloor()
    {
        var entry = EntryFor(RosterCharacter(5, 0f, 0f, 0f, 850, 320));

        var slots = LoginTrain.BuildAvatarSlots([entry]);

        Assert.Equal(850, slots[0].LogoutInfo[4]);
        Assert.Equal(320, slots[0].LogoutInfo[5]);
    }

    [Fact]
    public void EmptySlot_LeavesLogoutInfoAtTheAllZeroTemplate()
    {
        var slots = LoginTrain.BuildAvatarSlots([
            EntryFor(RosterCharacter(7, 1f, 2f,
                3f, 10, 20, 0))
        ]);

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
