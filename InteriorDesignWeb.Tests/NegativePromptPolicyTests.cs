using InteriorDesignWeb.Services.AI;

namespace InteriorDesignWeb.Tests;

public sealed class NegativePromptPolicyTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Compose_EmptyUserPrompt_ReturnsSystemPrompt(string? userPrompt)
    {
        Assert.Equal(NegativePromptPolicy.SystemPrompt, NegativePromptPolicy.Compose(userPrompt));
    }

    [Fact]
    public void Compose_UserPrompt_PlacesSystemPromptFirst()
    {
        var result = NegativePromptPolicy.Compose("watermark, text");

        Assert.Equal($"{NegativePromptPolicy.SystemPrompt},watermark, text", result);
    }

    [Fact]
    public void Compose_AlreadyInjectedPrompt_DoesNotDuplicateSystemPrompt()
    {
        var input = $"{NegativePromptPolicy.SystemPrompt}, watermark";

        var result = NegativePromptPolicy.Compose(input);

        Assert.Equal($"{NegativePromptPolicy.SystemPrompt},watermark", result);
        Assert.Equal(1, CountOccurrences(result, NegativePromptPolicy.SystemPrompt));
    }

    private static int CountOccurrences(string value, string part) =>
        value.Split(part, StringSplitOptions.None).Length - 1;
}
