# campus-ai-assistant

**campus-ai-assistant** to referencyjny projekt asystenta AI, który pomaga studentom odnaleźć się na kampusie i ułatwia im codzienne życie na uczelni.

Projekt jest wykorzystywany podczas zajęć dydaktycznych jako wspólny przypadek do analizy, projektowania i eksperymentowania z różnymi sposobami realizacji systemu.

## Dokumentacja

Projekt będzie rozwijany stopniowo wraz z kolejnymi etapami analizy i projektowania.

Na początek:

- [Wizja](docs/vision.md)

## Laboratoria

- [03 · Tool Calling](labs/03-tool-calling/README.md) — agent Tup Żak wybiera wejście do budynku na podstawie geometrii GeoJSON (.NET 10, Microsoft.Extensions.AI, Ollama, Leaflet)
- [04 · MCP](labs/04-mcp/README.md) — te same narzędzia udostępnione jako serwer MCP (Streamable HTTP); rozmowa z agentem w Claude Code z podglądem na żywo na mapie

## Struktura

```text
campus-ai-assistant/
├── README.md
├── docs/
│   ├── vision.md
│   ├── glossary.md
│   └── requirements.md
├── labs/
│   ├── 03-tool-calling/
│   └── 04-mcp/
└── src/
```

## Autor

**mgr Marcin Sulecki**  
Wydział Matematyki i Nauk Informacyjnych  
Politechnika Warszawska

© Marcin Sulecki / Politechnika Warszawska, 2026

## Licencja

Materiały dydaktyczne w tym repozytorium są udostępniane na licencji Creative Commons Attribution 4.0 International (CC BY 4.0).

Kod źródłowy przykładów jest udostępniany na licencji MIT.

Materiały pochodzące z zewnętrznych źródeł podlegają licencjom i prawom ich odpowiednich autorów.
