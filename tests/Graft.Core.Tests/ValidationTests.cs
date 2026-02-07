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
}
