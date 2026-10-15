using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace FryWorld.Sdk.Tests;

/// <summary>
/// Keeps the SDK README honest: its C# samples must be exactly the code in Samples/ReadmeSamples.cs, which compiles
/// and runs as part of this test project.
/// </summary>
public class ReadmeSampleTests
{
    private static string FindRepoFile(params string[] relative)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var candidate = Path.Combine(new[] { dir.FullName }.Concat(relative).ToArray());
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException(string.Join('/', relative));
    }

    private static string Normalize(string code) =>
        string.Join('\n', code.Replace("\r\n", "\n").Split('\n').Select(l => l.TrimEnd())).Trim();

    [Theory]
    [InlineData("quickstart")]
    [InlineData("testing")]
    public void ReadmeSample_MatchesCompiledCode(string sample)
    {
        var samples = File.ReadAllText(FindRepoFile("tests", "FryWorld.Sdk.Tests", "Samples", "ReadmeSamples.cs"));
        var readme = File.ReadAllText(FindRepoFile("src", "FryWorld.Sdk", "README.md"));

        var match = Regex.Match(samples, $@"// README:{sample}:start\n(?<code>.*?)\n// README:{sample}:end", RegexOptions.Singleline);
        Assert.True(match.Success, $"Sample '{sample}' is missing from ReadmeSamples.cs");

        var code = Normalize(match.Groups["code"].Value);
        Assert.True(Normalize(readme).Contains(code, StringComparison.Ordinal),
            $"README.md no longer contains the '{sample}' sample from tests/FryWorld.Sdk.Tests/Samples/ReadmeSamples.cs. Copy it over:\n\n{code}");
    }
}
