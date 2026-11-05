---
name: delivery-route
description: Wyznaczanie trasy dostawy kurierskiej po kampusie Tup Żak do kilku budynków — optymalna kolejność i wejścia, liczone skryptem na odległościach z serwera MCP tupzak. Użyj, gdy użytkownik (kurier) chce zaplanować trasę, kolejność dostaw lub objazd kilku budynków kampusu. Nie używaj do trasy między dwoma punktami ani do zaznaczania pojedynczego wejścia.
---

# Wyznaczanie trasy dostawy

Kolejność dostaw liczy **skrypt** `scripts/optimize_route.py`, a nie model.
Model ustala z kurierem dane wejściowe, pobiera odległości z serwera MCP, uruchamia skrypt i prezentuje wynik.

## Kroki

1. **Punkt startowy.** Wywołaj `find_place` z nazwą podaną przez kuriera.
   - Jeden wynik — użyj jego `id` (np. `A-P1`).
   - Kilka wyników (np. „P1” → `A-P1`, `B-P1`, `C-P1`) — **zapytaj**, o który chodzi. Nie wybieraj sam.
   - Brak wyników — pokaż, jakie parkingi istnieją, i zapytaj.
2. **Dostawy.** Ustal listę budynków. Jeżeli kurier podał konkretne wejście (np. „B od dziedzińca”), użyj identyfikatora wejścia (np. `B2`) — ustal go narzędziem `get_building_geometry`.
3. **Ograniczenia.** Zapytaj tylko wtedy, gdy kurier o nich nie wspomniał, a mają znaczenie:
   - wózek → `requiresCart: true` (trasa omija schody),
   - zamknięte ścieżki → `avoidPathIds`,
   - powrót do punktu startowego → flaga `--return` skryptu.
   Bez informacji przyjmij: bez wózka, bez powrotu.
4. **Odległości.** Wywołaj `get_distance_matrix` z punktami `[start, ...budynki]` i tymi samymi ograniczeniami.
   Budynki są rozwijane do wszystkich wejść — dzięki temu skrypt wybierze najlepsze wejście.
5. **Optymalizacja.** Przekaż wynik `get_distance_matrix` (cały JSON) na standardowe wejście skryptu:

   ```bash
   python3 <katalog tego Skilla>/scripts/optimize_route.py --start A-P1 --deliveries A B C [--return] <<'JSON'
   { ...wynik get_distance_matrix... }
   JSON
   ```

   `--deliveries` przyjmuje identyfikatory budynków (`B`) lub wejść (`B2`). Nie poprawiaj wyniku skryptu ręcznie.
6. **Mapa.** Wywołaj `show_delivery_plan` z argumentami z pola `showDeliveryPlan` wyniku skryptu
   (te same ograniczenia co w kroku 4). Narzędzie liczy przebieg trasy po ścieżkach i rysuje ją na mapie.
7. **Odpowiedź** w formacie z [references/answer-format.md](references/answer-format.md).

## Błędy

- Skrypt zwraca `"success": false` i listę `unreachable` → powiedz, do których budynków nie ma trasy i dlaczego
  (np. tylko schody przy wózku). Zaproponuj zmianę: bez wózka, inne wejście albo pominięcie budynku.
- `show_delivery_plan` zwróci inną łączną długość niż skrypt → zgłoś rozbieżność, nie ukrywaj jej.

## Kryterium sukcesu

Kurier dostaje kolejność wejść, długość każdego etapu i łączny dystans, a trasa jest widoczna na mapie.
