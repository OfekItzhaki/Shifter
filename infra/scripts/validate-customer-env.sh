#!/bin/bash
# Validate the env file used by a customer-hosted Docker Compose deployment.

set -euo pipefail

SHIFTER_DIR="${SHIFTER_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}"
ENV_FILE="${ENV_FILE:-$SHIFTER_DIR/infra/compose/.env}"

if [ ! -f "$ENV_FILE" ]; then
  echo "Missing env file: $ENV_FILE" >&2
  echo "Copy infra/compose/.env.customer.example to infra/compose/.env first." >&2
  exit 1
fi

env_value() {
  local key="$1"
  local value

  value="$(grep -E "^[[:space:]]*${key}=" "$ENV_FILE" | tail -n 1 | cut -d '=' -f 2- || true)"
  value="${value%%#*}"
  echo "$value" | sed -e 's/^[[:space:]]*//' -e 's/[[:space:]]*$//'
}

errors=0
warnings=0

require_value() {
  local key="$1"
  local value

  value="$(env_value "$key")"
  if [ -z "$value" ]; then
    echo "ERROR: $key is required." >&2
    errors=$((errors + 1))
    return
  fi

  case "$value" in
    change_me*|changeme*|your-*|*customer.example*|*example.com*)
      echo "ERROR: $key still looks like a placeholder: $value" >&2
      errors=$((errors + 1))
      ;;
  esac
}

warn_if_set() {
  local key="$1"
  local reason="$2"
  local value

  value="$(env_value "$key")"
  if [ -n "$value" ]; then
    echo "WARN: $key is set. $reason" >&2
    warnings=$((warnings + 1))
  fi
}

warn_if_empty() {
  local key="$1"
  local reason="$2"
  local value

  value="$(env_value "$key")"
  if [ -z "$value" ]; then
    echo "WARN: $key is empty. $reason" >&2
    warnings=$((warnings + 1))
  fi
}

has_value() {
  local key="$1"
  [ -n "$(env_value "$key")" ]
}

require_complete_group() {
  local name="$1"
  shift
  local keys=("$@")
  local has_any=0
  local key

  for key in "${keys[@]}"; do
    if has_value "$key"; then
      has_any=1
      break
    fi
  done

  if [ "$has_any" -eq 0 ]; then
    return
  fi

  for key in "${keys[@]}"; do
    if ! has_value "$key"; then
      echo "ERROR: $name is partially configured. $key is required when any $name setting is set." >&2
      errors=$((errors + 1))
    else
      require_value "$key"
    fi
  done
}

validate_integer_range() {
  local key="$1"
  local min="$2"
  local max="$3"
  local value

  value="$(env_value "$key")"
  if [ -z "$value" ]; then
    return
  fi

  if ! echo "$value" | grep -Eq '^-?[0-9]+$'; then
    echo "ERROR: $key must be an integer between $min and $max." >&2
    errors=$((errors + 1))
    return
  fi

  if [ "$value" -lt "$min" ] || [ "$value" -gt "$max" ]; then
    echo "ERROR: $key must be between $min and $max." >&2
    errors=$((errors + 1))
  fi
}

validate_boolean() {
  local key="$1"
  local value

  value="$(env_value "$key")"
  if [ -z "$value" ]; then
    return
  fi

  if [ "$value" != "true" ] && [ "$value" != "false" ]; then
    echo "ERROR: $key must be true, false, or empty." >&2
    errors=$((errors + 1))
  fi
}

mode="$(env_value SHIFTER_DEPLOYMENT_MODE)"
if [ "$mode" != "customer-hosted" ]; then
  echo "ERROR: SHIFTER_DEPLOYMENT_MODE must be customer-hosted for this validator." >&2
  errors=$((errors + 1))
fi

required_keys=(
  POSTGRES_DB
  POSTGRES_USER
  POSTGRES_PASSWORD
  REDIS_PASSWORD
  API_PORT
  WEB_PORT
  SHIFTER_LICENSEE
  JWT_SECRET
  JWT_ISSUER
  JWT_AUDIENCE
  FIELD_ENCRYPTION_KEY
  SOLVER_TIMEOUT_SECONDS
  APP_FRONTEND_BASE_URL
  APP_API_BASE_URL
  NEXT_PUBLIC_API_URL
  NEXT_PUBLIC_LEGAL_EMAIL
  MINIO_ROOT_USER
  MINIO_ROOT_PASSWORD
  SEQ_ADMIN_PASSWORD
)

