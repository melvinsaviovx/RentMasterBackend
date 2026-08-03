#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:5085}"
OWNER_EMAIL="${OWNER_EMAIL:-}"
TENANT_EMAIL="${TENANT_EMAIL:-}"
TEST_PASSWORD="${TEST_PASSWORD:-${UAT_PASSWORD:-}}"

if [[ -z "$OWNER_EMAIL" || -z "$TENANT_EMAIL" || -z "$TEST_PASSWORD" ]]; then
  echo "Set OWNER_EMAIL, TENANT_EMAIL and TEST_PASSWORD before running this check." >&2
  echo "Both accounts must already be verified, with Owner and Tenant roles." >&2
  exit 2
fi

TODAY="$(TZ=Asia/Kolkata date +%F)"
STAMP="$(date +%s)"
PHOTO_FILE="$(mktemp -t rentmaster-photo-XXXXXX).png"
trap 'rm -f "$PHOTO_FILE"' EXIT

python3 - "$PHOTO_FILE" <<'PY'
import base64, pathlib, sys
pathlib.Path(sys.argv[1]).write_bytes(base64.b64decode(
    'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII='))
PY

json_value() {
  local expression="$1"
  python3 -c "import json,sys; d=json.load(sys.stdin); print($expression)"
}

json_string() {
  python3 -c 'import json,sys; print(json.dumps(sys.argv[1]))' "$1"
}

request() {
  local method="$1" url="$2" token="${3:-}" body="${4:-}"
  local args=(-sS -f -X "$method" "$BASE_URL$url" -H 'Content-Type: application/json')
  [[ -n "$token" ]] && args+=(-H "Authorization: Bearer $token")
  [[ -n "$body" ]] && args+=(--data "$body")
  curl "${args[@]}"
}

step() { printf '\n[%s/22] %s\n' "$1" "$2"; }

step 1 "Checking Rent Master"
request GET /health >/dev/null

step 2 "Signing in the verified owner and tenant"
OWNER_LOGIN="$(request POST /api/v1/auth/login '' "{\"email\":$(json_string "$OWNER_EMAIL"),\"password\":$(json_string "$TEST_PASSWORD")}")"
TENANT_LOGIN="$(request POST /api/v1/auth/login '' "{\"email\":$(json_string "$TENANT_EMAIL"),\"password\":$(json_string "$TEST_PASSWORD")}")"
OWNER_TOKEN="$(printf '%s' "$OWNER_LOGIN" | json_value "d['accessToken']")"
TENANT_TOKEN="$(printf '%s' "$TENANT_LOGIN" | json_value "d['accessToken']")"
OWNER_CODE="$(printf '%s' "$OWNER_LOGIN" | json_value "d['publicProfileCode']")"
TENANT_CODE="$(printf '%s' "$TENANT_LOGIN" | json_value "d['publicProfileCode']")"

step 3 "Loading both reputation profiles before any review exists"
request GET "/api/v1/reputation/$OWNER_CODE?page=1&pageSize=100" "$OWNER_TOKEN" >/dev/null
request GET "/api/v1/reputation/$TENANT_CODE?page=1&pageSize=100" "$TENANT_TOKEN" >/dev/null

step 4 "Creating a draft property"
PROPERTY="$(request POST /api/v1/properties "$OWNER_TOKEN" "{\"title\":\"End-to-end home $STAMP\",\"addressLine1\":\"1 Test Street\",\"locality\":\"Thoraipakkam\",\"city\":\"Chennai\",\"state\":\"Tamil Nadu\",\"postalCode\":\"600097\",\"monthlyRent\":20000,\"securityDeposit\":60000,\"bedrooms\":2,\"bathrooms\":2,\"publish\":false}")"
PROPERTY_ID="$(printf '%s' "$PROPERTY" | json_value "d['id']")"
PROPERTY_ROW_VERSION="$(printf '%s' "$PROPERTY" | json_value "d['rowVersion']")"

step 5 "Attaching the required property photo"
curl -sS -f -X POST "$BASE_URL/api/v1/properties/$PROPERTY_ID/photos" \
  -H "Authorization: Bearer $OWNER_TOKEN" \
  -F "file=@$PHOTO_FILE;type=image/png" >/dev/null

step 6 "Publishing the property"
request PUT "/api/v1/properties/$PROPERTY_ID" "$OWNER_TOKEN" "{\"title\":\"End-to-end home $STAMP\",\"addressLine1\":\"1 Test Street\",\"locality\":\"Thoraipakkam\",\"city\":\"Chennai\",\"state\":\"Tamil Nadu\",\"postalCode\":\"600097\",\"monthlyRent\":20000,\"securityDeposit\":60000,\"bedrooms\":2,\"bathrooms\":2,\"status\":2,\"rowVersion\":\"$PROPERTY_ROW_VERSION\"}" >/dev/null

