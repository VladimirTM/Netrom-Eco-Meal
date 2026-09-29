#!/usr/bin/env bash
# Captures snapshots of the three public contracts (impact widget, package types, webhook).
# Run once docker-compose.test.yml's `api` service is up on http://localhost:8081.
set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:8081}"
OUT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/contracts"
STADIONUL_DE_GUSTURI="44444444-0000-0000-0000-000000000001"
DEMO_WEBHOOK_KEY="eco_demo_stadionul_webhook_key"

mkdir -p "$OUT_DIR"

echo "GET /api/businesses/{id}/impact"
curl -sS -w '\n%{http_code}\n' "$BASE_URL/api/businesses/$STADIONUL_DE_GUSTURI/impact" \
  > "$OUT_DIR/impact-widget.txt"

echo "GET /api/package-types"
curl -sS -w '\n%{http_code}\n' "$BASE_URL/api/package-types" \
  > "$OUT_DIR/package-types.txt"

echo "POST /api/webhooks/packages (valid key)"
curl -sS -w '\n%{http_code}\n' -X POST "$BASE_URL/api/webhooks/packages" \
  -H "X-Api-Key: $DEMO_WEBHOOK_KEY" -H "Content-Type: application/json" \
  -d '{
        "name": "R7 baseline contract package",
        "description": "Created by e2e/baseline/capture-contracts.sh",
        "price": 5.99,
        "quantity": 1,
        "weightKg": 1.0,
        "packageTypeId": "22222222-0000-0000-0000-000000000001",
        "pickupStart": "2099-01-01T10:00:00Z",
        "pickupEnd": "2099-01-01T12:00:00Z"
      }' \
  > "$OUT_DIR/webhook-valid-key.txt"

echo "POST /api/webhooks/packages (invalid key)"
curl -sS -w '\n%{http_code}\n' -X POST "$BASE_URL/api/webhooks/packages" \
  -H "X-Api-Key: not-a-real-key" -H "Content-Type: application/json" \
  -d '{"name":"x","price":1,"quantity":1,"weightKg":1,"packageTypeId":"22222222-0000-0000-0000-000000000001","pickupStart":"2099-01-01T10:00:00Z","pickupEnd":"2099-01-01T12:00:00Z"}' \
  > "$OUT_DIR/webhook-invalid-key.txt"

echo "Saved to $OUT_DIR"
