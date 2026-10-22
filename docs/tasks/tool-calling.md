# Zadanie: Tup Żak — demonstracja Tool Calling w .NET

## Kontekst

Tworzysz demonstrację na wykład **„Tool Calling”** w ramach przedmiotu **„Tworzenie agentów AI w .NET”**.

Projekt referencyjny kursu nazywa się **Tup Żak** — agent pomagający studentom poruszać się po kampusie.

Na wcześniejszych zajęciach studenci analizowali nieustrukturyzowane notatki kurierów za pomocą modeli językowych. Teraz chcemy pokazać, jak model może wykorzystać narzędzia do rozwiązania problemu wymagającego dostępu do danych zewnętrznych.

Demo będzie prezentowane na żywo podczas wykładu. Musi być **proste, czytelne, powtarzalne i niezawodne**.

## Cel demonstracji

Użytkownik przekazuje agentowi polecenie:

> Znajdź budynek A i zaznacz wejście od strony parkingu.

Agent powinien:

1. Znaleźć budynek.
2. Pobrać jego geometrię z pliku GeoJSON.
3. Zinterpretować opis użytkownika w kontekście geometrii.
4. Wybrać właściwe wejście spośród istniejących wejść.
5. Wywołać narzędzie zaznaczające wejście na mapie.
6. Odpowiedzieć użytkownikowi.

LLM odpowiada za interpretację opisu i wybór narzędzi. Aplikacja .NET odpowiada za ich wykonanie, walidację i wizualizację.

## Wymagania technologiczne

- .NET 10 / C#
- ASP.NET Core
- `Microsoft.Extensions.AI`
- Ollama jako domyślny lokalny dostawca modelu obsługującego Tool Calling
- Leaflet do wizualizacji mapy
- GeoJSON jako format danych przestrzennych
- Brak zewnętrznych baz danych
- Brak zależności od usług OSM, Nominatim i Overpass podczas demonstracji

Jeżeli używana wersja bibliotek wymaga innego sposobu integracji z Ollama, zastosuj aktualne, oficjalnie wspierane API. Nie zakładaj istnienia metod bez sprawdzenia ich sygnatur.

Model i adres Ollama powinny być konfigurowalne.

> [!IMPORTANT]
> Nie używaj MCP. To temat kolejnego wykładu.

## Dane demonstracyjne

Przygotuj trzy fikcyjne budynki kampusu.

### Budynek A — prosty

- Prostokątny obrys.
- Parking po zachodniej stronie.
- Trzy wejścia: `A1`, `A2`, `A3`.
- Wejście `A1` znajduje się od strony parkingu.
- Wejście `A2` znajduje się od południa.
- Wejście `A3` znajduje się od wschodu.

### Budynek B — dziedziniec

- Budynek o kształcie U.
- Dziedziniec wewnętrzny.
- Co najmniej trzy wejścia.
- Jedno wejście od strony dziedzińca.

### Budynek C — nieregularny

- Budynek w kształcie L.
- Parking i ścieżki piesze.
- Co najmniej trzy wejścia.
- Różne relacje przestrzenne między wejściami a punktami orientacyjnymi.

### Format danych

Dane zapisz w trzech osobnych plikach GeoJSON.

Każdy plik powinien zawierać obrys budynku, wejścia jako punkty z identyfikatorami, parkingi i ścieżki.

Geometrie powinny być poprawne i łatwe do wizualizacji.

Nie zapisuj w danych gotowych odpowiedzi, takich jak `correctEntrance`, `entranceFromParking` czy `expectedResult`.

Identyfikatory wejść powinny być neutralne. Model ma wywnioskować właściwe wejście na podstawie położenia obiektów.

Dane mogą wykorzystywać lokalny, uproszczony układ współrzędnych, jeżeli zostanie on poprawnie obsłużony przez Leaflet. Nie przedstawiaj fikcyjnych geometrii jako rzeczywistych danych kampusu PW.

## Narzędzia Tool Calling

Zaimplementuj trzy narzędzia.

### 1. `find_building`

**Argument:**

- `query` — nazwa lub identyfikator budynku.

**Wynik:**

- `buildingId`
- `name`
- informacja o znalezieniu budynku lub błąd

### 2. `get_building_geometry`

**Argument:**

- `buildingId`

**Wynik:**

- geometria budynku,
- wejścia wraz z identyfikatorami i współrzędnymi,
- parkingi,
- ścieżki i punkty orientacyjne.

Zwróć dane wystarczające do przestrzennego rozumowania. Nie ograniczaj wyniku do samej listy identyfikatorów.

### 3. `mark_entrance`

**Argumenty:**

- `buildingId`
- `entranceId`

**Działanie:**

- sprawdza, czy budynek istnieje,
- sprawdza, czy wejście należy do wskazanego budynku,
- aktualizuje stan wizualizacji,
- zwraca informację o powodzeniu albo błąd.

Nie przyjmuj dowolnych współrzędnych wygenerowanych przez LLM. Model powinien wybierać istniejący identyfikator wejścia.

## Agent Loop

Zaimplementuj rzeczywistą pętlę Tool Calling.

Aplikacja powinna:

1. Wysłać wiadomość użytkownika i definicje narzędzi do modelu.
2. Odczytać żądania Tool Call.
3. Zweryfikować nazwy narzędzi i argumenty.
4. Wykonać odpowiednie funkcje C#.
5. Przekazać Tool Results do modelu.
6. Powtarzać proces aż do uzyskania odpowiedzi końcowej albo osiągnięcia limitu iteracji.

