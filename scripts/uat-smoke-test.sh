#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:5085}"
OWNER_EMAIL="${OWNER_EMAIL:-owner@rentmaster.local}"
TENANT_EMAIL="${TENANT_EMAIL:-tenant@rentmaster.local}"
DEMO_PASSWORD="${DEMO_PASSWORD:-RentMasterDemo@2026!}"
TODAY="$(TZ=Asia/Kolkata date +%F)"
STAMP="$(date +%s)"

json_value() {
  local expression="$1"
  python3 -c "import json,sys; d=json.load(sys.stdin); print($expression)"
}

request() {
  local method="$1" url="$2" token="${3:-}" body="${4:-}"
  local args=(-sS -f -X "$method" "$BASE_URL$url" -H 'Content-Type: application/json')
  [[ -n "$token" ]] && args+=(-H "Authorization: Bearer $token")
  [[ -n "$body" ]] && args+=(--data "$body")
  curl "${args[@]}"
}

echo "[1/12] Checking API health"
request GET /health >/dev/null

echo "[2/12] Signing in seeded owner and tenant"
OWNER_LOGIN="$(request POST /api/v1/auth/login '' "{\"email\":\"$OWNER_EMAIL\",\"password\":\"$DEMO_PASSWORD\"}")"
TENANT_LOGIN="$(request POST /api/v1/auth/login '' "{\"email\":\"$TENANT_EMAIL\",\"password\":\"$DEMO_PASSWORD\"}")"
OWNER_TOKEN="$(printf '%s' "$OWNER_LOGIN" | json_value "d['accessToken']")"
TENANT_TOKEN="$(printf '%s' "$TENANT_LOGIN" | json_value "d['accessToken']")"

echo "[3/12] Creating a published UAT property"
PROPERTY="$(request POST /api/v1/properties "$OWNER_TOKEN" "{\"title\":\"UAT Flow Property $STAMP\",\"addressLine1\":\"1 Test Street\",\"locality\":\"Thoraipakkam\",\"city\":\"Chennai\",\"state\":\"Tamil Nadu\",\"postalCode\":\"600097\",\"monthlyRent\":20000,\"securityDeposit\":60000,\"bedrooms\":2,\"bathrooms\":2,\"publish\":true}")"
PROPERTY_ID="$(printf '%s' "$PROPERTY" | json_value "d['id']")"

echo "[4/12] Opening owner-tenant property chat"
CONVERSATION="$(request POST "/api/v1/chat/properties/$PROPERTY_ID/open" "$TENANT_TOKEN" '{}')"
CONVERSATION_ID="$(printf '%s' "$CONVERSATION" | json_value "d['id']")"
request POST "/api/v1/chat/conversations/$CONVERSATION_ID/messages" "$TENANT_TOKEN" '{"content":"Hello, I am interested in this UAT property."}' >/dev/null

echo "[5/12] Submitting tenant application"
APPLICATION="$(request POST "/api/v1/properties/$PROPERTY_ID/applications" "$TENANT_TOKEN" "{\"expectedMoveInDate\":\"$TODAY\",\"expectedMoveOutDate\":null,\"occupantCount\":2,\"message\":\"UAT application for the complete local workflow.\"}")"
APPLICATION_ID="$(printf '%s' "$APPLICATION" | json_value "d['id']")"
APPLICATION_CONVERSATION_ID="$(printf '%s' "$APPLICATION" | json_value "d['conversationId']")"
if [[ "$APPLICATION_CONVERSATION_ID" != "$CONVERSATION_ID" ]]; then
  echo "Smoke test failed: application was not linked to the property conversation." >&2
  exit 1
fi

echo "[6/12] Owner shortlisting application"
request POST "/api/v1/applications/$APPLICATION_ID/shortlist" "$OWNER_TOKEN" '{"reason":"UAT shortlist"}' >/dev/null