step 7 "Confirming the published property appears in tenant search"
SEARCH="$(request GET '/api/v1/properties/search?city=Chennai&locality=Thoraipakkam&page=1&pageSize=100' "$TENANT_TOKEN")"
FOUND="$(printf '%s' "$SEARCH" | json_value "any(x['id'] == '$PROPERTY_ID' for x in d['items'])")"
[[ "$FOUND" == "True" ]] || { echo "Published property did not appear in search." >&2; exit 1; }

step 8 "Tenant opening the property conversation"
CONVERSATION="$(request POST "/api/v1/chat/properties/$PROPERTY_ID/open" "$TENANT_TOKEN" '{}')"
CONVERSATION_ID="$(printf '%s' "$CONVERSATION" | json_value "d['id']")"
request POST "/api/v1/chat/conversations/$CONVERSATION_ID/messages" "$TENANT_TOKEN" '{"content":"Hello, I am interested in this property."}' >/dev/null

step 9 "Confirming the same conversation appears for owner and tenant"
OWNER_CONVERSATIONS="$(request GET '/api/v1/chat/conversations?page=1&pageSize=100' "$OWNER_TOKEN")"
TENANT_CONVERSATIONS="$(request GET '/api/v1/chat/conversations?page=1&pageSize=100' "$TENANT_TOKEN")"
for CONTENT in "$OWNER_CONVERSATIONS" "$TENANT_CONVERSATIONS"; do
  FOUND="$(printf '%s' "$CONTENT" | json_value "any(x['id'] == '$CONVERSATION_ID' for x in d['items'])")"
  [[ "$FOUND" == "True" ]] || { echo "Conversation was not visible to both participants." >&2; exit 1; }
done

step 10 "Tenant submitting an application"
APPLICATION="$(request POST "/api/v1/properties/$PROPERTY_ID/applications" "$TENANT_TOKEN" "{\"expectedMoveInDate\":\"$TODAY\",\"expectedMoveOutDate\":null,\"occupantCount\":2,\"message\":\"Application created by the complete workflow check.\"}")"
APPLICATION_ID="$(printf '%s' "$APPLICATION" | json_value "d['id']")"
APPLICATION_CONVERSATION_ID="$(printf '%s' "$APPLICATION" | json_value "d['conversationId']")"
[[ "$APPLICATION_CONVERSATION_ID" == "$CONVERSATION_ID" ]] || { echo "Application created a different conversation." >&2; exit 1; }

step 11 "Owner shortlisting the application"
request POST "/api/v1/applications/$APPLICATION_ID/shortlist" "$OWNER_TOKEN" '{"reason":"Proceeding to the next step."}' >/dev/null

step 12 "Owner accepting the application"
ACCEPTED="$(request POST "/api/v1/applications/$APPLICATION_ID/accept" "$OWNER_TOKEN" '{"reason":"Accepted for the workflow check."}')"
TENANCY_ID="$(printf '%s' "$ACCEPTED" | json_value "d['tenancyId']")"

step 13 "Tenant confirming the tenancy invitation"
CONFIRMED="$(request POST "/api/v1/tenancies/$TENANCY_ID/confirm" "$TENANT_TOKEN" '{}')"
CONFIRMED_STATUS="$(printf '%s' "$CONFIRMED" | json_value "d['status']")"
[[ "$CONFIRMED_STATUS" == "2" ]] || { echo "Tenancy starting today did not become active." >&2; exit 1; }

step 14 "Owner replying in the continuous conversation"
request POST "/api/v1/chat/conversations/$CONVERSATION_ID/messages" "$OWNER_TOKEN" '{"content":"Your tenancy is confirmed."}' >/dev/null

step 15 "Tenant requesting closure and owner approving it"
request POST "/api/v1/tenancies/$TENANCY_ID/request-end" "$TENANT_TOKEN" "{\"requestedEndDate\":\"$TODAY\",\"reason\":\"Completing the workflow check.\"}" >/dev/null
SCHEDULED="$(request POST "/api/v1/tenancies/$TENANCY_ID/confirm-end" "$OWNER_TOKEN" '{}')"
[[ "$(printf '%s' "$SCHEDULED" | json_value "d['status']")" == "6" ]] || { echo "Closure was not scheduled." >&2; exit 1; }

