using MultiTenantSaaS.Infrastructure.Identity;
using Xunit;

namespace MultiTenantSaaS.UnitTests.Authorization;

public sealed class PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _sut = new();

    [Fact]
    public void Hash_ProducesDifferentOutputForSamePassword()
    {
        // Salt aleatoriu per parolă: două conturi cu aceeași parolă au hash-uri diferite,
        // deci o breșă de bază de date nu arată cine folosește aceeași parolă.
        Assert.NotEqual(_sut.Hash("aceeasi-parola"), _sut.Hash("aceeasi-parola"));
    }

    [Fact]
    public void Verify_AcceptsCorrectPassword()
    {
        Assert.True(_sut.Verify("Parola-Sigura-123", _sut.Hash("Parola-Sigura-123")));
    }

    [Fact]
    public void Verify_RejectsWrongPassword()
    {
        Assert.False(_sut.Verify("alta-parola", _sut.Hash("Parola-Sigura-123")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("fara-separatori")]
    [InlineData("nu-un-numar.c2FsdA==.a2V5")]
    [InlineData("210000.nu-e-base64!.a2V5")]
    public void Verify_WithMalformedHash_ReturnsFalseInsteadOfThrowing(string malformedHash)
    {
        // Un hash corupt în baza de date trebuie să blocheze login-ul, nu să dea 500.
        Assert.False(_sut.Verify("orice", malformedHash));
    }

    [Fact]
    public void Hash_EmbedsIterationCount_SoItCanBeRaisedLater()
    {
        var parts = _sut.Hash("parola").Split('.');

        Assert.Equal(3, parts.Length);
        Assert.Equal("210000", parts[0]);
    }
}
