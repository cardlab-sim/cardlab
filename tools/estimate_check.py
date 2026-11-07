"""Estimation gate check for one feature (stdlib only).

Reads docs/features/<feature>/estimate.md, a table with one row per gate:

| Gate | Human (blind) | Agent | Committed | Override (who: reason) |
|------|---------------|-------|-----------|------------------------|
| 1    | L             | L     | L         |                        |
| 2    | XL            | L     | XL        |                        |

Fails (exit 1) when:
  - a committed size is XL (must split);
  - committed size jumps two or more steps from the previous gate, unless that row has an override;
Warns when the human and agent sizes differ by two or more steps (an item for the tech lead).

Usage: python -I estimate_check.py docs/features/002-transfers/estimate.md
"""
import re
import sys

SIZES = ["XS", "S", "M", "L", "XL"]


def rows(text):
    out = []
    for line in text.splitlines():
        cells = [c.strip() for c in line.strip().strip("|").split("|")]
        if len(cells) >= 4 and re.fullmatch(r"\d", cells[0]):
            out.append({"gate": int(cells[0]), "human": cells[1].upper(), "agent": cells[2].upper(),
                        "committed": cells[3].upper(), "override": cells[4] if len(cells) > 4 else ""})
    return sorted(out, key=lambda r: r["gate"])


def idx(size, where):
    if size not in SIZES:
        raise SystemExit(f"FAIL {where}: unknown size '{size}' (use {', '.join(SIZES)})")
    return SIZES.index(size)


def main(path):
    rs = rows(open(path, encoding="utf-8").read())
    if not rs:
        raise SystemExit(f"FAIL {path}: no gate rows found")
    failed = False
    prev = None
    for r in rs:
        g = f"gate {r['gate']}"
        c = idx(r["committed"], g)
        if r["human"] and r["agent"]:
            gap = abs(idx(r["human"], g) - idx(r["agent"], g))
            if gap >= 2:
                print(f"WARN {g}: human {r['human']} vs agent {r['agent']} differ by {gap} sizes -> item for the tech lead")
        if r["committed"] == "XL":
            print(f"FAIL {g}: committed XL must be split")
            failed = True
        if prev is not None:
            jump = c - idx(prev["committed"], g)
            if abs(jump) >= 2:
                if r["override"]:
                    print(f"OVERRIDE {g}: jump {prev['committed']} -> {r['committed']} accepted ({r['override']})")
                else:
                    print(f"FAIL {g}: jump {prev['committed']} -> {r['committed']} is {abs(jump)} sizes -> return to gate {prev['gate']}")
                    failed = True
            else:
                print(f"OK   {g}: {prev['committed']} -> {r['committed']}")
        else:
            print(f"OK   {g}: first estimate {r['committed']}")
        prev = r
    sys.exit(1 if failed else 0)


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit(__doc__)
    main(sys.argv[1])
