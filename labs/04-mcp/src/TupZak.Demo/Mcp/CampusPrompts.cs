using System.ComponentModel;
using ModelContextProtocol.Server;
using TupZak.Demo.Services;
using TupZak.Demo.Tools;

namespace TupZak.Demo.Mcp;

/// <summary>
/// Prompty MCP: gotowe szablony wiadomości z parametrami (prompts/list, prompts/get).
/// W przeciwieństwie do narzędzi wybiera je użytkownik, a nie model
/// (w Claude Code jako polecenie /mcp__tupzak__building_entrance).
/// </summary>
[McpServerPromptType]
public sealed class CampusPrompts
{
    public const string BuildingEntranceName = "building_entrance";

    [McpServerPrompt(Name = BuildingEntranceName, Title = "Wejście do budynku od strony obiektu")]
    [Description("Wyszukuje budynek i wskazuje jego wejście od strony podanego obiektu w otoczeniu, np. parkingu. Identyfikator wejścia zaznacza bezpośrednio.")]
    public static string BuildingEntrance(
        BuildingRepository repository,
        [Description("Nazwa lub identyfikator budynku, np. \"A\" albo \"Budynek B\".")] string name,
        [Description("Obiekt w otoczeniu budynku, od którego strony szukasz wejścia, np. \"parking\" albo \"przystanek autobusowy\", albo identyfikator wejścia, np. \"A1\".")] string target)
    {
        // Gdy target jest identyfikatorem wejścia tego budynku, nie ma czego wyszukiwać: wejście jest jednoznaczne.
        var building = repository.Find(name);
        var entrance = building?.Entrances.FirstOrDefault(e => e.Id.Equals(target.Trim(), StringComparison.OrdinalIgnoreCase));
        if (building is not null && entrance is not null)
            return $"""
                Zaznacz wejście {entrance.Id} budynku {building.Id} narzędziem {CampusTools.MarkEntranceName}
                (buildingId: "{building.Id}", entranceId: "{entrance.Id}").

                Odpowiedz krótko: identyfikator wejścia i wynik zaznaczenia. Jeżeli narzędzie zwróci błąd, przekaż go.
                """;

        return $"""
        Znajdź budynek „{name}” i wskaż jego wejście od strony obiektu „{target}”.

        1. Wywołaj {CampusTools.FindBuildingName} z nazwą budynku, żeby ustalić jego identyfikator.
        2. Wywołaj {CampusTools.GetBuildingGeometryName}, żeby pobrać wejścia i obiekty w otoczeniu.
        3. Wybierz wejście na elewacji zwróconej w stronę obiektu „{target}”, które jest do niego najbliżej
           lub do którego prowadzi od niego ścieżka.
        4. Jeżeli wejście jest jednoznaczne, zaznacz je narzędziem {CampusTools.MarkEntranceName}.
           Jeżeli nie, albo w otoczeniu nie ma takiego obiektu, nie zaznaczaj niczego i zapytaj mnie.

        Odpowiedz krótko: identyfikator wejścia, elewacja i uzasadnienie na podstawie danych.
        """;
    }
}
