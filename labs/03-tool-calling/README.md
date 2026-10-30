# Tup Żak — demonstracja Tool Calling w .NET

Demo do wykładu **„Tool Calling”** w ramach przedmiotu **„Tworzenie agentów AI w .NET”**.

Agent Tup Żak dostaje polecenie w języku naturalnym, np.:

> Znajdź budynek A i zaznacz wejście od strony parkingu.

Agent sam decyduje, których narzędzi użyć: znajduje budynek, pobiera jego geometrię z pliku GeoJSON, na tej podstawie wybiera wejście i zaznacza je na mapie Leaflet.

> **Model nie wykonuje funkcji. Model proponuje jej wywołanie, a aplikacja kontroluje wykonanie.**

LLM odpowiada za interpretację polecenia i wybór narzędzi. Aplikacja .NET odpowiada za walidację, wykonanie i wizualizację.

## Wymagania

- .NET SDK 10
- [Ollama](https://ollama.com) z modelem obsługującym Tool Calling (domyślnie `qwen3:8b`) albo klucz API [Gemini](https://ai.google.dev/gemini-api/docs/api-key)

```bash
ollama pull qwen3:8b
```

Demo nie korzysta z internetu: nie używa OSM, Nominatim ani Overpass. Leaflet jest dołączony lokalnie w `wwwroot/lib/leaflet`.

## Uruchomienie

```bash
cd labs/03-tool-calling
dotnet build
dotnet run --project src/TupZak.Demo
```

Aplikacja działa pod adresem <http://localhost:5144>.

Testy jednostkowe:

```bash
dotnet test
```

## Konfiguracja modelu

Plik `src/TupZak.Demo/appsettings.json`:

```json
{
  "Ollama": {
    "Endpoint": "http://localhost:11434",
    "Model": "qwen3:8b",
    "Think": true
  },
  "Agent": {
    "MaxIterations": 6,
    "TimeoutSeconds": 180,
    "Temperature": 0,
    "Seed": 42
  },
  "DataDirectory": "../../data"
}
```

| Ustawienie | Znaczenie |
|---|---|
| `Ollama:Endpoint` | Adres serwera Ollama. |
| `Ollama:Model` | Model obsługujący Tool Calling. |
| `Ollama:Think` | Tryb „thinking” (np. dla qwen3). `true` daje lepsze rozumowanie przestrzenne, ale wydłuża odpowiedź. |
| `Agent:MaxIterations` | Maksymalna liczba zapytań do modelu w jednej turze (limit Agent Loop). |
| `Agent:TimeoutSeconds` | Limit czasu na całą turę. |
| `Agent:Temperature`, `Agent:Seed` | Ustawienia zwiększające powtarzalność odpowiedzi. |

Każde ustawienie można nadpisać zmienną środowiskową, bez edycji pliku:

```bash
Ollama__Model=qwen3:14b Ollama__Think=false dotnet run --project src/TupZak.Demo
```

### Wybór dostawcy modelu

Dostawcę i model wybiera się parametrami uruchomienia, a nie w `appsettings.json`:

```bash
dotnet run --project src/TupZak.Demo                                                # Ollama, model z appsettings.json
dotnet run --project src/TupZak.Demo -- --provider gemini                           # Gemini, model gemini-3.8-flash
dotnet run --project src/TupZak.Demo -- --provider gemini --model gemini-3.6-flash  # Gemini, wskazany model
```

| Parametr / zmienna | Znaczenie |
|---|---|
| `--provider` | `ollama` (domyślnie) albo `gemini`. |
| `--model` | Model wybranego dostawcy. Nadpisuje `Ollama:Model` i domyślny model Gemini. |
| `MODEL_API_KEY` | Zmienna środowiskowa ze wspólnym kluczem API. Wymagana dla Gemini. Dla Ollamy opcjonalna: lokalny serwer jej nie potrzebuje, a ustawiona trafia do nagłówka `Authorization: Bearer`. |

Klucza nie podaje się jako parametru, bo wartości z wiersza poleceń trafiają do historii powłoki i listy procesów.

> Przy Gemini treść rozmowy i wyniki narzędzi są wysyłane do chmury Google. Limity i warunki przetwarzania danych zależą od planu. Ollama działa lokalnie.

Integracja z Ollama korzysta z pakietu [OllamaSharp](https://github.com/awaescher/OllamaSharp), a integracja z Gemini z oficjalnego pakietu [Google.GenAI](https://github.com/googleapis/dotnet-genai). Oba udostępniają `IChatClient` z `Microsoft.Extensions.AI`, a `AgentLoop` zna tylko ten interfejs. Klienta tworzy fabryka z `Agent/ChatClientFactory.cs`: nowy dostawca to jedna nowa klasa `IChatClientFactory` i jeden przypadek w `ChatClientFactory.Select`.

## Struktura projektu

```text
labs/03-tool-calling/
├── README.md
├── TupZak.ToolCalling.slnx
├── data/                         # fikcyjne budynki w GeoJSON
│   ├── building-a.geojson
│   ├── building-b.geojson
│   └── building-c.geojson
├── src/TupZak.Demo/
│   ├── Program.cs                # konfiguracja, rejestracja IChatClient, endpointy HTTP
│   ├── Agent/
│   │   ├── AgentLoop.cs          # pętla Tool Calling (napisana ręcznie)
│   │   ├── ChatClientFactory.cs  # fabryki IChatClient: Ollama, Gemini
│   │   ├── ToolExecutor.cs       # walidacja argumentów i wykonanie narzędzi
│   │   ├── AgentSession.cs       # historia rozmowy, timeout, czyszczenie
│   │   ├── SystemPrompt.cs
│   │   ├── AgentEvent.cs         # zdarzenia przebiegu wysyłane do UI
│   │   └── AgentOptions.cs
│   ├── Tools/
│   │   └── CampusTools.cs        # find_building, get_building_geometry, mark_entrance
│   ├── Services/
│   │   ├── BuildingRepository.cs # wczytanie i walidacja GeoJSON
│   │   ├── Geometry.cs           # kierunki, odległości, elewacje
│   │   └── MapState.cs           # stan wizualizacji
│   └── wwwroot/                  # HTML + CSS + JS, Leaflet
└── tests/TupZak.Demo.Tests/      # testy xUnit
```

## Dane demonstracyjne

Budynki są **fikcyjne** i nie przedstawiają rzeczywistego kampusu PW. Współrzędne są zapisane w lokalnym układzie w metrach: `x` rośnie na wschód, `y` na północ. Leaflet wyświetla je w układzie `L.CRS.Simple`, bez podkładu mapowego.

| Budynek | Kształt | Wejścia | Otoczenie |
|---|---|---|---|
| A | prostokąt 24 × 60 m | A1 (zachód), A2 (południe), A3 (wschód) | parking od zachodu, ścieżki, przystanek od wschodu |
| B | litera U otwarta na północ | B1 (południe), B2 (dziedziniec), B3 (zachód), B4 (wschód) | dziedziniec z fontanną, parking od wschodu, ścieżki |
| C | litera L | C1 (południe), C2 (wewnętrzny narożnik), C3 (północ), C4 (wschód) | parking w „kolanie” litery L, ścieżki, stojaki rowerowe, przystanek |

Każdy obiekt w GeoJSON ma właściwości `id`, `kind` (`building`, `entrance`, `parking`, `path`, `courtyard`, `landmark`) i `name`. Wejścia mają też `buildingId`.

Dane **nie zawierają gotowych odpowiedzi**, takich jak `correctEntrance`. Identyfikatory wejść są neutralne. Przy starcie aplikacja sprawdza poprawność plików: zamknięte pierścienie, typy geometrii, unikalne identyfikatory oraz to, czy wejścia leżą na obrysie własnego budynku.

## Narzędzia

Narzędzia to zwykłe metody C# w `Tools/CampusTools.cs`. `AIFunctionFactory.Create` tworzy z nich **Tool Definitions**: nazwę, opis i **JSON Schema** argumentów, wygenerowane z sygnatury metody i atrybutów `[Description]`. Definicje można podejrzeć pod adresem <http://localhost:5144/api/info>.

### `find_building(query)`

Szuka budynku po identyfikatorze lub nazwie („A”, „budynek A”, „Budynek A”).

- Wynik: `found`, `buildingId`, `name`.
- Gdy budynku nie ma: `found: false`, `error` oraz lista `availableBuildings`.

### `get_building_geometry(buildingId)`

Zwraca dane potrzebne do rozumowania przestrzennego:

- `outline`: obrys (współrzędne), środek ciężkości, prostokąt ograniczający;
- `entrances`: identyfikator, współrzędne, `facade` (kierunek, w którym zwrócona jest ściana z wejściem) oraz `nearby`, czyli odległości do parkingów, ścieżek i punktów orientacyjnych;
- `parkings`, `paths`, `landmarks`: geometria, środek, `directionFromBuilding`, `distanceToBuildingM`, a dla ścieżek `connectedEntrances`.

Aplikacja liczy **fakty** (kierunki, odległości). O tym, które wejście pasuje do opisu użytkownika, decyduje **model**.

### `mark_entrance(buildingId, entranceId)`

Sprawdza, czy budynek istnieje i czy wejście do niego należy. Dopiero wtedy aktualizuje stan mapy. Przyjmuje wyłącznie identyfikatory, a nie współrzędne wygenerowane przez model. Przy błędzie zwraca opis problemu, np. „Wejście B1 należy do budynku B, a nie do budynku A”, oraz listę dostępnych wejść.

## Agent Loop

`Agent/AgentLoop.cs` celowo **nie** używa `FunctionInvokingChatClient`, tylko implementuje pętlę ręcznie, żeby każdy krok był widoczny:

```text
┌─► 1. wyślij do modelu: system prompt + historia + Tool Definitions
│   2. odczytaj odpowiedź: czy zawiera żądania Tool Call (FunctionCallContent)?
│        └─ nie → odpowiedź końcowa, koniec
│   3. dla każdego Tool Call: sprawdź nazwę narzędzia i argumenty (JSON Schema)
│   4. wykonaj metodę C#
│   5. dołącz Tool Results (FunctionResultContent) powiązane przez CallId
└── 6. kolejna iteracja (do limitu MaxIterations lub timeoutu)
```

- Model może zwrócić **kilka Tool Calls w jednej odpowiedzi**. Każdy wynik trafia do modelu z tym samym `CallId` co wywołanie.
- **Walidacja** (`ToolExecutor`) odrzuca nieznane narzędzia, brakujące lub nadmiarowe argumenty, puste wartości i niewłaściwe typy. Błąd nie przerywa pętli: wraca do modelu jako Tool Result, a model może zareagować.
- Po przekroczeniu **limitu czasu** lub przerwaniu tury niedokończona tura jest usuwana z historii, żeby nie zostawiać w niej Tool Calls bez Tool Results.
- W kodzie nie ma sztywnego scenariusza. To model decyduje, czy i które narzędzia wywołać.

Przebieg jest wysyłany do przeglądarki na żywo jako strumień NDJSON (`POST /api/chat`).

## Interfejs

Układ jest przygotowany pod projektor 1920 × 1080.

1. **Rozmowa z agentem**: polecenia i odpowiedzi.
2. **Tool Calling — przebieg wykonania**: kolejne iteracje modelu (czas, tokeny, opcjonalnie rozumowanie w trybie thinking), **Tool Call** (pomarańczowy: nazwa, argumenty JSON, `CallId`) i **Tool Result** (zielony albo czerwony przy błędzie: wynik JSON, czas wykonania).
3. **Mapa**: obrysy, parkingi, dziedziniec, ścieżki i wszystkie wejścia. Wejście wybrane przez agenta jest wyróżnione pulsującym czerwonym znacznikiem.

Przycisk **Wyczyść** usuwa rozmowę, przebieg i oznaczenie na mapie. Przerywa też trwającą turę.

## Scenariusze demonstracyjne

Dostępne jako przyciski w nagłówku i do uruchamiania wielokrotnie.

| # | Polecenie | Oczekiwany rezultat |
|---|---|---|
| 1 | Znajdź budynek A i zaznacz wejście od strony parkingu, mniej więcej w połowie długości budynku. | `find_building` → `get_building_geometry` → `mark_entrance(A, A1)`. Wybór wynika z geometrii: parking leży na zachodzie, A1 jest na zachodniej elewacji, 12 m od parkingu, i prowadzi do niego ścieżka. |
| 2 | Zaznacz wejście z tyłu budynku A. | Agent nie wywołuje `mark_entrance`, tylko wymienia wejścia i prosi o doprecyzowanie, bo dane nie określają, gdzie jest „tył”. |
| 3 | Znajdź wejście do budynku D. | `find_building` zwraca `found: false`, a agent informuje, że budynku nie ma, bez wymyślania geometrii. |

Dodatkowe polecenia na laboratoria:

- „Znajdź budynek B i zaznacz wejście od strony dziedzińca” (oczekiwane B2),
- „Zaznacz wejście do budynku C od strony parkingu” (oczekiwane C2),
- „Zaznacz w budynku A wejście B1” (walidacja: wejście innego budynku).

## Wyniki testów z rzeczywistym modelem

Testy wykonane 2026-10-22 na Apple M2 Max (32 GB) z Ollama 0.40.8 i modelem `qwen3:8b` (Q4_K_M).

| Scenariusz | `Think: false` | `Think: true` (domyślnie) |
|---|---|---|
| 1 — budynek A, parking | ✗ wybiera A2 lub A3 (0/5 poprawnych), ok. 5 s | ✓ A1 (7/7 prób), 35–65 s |
| 2 — „z tyłu” | ✗ zgaduje A2 (0/2 poprawnych) | niestabilny: dopytuje w 3/6 prób, w pozostałych zgaduje A1 |
| 3 — budynek D | ✓ (2/2 próby), ok. 3 s | ✓ (2/2 próby), ok. 7 s |
| Walidacja — „w budynku A wejście B1” | — | ✓ błąd z `mark_entrance` przekazany użytkownikowi (2/2 próby), ok. 6 s |
| B — dziedziniec | — | ✓ B2 (1/1 próba), ok. 25 s |
| C — parking | — | ✗ wybrał C4 i przekroczył ówczesny limit 120 s (0/1 poprawnych) |

Wnioski:

- Mechanika Tool Calling (wybór narzędzi, sekwencja wywołań, przekazywanie wyników, obsługa błędów) działa poprawnie we wszystkich próbach.
- **Jakość rozumowania przestrzennego** zależy od modelu. Model 8B bez trybu thinking nie radzi sobie z wyborem wejścia, a z thinkingiem jest poprawny w scenariuszu 1, ale niestabilny w scenariuszu 2 i w budynku C.
- Większy model (np. `qwen3:14b`, `qwen3:30b`) powinien poprawić skuteczność, ale nie był testowany. Model zmienia się w konfiguracji, bez zmian w kodzie.
- Niestabilność scenariusza 2 to dobry materiał dydaktyczny: pokazuje, że prompt nie gwarantuje zachowania modelu, a twarde reguły (walidacja identyfikatorów) muszą być po stronie aplikacji.

Testy jednostkowe (`dotnet test`) nie wymagają modelu. Sprawdzają dane GeoJSON, geometrię, logikę narzędzi i walidację, a mechanikę Agent Loop testują z zaprogramowanym klientem `IChatClient`.

## Prowadzenie demonstracji na wykładzie

**Przed wykładem**

1. Uruchom Ollama i „rozgrzej” model, żeby pierwsze zapytanie nie czekało na jego załadowanie:
   `ollama run qwen3:8b "ok" --think=false`.
2. Uruchom aplikację i otwórz <http://localhost:5144> w trybie pełnoekranowym (F11).
3. Przećwicz trzy scenariusze i kliknij **Wyczyść**.

**Przebieg (ok. 15 minut)**

1. **Tool Definition i JSON Schema.** Pokaż <http://localhost:5144/api/info> i metody w `CampusTools.cs`. Model dostaje tylko nazwę, opis i schemat, a nie kod.
2. **Scenariusz 1.** Śledź panel środkowy:
   - *MODEL · iteracja 1* → **Tool Call** `find_building`: model *proponuje* wywołanie;
   - **Tool Result**: wynik wykonany przez aplikację (czas w ms) wraca do modelu;
   - kolejne iteracje to **Sequential Tool Calling**: model sam decyduje o następnym kroku;
   - rozwiń „Rozumowanie modelu”, żeby pokazać, jak model interpretuje „od strony parkingu” na podstawie `facade`, `directionFromBuilding` i `nearby`;
   - pokaż na mapie wyróżnione A1.
3. **Agent Loop.** Pokaż `AgentLoop.cs`: pętla, `FunctionCallContent`, `FunctionResultContent`, `CallId`, `MaxIterations`.
4. **Scenariusz 3 — obsługa błędów.** Narzędzie zwraca `found: false`, a model przekazuje to użytkownikowi zamiast wymyślać dane.
5. **Walidacja argumentów.** Wpisz „Zaznacz w budynku A wejście B1” i pokaż czerwony Tool Result oraz `ToolExecutor.cs`.
6. **Scenariusz 2 — niejednoznaczność.** Uruchom kilka razy. Jeśli model zgadnie, omów, dlaczego reguła w prompcie to tylko sugestia i co aplikacja może (i czego nie może) wymusić.
7. **Pytanie do sali:** jaki jest jedyny moment, w którym model wpływa na stan aplikacji? Odpowiedź: tylko przez wywołanie narzędzia, które aplikacja sprawdziła i wykonała.

**Gdy coś pójdzie nie tak**

- „Brak połączenia z modelem”: sprawdź `ollama serve` i `Ollama:Endpoint`.
- Długie oczekiwanie: włączony thinking wydłuża każdą iterację. Ustaw `Ollama__Think=false`, żeby pokazać samą mechanikę (wybór wejścia będzie wtedy słabszy).
- Przycisk **Wyczyść** przerywa trwającą turę.

## Autor i licencja

Patrz [README](../../README.md) w katalogu głównym repozytorium.
