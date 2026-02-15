using AIOMux.Skills.Notes.Pipeline;
using AIOMux.Skills.Notes.Models;
using Xunit;

namespace AIOMux.Skills.Notes.Tests.Pipeline;

public class PipelineTests
{
    [Fact]
    public void NormalizeStep_RemovesExtraWhitespace()
    {
        var input = new NoteInput
        {
            Text = "This  has   extra\n\nwhitespace\t\there"
        };

        var result = NormalizeStep.Execute(input);

        Assert.Equal("This has extra whitespace here", result.Text);
    }

    [Theory]
    [InlineData("Remember to review the PR", "task")]
    [InlineData("Need to implement caching", "task")]
    [InlineData("Idea: what if we use Redis?", "idea")]
    [InlineData("Check out this article https://example.com", "reference")]
    [InlineData("Just a regular note", "note")]
    public void ClassifyStep_ClassifiesCorrectly(string text, string expectedType)
    {
        var result = ClassifyStep.Execute(text);

        Assert.Equal(expectedType, result);
    }

    [Fact]
    public void SummarizeStep_ExtractsTitleAndSummary()
    {
        var text = "This is the first sentence. This is the second sentence with more content.";

        var (title, summary) = SummarizeStep.Execute(text);

        Assert.Equal("This is the first sentence", title);
        Assert.Contains("first sentence", summary);
    }

    [Fact]
    public void SummarizeStep_TruncatesLongText()
    {
        var longText = new string('x', 500);

        var (title, summary) = SummarizeStep.Execute(longText);

        Assert.True(title.Length <= 63); // 60 + "..."
        Assert.True(summary.Length <= 203); // 200 + "..."
    }

    [Fact]
    public void ExtractMetadataStep_ExtractsHashtags()
    {
        var text = "This is a note about #performance and #caching";

        var (tags, entities) = ExtractMetadataStep.Execute(text);

        Assert.Contains("performance", tags);
        Assert.Contains("caching", tags);
    }

    [Fact]
    public void ExtractMetadataStep_ExtractsMentions()
    {
        var text = "Discussed with @Alice and @Bob";

        var (tags, entities) = ExtractMetadataStep.Execute(text);

        Assert.Contains("Alice", entities);
        Assert.Contains("Bob", entities);
    }

    [Fact]
    public void ExtractMetadataStep_ExtractsUrls()
    {
        var text = "Check https://example.com for details";

        var (tags, entities) = ExtractMetadataStep.Execute(text);

        Assert.Contains("https://example.com", entities);
    }

    [Fact]
    public void ExtractMetadataStep_ExtractsProperNouns()
    {
        var text = "Met with John in Seattle to discuss the project";

        var (tags, entities) = ExtractMetadataStep.Execute(text);

        Assert.Contains("John", entities);
        Assert.Contains("Seattle", entities);
    }
}
