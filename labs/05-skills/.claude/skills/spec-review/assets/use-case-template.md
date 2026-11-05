# UC-NN — Nazwa przypadku użycia

<!-- Pola oznaczone (*) są obowiązkowe. Nazwa opisuje cel aktora, np. „Wyszukiwanie miejsca”. -->

## Aktor (*)

Główny aktor inicjujący przypadek użycia, np. Student.

## Aktorzy pomocniczy

Systemy zewnętrzne lub osoby biorące udział, np. USOS.

## Cel (*)

Co aktor chce osiągnąć — jedno zdanie z perspektywy aktora.

## Powiązane wymagania (*)

FR-NN, FR-NN, NFR-NN

## Warunek wstępny (*)

Co musi być spełnione, zanim przypadek użycia się rozpocznie.

## Warunek końcowy (*)

Sprawdzalny stan po pomyślnym zakończeniu, np. „Student widzi lokalizację sali na mapie”.

## Scenariusz podstawowy (*)

1. Aktor …
2. System …
3. System …
4. System prezentuje …

## Scenariusze alternatywne (*)

- **2a. Warunek:** reakcja systemu.
- **3a. Warunek:** reakcja systemu.

<!-- Numer odwołuje się do kroku scenariusza podstawowego. Uwzględnij: brak wyników, wiele wyników, błąd narzędzia lub źródła danych, niejednoznaczne żądanie. -->

## Rola agenta AI (*)

| Krok | Co robi agent | Narzędzie / źródło wiedzy |
|---|---|---|
| 2 | Rozpoznaje, o jakie miejsce chodzi | — (model) |
| 3 | Wyszukuje miejsce | `find_place` (MCP) |

<!-- Akcje zmieniające dane wymagają kroku potwierdzenia przez użytkownika. -->

## Otwarte pytania

- Pytanie dotyczące tego przypadku użycia.
