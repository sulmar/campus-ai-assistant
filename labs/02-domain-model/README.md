# Model dziedziny w C#

Kod zapisuje [diagram klas](../../docs/domain-model.md) w C#. Nie ma tu API, bazy danych ani AI, dzięki czemu widać samo przejście od pojęcia do klasy.

## Uruchomienie testów

```bash
dotnet test CampusAssistant.DomainModel.slnx
```

## Od pojęcia do klasy

Dokumenty są po polsku, a kod po angielsku:

| Pojęcie | Klasa |
|---|---|
| Kampus | `Campus` |
| Miejsce | `Place` |
| Budynek | `Building` |
| Sala | `Room` |
| Automat z kawą | `CoffeeMachine` |
| Dostępność | `Availability` |
| Lokalizacja | `Location` |
| Trasa | `Route` |
| Student | brak klasy |

Student na diagramie ma tylko zależności, bez atrybutów i operacji, więc w modelu nie ma dla niego nic do zapisania.

## Od diagramu do C#

| Element diagramu | Konstrukcja C# | Przykład |
|---|---|---|
| Klasa abstrakcyjna | `abstract class` | `Place` |
| Dziedziczenie | Klasa pochodna | `Room : Place` |
| Obiekt wartości | `record` (równość po wartości) | `Location` |
| Wyliczenie | `enum` | `Availability` |
| Krotność `1` | Wymagany parametr konstruktora | `Route.Start` |
| Krotność `0..1` | Typ nullable | `CoffeeMachine.Building` |
| Krotność `0..*` | `IReadOnlyList<T>` | `Building.Rooms` |
| Kompozycja | Właściciel tworzy część, konstruktor części jest `internal` | `building.AddRoom(...)` |

## Od przypadku użycia do kodu

Zachowanie dodajemy pionowymi wycinkami (Vertical Slice): każdy przypadek użycia ma własny katalog w `Features/`, a klasa nosi jego nazwę. [UC-01](../../docs/use-cases/UC-01-search-place.md) to `Features/SearchPlace/`:

| Plik | Rola |
|---|---|
| `SearchPlaceQuery` | Dane wejściowe: szukana fraza |
| `SearchPlaceResult` | Możliwe wyniki przypadku użycia |
| `SearchPlaceUseCase` | Logika wyszukiwania w `Execute` |

| Krok UC-01 | Kod |
|---|---|
| 1–2. Pytanie w języku naturalnym i rozpoznanie miejsca | Poza wycinkiem: wejściem jest już fraza, np. `A101` |
| 3. Wyszukanie miejsca | `SearchPlaceUseCase.Execute` |
| 4. Prezentacja lokalizacji | `SearchPlaceResult.Found` |
| 3a. Brak wyników | `SearchPlaceResult.NotFound` |
| 3b. Kilka pasujących miejsc | `SearchPlaceResult.Ambiguous` |

Dokładna nazwa ma pierwszeństwo przed dopasowaniem częściowym. Bez tego student nie mógłby wybrać sali `A101`, gdyby istniała też `A1011`.

## Reguły spoza samego diagramu

- `Route` odrzuca jednakowy początek i cel, bo słownik mówi o drodze łączącej dwie lokalizacje.
- `Campus` nie przyjmie automatu stojącego w budynku innego kampusu.
- Domyślna dostępność automatu to `Unknown`, żeby brak danych nie wyglądał jak automat czynny (UC-03, 3a).
