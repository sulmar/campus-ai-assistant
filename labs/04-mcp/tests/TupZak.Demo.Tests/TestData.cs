using TupZak.Demo.Services;

namespace TupZak.Demo.Tests;

internal static class TestData
{
    /// <summary>Katalog labs/04-mcp/data, wyszukiwany w górę od katalogu z testami.</summary>
    public static string DataDirectory { get; } = FindDataDirectory();

    public static BuildingRepository LoadRepository() => BuildingRepository.LoadFromDirectory(DataDirectory);

    private static string FindDataDirectory()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, "data");
            if (File.Exists(Path.Combine(candidate, "building-a.geojson")))
                return candidate;
        }
        throw new DirectoryNotFoundException("Nie znaleziono katalogu data z plikami GeoJSON.");
    }
}
