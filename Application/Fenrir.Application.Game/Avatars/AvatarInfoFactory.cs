using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.Social;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Data.Characters;

namespace Fenrir.Application.Game.Avatars;

/// <summary>Independent copy of Login's AvatarInfoFactory; projects a persisted character onto AVATAR_INFO for ZC_REGISTER_AVATAR_RECV.</summary>
public static class AvatarInfoFactory
{
    public static AvatarInfo CreateForCharacter(CharacterWorldSnapshotDto character,
        IReadOnlyList<CharacterItemSlotDto> items, AvatarSocialSnapshot? social = null)
    {
        var s = social ?? AvatarSocialSnapshot.Empty;

        return AvatarInfoTemplates.Zeroed with
        {
            Name = character.Name,
            Tribe = character.Tribe,
            Gender = character.Gender,
            HeadType = character.HeadType,
            FaceType = character.FaceType,
            Level1 = character.Level,
            Level2 = character.Level2,
            Vit = character.StatVit,
            Str = character.StatStr,
            Int = character.StatInt,
            Dex = character.StatDex,
            StatPoint = character.StatPoints,
            SkillPoint = character.SkillPoints,
            Money = (int)character.Money,
            InventoryDate = character.InventoryDate,
            StoreDate = character.StoreDate,
            StoreMoney = (int)character.StoreMoney,
            BigMoney = character.BigMoney,
            BigStoreMoney = character.BigStoreMoney,
            Title = character.Title,
            Halo = character.Halo,
            RebirthNum = character.RebirthCount,
            Equip = BuildEquipArrayFromRows(items),
            Friend = s.BuildFriendArray(),
            Teacher = s.Teacher,
            Student = s.Student,
            GuildName = s.GuildName,
            GuildRole = s.GuildRoleWire,
            CallName = s.CallName,
            QuestInfo =
            [
                character.QuestStepPermanent, character.QuestActiveId, character.QuestSort,
                character.QuestTargetPhase, character.QuestKillCounter
            ],
            LogoutInfo =
            [
                character.MapId,
                (int)character.PosX,
                (int)character.PosY,
                (int)character.PosZ,
                character.Life,
                character.Mana
            ]
        };
    }

    /// <summary>mapId/pos are the just-resolved destination -- <paramref name="state" /> itself still holds the source zone's position at call time.</summary>
    public static AvatarInfo CreateForRuntimeState(PlayerRuntimeState state, short mapId, float posX, float posY,
        float posZ)
    {
        return AvatarInfoTemplates.Zeroed with
        {
            Name = state.Name,
            Tribe = state.Tribe,
            Gender = state.Gender,
            HeadType = state.HeadType,
            FaceType = state.FaceType,
            Level1 = state.Level,
            Vit = state.StatVit,
            Str = state.StatStr,
            Int = state.StatInt,
            Dex = state.StatDex,
            StatPoint = state.StatPoints,
            SkillPoint = state.SkillPoints,
            Title = state.Title,
            Halo = state.Halo,
            RebirthNum = state.RebirthCount,
            GuildName = state.GuildName,
            GuildRole = GuildRoleCodec.DbRoleToWire(state.GuildRoleDb),
            CallName = state.GuildCallName,
            Equip = BuildEquipArrayFromContainer(state.Inventory.GetContainer(ContainerMatrix.Equipment)),
            QuestInfo =
            [
                state.QuestStepPermanent, state.QuestActiveFlag, state.QuestSort, state.QuestTargetPhase,
                state.QuestKillCounter
            ],
            LogoutInfo = [mapId, (int)posX, (int)posY, (int)posZ, state.Life, state.Mana]
        };
    }

    /// <summary><c>aEquip[13][4]</c>'s 4th int per slot is unknown/unmapped -- left at wire-zero rather than guessed.</summary>
    private static int[] BuildEquipArrayFromRows(IReadOnlyList<CharacterItemSlotDto> items)
    {
        var equip = new int[52];

        foreach (var item in items)
        {
            if (item.Container != ContainerMatrix.Equipment || item.Slot >= 13)
                continue;

            var baseIndex = item.Slot * 4;
            equip[baseIndex] = item.ItemId;
            equip[baseIndex + 1] = item.ExpireDate;
            equip[baseIndex + 2] = PackUpgradeBytes(item.Enchant, item.Combine, item.Refine, item.Socket);
        }

        return equip;
    }

    private static int[] BuildEquipArrayFromContainer(IReadOnlyDictionary<byte, ItemStack> equipmentContainer)
    {
        var equip = new int[52];

        foreach (var (slot, stack) in equipmentContainer)
        {
            if (slot >= 13)
                continue;

            var baseIndex = slot * 4;
            equip[baseIndex] = stack.ItemId;
            equip[baseIndex + 1] = stack.ExpireDate;
            equip[baseIndex + 2] = PackUpgradeBytes(stack.Enchant, stack.Combine, stack.Refine, stack.Socket);
        }

        return equip;
    }

    /// <summary>Bit pattern is identical whether read as signed or unsigned, so packing unsigned bytes reproduces the legacy's signed-char wire int.</summary>
    private static int PackUpgradeBytes(byte enchant, byte combine, byte refine, byte socket)
    {
        return enchant | (combine << 8) | (refine << 16) | (socket << 24);
    }
}

/// <summary>PartyName/DuelState aren't modeled here: neither party membership nor a duel can exist at login time, so both stay blank.</summary>
public sealed record AvatarSocialSnapshot(
    IReadOnlyDictionary<byte, string> FriendNameBySlot,
    string Teacher,
    string Student,
    string GuildName,
    int GuildRoleWire,
    string CallName = "")
{
    public static readonly AvatarSocialSnapshot Empty =
        new(new Dictionary<byte, string>(), "", "", "", 0);

    /// <summary>Slots are client-chosen, so gaps in the sparse map are normal -- unfilled slots stay empty string.</summary>
    public string[] BuildFriendArray()
    {
        var friends = new string[10];
        Array.Fill(friends, "");

        foreach (var (slot, name) in FriendNameBySlot)
            if (slot < 10)
                friends[slot] = name;

        return friends;
    }
}
