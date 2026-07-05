using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.World;

// world.usp_Quest_GetAll; ordinal-mapped, ctor order must match the SELECT.
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
///     world.usp_QuestReward_GetAll (SlotIndex 0-2); ItemId set only for RewardType 6, Amount only for 2-5
///     (CK_QuestRewards_ItemXorAmount enforces this).
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
