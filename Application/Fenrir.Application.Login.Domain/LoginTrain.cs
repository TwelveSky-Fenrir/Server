using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Login;

namespace Fenrir.Application.Login.Domain;

/// <summary>
///     Legacy SEND_LOGIN train (S04_MyWork02.cpp l.42-67): every CL_LOGIN_SEND, success or failure, gets
///     LC_LOGIN_RECV + 3x LC_USER_AVATAR_RECV2 + ops 24 + 26, in that order.
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
    private static readonly WorldRecommendationResponse RecommandWorld = new()
        { AddKillOtherTribe0 = 0, AddKillOtherTribe1 = 0, AddKillOtherTribe2 = 0 };

    private static readonly WorldRecommendationFinalResponse RecommandWorld2 = new()
        { AddKillOtherTribe0 = 0, AddKillOtherTribe1 = 0, AddKillOtherTribe2 = 0 };

    // Wire-zero template for an empty character-select slot; a populated slot overrides only the DB-backed fields via `with`.
    private static readonly AvatarRosterResponse EmptyAvatarSlot = new()
    {
        VisibleState = 0,
        SpecialState = 0,
        // -1, not 0: legacy's aCostumeIndex is DB-backed (Server/Header/CSQLAvatar.cpp:661's FIELD_AVATAR0(
        // aCostumeIndex) round-trips it verbatim on every avatar load) and defaults to -1 both at creation
        // (Server/ts25login/S04_MyWork02.cpp:1099, never overwritten again in that function -- see
        // AvatarInfoTemplates.Zeroed's own remarks for the full citation) and in the legacy DB schema itself
        // (Server/BuildEU33/DB/nxtserver.sql:128, `DEFAULT -1`). Fenrir doesn't persist a per-character
        // CostumeIndex at all (game.Characters has no such column -- no costume acquisition path exists yet),
        // so this wire-zero template value is the only value a real, populated roster slot ever shows too
        // (BuildAvatarSlots' `with` overlay below doesn't touch CostumeIndex either) -- 0 would misrepresent
        // "wardrobe slot 0 selected" per the -1/0-9/10-19 encoding CostumeStateResolver.Context documents.
        CostumeIndex = -1,
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

    /// <summary>Every LC_LOGIN_RECV field at its "nothing to report" value (legacy always-zero fields, report §5.11).</summary>
    public static LoginResponse BuildLoginRecv(int result, string id, int secondLoginSort, string mousePassword,
        string resultString = "")
    {
        return new LoginResponse
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
            ResultString = resultString
        };
    }

    /// <summary>
    ///     Exactly <see cref="AvatarSlotCount" /> entries, always in slot order, zeroed for an empty slot —
    ///     never fewer (report §4.11.9: the legacy loops over MAX_USER_AVATAR_NUM unconditionally).
    /// </summary>
    /// <param name="characters">The account's character-select rows, in any order.</param>
    /// <param name="guildNamesByCharacterId">
    ///     Live guild-membership lookup keyed by CharacterId, resolved by the caller (LoginService, which alone
    ///     has access to IGuildRepository — this Domain project stays I/O-free). A character absent from this
    ///     lookup (or an entirely <see langword="null" /> lookup, e.g. every existing pre-guild caller/test) gets
    ///     the same "" GuildName the wire-zero template already carries.
    /// </param>
    /// <remarks>
    ///     Réf. C++ : Server/ts25login/S08_MyDB.cpp:638-671 (the legacy guild lookup this reproduces) ;
    ///     Server/Header/Protocol/LOGIN.h:134-174 (LC_USER_AVATAR_RECV2 — aGuildName is the only guild-related
    ///     field this wire struct carries). Legacy caches aGuildName on the character row once at account-login
    ///     time and self-heals it against the live guild table on mismatch/not-found (S08_MyDB.cpp:638-671);
    ///     Fenrir's normalized schema has no such cached column at all (guild membership lives only in
    ///     game.GuildMembers, resolved live via <c>IGuildRepository.GetByCharacterAsync</c>), so a straight live
    ///     lookup naturally reproduces the correct end state without needing to replicate that two-step
    ///     cache-then-validate mechanic — a deliberate simplification, not an assumed equivalence.
    /// </remarks>
    public static AvatarRosterResponse[] BuildAvatarSlots(IReadOnlyCollection<CharacterSummaryDto> characters,
        IReadOnlyDictionary<int, string>? guildNamesByCharacterId = null)
    {
        var slots = new AvatarRosterResponse[AvatarSlotCount];
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
                    Name = character.Name,
                    GuildName = guildNamesByCharacterId?.GetValueOrDefault(character.CharacterId) ?? ""
                };
        }

        return slots;
    }

    /// <summary>The failure train's avatar block: 3 zeroed slots (the legacy mAvatarInfo is still zero pre-login).</summary>
    public static AvatarRosterResponse[] BuildEmptyAvatarSlots()
    {
        return [EmptyAvatarSlot, EmptyAvatarSlot, EmptyAvatarSlot];
    }

    /// <summary>Puts the full 6-packet train on the wire in the legacy SEND_LOGIN order.</summary>
    public static void Send(IPacketSession session, in LoginResponse loginRecv, AvatarRosterResponse[] avatarSlots)
    {
        session.Send(loginRecv);
        foreach (var slot in avatarSlots)
            session.Send(slot);

        session.Send(RecommandWorld);
        session.Send(RecommandWorld2);
    }

    /// <summary>
    ///     The complete failure train: tID echoed back even on failure (legacy XORs it via USE_XOR_UID;
    ///     [ObfuscatedUidField] does the same here).
    /// </summary>
    public static void SendFailure(IPacketSession session, int result, string requestId, string resultString = "")
    {
        Send(session, BuildLoginRecv(result, requestId, 0, FailurePinMask, resultString), BuildEmptyAvatarSlots());
    }

    private static string[] ZeroStrings(int count)
    {
        var result = new string[count];
        for (var i = 0; i < count; i++)
            result[i] = "";

        return result;
    }
}
