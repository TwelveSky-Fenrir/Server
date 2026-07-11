using Fenrir.Data.Security;

namespace Fenrir.Data.Tests.Security;

public class PasswordHasherTests
{
    [Fact]
    public void Verify_AcceptsTheHashedPassword()
    {
        var (hash, salt) = PasswordHasher.Hash("Correct horse battery staple");

        Assert.True(PasswordHasher.Verify("Correct horse battery staple", hash, salt));
    }

    [Fact]
    public void Verify_RejectsAWrongPassword()
    {
        var (hash, salt) = PasswordHasher.Hash("Correct horse battery staple");

        Assert.False(PasswordHasher.Verify("wrong password", hash, salt));
    }

    [Fact]
    public void Hash_UsesARandomSaltPerCall()
    {
        var (hash1, salt1) = PasswordHasher.Hash("same password");
        var (hash2, salt2) = PasswordHasher.Hash("same password");

        Assert.False(salt1.AsSpan().SequenceEqual(salt2));
        Assert.False(hash1.AsSpan().SequenceEqual(hash2));
    }
}
