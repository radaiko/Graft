namespace Graft.Core.Tests;

public sealed class ValidationTests
{
    [Theory]
    [InlineData("feature/auth")]
    [InlineData("my-stack")]
    [InlineData("stack_v2")]
    [InlineData("auth/base-types")]
    [InlineData("v1.0.0")]
    public void ValidateName_ValidNames_DoesNotThrow(string name)
    {
        Validation.ValidateName(name, "test");
    }

    [Theory]
    [InlineData("../etc/passwd")]
    [InlineData("stack/../other")]
    [InlineData("foo/../../bar")]
    public void ValidateName_PathTraversal_Throws(string name)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            Validation.ValidateName(name, "test"));
    }

    [Theory]
    [InlineData("-flag")]
    [InlineData("--option")]
    public void ValidateName_LeadingDash_Throws(string name)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            Validation.ValidateName(name, "test"));
    }

    [Theory]
    [InlineData("/absolute")]
    [InlineData("trailing/")]
    public void ValidateName_InvalidSlashes_Throws(string name)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            Validation.ValidateName(name, "test"));
    }

    [Fact]
    public void ValidateName_Backslash_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            Validation.ValidateName("foo\\bar", "test"));
    }

    [Fact]
    public void ValidateName_NullByte_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            Validation.ValidateName("foo\0bar", "test"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateName_NullOrWhitespace_Throws(string? name)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            Validation.ValidateName(name!, "test"));
    }

    // ValidateStackName tests
    [Theory]
    [InlineData("my-stack")]
    [InlineData("stack_v2")]
    [InlineData("auth-refactor")]
    [InlineData("v1.0.0")]
    public void ValidateStackName_ValidNames_DoesNotThrow(string name)
    {
        Validation.ValidateStackName(name);
    }

    [Theory]
    [InlineData("feature/auth")]
    [InlineData("auth/base-types")]
    public void ValidateStackName_ForwardSlash_Throws(string name)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            Validation.ValidateStackName(name));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateStackName_NullOrWhitespace_Throws(string? name)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            Validation.ValidateStackName(name!));
    }

    [Theory]
    [InlineData("-flag")]
    [InlineData("--option")]
    public void ValidateStackName_LeadingDash_Throws(string name)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            Validation.ValidateStackName(name));
    }

    [Fact]
    public void ValidateStackName_Backslash_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            Validation.ValidateStackName("foo\\bar"));
    }

    // Coverage for ValidateName paths changed in SonarCloud fix
    [Fact]
    public void ValidateName_ConsecutiveSlashes_Throws()
    {
        var ex = Assert.ThrowsAny<ArgumentException>(() =>
            Validation.ValidateName("foo//bar", "Branch name"));
        Assert.Equal("name", ex.ParamName);
    }

    [Fact]
    public void ValidateName_LockSuffix_Throws()
    {
        var ex = Assert.ThrowsAny<ArgumentException>(() =>
            Validation.ValidateName("refs.lock", "Branch name"));
        Assert.Equal("name", ex.ParamName);
    }

    [Fact]
    public void ValidateName_AtBrace_Throws()
    {
        var ex = Assert.ThrowsAny<ArgumentException>(() =>
            Validation.ValidateName("foo@{bar", "Branch name"));
        Assert.Equal("name", ex.ParamName);
    }

    [Fact]
    public void ValidateName_InvalidCharacters_Throws()
    {
        var ex = Assert.ThrowsAny<ArgumentException>(() =>
            Validation.ValidateName("foo bar", "Branch name"));
        Assert.Equal("name", ex.ParamName);
    }

    [Fact]
    public void ValidateName_ParamName_IsCorrect()
    {
        var ex = Assert.ThrowsAny<ArgumentException>(() =>
            Validation.ValidateName("../bad", "Branch name"));
        Assert.Equal("name", ex.ParamName);
    }

    [Fact]
    public void ValidateStackName_PathTraversal_Throws()
    {
        var ex = Assert.ThrowsAny<ArgumentException>(() =>
            Validation.ValidateStackName("foo..bar"));
        Assert.Equal("name", ex.ParamName);
    }

    [Fact]
    public void ValidateStackName_NullByte_Throws()
    {
        var ex = Assert.ThrowsAny<ArgumentException>(() =>
            Validation.ValidateStackName("foo\0bar"));
        Assert.Equal("name", ex.ParamName);
    }

    [Fact]
    public void ValidateStackName_InvalidCharacters_Throws()
    {
        var ex = Assert.ThrowsAny<ArgumentException>(() =>
            Validation.ValidateStackName("foo bar"));
        Assert.Equal("name", ex.ParamName);
    }

    [Fact]
    public void ValidateStackName_ParamName_IsCorrect()
    {
        var ex = Assert.ThrowsAny<ArgumentException>(() =>
            Validation.ValidateStackName("-bad"));
        Assert.Equal("name", ex.ParamName);
    }

    [Fact]
    public void ValidateStackName_ForwardSlash_ParamName_IsCorrect()
    {
        var ex = Assert.ThrowsAny<ArgumentException>(() =>
            Validation.ValidateStackName("foo/bar"));
        Assert.Equal("name", ex.ParamName);
    }
}