step 16 "Owner recording final handover"
ENDED="$(request POST "/api/v1/tenancies/$TENANCY_ID/complete-end" "$OWNER_TOKEN" '{}')"
[[ "$(printf '%s' "$ENDED" | json_value "d['status']")" == "4" ]] || { echo "Tenancy did not end." >&2; exit 1; }
[[ "$(printf '%s' "$ENDED" | json_value "d['actualEndDate']")" == "$TODAY" ]] || { echo "Actual end date was not recorded." >&2; exit 1; }

step 17 "Confirming the conversation contains user and workflow messages"
MESSAGES="$(request GET "/api/v1/chat/conversations/$CONVERSATION_ID/messages?page=1&pageSize=100" "$TENANT_TOKEN")"
MESSAGE_COUNT="$(printf '%s' "$MESSAGES" | json_value "len(d['items'])")"
(( MESSAGE_COUNT >= 7 )) || { echo "Expected conversation history was not found." >&2; exit 1; }

step 18 "Owner publishing a tenant review"
OWNER_REVIEW="$(request POST /api/v1/reviews "$OWNER_TOKEN" "{\"tenancyId\":\"$TENANCY_ID\",\"overallRating\":5,\"categoryScores\":{\"RentPayment\":5,\"PropertyCare\":5,\"NeighbourConduct\":5,\"Communication\":5},\"comment\":\"The tenancy was completed smoothly and all agreed steps were followed.\"}")"
OWNER_REVIEW_ID="$(printf '%s' "$OWNER_REVIEW" | json_value "d['reviewId']")"

step 19 "Tenant publishing an owner review"
TENANT_REVIEW="$(request POST /api/v1/reviews "$TENANT_TOKEN" "{\"tenancyId\":\"$TENANCY_ID\",\"overallRating\":5,\"categoryScores\":{\"MaintenanceResponse\":5,\"Communication\":5,\"PrivacyRespect\":5,\"DepositFairness\":5},\"comment\":\"The owner communicated clearly and completed the closure fairly.\"}")"
TENANT_REVIEW_ID="$(printf '%s' "$TENANT_REVIEW" | json_value "d['reviewId']")"

step 20 "Confirming both reviews were published immediately"
OWNER_REPUTATION="$(request GET "/api/v1/reputation/$OWNER_CODE?page=1&pageSize=100" "$TENANT_TOKEN")"
TENANT_REPUTATION="$(request GET "/api/v1/reputation/$TENANT_CODE?page=1&pageSize=100" "$OWNER_TOKEN")"
(( $(printf '%s' "$OWNER_REPUTATION" | json_value "d['totalPublishedReviews']") >= 1 )) || { echo "Owner review was not published." >&2; exit 1; }
(( $(printf '%s' "$TENANT_REPUTATION" | json_value "d['totalPublishedReviews']") >= 1 )) || { echo "Tenant review was not published." >&2; exit 1; }

step 21 "Reporting a review without removing it"
request POST "/api/v1/reviews/$OWNER_REVIEW_ID/dispute" "$TENANT_TOKEN" '{"reason":"Checking the reported-review workflow."}' >/dev/null
TENANT_REPUTATION_AFTER="$(request GET "/api/v1/reputation/$TENANT_CODE?page=1&pageSize=100" "$TENANT_TOKEN")"
REPORTED_VISIBLE="$(printf '%s' "$TENANT_REPUTATION_AFTER" | json_value "any(x['id'] == '$OWNER_REVIEW_ID' and x['isDisputed'] for x in d['reviews']['items'])")"
[[ "$REPORTED_VISIBLE" == "True" ]] || { echo "Reported review was hidden or not marked for checking." >&2; exit 1; }

step 22 "Creating and reading a support request"
TICKET="$(request POST /api/v1/support "$TENANT_TOKEN" '{"category":"Messages","subject":"Workflow check request","description":"Checking that support history records a submitted request."}')"
TICKET_ID="$(printf '%s' "$TICKET" | json_value "d['id']")"
TICKETS="$(request GET '/api/v1/support/mine?page=1&pageSize=100' "$TENANT_TOKEN")"
TICKET_FOUND="$(printf '%s' "$TICKETS" | json_value "any(x['id'] == '$TICKET_ID' for x in d['items'])")"
[[ "$TICKET_FOUND" == "True" ]] || { echo "Support request did not appear in history." >&2; exit 1; }

printf '\nPASS: Rent Master completed the full owner–tenant workflow.\n'
printf 'Property: %s\nConversation: %s\nApplication: %s\nTenancy: %s\nOwner review: %s\nTenant review: %s\nSupport request: %s\n' \
  "$PROPERTY_ID" "$CONVERSATION_ID" "$APPLICATION_ID" "$TENANCY_ID" "$OWNER_REVIEW_ID" "$TENANT_REVIEW_ID" "$TICKET_ID"
