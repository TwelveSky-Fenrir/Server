using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Runtime;

[GenerateDto]
public sealed partial record PopupEventLeaseAcquireResult(bool Acquired, DateTime LeaseExpiresAtUtc);
