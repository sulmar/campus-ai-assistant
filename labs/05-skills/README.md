# Tup Żak — optymalizacja dostaw (przygotowanie do Skills)

Kontynuacja [lab 04](../04-mcp/README.md): ten sam serwer MCP kampusu, rozszerzony o **planowanie dostaw kurierskich do wielu budynków**.

> „Startuję z parkingu P1. Mam dostarczyć paczki do budynków A, B i C. Zaplanuj optymalną trasę.”

To podstawa do porównania trzech wariantów rozwiązania tego samego problemu:

| Wariant | Kto liczy kolejność | Narzędzia MCP |
|---|---|---|
| 1. Gotowe narzędzie | aplikacja (C#) | `optimize_deliveries` |
| 2. Agent Skill | skrypt dostarczony ze Skillem | `get_distance_matrix` → skrypt → `show_delivery_plan` |
| 3. Code Execution | kod wygenerowany przez agenta | `get_campus_graph` → kod → `show_delivery_plan` |

Działają warianty 1 i 2 (Skill [`delivery-route`](.claude/skills/delivery-route/SKILL.md)). Wariant 3 ma już przygotowane dane (`get_campus_graph`) i wspólną wizualizację (`show_delivery_plan`); Code Execution dodamy na kolejnych zajęciach.

## Uruchomienie

```bash
dotnet run --project src/TupZak.Demo   # http://localhost:5144, MCP: /mcp
dotnet test                            # testy, bez modelu
```

W tym katalogu uruchom `claude` i zatwierdź serwer `tupzak` z `.mcp.json`. Potem zapytaj:

> Startuję z parkingu P1. Mam dostarczyć paczki do budynków A, B i C. Zaplanuj optymalną trasę.

„P1” pasuje do trzech parkingów (`A-P1`, `B-P1`, `C-P1`), więc agent powinien dopytać, o który chodzi. Trasa pojawi się na mapie w przeglądarce (http://localhost:5144), a w Claude Desktop także w rozmowie, jako MCP App.

## Skill `delivery-route` (wariant 2)

```text
.claude/skills/delivery-route/
├── SKILL.md                      # kiedy użyć, kroki, warunki zatrzymania, kryterium sukcesu
├── references/answer-format.md   # format odpowiedzi dla kuriera (czytany na końcu)
└── scripts/optimize_route.py     # permutacje kolejności × wejścia budynków, odległości z get_distance_matrix
```

Claude Code znajduje Skill sam, gdy uruchomisz `claude` w tym katalogu. Na pytanie o trasę dostaw ładuje `SKILL.md` i prowadzi kuriera przez kroki:

1. `find_place` — przy „P1” dopyta, o który parking chodzi.
2. `get_distance_matrix` z punktem startowym i budynkami.
3. Skrypt `optimize_route.py` — wynik JSON trafia na wejście skryptu.
4. `show_delivery_plan` z kolejnością wejść policzoną przez skrypt.

Skrypt to rozwinięcie przykładu z permutacjami. Słownik odległości wypełnia wynik z grafu kampusu, a dla każdego budynku skrypt sprawdza wszystkie jego wejścia (`itertools.product`). Można go uruchomić samodzielnie:

```bash
python3 .claude/skills/delivery-route/scripts/optimize_route.py --start A-P1 --deliveries A B C < macierz.json
```

Na danych demonstracyjnych skrypt daje te same kolejności i długości co `optimize_deliveries`. Różni się tylko sposób wyboru wejść: skrypt sprawdza wszystkie kombinacje (288 tras dla 3 budynków), a aplikacja programowaniem dynamicznym (6 kolejności).

Dla porównania wariantów zapytaj w Claude Code najpierw ze Skillem, a potem wprost: „Użyj optimize_deliveries…”.

## Czego brakowało w danych

W lab 04 każda ścieżka (`A-S1`…`C-S3`) była krótkim odcinkiem od parkingu lub przystanku do jednego wejścia. Budynki były osobnymi „wyspami”, więc nie dało się policzyć trasy między nimi.

Minimalne rozszerzenie to plik [`data/campus-paths.geojson`](data/campus-paths.geojson) z alejami `W-01`…`W-10`, które łączą istniejące ścieżki w jedną sieć. Każda aleja może mieć dwa dodatkowe atrybuty:

| Atrybut | Znaczenie |
|---|---|
| `accessible: false` | schody — trasa z wózkiem je omija (np. `W-04` przy przystanku A) |
| `closed: true` | ścieżka zamknięta, np. remont |

Pliki budynków się nie zmieniły. `BuildingRepository` wczytuje teraz tylko pliki `building-*.geojson`.

## Jak działa algorytm

Obliczenia są rozdzielone na trzy niezależne części w `src/TupZak.Demo/Services`:

1. **`CampusNetwork`** — graf ścieżek. Węzły to wierzchołki ścieżek i ich skrzyżowania. Ścieżka, która kończy się w środku innej, dzieli ją na dwie krawędzie. Krawędzie mają długość w metrach.
   - Wejście jest połączone z siecią, jeśli dochodzi do niego ścieżka.
   - Parking ma kilka węzłów dostępu (miejsc, gdzie ścieżki dochodzą do jego krawędzi), połączonych przejściem przez parking.
   - Punkt orientacyjny (np. przystanek) jest przyciągany do najbliższej ścieżki w promieniu 5 m.
   - Najkrótsze trasy liczy algorytm Dijkstry z ograniczeniami: wózek, ścieżki do ominięcia, ścieżki zamknięte.
2. **`DeliveryOrderOptimizer`** — kolejność odwiedzania. Dostaje **wyłącznie macierz kosztów**, nie zna grafu ani budynków.
   - Przegląda wszystkie permutacje dostaw (n!, do 8 dostaw).
   - Dla każdej kolejności wybiera wejścia programowaniem dynamicznym po kolejnych etapach. Najlepsze wejście zależy od poprzedniego i następnego punktu.
   - Przy remisie zostaje pierwsza znaleziona kolejność, więc wynik jest deterministyczny.
3. **`DeliveryPlanner`** — łączy obie części:
   - ustala start i wejścia-kandydatów,
   - buduje macierz kosztów (Dijkstra z każdego punktu),
   - wywołuje optymalizator,
   - odtwarza przebieg każdego etapu po ścieżkach.

Optymalizator działa na tej samej macierzy, co przykładowy skrypt w Pythonie (`permutations` po słowniku odległości). Test `Matches_the_python_script_example` sprawdza, że dla odległości ze skryptu wynikiem jest P1 → B → A → C, 500 m.

### Model dostawy

```csharp
record Delivery(string BuildingId, string? EntranceId = null, bool RequiresCart = false);
record DeliveryPlanRequest(string Start, IReadOnlyList<Delivery> Deliveries, bool ReturnToStart = false, IReadOnlyList<string>? AvoidPathIds = null);
```

Jeżeli którakolwiek paczka wymaga wózka, cała trasa omija schody. Zakładamy, że kurier ma wózek przez całą trasę.

## Narzędzia MCP

Oprócz narzędzi z lab 04:

| Narzędzie | Co robi |
|---|---|
| `find_place` | Szuka punktów trasy: parkingów, wejść, punktów orientacyjnych i budynków. Zwraca wszystkie pasujące, np. dla „P1” trzy parkingi. |
| `find_route` | Najkrótsza trasa między dwoma punktami. Budynek oznacza jego najbliższe wejście. |
| `get_distance_matrix` | Odległości po ścieżkach między parami punktów, w postaci par `from`/`to`/`distanceM`. Budynek jest rozwijany do wszystkich wejść. Dane dla wariantu 2. |
| `get_campus_graph` | Surowy graf: węzły, krawędzie z długością i dostępnością oraz punkty tras. Dane dla wariantu 3. |
| `optimize_deliveries` | Optymalna kolejność i wejścia: etapy, długości, łączny dystans, liczba sprawdzonych kolejności. Pokazuje plan na mapie w przeglądarce. |
| `show_delivery_plan` | MCP App: mapa kampusu z trasą dla kolejności wejść **podanej przez wywołującego**. Pokazuje wynik dowolnego wariantu. |

Narzędzia wywołują logikę domenową. Warstwa MCP (`Tools/DeliveryTools.cs`, `Mcp/CampusMapApp.cs`) tylko mapuje argumenty i aktualizuje mapę. Nowy zasób `tupzak://paths` zwraca aleje kampusu.

Przykład z MCP Inspector:

```bash
npx @modelcontextprotocol/inspector --cli http://localhost:5144/mcp --transport http --method tools/call \
  --tool-name optimize_deliveries --tool-arg start=A-P1 \
  --tool-arg 'deliveries=[{"buildingId":"C"},{"buildingId":"A"},{"buildingId":"B"}]'
```

Wynik dla danych demonstracyjnych:

| Start | Kolejność wejść | Długość |
|---|---|---|
| `A-P1` | A1 → B1 → C1 | 468,1 m |
| `A-P1` z powrotem | A1 → B1 → C1 → A-P1 | 838,1 m |
| `B-P1` | C1 → B1 → A2 | 520 m |
| `C-P1` | C4 → B1 → A2 | 512 m |

## Wizualizacja

- **MCP App** (`Mcp/BuildingMapView.html`). Ten sam widok co `show_building_map` pokazuje teraz cały kampus z trasą, startem (S) i ponumerowanymi punktami dostaw. Schody są oznaczone na czerwono. Kliknięcie punktu pokazuje budynek, wybrane wejście i etap trasy.
- **Strona w przeglądarce** (`wwwroot`). Rysuje aleje kampusu i ostatni plan dostaw. Przydaje się w Claude Code, który nie wyświetla MCP Apps.
  Przyciski w nagłówku uruchamiają dwa scenariusze („Dostawy A, B, C” oraz „Wózek i powrót”).
- **Podgląd Skills** (`wwwroot/skills.html`). Fioletowy link „Skill: …” w nagłówku otwiera w nowej karcie pliki Skilla, czyli `SKILL.md`, referencje i skrypty, w takiej postaci, w jakiej czyta je agent. Frontmatter jest wyróżniony, bo tylko on jest w kontekście od początku. Pliki serwuje `/api/skills` (tylko odczyt, bez wychodzenia poza katalog Skilla).

## Testy

- **`DeliveryOrderOptimizerTests`**: optymalizator na samej macierzy, w tym przykład ze skryptu w Pythonie.
- **`DeliveryPlannerTests`**: mały kampus testowy [`tests/TupZak.Demo.Tests/TestCampus`](tests/TupZak.Demo.Tests/TestCampus), w którym wszystkie odległości da się policzyć w pamięci (rysunek w komentarzu klasy). Pokrywa przypadki:
  - jedna dostawa,
  - trzy dostawy w złej kolejności,
  - inny start,
  - powrót do startu,
  - niedostępna ścieżka i wejście,
  - wózek,
  - brak połączenia,
  - dwa wejścia do jednego budynku.
- **`CampusNetworkTests`**: spójność sieci demonstracyjnej, scenariusz A, B, C i niejednoznaczne „P1”.
- **`McpServerTests`**: nowe narzędzia przez prawdziwy transport MCP.
