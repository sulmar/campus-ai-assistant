# Kontrole spójności

Wykonaj wszystkie kontrole. Każda niezgodność trafia do raportu jako brak lub otwarte pytanie.

## Śledzenie powiązań

| ID | Kontrola | Jak sprawdzić |
|---|---|---|
| K1 | Każdy scenariusz z wizji ma co najmniej jeden UC | Zestaw zdania-scenariusze z wizji z nazwami UC |
| K2 | Każdy UC ma powiązane FR | Pole „Powiązane wymagania” w UC |
| K3 | Każde FR jest realizowane przez jakiś UC | Szukaj identyfikatora FR w plikach UC |
| K4 | Odwołania wskazują na istniejące elementy | Każde `FR-xx`, `NFR-xx`, `UC-xx` musi być zdefiniowane |
| K5 | Identyfikatory są unikalne, a katalog UC zgadza się z plikami UC | Porównaj tabelę katalogu z nagłówkami i polami plików |

Wynik K1–K3 przedstaw jako macierz: wiersze UC, kolumny FR (✓ = powiązane).

## Spójność treści

| ID | Kontrola |
|---|---|
| K6 | Aktorzy z UC są opisani jako użytkownicy w wizji — i odwrotnie (każdy użytkownik z wizji ma UC) |
| K7 | Funkcje wykluczone w wizji nie pojawiają się w FR ani UC |
| K8 | To samo pojęcie nazywane jest tak samo we wszystkich dokumentach |
| K9 | Rola agenta AI z wizji ma odzwierciedlenie w UC |
| K10 | Wymagania minimalne kursu są zaplanowane: tool calling, MCP, RAG, ewaluacja — wskaż, gdzie się pojawiają |
| K11 | Brak sprzeczności między dokumentami (np. NFR o prywatności a scenariusz wymagający lokalizacji) |
| K12 | README odsyła do całej dokumentacji (wizja, słownik, wymagania, UC) |

## Typowe luki w projektach agentowych

Jeśli dokumentacja nie odpowiada na pytanie — dodaj otwarte pytanie.

- Co agent robi, gdy nie zna odpowiedzi albo narzędzie zwróci błąd?
- Które akcje agenta zmieniają dane i czy wymagają potwierdzenia użytkownika?
- Skąd pochodzą dane (np. dla RAG), kto i jak często je aktualizuje?
- Jakie dane użytkownika trafiają do modelu?
- Jak będzie mierzona jakość działania agenta (zbiór testowy, metryka)?
- Co dzieje się przy niejednoznacznym żądaniu użytkownika?
- Jak weryfikowane są treści dodawane przez użytkowników?

## Formułowanie otwartych pytań

Dobre pytanie odwołuje się do miejsca w dokumentacji, dotyczy brakującej decyzji i da się na nie odpowiedzieć krótko.

| Źle | Dobrze |
|---|---|
| Czy przemyśleliście błędy? | UC-02, krok 4: co system robi, gdy trasa wymaga przejścia przez zamknięty budynek? |
| Doprecyzujcie wymagania. | FR-05: kiedy automat jest „czynny” — działa, ma kawę, czy budynek jest otwarty? |

## Priorytet braków

- **Krytyczny** — uniemożliwia ocenę lub realizację: brak wizji, główny scenariusz bez UC, sprzeczność.
- **Istotny** — obniża jakość: niemierzalne NFR, brak warunków końcowych, pojęcia spoza słownika.
- **Drobny** — nieużywane pojęcie w słowniku, literówka w definicji, niespójna numeracja.
