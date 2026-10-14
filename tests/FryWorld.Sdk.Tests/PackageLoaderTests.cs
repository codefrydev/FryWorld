using System;
using FryWorld.Sdk.Loading;
using Xunit;

namespace FryWorld.Sdk.Tests;

public class PackageLoaderTests
{
    [Theory]
    [InlineData("../sneaky")]
    [InlineData("..\\sneaky")]
    [InlineData("/etc/passwd")]
    [InlineData("id with spaces")]
    [InlineData("id*with#symbols")]
    public void PluginIdValidator_RejectsPathTraversalAndInvalidChars(string unsafeId)
    {
        Assert.Throws<ArgumentException>(() => PluginIdValidator.ValidateIdentifier(unsafeId));
    }

    [Theory]
    [InlineData("com.fryworld.tool")]
    [InlineData("valid-plugin-id_123")]
    [InlineData("my.tool.v1")]
    public void PluginIdValidator_AcceptsValidIdentifiers(string validId)
    {
        PluginIdValidator.ValidateIdentifier(validId);
    }
}
