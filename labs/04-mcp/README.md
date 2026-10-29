# Tup Żak — MCP w .NET

Kontynuacja [lab 03](../03-tool-calling/README.md): te same narzędzia kampusu, udostępnione jako **serwer MCP**. Pętlę agenta prowadzi teraz host (Claude Code, Claude Desktop), a aplikacja tylko wykonuje narzędzia.

## Uruchomienie

```bash
dotnet run --project src/TupZak.Demo   # http://localhost:5144, MCP: /mcp
```

W tym katalogu uruchom `claude`. Serwer `tupzak` jest zdefiniowany w `.mcp.json`, więc wystarczy go zatwierdzić. Potem zapytaj:

> Znajdź budynek A i zaznacz wejście od strony parkingu.

## Serwer udostępnia

- **Narzędzia:** `find_building`, `get_building_geometry`, `mark_entrance`, `show_building_map`.
- **Zasoby:** `tupzak://buildings` i `tupzak://buildings/{id}`, np. `@tupzak:tupzak://buildings`.
- **Prompt:** `building_entrance`, np. `/mcp__tupzak__building_entrance A parking`.
- **MCP App:** mapa budynku wyświetlana w rozmowie (tylko Claude Desktop i claude.ai).

## Mapa w Claude Desktop

1. Uruchom tunel: `cloudflared tunnel --url http://localhost:5144`.
2. W *Settings → Connectors → Add custom connector* podaj adres tunelu z dopiskiem `/mcp`.
3. Zapytaj: „Pokaż mapę budynku B z wejściem B1”.

Jeśli mapa się nie pojawia, zamknij Claude Desktop przez ⌘Q i uruchom go ponownie.
