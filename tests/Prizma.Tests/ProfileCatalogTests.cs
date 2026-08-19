using Prizma.Core.Profiles;
using Prizma.Engine;

namespace Prizma.Tests;

public sealed class ProfileCatalogTests
{
    [Fact]
    public void BundledProfiles_AreValidAndHaveOneRecommendation()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "profiles", "tr");

        var profiles = new ProfileCatalog().Load(directory);

        Assert.Equal(4, profiles.Count);
        Assert.Single(profiles, profile => profile.Recommended);
        var main = profiles.Single(profile => profile.Recommended);
        Assert.Equal("tr-balanced", main.Id);
        Assert.Equal("Türkiye • Ana", main.Name);
        Assert.Contains("--tls-split", main.Arguments);
        Assert.Contains("2", main.Arguments);
        Assert.Contains("--reverse-fragments", main.Arguments);
        Assert.Contains("--fake-ttl", main.Arguments);
        Assert.Contains("--fake-host-suffix", main.Arguments);
        Assert.Contains("--allow-quic", main.Arguments);
        Assert.Contains("--dns-doh", main.Arguments);
        Assert.All(profiles, profile => EngineOptions.Parse(profile.Arguments));
    }

    [Fact]
    public void Load_RejectsDuplicateProfileIds()
    {
        var directory = CreateDirectory();
        try
        {
            const string profile = """
                {
                  "id": "same",
                  "name": "Test",
                  "description": "Test",
                  "arguments": ["--wf-tcp-out=443"]
                }
                """;
            File.WriteAllText(Path.Combine(directory, "one.json"), profile);
            File.WriteAllText(Path.Combine(directory, "two.json"), profile);

            var exception = Assert.Throws<InvalidDataException>(() => new ProfileCatalog().Load(directory));

            Assert.Contains("same", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Load_ReturnsEmptyListForMissingDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        var profiles = new ProfileCatalog().Load(path);

        Assert.Empty(profiles);
    }

    [Fact]
    public void Load_IgnoresLegacyEngineSelectorMetadata()
    {
        var directory = CreateDirectory();
        try
        {
            File.WriteAllText(Path.Combine(directory, "legacy.json"), """
                {
                  "id": "legacy",
                  "name": "Eski profil",
                  "description": "Tek motor modeline taşınan profil",
                  "engine": "GoodbyeDpi",
                  "arguments": ["--mode=auto"]
                }
                """);

            var profile = Assert.Single(new ProfileCatalog().Load(directory));

            Assert.Equal("legacy", profile.Id);
            Assert.Equal(["--mode=auto"], profile.Arguments);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "prizma-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
