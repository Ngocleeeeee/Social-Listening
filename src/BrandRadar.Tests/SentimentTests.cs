using BrandRadar.Shared.Contracts;
using BrandRadar.Shared.Sentiment;
using Xunit;

namespace BrandRadar.Tests;

public class SentimentTests
{
    private readonly LexiconSentimentAnalyzer _a = new();

    [Theory]
    [InlineData("dịch vụ quá tệ, thất vọng", SentimentLabel.Negative)]
    [InlineData("terrible service, very disappointed", SentimentLabel.Negative)]
    [InlineData("dịch vụ tuyệt vời, rất hài lòng", SentimentLabel.Positive)]
    [InlineData("great product, love it", SentimentLabel.Positive)]
    [InlineData("thông tin mới về sản phẩm", SentimentLabel.Neutral)]
    public async Task Analyze_classifies_expected(string text, SentimentLabel expected)
    {
        var r = await _a.AnalyzeAsync(text);
        Assert.Equal(expected, r.Label);
    }

    [Fact]
    public async Task Empty_text_is_neutral()
    {
        var r = await _a.AnalyzeAsync("");
        Assert.Equal(SentimentLabel.Neutral, r.Label);
        Assert.Equal(0, r.Score);
    }

    [Fact]
    public void ExtractTopics_returns_salient_words()
    {
        var topics = _a.ExtractTopics("VinFast ra mắt xe điện VinFast tại triển lãm xe điện");
        Assert.Contains("vinfast", topics);
    }
}
