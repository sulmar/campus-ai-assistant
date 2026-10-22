# Przypadki użycia


## Diagram przypadków użycia UML 

```mermaid
flowchart LR
    Student["👤 Student"]

    subgraph System["Campus AI Assistant"]
        UC01(["UC-01<br/>Wyszukiwanie miejsca<br/>lub obiektu"])
        UC02(["UC-02<br/>Wyznaczanie trasy<br/>do miejsca"])
        UC03(["UC-03<br/>Wyszukiwanie czynnego<br/>automatu z kawą"])
    end

    Student --- UC01
    Student --- UC02
    Student --- UC03

    UC03 -- "generalization" --> UC01
```    

Uwaga: Mermaid flowchart nie obsługuje pełnej notacji diagramów przypadków użycia UML. W poprawnym diagramie UML generalizację przedstawia się linią ciągłą z pustym trójkątem skierowanym do UC-01.


## Wersja PlantUML — zgodna z notacją UML

```plantuml
@startuml
actor Student
usecase "Wyszukiwanie miejsca" as UC01
Student --> UC01
@enduml
```


``` plantuml
@startuml
left to right direction

actor Student

rectangle "Campus AI Assistant" {
    usecase "UC-01\nWyszukiwanie miejsca\nlub obiektu" as UC01
    usecase "UC-02\nWyznaczanie trasy\ndo miejsca" as UC02
    usecase "UC-03\nWyszukiwanie czynnego\nautomatu z kawą" as UC03
}

Student -- UC01
Student -- UC02
Student -- UC03

UC03 --|> UC01

@enduml
```

## Katalog przypadków użycia

| ID | Przypadek użycia | Aktor | Wymagania |
|----|------------------|-------|-----------|
| [UC-01](UC-01-search-place.md) | Wyszukiwanie miejsca lub obiektu | Student | FR-01, FR-02, FR-03 |
| [UC-02](UC-02-find-route.md) | Wyznaczanie trasy do miejsca | Student | FR-01, FR-03, FR-04 |
| [UC-03](UC-03-find-coffee-machine.md) | Wyszukiwanie czynnego automatu z kawą | Student | FR-01, FR-02, FR-03, FR-05 |
