using Fenrir.Application.Login.Domain;
using Fenrir.Application.Login.Domain.Avatars;
using Fenrir.Data.Abstractions.Characters;

namespace Fenrir.Application.Login.Tests;

// D1 finding close ("UPDATE_LOGOUT_INFO[6] is never populated/restored on reconnect"): the character-select
// roster (LC_USER_AVATAR_RECV2) must carry each occupied slot's UPDATE_LOGOUT_INFO[6] wire array
// (Server/Header/Protocol/DEFINE.h:750-757: [0] = zone/map, [1..3] = position, [4] = life, [5] = mana),
// sourced from the character's own persisted placement (usp_Character_GetAccountRoster's raw
// MapId/PosX/PosY/PosZ/Life/Mana), with the legacy LOGIN_SEND-tail low-word vitals floor applied
// (Server/ts25login/S04_MyWork02.cpp:357-358) and the position floats truncated -- and NOT left at the
// all-zero EmptyAvatarSlot template it used to inherit. The tribe-consistency position reset
// (S04_MyWork02.cpp:330-356) stays deferred (its four fixed town-spawn value sets were never enumerated by
// the D1 contract), so the raw persisted position is carried through.
public class LoginTrainLogoutInfoTests
{
    [Fact]
    public void OccupiedSlot_PopulatesLogoutInfoFromPersistedPlacement_ZonePositionLifeMana()
    {
        // Same values AvatarInfoFactoryTests already asserts for the create-response AVATAR_INFO's identical
        // six-element array, so both LogoutInfo-carrying wire structs project one character's placement the
        // same way.
        var entry = EntryFor(RosterCharacter(mapId: 101, posX: 1500f, posY: -200f, posZ: 30f, life: 850,
            mana: 320));

        var slots = LoginTrain.BuildAvatarSlots([entry]);

        Assert.Equal(6, slots[0].LogoutInfo.Length);
        Assert.Equal(new[] { 101, 1500, -200, 30, 850, 320 }, slots[0].LogoutInfo);
    }

    [Fact]
    public void OccupiedSlot_TruncatesFractionalPositionTowardZero()
    {
        // (int) truncates toward zero, matching the legacy capture and AvatarInfoFactory.CreateForCharacter:
        // 6.9 -> 6, 0.4 -> 0, -7.9 -> -7 (NOT -8).
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
        // Legacy LOGIN_SEND-tail SetIntegerLow floor (Server/ts25login/S04_MyWork02.cpp:357-358, via
        // AvatarVitalsFloor): a downed character persisted at 0 life / negative mana still shows life >= 1 and
        // mana >= 0 on the character-select roster.
        var entry = EntryFor(RosterCharacter(mapId: 5, posX: 0f, posY: 0f, posZ: 0f, life: 0, mana: -5));

        var slots = LoginTrain.BuildAvatarSlots([entry]);

        Assert.Equal(1, slots[0].LogoutInfo[4]);
        Assert.Equal(0, slots[0].LogoutInfo[5]);
    }

    [Fact]
    public void OccupiedSlot_HealthyVitals_AreCarriedThroughUnchangedByTheFloor()
    {
        // The floor is a no-op for any character above its thresholds -- it must never clamp a live value down.
        var entry = EntryFor(RosterCharacter(mapId: 5, posX: 0f, posY: 0f, posZ: 0f, life: 850, mana: 320));

        var slots = LoginTrain.BuildAvatarSlots([entry]);

        Assert.Equal(850, slots[0].LogoutInfo[4]);
        Assert.Equal(320, slots[0].LogoutInfo[5]);
    }

    [Fact]
    public void EmptySlot_LeavesLogoutInfoAtTheAllZeroTemplate()
    {
        // A slot with no character in it at all stays at the EmptyAvatarSlot template (all six zero) -- the
        // population only overlays occupied slots.
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
