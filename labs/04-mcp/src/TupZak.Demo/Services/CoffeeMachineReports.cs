namespace TupZak.Demo.Services;

/// <summary>Miejsce ekspresu do kawy wskazane przez użytkownika na mapie.</summary>
public sealed record CoffeeMachineReport(string BuildingId, double X, double Y);

/// <summary>
/// Wskazania ekspresów do kawy, przechowywane tylko w pamięci.
/// Uproszczenie na potrzeby demo: jeden ekspres na budynek, nowe wskazanie nadpisuje poprzednie.
/// </summary>
public sealed class CoffeeMachineReports
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, CoffeeMachineReport> _reports = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<CoffeeMachineReport> All
    {
        get { lock (_lock) return _reports.Values.OrderBy(r => r.BuildingId).ToList(); }
    }

    public CoffeeMachineReport? Get(string buildingId)
    {
        lock (_lock) return _reports.GetValueOrDefault(buildingId);
    }

    public CoffeeMachineReport Report(string buildingId, double x, double y)
    {
        var report = new CoffeeMachineReport(buildingId, x, y);
        lock (_lock) _reports[buildingId] = report;
        return report;
    }

    public void Clear()
    {
        lock (_lock) _reports.Clear();
    }
}