Obsłuż wiele wywołań narzędzi w jednej odpowiedzi modelu, w tym ich identyfikatory i powiązanie z wynikami.

Ustaw konfigurowalny limit iteracji i timeout.

Nie implementuj sztywnego scenariusza, który zawsze wykonuje trzy narzędzia. To model ma decydować, czy i kiedy ich użyć.

## Interfejs demonstracyjny

Przygotuj prostą aplikację webową z trzema obszarami.

### 1. Rozmowa z agentem

- Pole wpisywania polecenia.
- Historia wiadomości.
- Odpowiedzi agenta.

### 2. Tool Calling — przebieg wykonania

Pokazuj czytelnie:

- nazwę wywoływanego narzędzia,
- argumenty JSON,
- wynik narzędzia,
- czas wykonania,
- ewentualny błąd.

Rozróżnij wizualnie Tool Call od Tool Result.

### 3. Mapa Leaflet

- Obrys budynku.
- Parking i ścieżki.
- Wszystkie dostępne wejścia.
- Wyraźne wyróżnienie wejścia wybranego przez agenta.

### Wymagania ogólne

Interfejs powinien dobrze wyglądać na projektorze, przy rozdzielczości 1920 × 1080. Użyj czytelnych fontów i prostego układu.

Dodaj możliwość wyczyszczenia rozmowy, logów i oznaczenia na mapie.

Nie jest potrzebny system logowania ani rozbudowany frontend.

## Scenariusze demonstracyjne

Dodaj trzy przyciski z gotowymi poleceniami, które można uruchamiać wielokrotnie.

### Scenariusz 1 — poprawne rozpoznanie

> Znajdź budynek A i zaznacz wejście od strony parkingu, mniej więcej w połowie długości budynku.

**Oczekiwany rezultat:** agent wybiera `A1` na podstawie geometrii.

### Scenariusz 2 — niejednoznaczność

> Zaznacz wejście z tyłu budynku A.

**Oczekiwany rezultat:** agent prosi o doprecyzowanie, jeżeli nie ma wystarczających informacji, aby jednoznacznie wskazać wejście.

Nie powinien zgadywać.

### Scenariusz 3 — nieistniejący budynek

> Znajdź wejście do budynku D.

**Oczekiwany rezultat:** narzędzie zgłasza brak budynku, a agent informuje o tym użytkownika.

Nie powinien wymyślać geometrii ani wejść.

## Wymagania dydaktyczne

Demo powinno pozwalać prowadzącemu wyjaśnić następujące pojęcia:

- Tool Definition
- JSON Schema
- Tool Call
- Tool Result
- Agent Loop
- Sequential Tool Calling
- Tool Selection
- Walidacja argumentów
- Obsługa błędów

Najważniejszy przekaz:

> **Model nie wykonuje funkcji. Model proponuje jej wywołanie, a aplikacja kontroluje wykonanie.**

Kod powinien być prosty i czytelny, bez nadmiernej abstrakcji i niepotrzebnych warstw architektonicznych.

## Struktura projektu

Zaproponuj i utwórz strukturę podobną do:

```text
labs/03-tool-calling/
├── README.md
├── TupZak.ToolCalling.slnx
├── src/
│   └── TupZak.Demo/
│       ├── Program.cs
│       ├── Agent/
│       ├── Tools/
│       ├── Services/
│       └── wwwroot/
└── data/
    ├── building-a.geojson
    ├── building-b.geojson
    └── building-c.geojson
```

Dostosuj strukturę do konwencji istniejącego repozytorium.

## Weryfikacja

Po implementacji:

1. Zbuduj rozwiązanie poleceniem `dotnet build`.
2. Uruchom aplikację.
3. Zweryfikuj poprawność trzech plików GeoJSON.
4. Sprawdź wyświetlanie geometrii w Leaflet.
5. Przetestuj walidację identyfikatorów budynków i wejść.
6. Przetestuj wszystkie trzy scenariusze z rzeczywistym modelem, jeśli Ollama jest dostępna.
7. Dodaj testy jednostkowe logiki narzędzi i obsługi błędów.

> [!WARNING]
> Jeżeli Ollama nie jest dostępna, nie symuluj pomyślnego testu Tool Calling. Wyraźnie wskaż, które elementy zostały przetestowane, a które wymagają uruchomienia modelu.

## Dokumentacja

Przygotuj `README.md` zawierający:

- cel demonstracji,
- wymagania,
- sposób uruchomienia,
- konfigurację modelu,
- opis trzech narzędzi,
- trzy scenariusze demonstracyjne,
- krótkie omówienie Agent Loop,
- instrukcję prowadzenia demonstracji na wykładzie.

## Sposób realizacji

Najpierw przeanalizuj strukturę istniejącego repozytorium i zaproponuj minimalną architekturę zgodną z jego konwencjami.

Następnie zaimplementuj kompletne, uruchamialne demo.

Nie zatrzymuj się na pseudokodzie ani szkielecie z pustymi metodami.

Unikaj niepotrzebnych zależności i dodatkowej infrastruktury.

**Priorytety:**

1. Poprawny, rzeczywisty Tool Calling.
2. Powtarzalność demonstracji.
3. Czytelna wizualizacja wywołań.
4. Prostota kodu.
5. Możliwość wykorzystania projektu podczas laboratoriów.

Na końcu przedstaw krótkie podsumowanie implementacji, instrukcję uruchomienia i wyniki testów.
