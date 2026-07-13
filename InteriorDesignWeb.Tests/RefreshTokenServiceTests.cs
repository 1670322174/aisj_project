using System.Text.RegularExpressions;
using InteriorDesignWeb.Services;

namespace InteriorDesignWeb.Tests;

public sealed class RefreshTokenServiceTests
{
    [Fact]
    public void CreateToken_ReturnsIndependentHighEntropyTokens()
    {
        var first = RefreshTokenService.CreateToken();
        var second = RefreshTokenService.CreateToken();

        Assert.NotEqual(first, second);
        Assert.Equal(64, Convert.FromBase64String(first).Length);
        Assert.Equal(64, Convert.FromBase64String(second).Length);
    }

    [Fact]
    public void HashToken_IsStableLowercaseSha256_WithoutRawToken()
    {
        const string token = "refresh-token-for-test";

        var first = RefreshTokenService.HashToken(token);
        var second = RefreshTokenService.HashToken(token);

        Assert.Equal(first, second);
        Assert.Equal(64, first.Length);
        Assert.Matches(new Regex("^[0-9a-f]{64}$"), first);
        Assert.DoesNotContain(token, first, StringComparison.Ordinal);
    }

    [Fact]
    public void HashToken_RejectsEmptyToken()
    {
        Assert.Throws<ArgumentException>(() => RefreshTokenService.HashToken(" "));
    }
}
