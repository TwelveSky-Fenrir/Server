using Fenrir.Application.Login.Domain.Avatars;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Login.Packets.Login;

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
    /// <param name="characters">
    ///     The account's resolved roster entries (occupied slots only, any order). Every field this response
    ///     carries beyond identity/appearance -- equipment, both inventory pages, both store pages,
    ///     progression scalars/potion counters, and guild/friend/teacher/student names -- is already resolved
    ///     onto each entry by the caller (LoginService, the only place with I/O access); this method stays a
    ///     pure projection, matching this Domain project's own "no I/O" rule.
    /// </param>
    /// <remarks>
    ///     Réf. C++ : Server/ts25login/S04_MyWork02.cpp:129-434 (CL_LOGIN_SEND as a whole; the per-slot loop at
    ///     324-419) ; Server/ts25login/S04_MyWork02.cpp:42-67 (SEND_LOGIN's unconditional 3-slot send) ;
    ///     Server/ts25login/S05_MyTransfer.cpp:133-169 (the exact field-by-field copy this response's own
    ///     population reproduces) ; Server/ts25login/S08_MyDB.cpp:563-671 (the per-slot persisted-data read
    ///     plus the live guild self-heal this method's <paramref name="characters" /> parameter is pre-resolved
    ///     from) ; Server/Header/Protocol/LOGIN.h:134-174 (this response's own field declaration, 19 scalars/5
    ///     strings/6 arrays, independently confirmed to already match <c>AvatarRosterResponse</c> one-for-one).
    ///     See the "Account-login avatar roster population" legacy-behavior-translator contract for the full
    ///     citation set (including <c>usp_Character_GetAccountRoster.sql</c>'s own header) this fix rests on.
    ///     <para>
    ///         Deliberately NOT overlaid here, per that contract's own "never invent legacy formulas from
    ///         memory" boundary: the logout-position tribe-consistency correction and the forced logout-flag
    ///         pair (the raw <c>LogoutInfo</c>[6] values -- zone/position plus the LOGIN_SEND-tail-floored
    ///         life/mana -- ARE now populated, by <see cref="BuildOccupiedSlot" />'s
    ///         <c>BuildLogoutInfoArray</c> from the character's own persisted placement; only the two ON-TOP
    ///         transforms stay deferred -- the four fixed tribe-specific town-spawn value sets behind
    ///         <c>Server/Header/mapcheck.h:298-326</c> that the tribe-mismatch position reset needs were never
    ///         enumerated by the contract, only cited by location), and the two hardcoded blacklisted-item-id
    ///         resets (their actual item ids were likewise never established). <c>VisibleState</c>/<c>SpecialState</c>/
    ///         <c>CostumeIndex</c>/<c>PetBag</c>/<c>Costume</c> stay at the wire-zero/-1 template too -- no
    ///         persisted storage exists anywhere in this schema for any of the five yet (see
    ///         <c>usp_Character_GetAccountRoster.sql</c>'s own header for the same admission on the read side).
    ///         Closing any of these needs its own follow-up legacy-behavior-translator pass, not a guess here.
    ///     </para>
    /// </remarks>
    public static AvatarRosterResponse[] BuildAvatarSlots(IReadOnlyCollection<AvatarRosterEntry> characters)
    {
        var slots = new AvatarRosterResponse[AvatarSlotCount];
        for (var slot = 0; slot < AvatarSlotCount; slot++)
        {
            var entry = characters.FirstOrDefault(c => c.Character.Slot == slot);
            slots[slot] = entry is null ? EmptyAvatarSlot : BuildOccupiedSlot(entry);
        }

        return slots;
    }

    private static AvatarRosterResponse BuildOccupiedSlot(AvatarRosterEntry entry)
    {
        var character = entry.Character;

        return EmptyAvatarSlot with
        {
            Tribe = character.Tribe,
            PreviousTribe = character.PreviousTribe,
            Gender = character.Gender,
            HeadType = character.HeadType,
            FaceType = character.FaceType,
            Level1 = character.Level,
            Level2 = character.Level2,
            Halo = character.Halo,
            RebirthNum = character.RebirthCount,
            KillOtherTribe = character.ContributionPoints,
            SkillPoint = character.SkillPoints,
            EatLifePotion = character.EatLifePotion,
            EatManaPotion = character.EatManaPotion,
            EatStrPotion = character.EatStrPotion,
            EatDexPotion = character.EatDexPotion,
            EatElePotion = character.EatElePotion,
            Name = character.Name,
            GuildName = entry.GuildName,
            Teacher = entry.Teacher,
            Student = entry.Student,
            Friend = BuildFriendArray(entry.FriendNameBySlot),
            Equip = AvatarInfoFactory.BuildEquipArrayFromRosterItems(entry.Items, character.PetGrowth,
                character.PetActivity),
            Inventory = AvatarInfoFactory.BuildInventoryArrayFromRosterItems(entry.Items),
            StoreItem = AvatarInfoFactory.BuildStoreItemArrayFromRosterItems(entry.Items),
            LogoutInfo = BuildLogoutInfoArray(character)
        };
    }

    /// <summary>
    ///     The <c>UPDATE_LOGOUT_INFO[6]</c> wire array (Server/Header/Protocol/DEFINE.h:750-757: [0] =
    ///     zone/map number, [1..3] = position, [4] = life, [5] = mana) for a returning character's
    ///     character-select slot -- Fenrir's close of the D1 finding "UPDATE_LOGOUT_INFO[6] is never
    ///     populated/restored on reconnect". Sourced from the character's own persisted placement
    ///     (<c>usp_Character_GetAccountRoster</c>'s raw <c>MapId/PosX/PosY/PosZ/Life/Mana</c>, i.e. the
    ///     continuously write-behind-flushed authoritative values <c>game.Characters</c> already holds --
    ///     the most-current "last-session" placement, not a separately-captured snapshot), no longer left at
    ///     the all-zero <see cref="EmptyAvatarSlot" /> template.
    /// </summary>
    /// <remarks>
    ///     The two vitals get the legacy LOGIN_SEND-tail low-word floor (Server/ts25login/S04_MyWork02.cpp:
    ///     357-358, via <see cref="AvatarVitalsFloor" />: life &gt;= 1, mana's low word &gt;= 0) -- the same
    ///     correction the legacy login train applies to every occupied avatar slot. The three position floats
    ///     are truncated to whole numbers exactly as the legacy capture does, and exactly as this Domain's own
    ///     <see cref="AvatarInfoFactory.CreateForCharacter" /> already lays out the identical six-element array
    ///     for the create-response AVATAR_INFO. Deliberately NOT applied here (deferred, contract-blocked):
    ///     the logout-position tribe-consistency correction (Server/ts25login/S04_MyWork02.cpp:330-356 -- a
    ///     last-zone whose classification does not match the character's tribe resets [1..3] to that tribe's
    ///     born-in-town spawn), because the four fixed tribe-specific town-spawn value sets behind
    ///     Server/Header/mapcheck.h:298-326 were never enumerated by the D1 behavior contract, only cited by
    ///     location -- so the raw persisted position is carried through until a follow-up
    ///     legacy-behavior-translator pass establishes them (see <c>usp_Character_GetAccountRoster.sql</c>'s
    ///     own header: "returned raw in RS0 for the caller to apply both transforms to").
    /// </remarks>
    private static int[] BuildLogoutInfoArray(CharacterRosterDto character)
    {
        var (life, mana) = AvatarVitalsFloor.Clamp(character.Life, character.Mana);
        return
        [
            character.MapId,
            (int)character.PosX,
            (int)character.PosY,
            (int)character.PosZ,
            life,
            mana
        ];
    }

    /// <summary>Slots are client-chosen, so gaps in the sparse map are normal -- unfilled slots stay empty string.</summary>
    private static string[] BuildFriendArray(IReadOnlyDictionary<byte, string> friendNameBySlot)
    {
        var friends = new string[10];
        Array.Fill(friends, "");

        foreach (var (slot, name) in friendNameBySlot)
            if (slot < 10)
                friends[slot] = name;

        return friends;
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