for key in "${required_keys[@]}"; do
  require_value "$key"
done

jwt_secret="$(env_value JWT_SECRET)"
if [ -n "$jwt_secret" ] && [ "${#jwt_secret}" -lt 32 ]; then
  echo "ERROR: JWT_SECRET must be at least 32 characters." >&2
  errors=$((errors + 1))
fi

license_key="$(env_value SHIFTER_LICENSE_KEY)"
license_file_host_path="$(env_value SHIFTER_LICENSE_FILE_HOST_PATH)"
license_file_container_path="$(env_value SHIFTER_LICENSE_FILE_CONTAINER_PATH)"
license_public_key="$(env_value SHIFTER_LICENSE_PUBLIC_KEY)"
uses_signed_license_file=0
if [ -n "$license_file_container_path" ]; then
  uses_signed_license_file=1
fi

if [ -z "$license_key" ] && [ "$uses_signed_license_file" -eq 0 ]; then
  echo "ERROR: SHIFTER_LICENSE_KEY is required unless SHIFTER_LICENSE_FILE_CONTAINER_PATH and SHIFTER_LICENSE_PUBLIC_KEY are configured." >&2
  errors=$((errors + 1))
elif [ -n "$license_key" ] && [ "${#license_key}" -lt 24 ]; then
  echo "ERROR: SHIFTER_LICENSE_KEY must be at least 24 characters." >&2
  errors=$((errors + 1))
fi

if [ "$uses_signed_license_file" -eq 1 ] || [ -n "$license_public_key" ]; then
  if [ -z "$license_file_host_path" ]; then
    echo "ERROR: SHIFTER_LICENSE_FILE_HOST_PATH is required when signed license file mode is configured." >&2
    errors=$((errors + 1))
  elif echo "$license_file_host_path" | grep -Eq 'license\.customer\.example\.json$'; then
    echo "ERROR: SHIFTER_LICENSE_FILE_HOST_PATH still points at the example license file." >&2
    errors=$((errors + 1))
  fi

  if [ -z "$license_file_container_path" ]; then
    echo "ERROR: SHIFTER_LICENSE_FILE_CONTAINER_PATH is required when signed license file mode is configured." >&2
    errors=$((errors + 1))
  fi

  if [ -z "$license_public_key" ]; then
    echo "ERROR: SHIFTER_LICENSE_PUBLIC_KEY is required when signed license file mode is configured." >&2
    errors=$((errors + 1))
  fi
fi

field_encryption_key="$(env_value FIELD_ENCRYPTION_KEY)"
if [ -n "$field_encryption_key" ] && [ "${#field_encryption_key}" -lt 32 ]; then
  echo "ERROR: FIELD_ENCRYPTION_KEY must be at least 32 characters." >&2
  errors=$((errors + 1))
fi

api_url="$(env_value APP_API_BASE_URL)"
public_api_url="$(env_value NEXT_PUBLIC_API_URL)"
for url_name in APP_FRONTEND_BASE_URL APP_API_BASE_URL NEXT_PUBLIC_API_URL; do
  url_value="$(env_value "$url_name")"
  if [ -n "$url_value" ] && ! echo "$url_value" | grep -Eq '^https://'; then
    echo "WARN: $url_name should be HTTPS in production: $url_value" >&2
    warnings=$((warnings + 1))
  fi
done

if [ -n "$api_url" ] && [ -n "$public_api_url" ] && [ "$api_url" != "$public_api_url" ]; then
  echo "WARN: APP_API_BASE_URL and NEXT_PUBLIC_API_URL differ. This is valid only if intended." >&2
  warnings=$((warnings + 1))
fi