echo "[7/12] Owner accepting and creating tenancy invitation"
ACCEPTED="$(request POST "/api/v1/applications/$APPLICATION_ID/accept" "$OWNER_TOKEN" '{"reason":"UAT accepted"}')"
TENANCY_ID="$(printf '%s' "$ACCEPTED" | json_value "d['tenancyId']")"
PROPERTIES_AFTER_ACCEPT="$(request GET '/api/v1/properties/mine?page=1&pageSize=100' "$OWNER_TOKEN")"
STATUS_AFTER_ACCEPT="$(printf '%s' "$PROPERTIES_AFTER_ACCEPT" | json_value "next(x['status'] for x in d['items'] if x['id'] == '$PROPERTY_ID')")"
if [[ "$STATUS_AFTER_ACCEPT" != "5" ]]; then
  echo "Smoke test failed: accepted application should reserve the property (status 5), received $STATUS_AFTER_ACCEPT" >&2
  exit 1
fi

echo "[8/12] Tenant confirming tenancy"
CONFIRMED_TENANCY="$(request POST "/api/v1/tenancies/$TENANCY_ID/confirm" "$TENANT_TOKEN" '{}')"
TENANCY_CONVERSATION_ID="$(printf '%s' "$CONFIRMED_TENANCY" | json_value "d['conversationId']")"
if [[ "$TENANCY_CONVERSATION_ID" != "$CONVERSATION_ID" ]]; then
  echo "Smoke test failed: tenancy was not linked to the property conversation." >&2
  exit 1
fi
PROPERTIES_AFTER_CONFIRM="$(request GET '/api/v1/properties/mine?page=1&pageSize=100' "$OWNER_TOKEN")"
STATUS_AFTER_CONFIRM="$(printf '%s' "$PROPERTIES_AFTER_CONFIRM" | json_value "next(x['status'] for x in d['items'] if x['id'] == '$PROPERTY_ID')")"
if [[ "$STATUS_AFTER_CONFIRM" != "3" ]]; then
  echo "Smoke test failed: confirmed tenancy should occupy the property (status 3), received $STATUS_AFTER_CONFIRM" >&2
  exit 1
fi

echo "[9/12] Owner replying in chat"
request POST "/api/v1/chat/conversations/$CONVERSATION_ID/messages" "$OWNER_TOKEN" '{"content":"Your application and tenancy are confirmed for UAT."}' >/dev/null

echo "[10/12] Tenant requesting move-out for today"
request POST "/api/v1/tenancies/$TENANCY_ID/request-end" "$TENANT_TOKEN" "{\"requestedEndDate\":\"$TODAY\",\"reason\":\"Complete UAT closure workflow\"}" >/dev/null

echo "[11/12] Owner approving closure"
ENDED="$(request POST "/api/v1/tenancies/$TENANCY_ID/confirm-end" "$OWNER_TOKEN" '{}')"
STATUS="$(printf '%s' "$ENDED" | json_value "d['status']")"
ACTUAL_END="$(printf '%s' "$ENDED" | json_value "d['actualEndDate']")"

if [[ "$STATUS" != "4" || "$ACTUAL_END" != "$TODAY" ]]; then
  echo "Smoke test failed: expected ended status=4 and actualEndDate=$TODAY, received status=$STATUS actualEndDate=$ACTUAL_END" >&2
  exit 1
fi
PROPERTIES_AFTER_END="$(request GET '/api/v1/properties/mine?page=1&pageSize=100' "$OWNER_TOKEN")"
STATUS_AFTER_END="$(printf '%s' "$PROPERTIES_AFTER_END" | json_value "next(x['status'] for x in d['items'] if x['id'] == '$PROPERTY_ID')")"
if [[ "$STATUS_AFTER_END" != "2" ]]; then
  echo "Smoke test failed: ended tenancy should republish the property (status 2), received $STATUS_AFTER_END" >&2
  exit 1
fi

echo "[12/12] Checking chat history"
MESSAGES="$(request GET "/api/v1/chat/conversations/$CONVERSATION_ID/messages?page=1&pageSize=100" "$TENANT_TOKEN")"
MESSAGE_COUNT="$(printf '%s' "$MESSAGES" | json_value "len(d['items'])")"
if (( MESSAGE_COUNT < 6 )); then
  echo "Smoke test failed: expected chat plus workflow messages, received $MESSAGE_COUNT" >&2
  exit 1
fi

echo
echo "PASS: Full local workflow completed."
echo "Property: $PROPERTY_ID"
echo "Conversation: $CONVERSATION_ID"
echo "Application: $APPLICATION_ID"
echo "Tenancy: $TENANCY_ID"
