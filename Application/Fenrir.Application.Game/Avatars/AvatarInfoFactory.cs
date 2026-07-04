using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.Social;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Data.Characters;

namespace Fenrir.Application.Game.Avatars;

/// <summary>
///     GameServer's counterpart to <c>Fenrir.Application.Login.Avatars.AvatarInfoFactory</c> -- same mapping from a
///     persisted character onto the shared <see cref="AvatarInfoTemplates.Zeroed" /> template, kept as a small
///     independent copy rather than a cross-Application-project reference (architecture reference §3.3: each
///     executable's application layer is independent). Feeds ZC_REGISTER_AVATAR_RECV's AVATAR_INFO payload.
/// </summary>
public static class AvatarInfoFactory
{
    /// <summary>
    ///     Projects a persisted character (the A3-extended world-entry snapshot, RS0 of
    ///     usp_Character_GetForWorldEntry) plus its item rows (RS1) onto the wire struct for
    ///     ZC_REGISTER_AVATAR_RECV / world entry. Unlike the earlier M1-prefix
    ///     <see cref="CharacterWorldEntryDto" /> overload this replaces, this also carries progression
    ///     (stats/money/title/halo/rebirth) and the real Equipment container -- see
    ///     <see cref="BuildEquipArrayFromRows" />'s own remarks for exactly which of <c>AvatarInfo.Equip</c>'s
    ///     4 ints/slot are populated.
    /// </summary>
    /// <param name="social">
    ///     Phase C/V6 Social: the friend list / teacher-student bond / guild membership loaded alongside
    ///     the rest of the world-entry snapshot (<c>EnterWorldHandler</c>'s own remarks) -- null keeps
    ///     every one of those AVATAR_INFO fields at the shared zeroed template's blank default (a
    ///     guildless, friendless, un-bonded fresh character, the common case for a test/tool caller that
    ///     does not care about this facet).
    /// </param>
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
            // Server Logic V9 Progression: wAvatar.aQuestInfo[5] (report 04 §5) -- previously left at the
            // shared zeroed template's all-zero default (an open issue this pass closes).
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

    /// <summary>
    ///     Same projection, but from a LIVE <see cref="PlayerRuntimeState" /> instead of a fresh SQL read --
    ///     used mid-session (in-process zone transfer, <c>ZoneMoveHandler</c>) where the
    ///     source of truth is the zone's own in-memory state, and the destination map/position must be the
    ///     JUST-RESOLVED arrival point, not whatever is still on <paramref name="state" /> (still the source
    ///     zone's own position at the point this is called -- the transfer hasn't happened yet).
    /// </summary>
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

    /// <summary>
    ///     <c>AvatarInfo.Equip</c> (52 ints = <c>aEquip[13][4]</c>, report 11 §2): per occupied slot, ints
    ///     [0]=ItemId, [1]=ExpireDate ("durée/état" per the report), [2]=the packed IS/IU/IM/IZ upgrade bytes
    ///     (<see cref="PackUpgradeBytes" />). OPEN ISSUE: the 4th int's role (<c>aEquip[slot][3]</c>) was never
    ///     located in the source pass -- left at its wire-zero default rather than guessed.
    /// </summary>
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

    /// <summary>Same projection as <see cref="BuildEquipArrayFromRows" />, from the live in-memory Equipment container.</summary>
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

    /// <summary>
    ///     <c>SetISIUIMValue(IS,IU,IM,IZ)</c> (report 11 §2): 4 bytes packed little-endian into one int, each
    ///     read back as a signed char by the legacy -- the bit PATTERN is identical whether read as signed or
    ///     unsigned, so packing our unsigned TINYINT columns this way reproduces the same wire int.
    /// </summary>
    private static int PackUpgradeBytes(byte enchant, byte combine, byte refine, byte socket)
    {
        return enchant | (combine << 8) | (refine << 16) | (socket << 24);
    }
}

/// <summary>
///     The Phase C/V6 Social facet of AVATAR_INFO, loaded by <c>EnterWorldHandler</c> from
///     <c>FriendRepository</c>/<c>MentorRepository</c>/<c>GuildRepository</c> alongside the rest of the
///     world-entry snapshot. <see cref="AvatarInfo.PartyName" />/<see cref="AvatarInfo.DuelState" /> are
///     deliberately NOT modeled here: party membership is never persisted (fresh login = no party,
///     PartyRegistry's own remarks) and a duel cannot possibly be in progress at login time either, so
///     both are correctly already blank on <c>AvatarInfoTemplates.Zeroed</c>.
/// </summary>
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

    /// <summary>
    ///     Expands the sparse (slot -&gt; name) map into AVATAR_INFO's fixed 10-slot <c>Friend</c> array, empty string
    ///     for every unfilled slot -- slots are client-chosen (contracts/05_social.md), so gaps are normal.
    /// </summary>
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
