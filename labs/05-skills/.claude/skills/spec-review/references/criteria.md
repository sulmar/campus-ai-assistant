# Kryteria oceny specyfikacji

Każdy krok otrzymuje jeden status: ✅ spełnione, ⚠️ częściowo, ❌ brak.

## 1. Autorzy w README

Wymagany początek głównego `README.md`:

```markdown
# Nazwa projektu

**Autorzy:**
- Imię Nazwisko
- Imię Nazwisko

**Grupa:** numer grupy zajęciowej
```

| Status | Warunek |
|---|---|
| ✅ | Nazwa projektu, lista autorów (pełne imiona i nazwiska) i numer grupy na początku pliku |
| ⚠️ | Autorzy podani, ale: brak grupy, loginy zamiast nazwisk, informacja na końcu pliku lub w innym formacie |
| ❌ | Brak `README.md` lub brak autorów |

## 2. Dokument wizji

Nazwy sekcji mogą się różnić — liczy się treść. Wizja napisana prozą też jest dopuszczalna,
jeśli da się w niej wskazać każdy element.

| Element | Pytanie kontrolne |
|---|---|
| Problem | Jaki konkretny problem rozwiązujemy? |
| Użytkownicy | Dla kogo? Czy grupa jest konkretna (nie „wszyscy”)? |
| Scenariusze | W jakich sytuacjach użytkownik korzysta z rozwiązania? |
| Rola agenta AI | Dlaczego agent, a nie formularz, wyszukiwarka albo reguły? |
| Zakres | Co realizujemy **i czego nie** realizujemy? |

| Status | Warunek |
|---|---|
| ✅ | Wszystkie elementy obecne i konkretne |
| ⚠️ | Brak 1–2 elementów, element ogólnikowy, brak wykluczeń zakresu, rola AI nieuzasadniona |
| ❌ | Brak dokumentu wizji |

Sygnały ostrzegawcze: problem opisany jako rozwiązanie („brakuje aplikacji do…”),
użytkownik „każdy”, rola agenta sprowadzona do „czatu z AI”.

## 3. Słownik pojęć domenowych

Słownik zawiera pojęcia **dziedziny problemu**, nie technologii (LLM, API, baza danych nie są wymagane).

Procedura:

1. Wypisz pojęcia ze słownika.
2. Dla każdego pojęcia policz wystąpienia w wymaganiach i w UC. Szukaj po rdzeniu, bo język polski
   odmienia wyrazy (`sal` → sala, sali, salę; `tras` → trasa, trasy, trasę).
3. Wypisz rzeczowniki domenowe z wymagań i UC, których brak w słowniku.
4. Wypisz synonimy — różne słowa na to samo pojęcie (np. „miejsce” / „lokalizacja” / „obiekt”).
5. Sprawdź definicje: czy nie są cykliczne lub puste („Trasa — trasa na kampusie”) i czy rozstrzygają
   pojęcia z pytań otwartych (np. „czynny”).

Wynik przedstaw jako tabelę: pojęcie × (wymagania, UC) z liczbą wystąpień.

| Status | Warunek |
|---|---|
| ✅ | Słownik istnieje, pojęcia są używane w wymaganiach i UC, brak istotnych pojęć niezdefiniowanych i synonimów |
| ⚠️ | Pojęcia nieużywane, pojęcia spoza słownika, synonimy, słabe definicje |
| ❌ | Brak słownika |

## 4. Wymagania funkcjonalne i niefunkcjonalne

**Wymaganie funkcjonalne (FR)** — co system robi. Dobre FR:

- ma identyfikator (`FR-01`),
- opisuje jedno zachowanie,
- jest testowalne (da się napisać test akceptacyjny),
- używa pojęć ze słownika.

**Wymaganie niefunkcjonalne (NFR)** — jak dobrze system działa. Dobre NFR jest **mierzalne**: liczba, próg, warunek pomiaru.

| Źle | Dobrze |
|---|---|
| System działa szybko. | 95% odpowiedzi w czasie < 2 s. |
| Agent odpowiada poprawnie. | ≥ 85% poprawnych odpowiedzi na zbiorze ewaluacyjnym 50 pytań. |
| Dane są bezpieczne. | Plan zajęć studenta nie jest przesyłany do modelu zewnętrznego. |

Kategorie NFR ważne w systemach agentowych: wydajność, koszt (tokeny, limity API), jakość odpowiedzi,
bezpieczeństwo (prompt injection, uprawnienia narzędzi), prywatność, dostępność, nadzór człowieka.

| Status | Warunek |
|---|---|
| ✅ | FR i NFR rozdzielone, z identyfikatorami, FR testowalne, NFR mierzalne, scenariusze wizji pokryte |
| ⚠️ | Brak identyfikatorów, wymagania niemierzalne lub nietestowalne, scenariusze wizji bez FR, brak NFR jakości odpowiedzi AI |
| ❌ | Brak wymagań albo brak jednej z grup (FR lub NFR) |

## 5. Przypadki użycia

Szablon: `assets/use-case-template.md`. Dla każdego UC sprawdź pola obowiązkowe i jakość treści:

- nazwa opisuje cel aktora (rzeczownik odczasownikowy lub „czasownik + rzeczownik”),
- scenariusz podstawowy to numerowane kroki aktora i systemu,
- scenariusze alternatywne odwołują się do numerów kroków (`3a`, `4b`),
- warunek końcowy jest sprawdzalny,
- „Rola agenta AI” wskazuje, które kroki wykonuje agent i jakich narzędzi lub źródeł wiedzy używa,
- akcja zmieniająca dane ma krok potwierdzenia przez użytkownika.

Wynik przedstaw jako tabelę: UC × pole szablonu (✅ / ⚠️ / ❌).

| Status | Warunek |
|---|---|
| ✅ | Wszystkie UC zgodne z szablonem, kluczowe scenariusze wizji mają UC |
| ⚠️ | Brakujące pola, brak scenariuszy alternatywnych, scenariusze wizji bez UC |
| ❌ | Brak przypadków użycia |
