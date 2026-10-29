# Tup Żak — Model Context Protocol (MCP) w .NET

Demo do wykładu **„Model Context Protocol”** w ramach przedmiotu **„Tworzenie agentów AI w .NET”**.

To kontynuacja [laboratorium 03 · Tool Calling](../03-tool-calling/README.md). Narzędzia kampusu (`find_building`, `get_building_geometry`, `mark_entrance`), dane GeoJSON i mapa Leaflet są te same. Nowe jest to, że aplikacja udostępnia narzędzia także jako **serwer MCP**. Dzięki temu z agentem Tup Żak można rozmawiać w **Claude Code**, a wybrane wejście pojawia się na żywo na mapie w przeglądarce.

> **W lab 03 pętlę agenta prowadziła aplikacja. Tutaj prowadzi ją host MCP (Claude Code), a aplikacja jest serwerem, który waliduje i wykonuje narzędzia.**

```text
                       ┌──────────────── TupZak.Demo (ASP.NET Core, :5144) ────────────────┐
 Przeglądarka ── /api/chat ──► AgentLoop ──► Ollama (qwen3)                                 │
      ▲          (NDJSON)         │                                                         │
      │                           ▼                                                         │
      │                     ToolExecutor ──► CampusTools ──► MapState ◄── /api/map          │
      │                           ▲                                                         │
      │                           │ ta sama walidacja i te same AIFunction                  │
 Claude Code ── /mcp ─────► serwer MCP (ModelContextProtocol.AspNetCore)                    │
 (host MCP)   Streamable HTTP     └──► McpActivityLog ──► /api/mcp/activity ──► przeglądarka│
                       └───────────────────────────────────────────────────────────────────┘
```

## Wymagania

