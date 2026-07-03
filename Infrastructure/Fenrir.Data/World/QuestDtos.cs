using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.World;

/// <summary>
///     One world.Quests row -- ordinal contract of world.usp_Quest_GetAll's single result set (688 rows,
///     the legacy QUEST_INFO catalog). Constructor order must track the SELECT column order exactly
///     (invariant I-04); [GenerateDto] maps by position, not by name.
/// </summary>
[GenerateDto]
public sealed partial record QuestRowDto(
    int QuestId,
    string Subject,
    byte Category,
    short Step,
    short Level,
    byte Type,
    byte Sort,
    short? SummonZoneNumber,
    int? SummonPosX,
    int? SummonPosY,
    int? SummonPosZ,
    int StartNPCNumber,
    int? KeyNpcNumber1,
    int? KeyNpcNumber2,
    int? KeyNpcNumber3,
    int? KeyNpcNumber4,
    int? KeyNpcNumber5,
    int EndNPCNumber,
    int? Solution1,
    int? Solution2,
    int? Solution3,
    int? Solution4,
    int? NextIndex);

/// <summary>
///     One populated world.QuestRewards slot -- world.usp_QuestReward_GetAll (SlotIndex 0-2). ItemId is set
///     for RewardType 6 (item reward) only; Amount for RewardType 2-5 (scalar reward) only -- the table's
///     CK_QuestRewards_ItemXorAmount check enforces the exclusivity.
/// </summary>
[GenerateDto]
public sealed partial record QuestRewardRowDto(
    int QuestId,
    byte SlotIndex,
    byte RewardType,
    int? ItemId,
    int? Amount);

/// <summary>One populated world.QuestSpeeches line -- world.usp_QuestSpeech_GetAll (SpeechKind 0-9, LineIndex 0-14).</summary>
[GenerateDto]
public sealed partial record QuestSpeechRowDto(
    int QuestId,
    byte SpeechKind,
    byte LineIndex,
    string Text,
    int Color);
