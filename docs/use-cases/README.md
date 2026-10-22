# Przypadki użycia


## Wersja Mermaid 


### 1. Aktor i przypadek użycia

```mermaid
flowchart LR
    Student["Student"]
    UC01(["Wyszukiwanie miejsca"])

    Student --- UC01
```    


2. Granice systemu i generalizacja
```mermaid
flowchart LR
    Student["Student"]

    subgraph System["Campus AI Assistant"]
        UC01(["UC-01<br/>Wyszukiwanie miejsca"])
        UC02(["UC-02<br/>Wyznaczanie trasy"])
        UC03(["UC-03<br/>Wyszukiwanie czynnego automatu"])
    end

    Student --- UC01
    Student --- UC02
    Student --- UC03

    UC03 -->|generalizacja| UC01
```    

> Uwaga: Mermaid flowchart nie obsługuje pełnej notacji diagramów przypadków użycia UML. W poprawnym diagramie UML generalizację przedstawia się linią ciągłą z pustym trójkątem skierowanym do UC-01.


## Wersja PlantUML — zgodna z notacją UML 


### 1. Aktor i przypadek użycia

```plantuml
@startuml
actor Student
usecase "Wyszukiwanie miejsca" as UC01
Student --> UC01
@enduml
```


### 2. Granice systemu i generalizacja

```plantuml

@startuml
left to right direction

actor Student

rectangle "Campus AI Assistant" {
    usecase "UC-01\nWyszukiwanie miejsca" as UC01
    usecase "UC-02\nWyznaczanie trasy" as UC02
    usecase "UC-03\nWyszukiwanie czynnego automatu" as UC03
}

Student -- UC01
Student -- UC02
Student -- UC03

UC03 --|> UC01

@enduml

```

> GitHub nie obsługuje natywnie renderowania diagramów PlantUML w plikach Markdown — wyświetla jedynie ich kod źródłowy.

> W Visual Studio Code można zainstalować rozszerzenie [Markdown PlantUML Preview](https://marketplace.visualstudio.com/items?itemName=myml.vscode-markdown-plantuml-preview), które umożliwia wyświetlanie diagramów PlantUML bezpośrednio w podglądzie plików Markdown.



## Katalog przypadków użycia

| ID | Przypadek użycia | Aktor | Wymagania |
|----|------------------|-------|-----------|
| [UC-01](UC-01-search-place.md) | Wyszukiwanie miejsca lub obiektu | Student | FR-01, FR-02, FR-03 |
| [UC-02](UC-02-find-route.md) | Wyznaczanie trasy do miejsca | Student | FR-01, FR-03, FR-04 |
| [UC-03](UC-03-find-coffee-machine.md) | Wyszukiwanie czynnego automatu z kawą | Student | FR-01, FR-02, FR-03, FR-05 |
