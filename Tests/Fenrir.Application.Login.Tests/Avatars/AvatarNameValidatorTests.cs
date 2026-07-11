using Fenrir.Application.Login.Domain.Avatars;

namespace Fenrir.Application.Login.Tests.Avatars;

public class AvatarNameValidatorTests
{
    [Theory]
    [InlineData("Hero")]
    [InlineData("hero")]
    [InlineData("HERO123")]
    [InlineData("0123456789")]
    [InlineData("a")]
    [InlineData("Z9z")]
    public void HasOnlyWhitelistedCharacters_AllAsciiAlphanumeric_IsAccepted(string name)
    {
        Assert.True(AvatarNameValidator.HasOnlyWhitelistedCharacters(name));
    }

    [Fact]
    public void HasOnlyWhitelistedCharacters_EmptyString_IsRejected()
    {
        Assert.False(AvatarNameValidator.HasOnlyWhitelistedCharacters(string.Empty));
    }

    [Theory]
    [InlineData("Hero Knight")]
    [InlineData("Hero_Knight")]
    [InlineData("Hero-Knight")]
    [InlineData("Hero!")]
    [InlineData("Héro")]
    [InlineData("héros")]
    [InlineData("é")]
    [InlineData("한글")]
    public void HasOnlyWhitelistedCharacters_AnyNonAsciiOrPunctuationByte_IsRejected(string name)
    {
        Assert.False(AvatarNameValidator.HasOnlyWhitelistedCharacters(name));
    }

    [Fact]
    public void HasOnlyWhitelistedCharacters_FirstInvalidByteShortCircuits_RestOfStringNeverExamined()
    {
        Assert.False(AvatarNameValidator.HasOnlyWhitelistedCharacters(" ValidTail"));
    }
}
