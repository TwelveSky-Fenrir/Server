using Fenrir.Domain.Login.Avatars;
using Fenrir.Protocol.Login;

namespace Fenrir.Application.Login;

public static class LoginTrain
{
    public const int AvatarSlotCount = 3;

    public const string FailurePinMask = "0000";

    public const string ExistingPinMask = "****";

    private static readonly WorldRecommendationResponse RecommandWorld = new()
        { AddKillOtherTribe0 = 0, AddKillOtherTribe1 = 0, AddKillOtherTribe2 = 0 };

    private static readonly WorldRecommendationFinalResponse RecommandWorld2 = new()
        { AddKillOtherTribe0 = 0, AddKillOtherTribe1 = 0, AddKillOtherTribe2 = 0 };

    private static readonly AvatarRosterResponse EmptyAvatarSlot = new()
    {
        VisibleState = 0,
        SpecialState = 0,
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

    private static int[] BuildLogoutInfoArray(CharacterRosterDto character)
    {
        var (life, mana) = AvatarVitalsFloor.Clamp(character.Life, character.Mana);
        var (mapId, posX, posY, posZ) = LogoutZoneSelfHeal.Apply(character.Tribe, character.MapId, character.PosX,
            character.PosY, character.PosZ);

        return
        [
            mapId,
            (int)posX,
            (int)posY,
            (int)posZ,
            life,
            mana
        ];
    }

    private static string[] BuildFriendArray(IReadOnlyDictionary<byte, string> friendNameBySlot)
    {
        var friends = new string[10];
        Array.Fill(friends, "");

        foreach (var (slot, name) in friendNameBySlot)
            if (slot < 10)
                friends[slot] = name;

        return friends;
    }

    public static AvatarRosterResponse[] BuildEmptyAvatarSlots()
    {
        return [EmptyAvatarSlot, EmptyAvatarSlot, EmptyAvatarSlot];
    }

    public static void Send(IPacketSession session, in LoginResponse loginRecv, AvatarRosterResponse[] avatarSlots)
    {
        session.Send(loginRecv);
        foreach (var slot in avatarSlots)
            session.Send(slot);

        session.Send(RecommandWorld);
        session.Send(RecommandWorld2);
    }

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
