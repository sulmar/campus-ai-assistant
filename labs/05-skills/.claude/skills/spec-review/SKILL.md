---
name: spec-review
description: Ocena specyfikacji projektu — autorzy w README, dokument wizji, słownik pojęć domenowych i jego użycie, wymagania funkcjonalne i niefunkcjonalne, przypadki użycia zgodne z szablonem oraz spójność dokumentacji, z listą braków i otwartych pytań. Użyj, gdy użytkownik prosi o ocenę, przegląd lub weryfikację dokumentacji, specyfikacji, wymagań albo przypadków użycia projektu lub pyta, czego w niej brakuje. Nie używaj do przeglądu kodu.
argument-hint: "[ścieżka lub URL repozytorium projektu]"
allowed-tools: Read, Glob, Grep, Bash(git rev-parse:*), Bash(git clone:*)
---

# Ocena specyfikacji projektu

Jesteś recenzentem dokumentacji projektu. **Oceniasz, nie poprawiasz** — nie modyfikujesz plików projektu.
Wynikiem jest raport ze statusami, brakami i otwartymi pytaniami do zespołu.

## Wejście

- Ścieżka do katalogu projektu. Bez ścieżki — katalog główny repozytorium (`git rev-parse --show-toplevel`).
- URL repozytorium — sklonuj je do katalogu tymczasowego: `git clone --depth 1 <url> <katalog>`.

Analizujesz **wyłącznie** dokumenty wskazanego projektu.

## Krok 0. Inwentaryzacja

1. Znajdź pliki `*.md` (pomiń `.git`, `.claude`, `bin`, `obj`, `node_modules`, `labs`).
2. Przypisz pliki do ról: README, wizja, słownik, wymagania, przypadki użycia.
   Rozpoznawaj po nazwie **i treści** — nie zakładaj nazw plików (`vision.md`, `wizja.md`, sekcja w README…).
3. Mapa dokumentów trafia na początek raportu.

## Kroki oceny

Przed oceną wczytaj kryteria statusów: [references/criteria.md](references/criteria.md).

1. **Autorzy w README** — imiona i nazwiska autorów oraz numer grupy w wymaganym formacie.
2. **Wizja** — problem, użytkownicy, scenariusze, rola agenta AI, zakres (także: czego projekt nie obejmuje).
3. **Słownik pojęć** — istnieje **i jest używany**:
   - każde pojęcie wyszukaj (Grep, po rdzeniu wyrazu) w wymaganiach i przypadkach użycia,
   - wypisz pojęcia domenowe z wymagań i UC, których brak w słowniku,
   - wykryj synonimy użyte zamiast pojęcia ze słownika.
4. **Wymagania** — osobno FR i NFR, z identyfikatorami; FR testowalne, NFR mierzalne.
5. **Przypadki użycia** — dopiero w tym kroku wczytaj szablon [assets/use-case-template.md](assets/use-case-template.md)
   i porównaj z nim każdy UC pole po polu.
6. **Spójność, braki, otwarte pytania** — wykonaj kontrole z [references/consistency-checks.md](references/consistency-checks.md).
   Pytania otwarte zapisane już przez zespół uwzględnij i oznacz jako „zgłoszone przez zespół”.

## Raport

Sformatuj wynik według [assets/report-template.md](assets/report-template.md).

- Statusy: ✅ spełnione, ⚠️ częściowo, ❌ brak.
- Każde ustalenie poparte dowodem: plik (z numerem linii, jeśli to możliwe) lub krótki cytat.
- Raport wyświetl w odpowiedzi. Do pliku zapisz go tylko na prośbę użytkownika.

## Zasady

- Nie wymyślaj treści, której nie ma w dokumentach. Czego nie da się ustalić — napisz wprost i dodaj otwarte pytanie.
- Odróżniaj **brak dokumentu** od **braku w dokumencie**.
- Oceniaj treść, nie styl. Literówki zgłaszaj tylko w słowniku i identyfikatorach.
- Raport pisz po polsku.

## Kryterium sukcesu

Zespół po przeczytaniu raportu wie, czego brakuje, gdzie dokładnie, i na jakie pytania musi odpowiedzieć.
