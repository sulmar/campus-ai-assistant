# Wymagania

Wymagania (Requirements) określają, co system powinien robić oraz jakie właściwości i ograniczenia powinien spełniać.

Wymagania powinny być jednoznaczne, zrozumiałe i możliwe do zweryfikowania. Opisujemy oczekiwane zachowanie systemu, a nie sposób jego implementacji.


## Oznaczenia

- **FR** (Functional Requirement) — wymaganie funkcjonalne, określające, co system powinien robić.

- **NFR** (Non-Functional Requirement) — wymaganie niefunkcjonalne, określające, jakie właściwości lub ograniczenia powinien spełniać system.

Każde wymaganie posiada unikalny identyfikator, np. `FR-01` lub `NFR-01`.

## Wymagania funkcjonalne

| ID | Wymaganie |
|---|---|
| FR-01 | System umożliwia studentowi zadawanie pytań w języku naturalnym. |
| FR-02 | System umożliwia wyszukiwanie sal, budynków i automatów z kawą. |
| FR-03 | System prezentuje lokalizację znalezionego miejsca lub obiektu. |
| FR-04 | System umożliwia wyznaczenie trasy do wybranej lokalizacji. |
| FR-05 | System umożliwia wyszukiwanie czynnych automatów z kawą. |

## Wymagania niefunkcjonalne

| ID | Wymaganie |
|---|---|
| NFR-01 | System zwraca wyniki wyszukiwania w czasie nieprzekraczającym 2 sekund dla 95% zapytań. |
| NFR-02 | System nie przechowuje historii lokalizacji studenta. |
| NFR-03 | Interfejs użytkownika spełnia wymagania WCAG 2.2 na poziomie AA. |


## Pytania otwarte

- Co oznacza, że automat z kawą jest czynny?
- Co powinien zrobić system, jeśli nie znajdzie wskazanego miejsca?
- Co powinien zrobić system, jeśli znajdzie kilka pasujących miejsc?
- Skąd system zna lokalizację początkową studenta?
- Czy trasa powinna uwzględniać poruszanie się wewnątrz budynków?
- Czy student może wskazać własną lokalizację początkową?