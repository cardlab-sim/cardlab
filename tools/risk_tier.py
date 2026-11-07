#!/usr/bin/env python3
"""Print the risk tier (1, 2 or 3) for a list of changed paths read from stdin, one per line.

Usage: gh pr diff <n> --name-only | python3 tools/risk_tier.py docs/sdlc/review-tiers.json
A path matches a tier when it starts with one of the tier's prefixes, or contains a prefix that
has no leading directory (e.g. "Migrations/" matches "src/X/Migrations/001.cs"). Highest tier wins.
"""
from __future__ import annotations

import json
import sys


def tier_of(path: str, tiers: dict[str, list[str]], default: int) -> int:
    best = default
    for tier, prefixes in tiers.items():
        for p in prefixes:
            if path.startswith(p) or ("/" + p) in path:
                best = max(best, int(tier))
    return best


def main() -> int:
    cfg = json.load(open(sys.argv[1], encoding="utf-8"))
    paths = [line.strip().replace("\\", "/") for line in sys.stdin if line.strip()]
    tier = max((tier_of(p, cfg["tiers"], cfg["default_tier"]) for p in paths), default=cfg["default_tier"])
    print(tier)
    return 0


if __name__ == "__main__":
    sys.exit(main())
    