using Fenrir.Application.Login.Domain.Avatars;

namespace Fenrir.Application.Login.Tests.Avatars;

// CheckNameString (Server/Header/safestring.h:43-81): byte-whitelist shared by avatar creation and rename.
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
    [InlineData("Hero Knight")] // space
    [InlineData("Hero_Knight")] // underscore
    [InlineData("Hero-Knight")] // hyphen
    [InlineData("Hero!")] // punctuation
    [InlineData("Héro")] // accented Latin byte
    [InlineData("héros")]
    [InlineData("é")] // a lone accented byte value, standing in for a DBCS lead/trail byte
    [InlineData("한글")] // multi-byte (would-be DBCS) sequence
    public void HasOnlyWhitelistedCharacters_AnyNonAsciiOrPunctuationByte_IsRejected(string name)
    {
        Assert.False(AvatarNameValidator.HasOnlyWhitelistedCharacters(name));
    }

    [Fact]
    public void HasOnlyWhitelistedCharacters_FirstInvalidByteShortCircuits_RestOfStringNeverExamined()
    {
        // Only asserting the observable outcome (rejected) -- the "stops at first bad byte" legacy behavior
        // has no other externally observable effect since this is a pure boolean predicate.
        Assert.False(AvatarNameValidator.HasOnlyWhitelistedCharacters(" ValidTail"));
    }
}
