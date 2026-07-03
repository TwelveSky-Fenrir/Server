using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Login;
using Fenrir.Data.Characters;

namespace Fenrir.Application.Login;

/// <summary>
///     The legacy <c>SEND_LOGIN</c> train (S04_MyWork02.cpp l.42-67), byte-order faithful: every CL_LOGIN_SEND —
///     success AND failure — is answered by exactly <c>LC_LOGIN_RECV</c> + 3× <c>LC_USER_AVATAR_RECV2</c> (one per
///     slot, zeroed when empty) + <c>LC_RECOMMAND_WORLD_RECV</c> (24) + <c>LC_RECOMMAND_WORLD2_RECV</c> (26), in
///     that order. The single legacy exception (playuser result 4 with a local session found: kick + <c>return</c>
///     with no reply at all) maps to Fenrir's SessionRegistry eviction, see <c>ClLoginSendHandler</c>.
///     Static and side-effect-free apart from <see cref="Send" /> so the exact wire sequence is unit-testable
///     without a database (Tests/Fenrir.Application.Login.Tests/LoginTrainTests).
/// </summary>
public static class LoginTrain
{
    /// <summary>MAX_USER_AVATAR_NUM.</summary>
    public const int AvatarSlotCount = 3;

    /// <summary>Legacy <c>c0000</c>: the tMousePassword echoed on every failed login (report §4.11.9).</summary>
    public const string FailurePinMask = "0000";

    /// <summary>Legacy mask when the account already has a PIN (S04_MyWork02.cpp l.314-317): the real PIN never travels here.</summary>
    public const string ExistingPinMask = "****";

    // Locals of the legacy serializer that are never assigned -> always three zeros on the wire (report §5.24).
    private static readonly LcRecommandWorldRecv RecommandWorld = new()
        { AddKillOtherTribe0 = 0, AddKillOtherTribe1 = 0, AddKillOtherTribe2 = 0 };

    private static readonly LcRecommandWorld2Recv RecommandWorld2 = new()
        { AddKillOtherTribe0 = 0, AddKillOtherTribe1 = 0, AddKillOtherTribe2 = 0 };

    // Template for an empty character-select slot: every LC_USER_AVATAR_RECV2 field at its wire-zero value
    // (mirrors Avatars/AvatarInfoFactory.Zeroed's convention). A populated slot overrides only the handful of
    // DB-backed fields via a `with` expression, instead of repeating the other ~20 zeroed fields twice.
    private static readonly LcUserAvatarRecv2 EmptyAvatarSlot = new()
    {
        VisibleState = 0,
        SpecialState = 0,
        CostumeIndex = 0,
        Tribe = 0,
        PreviousTribe = 0,
        EatLifePotion = 0,
        Gender = 0,
        HeadType = 0,
        FaceType = 0,
        EatStrPotion = 0,
        Level1 = 0,
        Inventory = new int[768],
        Level2 = 0,
        EatManaPotion = 0,
        Halo = 0,
        RebirthNum = 0,
        KillOtherTribe = 0,
        SkillPoint = 0,
        Equip = new int[52],
        EatDexPotion = 0,
        Name = "",
        EatElePotion = 0,
        LogoutInfo = new int[6],
        GuildName = "",
        StoreItem = new int[224],
        PetBag = new int[20],
        Friend = ZeroStrings(10),
        Teacher = "",
        Student = "",
        Costume = new int[10]
    };

    /// <summary>
    ///     Every LC_LOGIN_RECV field at its "nothing to report" value. The legacy always-zero fields
    ///     (tGoodFellow/tLoginPlace/tLoginPremium/tSecretCard*/tGiftInfo, report §5.11) are pinned here in one place.
    /// </summary>
    public static LcLoginRecv BuildLoginRecv(int result, string id, int secondLoginSort, string mousePassword)
    {
        return new LcLoginRecv
        {
            Result = result,
            Id = id,
            UserSort = 0,
            GoodFellow = 0,
            LoginPlace = 0,
            LoginPremium = 0,
            SecondLoginSort = secondLoginSort,
            MousePassword = mousePassword,
            SecretCardIndex01 = 0,
            SecretCardIndex02 = 0,
            GiftInfo = new int[50],
            ResultString = ""
        };
    }

    /// <summary>
    ///     Exactly <see cref="AvatarSlotCount" /> entries, always in slot order, zeroed for an empty slot —
    ///     never fewer (report §4.11.9: the legacy loops over MAX_USER_AVATAR_NUM unconditionally).
    /// </summary>
    public static LcUserAvatarRecv2[] BuildAvatarSlots(IReadOnlyCollection<CharacterSummaryDto> characters)
    {
        var slots = new LcUserAvatarRecv2[AvatarSlotCount];
        for (var slot = 0; slot < AvatarSlotCount; slot++)
        {
            var character = characters.FirstOrDefault(c => c.Slot == slot);
            slots[slot] = character is null
                ? EmptyAvatarSlot
                : EmptyAvatarSlot with
                {
                    Tribe = character.Tribe,
                    Gender = character.Gender,
                    HeadType = character.HeadType,
                    FaceType = character.FaceType,
                    Level1 = character.Level,
                    Name = character.Name
                };
        }

        return slots;
    }

    /// <summary>The failure train's avatar block: 3 zeroed slots (the legacy mAvatarInfo is still zero pre-login).</summary>
    public static LcUserAvatarRecv2[] BuildEmptyAvatarSlots()
    {
        return [EmptyAvatarSlot, EmptyAvatarSlot, EmptyAvatarSlot];
    }

    /// <summary>Puts the full 6-packet train on the wire in the legacy SEND_LOGIN order.</summary>
    public static void Send(IPacketSession session, in LcLoginRecv loginRecv, LcUserAvatarRecv2[] avatarSlots)
    {
        session.Send(loginRecv);
        foreach (var slot in avatarSlots)
            session.Send(slot);

        session.Send(RecommandWorld);
        session.Send(RecommandWorld2);
    }

    /// <summary>
    ///     The complete failure train: result code, the client's own tID echoed back (the legacy XORs it via
    ///     USE_XOR_UID even on failure — our [LegacyUidField] does the same at write time), sort=0, loginSort=0,
    ///     PIN mask "0000", three zeroed avatar slots, then 24/26 (report §4.11.9 "DO_SEND").
    /// </summary>
    public static void SendFailure(IPacketSession session, int result, string requestId)
    {
        Send(session, BuildLoginRecv(result, requestId, 0, FailurePinMask), BuildEmptyAvatarSlots());
    }

    private static string[] ZeroStrings(int count)
    {
        var result = new string[count];
        for (var i = 0; i < count; i++)
            result[i] = "";

        return result;
    }
}