ai_key="$(env_value AI_API_KEY)"
ai_base_url="$(env_value AI_BASE_URL)"
ai_model="$(env_value AI_MODEL)"
ai_no_export="$(env_value AI_NO_EXPORT_REQUIRED)"
if [ -n "$ai_key" ] && [ -z "$ai_base_url" ]; then
  echo "WARN: AI_API_KEY is set but AI_BASE_URL is empty; the API will use OpenAI's default endpoint." >&2
  warnings=$((warnings + 1))
fi
if [ -n "$ai_base_url" ] && [ -z "$ai_model" ]; then
  echo "ERROR: AI_MODEL is required when AI_BASE_URL is set." >&2
  errors=$((errors + 1))
fi
if [ -z "$ai_key" ] && [ -z "$ai_base_url" ]; then
  echo "WARN: AI is disabled. Schedule solving still works, but AI chat/import features will not." >&2
  warnings=$((warnings + 1))
fi
if [ "$ai_no_export" = "true" ]; then
  if [ -z "$ai_base_url" ]; then
    echo "ERROR: AI_NO_EXPORT_REQUIRED=true requires AI_BASE_URL to point to a private/local OpenAI-compatible endpoint." >&2
    errors=$((errors + 1))
  elif ! echo "$ai_base_url" | grep -Eiq '^(http://(localhost|127\.|10\.|192\.168\.|172\.(1[6-9]|2[0-9]|3[0-1])\.|[^/]*\.(internal|local))|https://[^/]*\.(internal|local))'; then
    echo "ERROR: AI_NO_EXPORT_REQUIRED=true requires AI_BASE_URL to use localhost, a private IP, .internal, or .local endpoint: $ai_base_url" >&2
    errors=$((errors + 1))
  fi
elif [ -n "$ai_no_export" ] && [ "$ai_no_export" != "false" ]; then
  echo "ERROR: AI_NO_EXPORT_REQUIRED must be true, false, or empty." >&2
  errors=$((errors + 1))
fi

storage_bucket="$(env_value STORAGE_S3_BUCKET_NAME)"
storage_service_url="$(env_value STORAGE_S3_SERVICE_URL)"
if [ -n "$storage_bucket" ]; then
  require_value STORAGE_S3_ACCESS_KEY
  require_value STORAGE_S3_SECRET_KEY
  if echo "$storage_service_url" | grep -q 'minio:9000' && [ "$(env_value STORAGE_S3_FORCE_PATH_STYLE)" != "true" ]; then
    echo "ERROR: STORAGE_S3_FORCE_PATH_STYLE must be true for bundled MinIO." >&2
    errors=$((errors + 1))
  fi
else
  echo "WARN: STORAGE_S3_BUCKET_NAME is empty; uploads will use local disk storage." >&2
  warnings=$((warnings + 1))
fi

warn_if_empty RESEND_API_KEY "Email delivery will be logged only; password reset and invitations may not reach users."
if has_value RESEND_API_KEY; then
  require_value RESEND_FROM_EMAIL
  require_value RESEND_FROM_NAME
fi
require_complete_group "Twilio" TWILIO_ACCOUNT_SID TWILIO_AUTH_TOKEN TWILIO_WHATSAPP_FROM
require_complete_group "Web Push VAPID" VAPID_PUBLIC_KEY VAPID_PRIVATE_KEY VAPID_SUBJECT NEXT_PUBLIC_VAPID_PUBLIC_KEY
vapid_public_key="$(env_value VAPID_PUBLIC_KEY)"
next_public_vapid_public_key="$(env_value NEXT_PUBLIC_VAPID_PUBLIC_KEY)"
if has_value VAPID_PUBLIC_KEY && has_value NEXT_PUBLIC_VAPID_PUBLIC_KEY && [ "$vapid_public_key" != "$next_public_vapid_public_key" ]; then
  echo "ERROR: NEXT_PUBLIC_VAPID_PUBLIC_KEY must match VAPID_PUBLIC_KEY." >&2
  errors=$((errors + 1))
fi

