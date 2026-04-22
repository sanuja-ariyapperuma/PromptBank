using PromptBank.Models;

namespace PromptBank.UnitTests.Models;

/// <summary>
/// Unit tests for the computed properties of the <see cref="Prompt"/> model.
/// These tests exercise the logic directly on the POCO without any database involvement.
/// </summary>
public class PromptModelTests
{
    /// <summary>
    /// Verifies that <see cref="Prompt.AverageRating"/> returns exactly 0
    /// when no votes have been cast (i.e. <see cref="Prompt.RatingCount"/> is 0).
    /// </summary>
    [Fact]
    public void AverageRating_ReturnsZero_WhenNoVotes()
    {
        var prompt = new Prompt
        {
            Title = "Test",
            Content = "Content",
            OwnerId = "user-1",
            RatingTotal = 0,
            RatingCount = 0
        };

        var average = prompt.AverageRating;

        Assert.Equal(0.0, average);
    }

    /// <summary>
    /// Verifies that <see cref="Prompt.AverageRating"/> returns the mathematically
    /// correct average when votes have been recorded.
    /// </summary>
    [Fact]
    public void AverageRating_CalculatesCorrectly()
    {
        // Arrange – 3 votes totalling 12 → expected average of 4.0
        var prompt = new Prompt
        {
            Title = "Test",
            Content = "Content",
            OwnerId = "user-1",
            RatingTotal = 12,
            RatingCount = 3
        };

        var average = prompt.AverageRating;

        Assert.Equal(4.0, average);
    }

    /// <summary>
    /// Verifies that <see cref="Prompt.AverageRating"/> rounds the result to
    /// exactly one decimal place rather than returning a long floating-point string.
    /// </summary>
    [Fact]
    public void AverageRating_RoundsToOneDecimalPlace()
    {
        // Arrange – 3 votes totalling 10 → raw average ≈ 3.333…, expected 3.3
        var prompt = new Prompt
        {
            Title = "Test",
            Content = "Content",
            OwnerId = "user-1",
            RatingTotal = 10,
            RatingCount = 3
        };

        var average = prompt.AverageRating;

        Assert.Equal(3.3, average);

        var asString = average.ToString("G");
        var decimalIndex = asString.IndexOf('.');
        if (decimalIndex >= 0)
        {
            var decimalPlaces = asString.Length - decimalIndex - 1;
            Assert.True(decimalPlaces <= 1,
                $"Expected at most 1 decimal place but got '{asString}'.");
        }
    }
}
