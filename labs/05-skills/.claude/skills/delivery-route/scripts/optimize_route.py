#!/usr/bin/env python3
"""Optymalna kolejność dostaw na kampusie Tup Żak.

Wejście (stdin): wynik narzędzia MCP get_distance_matrix, czyli
    { "points": [{ "id", "kind", "buildingId", ... }], "distances": [{ "from", "to", "distanceM" }] }
Argumenty:
    --start A-P1            punkt startowy (musi być w points)
    --deliveries A B2 C     budynki (dowolne ich wejście) lub konkretne wejścia
    --return                doliczyć powrót do punktu startowego

Wyjście (stdout): JSON z kolejnością wejść, etapami, łączną długością
i gotowymi argumentami dla narzędzia show_delivery_plan.

Algorytm jak w przykładzie z permutacjami, ale odległości pochodzą z grafu ścieżek kampusu,
a dla każdego budynku sprawdzane są wszystkie jego wejścia (iloczyn kartezjański).
"""

import argparse
import json
import sys
from itertools import permutations, product

MAX_COMBINATIONS = 2_000_000


def load_matrix(data):
    points = {p["id"]: p for p in data["points"]}
    distances = {}
    for d in data["distances"]:
        value = d.get("distanceM")  # brak pola (albo null) = brak trasy
        value = float("inf") if value is None else value
        distances[(d["from"], d["to"])] = value
        distances[(d["to"], d["from"])] = value
    return points, distances


def distance(distances, a, b):
    if a == b:
        return 0
    return distances.get((a, b), float("inf"))


def candidates(points, delivery):
    """Budynek → wszystkie jego wejścia; wejście → tylko ono."""
    if delivery in points and points[delivery]["kind"] == "entrance":
        return [delivery]
    entrances = [p["id"] for p in points.values() if p["kind"] == "entrance" and p.get("buildingId") == delivery]
    if not entrances:
        raise ValueError(f"Brak wejść dla „{delivery}” w macierzy odległości. Dodaj ten budynek do get_distance_matrix.")
    return entrances


def optimize(points, distances, start, deliveries, return_to_start):
    options = [candidates(points, d) for d in deliveries]

    combinations = 1
    for o in options:
        combinations *= len(o)
    for n in range(2, len(deliveries) + 1):
        combinations *= n
    if combinations > MAX_COMBINATIONS:
        raise ValueError(f"Za dużo kombinacji ({combinations}). Ogranicz liczbę dostaw albo podaj konkretne wejścia.")

    best_route, best_total, evaluated = None, float("inf"), 0
    for order in permutations(range(len(deliveries))):
        for entrances in product(*(options[i] for i in order)):
            evaluated += 1
            route = [start, *entrances]
            if return_to_start:
                route.append(start)

            total = sum(distance(distances, a, b) for a, b in zip(route, route[1:]))
            if total < best_total:
                best_total, best_route = total, route

    if best_route is None or best_total == float("inf"):
        unreachable = [d for d, o in zip(deliveries, options)
                       if all(distance(distances, start, e) == float("inf") for e in o)]
        return {"success": False, "error": "Brak trasy do wszystkich dostaw.", "unreachable": unreachable}

    legs = [{"from": a, "to": b, "distanceM": round(distance(distances, a, b), 1)}
            for a, b in zip(best_route, best_route[1:])]
    stops = best_route[1:-1] if return_to_start else best_route[1:]
    return {
        "success": True,
        "start": start,
        "order": stops,
        "legs": legs,
        "totalDistanceM": round(best_total, 1),
        "returnToStart": return_to_start,
        "evaluatedRoutes": evaluated,
        "showDeliveryPlan": {"start": start, "entranceIds": stops, "returnToStart": return_to_start},
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--start", required=True)
    parser.add_argument("--deliveries", nargs="+", required=True)
    parser.add_argument("--return", dest="return_to_start", action="store_true")
    args = parser.parse_args()

    try:
        data = json.load(sys.stdin)
        points, distances = load_matrix(data)
        if args.start not in points:
            raise ValueError(f"Punkt startowy „{args.start}” nie występuje w macierzy odległości.")
        result = optimize(points, distances, args.start, args.deliveries, args.return_to_start)
    except (ValueError, KeyError, json.JSONDecodeError) as error:
        result = {"success": False, "error": str(error)}

    json.dump(result, sys.stdout, ensure_ascii=False, indent=2)
    print()
    return 0 if result["success"] else 1


if __name__ == "__main__":
    sys.exit(main())