validate_integer_range SELF_SERVICE_DEFAULT_MIN_SHIFTS_PER_CYCLE 0 100
validate_integer_range SELF_SERVICE_DEFAULT_MAX_SHIFTS_PER_CYCLE 1 100
validate_integer_range SELF_SERVICE_DEFAULT_REQUEST_WINDOW_OPEN_OFFSET_HOURS 1 720
validate_integer_range SELF_SERVICE_DEFAULT_REQUEST_WINDOW_CLOSE_OFFSET_HOURS 1 720
validate_integer_range SELF_SERVICE_DEFAULT_CANCELLATION_CUTOFF_HOURS 1 720
validate_integer_range SELF_SERVICE_DEFAULT_MAX_ABSENCES_PER_CYCLE 0 100
validate_integer_range SELF_SERVICE_DEFAULT_MAX_LATE_CANCELLATIONS_PER_CYCLE 0 100
validate_integer_range SELF_SERVICE_DEFAULT_LATE_CANCELLATION_WINDOW_HOURS 1 720
validate_integer_range SELF_SERVICE_DEFAULT_WAITLIST_OFFER_MINUTES 15 1440
validate_integer_range SELF_SERVICE_DEFAULT_CYCLE_DURATION_DAYS 1 30

default_min_shifts="$(env_value SELF_SERVICE_DEFAULT_MIN_SHIFTS_PER_CYCLE)"
default_max_shifts="$(env_value SELF_SERVICE_DEFAULT_MAX_SHIFTS_PER_CYCLE)"
if echo "$default_min_shifts" | grep -Eq '^-?[0-9]+$' && echo "$default_max_shifts" | grep -Eq '^-?[0-9]+$' && [ "$default_min_shifts" -gt "$default_max_shifts" ]; then
  echo "ERROR: SELF_SERVICE_DEFAULT_MIN_SHIFTS_PER_CYCLE must be less than or equal to SELF_SERVICE_DEFAULT_MAX_SHIFTS_PER_CYCLE." >&2
  errors=$((errors + 1))
fi

default_open_offset="$(env_value SELF_SERVICE_DEFAULT_REQUEST_WINDOW_OPEN_OFFSET_HOURS)"
default_close_offset="$(env_value SELF_SERVICE_DEFAULT_REQUEST_WINDOW_CLOSE_OFFSET_HOURS)"
if echo "$default_open_offset" | grep -Eq '^-?[0-9]+$' && echo "$default_close_offset" | grep -Eq '^-?[0-9]+$' && [ "$default_open_offset" -le "$default_close_offset" ]; then
  echo "ERROR: SELF_SERVICE_DEFAULT_REQUEST_WINDOW_OPEN_OFFSET_HOURS must be greater than SELF_SERVICE_DEFAULT_REQUEST_WINDOW_CLOSE_OFFSET_HOURS." >&2
  errors=$((errors + 1))
fi

validate_boolean SELF_SERVICE_DEFAULT_ALLOW_MEMBER_SHIFT_CLAIMS
validate_boolean SELF_SERVICE_DEFAULT_ALLOW_WAITLIST
validate_boolean SELF_SERVICE_DEFAULT_ALLOW_SHIFT_CHANGE_REQUESTS
validate_boolean SELF_SERVICE_DEFAULT_ALLOW_ABSENCE_REPORTS
validate_boolean SELF_SERVICE_DEFAULT_ALLOW_SHIFT_SWAPS
require_complete_group "Pushover health alerts" PUSHOVER_USER_KEY PUSHOVER_APP_TOKEN
require_complete_group "LemonSqueezy" \
  LEMONSQUEEZY_API_KEY \
  LEMONSQUEEZY_WEBHOOK_SECRET \
  LEMONSQUEEZY_STORE_ID \
  LEMONSQUEEZY_DEFAULT_VARIANT_ID \
  LEMONSQUEEZY_TEST_VARIANT_ID
warn_if_set NEXT_PUBLIC_POSTHOG_KEY "Analytics may send usage data outside the customer environment."
warn_if_set NEXT_PUBLIC_SENTRY_DSN "Frontend errors may be sent outside the customer environment."
warn_if_set NEXT_PUBLIC_CRISP_WEBSITE_ID "Chat widget data may be sent outside the customer environment."
warn_if_set LEMONSQUEEZY_API_KEY "Billing calls may leave the customer environment."

if [ "$errors" -gt 0 ]; then
  echo "Validation failed: $errors error(s), $warnings warning(s)." >&2
  exit 1
fi

echo "Validation passed: $warnings warning(s)."
