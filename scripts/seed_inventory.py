#!/usr/bin/env python3
"""Seed the Inventory service with sample StockItems through the real admin API
(gateway -> Identity for auth, gateway -> Inventory for the actual seeding),
so the data goes through the same validation/upsert logic real admins would hit.

Usage:
    python scripts/seed_inventory.py

Env overrides:
    GATEWAY_URL          default http://localhost:8080
    SEED_ADMIN_EMAIL      default seed-admin@orderflow.local
    SEED_ADMIN_PASSWORD   default Seed12345!
    SEED_COUNT            default 100

Requires the Admin role already granted to SEED_ADMIN_EMAIL - see `make grant-admin`.
"""

import json
import os
import random
import sys
import urllib.error
import urllib.request

GATEWAY_URL = os.environ.get("GATEWAY_URL", "http://localhost:8080")
SEED_EMAIL = os.environ.get("SEED_ADMIN_EMAIL", "seed-admin@orderflow.local")
SEED_PASSWORD = os.environ.get("SEED_ADMIN_PASSWORD", "Seed12345!")
COUNT = int(os.environ.get("SEED_COUNT", "100"))

ADJECTIVES = [
    "Compact", "Rugged", "Wireless", "Portable", "Ergonomic", "Premium",
    "Industrial", "Smart", "Lightweight", "Heavy-Duty", "Foldable", "Modular",
]
NOUNS = [
    "Widget", "Bracket", "Sensor", "Adapter", "Gauge", "Controller",
    "Enclosure", "Fastener", "Battery", "Cable", "Panel", "Valve",
]


def request(method, path, body=None, token=None):
    url = f"{GATEWAY_URL}{path}"
    data = json.dumps(body).encode() if body is not None else None
    headers = {"Content-Type": "application/json"}
    if token:
        headers["Authorization"] = f"Bearer {token}"

    req = urllib.request.Request(url, data=data, headers=headers, method=method)
    try:
        with urllib.request.urlopen(req) as resp:
            raw = resp.read()
            return resp.status, (json.loads(raw) if raw else {})
    except urllib.error.HTTPError as e:
        raw = e.read().decode(errors="replace")
        try:
            return e.code, json.loads(raw)
        except json.JSONDecodeError:
            return e.code, {"raw": raw}
    except urllib.error.URLError as e:
        print(f"Could not reach {url}: {e.reason}. Is the stack up (`make up`)?", file=sys.stderr)
        sys.exit(1)


def ensure_admin_user():
    status, body = request("POST", "/identity/register", {"email": SEED_EMAIL, "password": SEED_PASSWORD})
    if status == 200:
        print(f"Registered seed admin user {SEED_EMAIL}")
    else:
        print(f"Register returned {status} ({body.get('message', body)}) - assuming {SEED_EMAIL} already exists")

    status, body = request("POST", "/identity/login", {"email": SEED_EMAIL, "password": SEED_PASSWORD})
    if status != 200:
        print(f"Login failed ({status}): {body}", file=sys.stderr)
        sys.exit(1)

    return body["accessToken"]


def product_names(count):
    combos = [f"{adj} {noun}" for adj in ADJECTIVES for noun in NOUNS]
    random.shuffle(combos)
    if count > len(combos):
        raise ValueError(f"Only {len(combos)} unique adjective/noun combinations available, requested {count}")
    return combos[:count]


def main():
    token = ensure_admin_user()

    status, _ = request("GET", "/inventory/api/stock?pageSize=1", token=token)
    if status == 403:
        print(
            f"{SEED_EMAIL} does not have the Admin role yet.\n"
            f"Run: make grant-admin EMAIL={SEED_EMAIL}\nThen re-run this script.",
            file=sys.stderr,
        )
        sys.exit(1)
    if status != 200:
        print(f"Unexpected {status} probing /inventory/api/stock: {_}", file=sys.stderr)
        sys.exit(1)

    created = 0
    for name in product_names(COUNT):
        quantity = random.randint(0, 500)
        status, body = request(
            "POST", "/inventory/api/stock",
            {"productName": name, "quantityAvailable": quantity},
            token=token,
        )
        if status != 200:
            print(f"Failed to seed '{name}': {status} {body}", file=sys.stderr)
            continue
        created += 1
        if created % 10 == 0:
            print(f"Seeded {created}/{COUNT}...")

    print(f"Done. Seeded {created}/{COUNT} stock items.")


if __name__ == "__main__":
    main()
