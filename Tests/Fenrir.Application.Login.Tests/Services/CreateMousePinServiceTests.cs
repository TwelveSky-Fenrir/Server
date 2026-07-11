using Fenrir.Application.Login.Abstractions.CreateMousePin;
using Fenrir.Application.Login.Services.CreateMousePin;
using Fenrir.Application.Login.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Login.Tests.Services;

public class CreateMousePinServiceTests
{
    private const int AccountId = 42;

    [Fact]
    public async Task CreateMousePinAsync_NoExistingPin_StoresHashedPin()
    {
        var pins = FakeAccountPinRepository.WithNoPin();
        var service = new CreateMousePinService(pins, NullLogger<CreateMousePinService>.Instance);

        var result = await service.CreateMousePinAsync(AccountId, "1234", CancellationToken.None);

        Assert.Equal(CreateMousePinOutcome.Success, result.Outcome);
        Assert.Equal(1, pins.SetCallCount);
    }

    [Fact]
    public async Task CreateMousePinAsync_PinAlreadyExists_ReportsAlreadyExistsWithoutFormatCheck()
    {
        var pins = FakeAccountPinRepository.WithPin("5678");
        var service = new CreateMousePinService(pins, NullLogger<CreateMousePinService>.Instance);

        var result = await service.CreateMousePinAsync(AccountId, "1234", CancellationToken.None);

        Assert.Equal(CreateMousePinOutcome.AlreadyExists, result.Outcome);
        Assert.Equal(0, pins.SetCallCount);
    }

    [Fact]
    public async Task CreateMousePinAsync_PinAlreadyExistsAndSubmittedFormatAlsoMalformed_ReportsAlreadyExists()
    {
        var pins = FakeAccountPinRepository.WithPin("5678");
        var service = new CreateMousePinService(pins, NullLogger<CreateMousePinService>.Instance);

        var result = await service.CreateMousePinAsync(AccountId, "12a4", CancellationToken.None);

        Assert.Equal(CreateMousePinOutcome.AlreadyExists, result.Outcome);
        Assert.Equal(0, pins.SetCallCount);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("12345")]
    [InlineData("12a4")]
    [InlineData("")]
    public async Task CreateMousePinAsync_NoExistingPinButMalformedInput_ReportsInvalidFormat(string malformedPin)
    {
        var pins = FakeAccountPinRepository.WithNoPin();
        var service = new CreateMousePinService(pins, NullLogger<CreateMousePinService>.Instance);

        var result = await service.CreateMousePinAsync(AccountId, malformedPin, CancellationToken.None);

        Assert.Equal(CreateMousePinOutcome.InvalidFormat, result.Outcome);
        Assert.Equal(0, pins.SetCallCount);
    }

    [Fact]
    public async Task CreateMousePinAsync_StorageFailure_ReportsStorageFailure()
    {
        var pins = FakeAccountPinRepository.WithNoPin();
        pins.ThrowOnSet = true;
        var service = new CreateMousePinService(pins, NullLogger<CreateMousePinService>.Instance);

        var result = await service.CreateMousePinAsync(AccountId, "1234", CancellationToken.None);

        Assert.Equal(CreateMousePinOutcome.StorageFailure, result.Outcome);
    }
}