- .NET SDK 10
- [Claude Code](https://claude.com/claude-code), czyli host MCP
- opcjonalnie [Ollama](https://ollama.com) z modelem `qwen3:8b`, jeżeli chcesz porównać działanie z wbudowanym Agent Loop z lab 03

## Uruchomienie

```bash
cd labs/04-mcp
dotnet run --project src/TupZak.Demo
```

Aplikacja działa pod adresem <http://localhost:5144>, a serwer MCP pod adresem <http://localhost:5144/mcp>.

Testy (nie wymagają ani modelu, ani Claude Code):

```bash
dotnet test
```

## Podłączenie Claude Code

**Sposób 1. Plik `.mcp.json` w tym katalogu (zakres projektu).** Uruchom Claude Code w `labs/04-mcp`:

```bash
cd labs/04-mcp
claude
```

Przy pierwszym uruchomieniu Claude Code zapyta, czy ufasz serwerowi `tupzak` z pliku `.mcp.json`. Po zatwierdzeniu polecenie `/mcp` pokaże serwer i jego trzy narzędzia.

**Sposób 2. Dodanie serwera ręcznie (np. w innym katalogu):**

```bash
claude mcp add --transport http tupzak http://localhost:5144/mcp
```

Następnie w Claude Code wpisz polecenie, np.:

> Znajdź budynek A i zaznacz wejście od strony parkingu, mniej więcej w połowie długości budynku.

Claude Code przy każdym wywołaniu narzędzia zapyta o zgodę, bo narzędzia mają nazwy `mcp__tupzak__find_building`, `mcp__tupzak__get_building_geometry` i `mcp__tupzak__mark_entrance`. W panelu **Przebieg wykonania** w przeglądarce pojawią się kolejne **Tool Call** i **Tool Result** z oznaczeniem klienta (`claude-code`), a na mapie wyróżnione wejście A1.

Tryb nieinteraktywny, przydatny do szybkiego sprawdzenia przed wykładem:

```bash
claude -p "Znajdź budynek A i zaznacz wejście od strony parkingu" \
  --mcp-config .mcp.json \
  --allowedTools "mcp__tupzak__find_building,mcp__tupzak__get_building_geometry,mcp__tupzak__mark_entrance"
```

## Co się zmieniło względem lab 03

| Plik | Zmiana |
|---|---|
| `Mcp/CampusMcpServer.cs` | Rejestracja serwera MCP: `AddMcpServer().WithHttpTransport()`, nazwa i instrukcje serwera oraz filtr `tools/call`. |
| `Mcp/CampusResources.cs` | Zasoby MCP: `tupzak://buildings` (lista budynków) i szablon `tupzak://buildings/{buildingId}` (GeoJSON budynku). |
| `Mcp/CampusPrompts.cs` | Prompt MCP `building_entrance` z argumentami `name` i `target`. |
| `Mcp/McpActivityLog.cs` | Dziennik wywołań przez MCP, który przeglądarka odczytuje cyklicznie. |
| `Program.cs` | `AddCampusMcpServer()`, `app.MapMcp("/mcp")`, endpoint `GET /api/mcp/activity?after={n}`. |
| `wwwroot/app.js` | Odpytywanie `/api/mcp/activity` co sekundę: wywołania pokazywane w panelu przebiegu i odświeżanie mapy. |
| `.mcp.json` | Konfiguracja serwera MCP dla Claude Code (zakres projektu). |
| `tests/.../McpServerTests.cs` | Testy przez prawdziwy transport HTTP z klientem `McpClient` z SDK. |

Wbudowany czat z Ollama z lab 03 działa bez zmian. To ułatwia porównanie obu hostów na tych samych narzędziach.

## Jak to działa

### Jedna definicja narzędzi, dwóch hostów

Narzędzia MCP powstają z **tych samych** obiektów `AIFunction`, z których korzysta `AgentLoop`:

```csharp
var function = sp.GetRequiredService<ToolExecutor>().Functions.Single(f => f.Name == name);
return McpServerTool.Create(function, CreateOptions(name));
```

Nazwa, opis i JSON Schema argumentów są więc identyczne w obu ścieżkach. Odpowiedź na `tools/list` można porównać z <http://localhost:5144/api/info>.

### Adnotacje narzędzi

MCP pozwala opisać charakter narzędzia. Host może z tego skorzystać, np. przy pytaniu o zgodę:

| Narzędzie | `title` | Adnotacje |
|---|---|---|
| `find_building` | Znajdź budynek | `readOnlyHint`, `idempotentHint` |
| `get_building_geometry` | Pobierz geometrię budynku | `idempotentHint` (ustawia fokus mapy, więc nie jest tylko do odczytu) |
| `mark_entrance` | Zaznacz wejście na mapie | `idempotentHint`, `destructiveHint: false` |

### Zasoby MCP

Narzędzia wywołuje model, a zasoby to dane tylko do odczytu, które host dołącza do kontekstu (`resources/list`, `resources/read`):

| URI | Zawartość |
|---|---|
| `tupzak://buildings` | Lista wszystkich budynków z identyfikatorami wejść (JSON). |
| `tupzak://buildings/{buildingId}` | Źródłowe dane GeoJSON budynku, np. `tupzak://buildings/B`. |

W Claude Code zasób można dołączyć do pytania przez `@`, np. `@tupzak:tupzak://buildings jakie masz budynki?`. Nieznany budynek zwraca błąd `ResourceNotFound` z listą dostępnych identyfikatorów.

### Prompty MCP

Prompt to szablon wiadomości z parametrami (`prompts/list`, `prompts/get`). Wybiera go użytkownik, a nie model:

| Prompt | Argumenty | Działanie |
|---|---|---|
| `building_entrance` | `name` (nazwa lub identyfikator budynku), `target` (obiekt w otoczeniu) | Prowadzi model przez `find_building` → `get_building_geometry` → `mark_entrance`; przy niejednoznaczności każe zapytać użytkownika. |

W Claude Code prompt jest dostępny jako polecenie, np. `/mcp__tupzak__building_entrance "Budynek A" parking`.

### Instrukcje serwera

Reguły z `SystemPrompt` z lab 03 (nie zgadywać, pytać przy niejednoznaczności, nie wymyślać identyfikatorów) trafiają do hosta jako `instructions` w odpowiedzi na `initialize`. Nie ma tu już system promptu aplikacji, bo prompt należy do hosta. Serwer może jedynie **zasugerować** zasady.

### Walidacja zostaje po stronie aplikacji

Filtr `tools/call` w `CampusMcpServer.LogAndValidate` wywołuje tę samą metodę `ToolExecutor.ValidateArguments` co Agent Loop. Błędny argument (np. pusty `query`) wraca do hosta jako wynik z `isError: true` i opisem błędu, a nie jako wyjątek protokołu. Błędy domenowe, np. „Wejście B1 należy do budynku B”, wracają w treści wyniku tak samo jak w lab 03.

### Wspólny stan mapy

Serwer MCP działa w tym samym procesie co aplikacja webowa (transport **Streamable HTTP**), więc `mark_entrance` wywołane przez Claude Code zmienia ten sam `MapState`, który rysuje przeglądarka. Przy transporcie **stdio** Claude Code uruchomiłby osobny proces z własnym stanem i mapa w przeglądarce by się nie zmieniła.

## Prowadzenie demonstracji na wykładzie

**Przed wykładem**

1. `dotnet run --project src/TupZak.Demo` i otwórz <http://localhost:5144>.
2. W drugim oknie terminala: `cd labs/04-mcp && claude`, zatwierdź serwer `tupzak` i sprawdź `/mcp`.
3. Przećwicz scenariusz 1 i kliknij **Wyczyść** w przeglądarce.

**Przebieg (ok. 15 minut)**

1. **Przypomnienie lab 03.** Kto prowadzi pętlę? `AgentLoop.cs`. Kto wybiera narzędzia? Model w Ollama.
2. **Serwer MCP.** Pokaż `CampusMcpServer.cs`: kilka linii rejestracji, `McpServerTool.Create(AIFunction)` i `MapMcp("/mcp")`. Narzędzia się nie zmieniły.
3. **Odkrywanie narzędzi.** W Claude Code wpisz `/mcp`: host sam pobrał `tools/list`, nazwy, opisy i schematy.
4. **Scenariusz 1 w Claude Code.** Zatwierdzaj kolejne wywołania. Każde od razu pojawia się w panelu przebiegu w przeglądarce z etykietą `MCP · klient: claude-code`, a A1 na mapie.
5. **Walidacja.** „Zaznacz w budynku A wejście B1”: czerwony Tool Result, a host przekazuje błąd użytkownikowi.
6. **Scenariusz 2 („z tyłu”).** Porównaj z `qwen3:8b`: instrukcje serwera są te same, a zachowanie zależy od modelu hosta.
7. **Pytanie do sali:** co musiałoby się zmienić, żeby z tego samego serwera korzystał inny host MCP, np. VS Code albo własny klient `McpClient`? Odpowiedź: nic po stronie serwera.

**Gdy coś pójdzie nie tak**

- `/mcp` w Claude Code pokazuje `failed`: sprawdź, czy aplikacja działa na porcie 5144. Uruchomienie aplikacji po starcie Claude Code wymaga ponownego połączenia przez `/mcp` → *Reconnect*.
- Wywołania nie pojawiają się w przeglądarce: odśwież stronę, bo dziennik jest odpytywany od bieżącego numeru kolejnego.

## Pomysły na zadania laboratoryjne

- Dodać do serwera **prompty MCP** (np. trzy scenariusze demonstracyjne), widoczne w Claude Code jako polecenia `/mcp__tupzak__…`.
- Dodać powiadomienie `notifications/resources/updated` dla zasobu ze stanem mapy (np. `tupzak://map`).
- Napisać własnego hosta: `McpClient` + `IChatClient` (Ollama), który pobiera narzędzia przez MCP zamiast `AIFunctionFactory`.

## Autor i licencja

Patrz [README](../../README.md) w katalogu głównym repozytorium.
