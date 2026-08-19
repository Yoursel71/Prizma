using System.Text.Json;
using System.Text.Json.Serialization;
using Prizma.Core.Models;

namespace Prizma.Core.Profiles;

public sealed class ProfileCatalog
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Converters = { new JsonStringEnumConverter() }
    };

    public IReadOnlyList<ConnectionProfile> Load(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var profiles = Directory
            .EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(LoadProfile)
            .ToArray();

        var duplicate = profiles
            .GroupBy(profile => profile.Id, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new InvalidDataException($"Yinelenen profil kimliği: {duplicate.Key}");
        }

        return profiles;
    }

    private static ConnectionProfile LoadProfile(string path)
    {
        using var stream = File.OpenRead(path);
        var profile = JsonSerializer.Deserialize<ConnectionProfile>(stream, SerializerOptions)
            ?? throw new InvalidDataException($"Profil okunamadı: {path}");

        if (string.IsNullOrWhiteSpace(profile.Id) ||
            string.IsNullOrWhiteSpace(profile.Name) ||
            profile.Arguments.Count == 0)
        {
            throw new InvalidDataException($"Profil eksik alan içeriyor: {path}");
        }

        if (profile.Arguments.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidDataException($"Profil boş argüman içeriyor: {path}");
        }

        return profile;
    }
}
