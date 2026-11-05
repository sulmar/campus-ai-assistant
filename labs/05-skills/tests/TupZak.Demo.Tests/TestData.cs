using TupZak.Demo.Services;

namespace TupZak.Demo.Tests;

internal static class TestData
{
    /// <summary>Katalog labs/05-skills/data, wyszukiwany w górę od katalogu z testami.</summary>
    public static string DataDirectory { get; } = FindDataDirectory();

    /// <summary>Mały kampus testowy (tests/TupZak.Demo.Tests/TestCampus) z odległościami do policzenia w pamięci.</summary>
    public static string TestCampusDirectory { get; } = FindDirectory("TestCampus", "building-k.geojson");

    public static BuildingRepository LoadRepository() => BuildingRepository.LoadFromDirectory(DataDirectory);

    public static (BuildingRepository Buildings, CampusNetwork Network, DeliveryPlanner Planner) LoadCampus(string directory)
    {
        var buildings = BuildingRepository.LoadFromDirectory(directory);
        var network = CampusNetwork.LoadFromDirectory(directory, buildings);
        return (buildings, network, new DeliveryPlanner(network, buildings));
    }

    private static string FindDataDirectory() => FindDirectory("data", "building-a.geojson");

    private static string FindDirectory(string name, string marker)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, name);
            if (File.Exists(Path.Combine(candidate, marker)))
                return candidate;
        }
        throw new DirectoryNotFoundException($"Nie znaleziono katalogu {name} z plikiem {marker}.");
    }
}
