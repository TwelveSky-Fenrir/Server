using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.World;

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

[GenerateDto]
public sealed partial record QuestRewardRowDto(
    int QuestId,
    byte SlotIndex,
    byte RewardType,
    int? ItemId,
    int? Amount);

[GenerateDto]
public sealed partial record QuestSpeechRowDto(
    int QuestId,
    byte SpeechKind,
    byte LineIndex,
    string Text,
    int Color);
