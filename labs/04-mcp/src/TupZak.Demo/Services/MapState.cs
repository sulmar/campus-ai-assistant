namespace TupZak.Demo.Services;

/// <summary>Stan wizualizacji widoczny na mapie. Zmieniają go wyłącznie narzędzia wykonywane przez aplikację.</summary>
public sealed class MapState
{
    private readonly Lock _lock = new();
    private MapSnapshot _snapshot = new(null, null, null);

    public MapSnapshot Snapshot
    {
        get { lock (_lock) return _snapshot; }
    }

    public void Focus(string buildingId)
    {
        lock (_lock) _snapshot = _snapshot with { FocusedBuildingId = buildingId };
    }

    public void MarkEntrance(string buildingId, string entranceId)
    {
        lock (_lock) _snapshot = new MapSnapshot(buildingId, buildingId, entranceId);
    }

    public void Clear()
    {
        lock (_lock) _snapshot = new MapSnapshot(null, null, null);
    }
}

public sealed record MapSnapshot(string? FocusedBuildingId, string? MarkedBuildingId, string? MarkedEntranceId);
