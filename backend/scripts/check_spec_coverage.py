#!/usr/bin/env python3
"""SPEC §23.3 — falha se Parking.Application < 75% ou Parking.Api < 60% (linhas)."""
import sys
import xml.etree.ElementTree as ET

def main() -> int:
    if len(sys.argv) != 2:
        print("usage: check_spec_coverage.py <coverage.cobertura.xml>", file=sys.stderr)
        return 2
    tree = ET.parse(sys.argv[1])
    root = tree.getroot()
    want = {
        "Parking.Application": 0.75,
        "Parking.Api": 0.60,
    }
    found = {}
    for pkg in root.findall(".//package"):
        name = pkg.get("name")
        if name in want:
            found[name] = float(pkg.get("line-rate", "0"))
    ok = True
    for name, min_lr in want.items():
        lr = found.get(name)
        if lr is None:
            print(f"::error::Coverage: package {name} missing from cobertura.")
            ok = False
            continue
        if lr + 1e-9 < min_lr:
            pct = lr * 100
            need = min_lr * 100
            print(f"::error::SPEC 23.3: {name} lines {pct:.1f}% < minimum {need:.0f}%.")
            ok = False
        else:
            print(f"{name}: {lr * 100:.1f}% (min {min_lr * 100:.0f}%)")
    return 0 if ok else 1

if __name__ == "__main__":
    raise SystemExit(main())
