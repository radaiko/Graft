using Graft.Core.Tui;

namespace Graft.Core.Tests.Tui;

public sealed class FuzzyMatcherTests
{
    [Fact]
    public void Match_ExactMatch_ReturnsHighScore()
    {
        var result = FuzzyMatcher.Match("Graft", "Graft");
        Assert.NotNull(result);
        Assert.True(result.Score > 0);
    }

    [Fact]
    public void Match_Subsequence_ReturnsMatch()
    {
        var result = FuzzyMatcher.Match("gft", "Graft");
        Assert.NotNull(result);
        Assert.Equal(3, result.MatchedIndices.Count);
    }

    [Fact]
    public void Match_NoSubsequence_ReturnsNull()
    {
        var result = FuzzyMatcher.Match("xyz", "Graft");
        Assert.Null(result);
    }

    [Fact]
    public void Match_EmptyPattern_ReturnsZeroScore()
    {
        var result = FuzzyMatcher.Match("", "Graft");
        Assert.NotNull(result);
        Assert.Equal(0, result.Score);
    }

    [Fact]
    public void Match_EmptyCandidate_ReturnsNull()
    {
        var result = FuzzyMatcher.Match("abc", "");
        Assert.Null(result);
    }

    [Fact]
    public void Match_CaseInsensitive_Matches()
    {
        var result = FuzzyMatcher.Match("graft", "GRAFT");
        Assert.NotNull(result);
    }

    [Fact]
    public void Match_ExactCase_ScoresHigherThanMismatch()
    {
        var exactCase = FuzzyMatcher.Match("Graft", "Graft");
        var wrongCase = FuzzyMatcher.Match("graft", "Graft");

        Assert.NotNull(exactCase);
        Assert.NotNull(wrongCase);
        Assert.True(exactCase.Score > wrongCase.Score);
    }

    [Fact]
    public void Match_WordBoundary_ScoresHigher()
    {
        // "sc" matching "scan-command" (word boundary) vs "discard" (no boundary)
        var boundary = FuzzyMatcher.Match("sc", "scan-command");
        var noBoundary = FuzzyMatcher.Match("sc", "discard");

        Assert.NotNull(boundary);
        Assert.NotNull(noBoundary);
        Assert.True(boundary.Score > noBoundary.Score);
    }

    [Fact]
    public void Match_Consecutive_ScoresHigher()
    {
        // "gr" consecutive in "Graft" vs non-consecutive in "gear"
        var consecutive = FuzzyMatcher.Match("gr", "Graft");
        var nonConsecutive = FuzzyMatcher.Match("gr", "gear");

        Assert.NotNull(consecutive);
        // "gear" doesn't have 'gr' as subsequence (g-e-a-r), 'r' comes after 'a'
        // Actually "gr" in "gear": g(0) then r(3) — both match
        Assert.NotNull(nonConsecutive);
        Assert.True(consecutive.Score > nonConsecutive.Score);
    }

    [Fact]
    public void Filter_SortsDescendingByScore()
    {
        var items = new[] { "xyzzy", "Graft", "graft-core" };

        var filtered = FuzzyMatcher.Filter("graft", items, x => x);

        Assert.Equal(2, filtered.Count);
        // "Graft" should score higher than "graft-core" (exact match vs prefix)
        Assert.Equal("Graft", filtered[0]);
    }

    [Fact]
    public void Filter_EmptyPattern_ReturnsAllItems()
    {
        var items = new[] { "a", "b", "c" };
        var filtered = FuzzyMatcher.Filter("", items, x => x);
        Assert.Equal(3, filtered.Count);
    }

    [Fact]
    public void Filter_NoMatches_ReturnsEmpty()
    {
        var items = new[] { "abc", "def" };
        var filtered = FuzzyMatcher.Filter("xyz", items, x => x);
        Assert.Empty(filtered);
    }

    [Theory]
    [InlineData("gft", "Graft", true)]
    [InlineData("abc", "aXbXc", true)]
    [InlineData("abc", "cab", false)]
    [InlineData("zzz", "abc", false)]
    public void Match_Subsequence_Variations(string pattern, string candidate, bool shouldMatch)
    {
        var result = FuzzyMatcher.Match(pattern, candidate);
        if (shouldMatch)
            Assert.NotNull(result);
        else
            Assert.Null(result);
    }
}
