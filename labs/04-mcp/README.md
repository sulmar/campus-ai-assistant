# Tup Żak — MCP w .NET

Kontynuacja [lab 03](../03-tool-calling/README.md): te same narzędzia kampusu, udostępnione jako **serwer MCP**. Pętlę agenta prowadzi teraz host (Claude Code, Claude Desktop), a aplikacja tylko wykonuje narzędzia.

## Uruchomienie

```bash
dotnet run --project src/TupZak.Demo   # http://localhost:5144, MCP: /mcp
```

W tym katalogu uruchom `claude`. Serwer `tupzak` jest zdefiniowany w `.mcp.json`, więc wystarczy go zatwierdzić. Potem zapytaj:

> Znajdź budynek A i zaznacz wejście od strony parkingu.

## Serwer udostępnia

- **Narzędzia:** `find_building`, `get_building_geometry`, `mark_entrance`, `show_building_map`, `find_coffee_machine`, `ask_coffee_machine_location` oraz `report_coffee_machine_location` (tylko dla widgetu).
- **Zasoby:** `tupzak://buildings` i `tupzak://buildings/{id}`, np. `@tupzak:tupzak://buildings`.
- **Prompt:** `building_entrance`, np. `/mcp__tupzak__building_entrance A parking`.
- **MCP App:** mapa budynku wyświetlana w rozmowie (tylko Claude Desktop i claude.ai).

## Mapa w Claude Desktop

1. Uruchom tunel: `cloudflared tunnel --url http://localhost:5144`.
2. W *Settings → Connectors → Add custom connector* podaj adres tunelu z dopiskiem `/mcp`.
3. Zapytaj: „Pokaż mapę budynku B z wejściem B1”.

Jeśli mapa się nie pojawia, zamknij Claude Desktop przez ⌘Q i uruchom go ponownie.

## Wskazywanie ekspresu na mapie

Zapytaj w Claude Desktop: „Gdzie jest ekspres do kawy w budynku A?”. Model nie zna odpowiedzi, więc prosi o nią użytkownika przez widget.

Natywne `elicitation/create` obsługuje tylko proste pola formularza albo przekierowanie na URL, więc nie pokaże mapy. Ten sam przepływ powstaje z elementów MCP Apps:

1. `find_coffee_machine` zwraca `known: false`, a model wywołuje `ask_coffee_machine_location` i kończy turę.
2. Widget przechodzi w tryb wskazywania: użytkownik klika na mapie i wybiera „Zatwierdź” albo „Nie wiem”.
3. Widget zapisuje punkt przez `tools/call` narzędzia `report_coffee_machine_location`. Ma ono `visibility: ["app"]`, więc host nie pokazuje go modelowi.
4. Widget wysyła `ui/message`, a model odpowiada w nowej turze. Jeśli host odrzuci wiadomość, widget używa `ui/update-model-context`.

Wskazania są tylko w pamięci, po jednym na budynek. Znikają po restarcie aplikacji lub resecie. W Claude Code mapa się nie pojawia, więc model prosi o opis słowny.
