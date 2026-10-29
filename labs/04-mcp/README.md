# Tup Żak — MCP w .NET

Kontynuacja [lab 03](../03-tool-calling/README.md): te same narzędzia kampusu, udostępnione jako **serwer MCP**. Pętlę agenta prowadzi teraz host (Claude Code, Claude Desktop), a aplikacja tylko wykonuje narzędzia.

## Uruchomienie

```bash
dotnet run --project src/TupZak.Demo   # http://localhost:5144, MCP: /mcp
```

W tym katalogu uruchom `claude`. Serwer `tupzak` jest zdefiniowany w `.mcp.json`, więc wystarczy go zatwierdzić. Potem zapytaj:

> Znajdź budynek A i zaznacz wejście od strony parkingu.

## MCP Inspector

[MCP Inspector](https://github.com/modelcontextprotocol/inspector) pozwala obejrzeć i wywołać wszystko, co udostępnia serwer, bez modelu. Wymaga Node.js, a aplikacja musi działać.

**Interfejs w przeglądarce:**

```bash
npx @modelcontextprotocol/inspector
```

W otwartej stronie wybierz *Transport Type: Streamable HTTP*, wpisz URL `http://localhost:5144/mcp` i kliknij *Connect*. Na zakładkach *Tools*, *Resources* i *Prompts* możesz przeglądać i wywoływać elementy serwera.

**Wiersz poleceń:**

```bash
npx @modelcontextprotocol/inspector --cli http://localhost:5144/mcp --transport http --method tools/list
npx @modelcontextprotocol/inspector --cli http://localhost:5144/mcp --transport http --method tools/call --tool-name find_building --tool-arg query=A
npx @modelcontextprotocol/inspector --cli http://localhost:5144/mcp --transport http --method prompts/get --prompt-name building_entrance --prompt-args name=C target=C1
```

## Co udostępnia serwer MCP

MCP rozróżnia, **kto decyduje** o użyciu danego elementu:

| Element | Kto decyduje | W naszym projekcie |
|---|---|---|
| Narzędzia (tools) | model | 4 narzędzia kampusu |
| Zasoby (resources) | użytkownik lub aplikacja | lista budynków i GeoJSON budynku |
| Prompty (prompts) | użytkownik | `building_entrance` |
| Instrukcje (instructions) | serwer, raz przy połączeniu | zasady pracy z danymi kampusu |
| MCP App | host, po wywołaniu narzędzia | mapa budynku w rozmowie |

### Narzędzia

| Narzędzie | Argumenty | Co robi |
|---|---|---|
| `find_building` | `query`, np. `"Budynek A"` | Znajduje budynek i zwraca jego `buildingId`. |
| `get_building_geometry` | `buildingId` | Zwraca obrys, wejścia (z elewacją) oraz parkingi, ścieżki i punkty orientacyjne z kierunkami i odległościami. |
| `mark_entrance` | `buildingId`, `entranceId` | Zaznacza wejście na mapie w przeglądarce. |
| `show_building_map` | `buildingId`, opcjonalnie `entranceId` | Pokazuje mapę budynku w rozmowie (MCP App). |

Typowy przebieg dla pytania „wejście do budynku A od strony parkingu”:

```text
find_building("A")            → { "buildingId": "A" }
get_building_geometry("A")    → parking A-P1 na zachodzie, wejście A1 na elewacji zachodniej, ścieżka A-S1 → A1
mark_entrance("A", "A1")      → { "success": true }
```

### Zasoby

| URI | Zawartość |
|---|---|
| `tupzak://buildings` | Lista budynków z wejściami, np. `[{ "buildingId": "A", "entrances": ["A1","A2","A3"] }, …]`. |
| `tupzak://buildings/{buildingId}` | Źródłowy GeoJSON budynku, np. `tupzak://buildings/B`. |

W Claude Code zasób dołącza się przez `@`: `@tupzak:tupzak://buildings jakie masz budynki?`

### Prompt `building_entrance`

Argumenty: `name` (budynek) i `target` (obiekt w otoczeniu albo identyfikator wejścia). W Claude Code: `/mcp__tupzak__building_entrance A parking`.

Serwer zwraca gotową wiadomość użytkownika, np. dla `A parking`:

```text
Znajdź budynek „A” i wskaż jego wejście od strony obiektu „parking”.

1. Wywołaj find_building z nazwą budynku, żeby ustalić jego identyfikator.
2. Wywołaj get_building_geometry, żeby pobrać wejścia i obiekty w otoczeniu.
3. Wybierz wejście na elewacji zwróconej w stronę obiektu „parking”, które jest do niego najbliżej
   lub do którego prowadzi od niego ścieżka.
4. Jeżeli wejście jest jednoznaczne, zaznacz je narzędziem mark_entrance.
   Jeżeli nie, albo w otoczeniu nie ma takiego obiektu, nie zaznaczaj niczego i zapytaj mnie.

Odpowiedz krótko: identyfikator wejścia, elewacja i uzasadnienie na podstawie danych.
```

Gdy `target` jest wejściem tego budynku (np. `C C1`), serwer sprawdza to w danych i zwraca krótszą wiadomość:

```text
Zaznacz wejście C1 budynku C narzędziem mark_entrance
(buildingId: "C", entranceId: "C1").
```

### Instrukcje serwera

Host dostaje je przy połączeniu (`initialize`). Najważniejsze zasady:

- pytanie o listę budynków → zasób `tupzak://buildings`;
- nie wymyślać budynków, wejść ani współrzędnych;
- wejście „od strony” obiektu leży na elewacji zwróconej w jego kierunku;
- gdy opis nie wskazuje jednego wejścia, zapytać użytkownika zamiast zaznaczać;
- prośba o mapę → `show_building_map`.

### MCP App

Narzędzie `show_building_map` wskazuje widok `ui://tupzak/building-map.html`. Host pobiera ten HTML, wyświetla go w rozmowie i przekazuje mu wynik narzędzia, a widok rysuje GeoJSON jako SVG. Działa w Claude Desktop i claude.ai. Claude Code pokazuje tylko tekst wyniku.

## Mapa w Claude Desktop

1. Uruchom tunel: `cloudflared tunnel --url http://localhost:5144`.
2. W *Settings → Connectors → Add custom connector* podaj adres tunelu z dopiskiem `/mcp`.
3. Zapytaj: „Pokaż mapę budynku B z wejściem B1”.

Jeśli mapa się nie pojawia, zamknij Claude Desktop przez ⌘Q i uruchom go ponownie.
