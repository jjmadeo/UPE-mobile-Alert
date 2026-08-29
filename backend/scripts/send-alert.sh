#!/usr/bin/env bash
#
# Dispara una alerta real contra el backend (POST /api/alerts), simulando al
# backend PROPIO de un cuartel — mismo rol que send-test-alert.js, pero sin
# depender de Node (solo curl, que ya viene en casi cualquier Linux/Mac/WSL).
# Pensado para poder probar la app sin instalar nada más que Docker + este
# script.
#
# Requiere: backend/ corriendo (`docker compose up -d` en backend/) y la app
# logueada al menos una vez contra ESE backend (para que el device token
# quede registrado — la URL configurada en la app, campo "Servidor" del
# login, tiene que apuntar a la misma IP:puerto que BACKEND_URL acá).
#
# Uso:
#   ./send-alert.sh
#   ./send-alert.sh --title "Incendio" --message "Depósito de químicos" --address "Av. Siempre Viva 742"
#   ./send-alert.sh --firefighter-ids 1,2       # a mano, si ya sabés los ids
#   ./send-alert.sh --api-key otra-key
#   ./send-alert.sh --lat -32.89 --lng -68.84
#   BACKEND_URL=http://192.168.0.28:5080 ./send-alert.sh
#
set -euo pipefail

BACKEND_URL="${BACKEND_URL:-http://localhost:5080}"
API_KEY="demo-central-CAMBIAR-EN-SERIO-esto-es-solo-para-dev"
INSTITUTION_CODE="BOMBEROS-CENTRAL"
USERNAME="juan"
PASSWORD="1234"
TITLE="Incendio estructural"
MESSAGE="Se solicita apoyo urgente."
ADDRESS="Av. Siempre Viva 742"
LAT=""
LNG=""
FIREFIGHTER_IDS=""
CORRELATION_ID=""

while [ $# -gt 0 ]; do
  case "$1" in
    --backend-url) BACKEND_URL="$2"; shift 2 ;;
    --api-key) API_KEY="$2"; shift 2 ;;
    --title) TITLE="$2"; shift 2 ;;
    --message) MESSAGE="$2"; shift 2 ;;
    --address) ADDRESS="$2"; shift 2 ;;
    --lat) LAT="$2"; shift 2 ;;
    --lng) LNG="$2"; shift 2 ;;
    --firefighter-ids) FIREFIGHTER_IDS="$2"; shift 2 ;;
    --correlation-id) CORRELATION_ID="$2"; shift 2 ;;
    -h|--help)
      cat <<'USAGE'
Uso: send-alert.sh [opciones]

  --backend-url URL       default: $BACKEND_URL o http://localhost:5080
  --api-key KEY           default: la API key demo sembrada en dev
  --title TEXT            default: "Incendio estructural"
  --message TEXT          default: "Se solicita apoyo urgente."
  --address TEXT          default: "Av. Siempre Viva 742"
  --lat NUM --lng NUM     ubicación del siniestro (opcional)
  --firefighter-ids IDS   ej: "1,2" — sin esto, resuelve el id logueándose como juan
  --correlation-id UUID   default: generado automático
USAGE
      exit 0
      ;;
    *)
      echo "Flag desconocida: $1" >&2
      exit 1
      ;;
  esac
done

if ! command -v curl >/dev/null 2>&1; then
  echo "Hace falta curl (no está instalado o no está en el PATH)." >&2
  exit 1
fi

# Sin --firefighter-ids, resuelve el id del usuario de prueba logueándose —
# así el script sigue andando aunque se resetee la base (los ids
# autoincrementales pueden cambiar).
if [ -z "$FIREFIGHTER_IDS" ]; then
  login_body=$(printf '{"institutionCode":"%s","username":"%s","password":"%s"}' \
    "$INSTITUTION_CODE" "$USERNAME" "$PASSWORD")
  login_response=$(curl -sS -X POST "$BACKEND_URL/api/auth/login" \
    -H "Content-Type: application/json" \
    -d "$login_body") || {
      echo "No se pudo conectar a $BACKEND_URL. ¿Está corriendo backend/ (docker compose up -d)?" >&2
      exit 1
    }
  # firefighter.id viaja como string en el JSON del login (FirefighterDto.Id
  # es string, ver AuthService.cs) — CreateAlertRequestDto.FirefighterIds
  # necesita number, por eso se despoja de las comillas acá.
  FIREFIGHTER_IDS=$(printf '%s' "$login_response" \
    | grep -oE '"firefighter":\{"id":"[0-9]+"' \
    | grep -oE '[0-9]+' || true)
  if [ -z "$FIREFIGHTER_IDS" ]; then
    echo "No se pudo loguear como '$USERNAME' para resolver su firefighterId." >&2
    echo "Pasá --firefighter-ids 1,2 a mano, o revisá que backend/ esté corriendo en $BACKEND_URL." >&2
    echo "Respuesta del login: $login_response" >&2
    exit 1
  fi
fi

if [ -z "$CORRELATION_ID" ]; then
  if command -v uuidgen >/dev/null 2>&1; then
    CORRELATION_ID=$(uuidgen)
  else
    # Fallback sin uuidgen — no es un UUID v4 de verdad, pero alcanza para
    # que sea único (que es todo lo que correlationId necesita acá).
    CORRELATION_ID=$(date +%s%N)-$RANDOM
  fi
fi

# firefighterIds como array JSON: "1,2" -> [1,2]
firefighter_ids_json="[$(echo "$FIREFIGHTER_IDS" | sed 's/,/, /g')]"

body=$(cat <<JSON
{
  "correlationId": "$CORRELATION_ID",
  "title": "$TITLE",
  "message": "$MESSAGE",
  "address": "$ADDRESS",
  "latitude": ${LAT:-null},
  "longitude": ${LNG:-null},
  "firefighterIds": $firefighter_ids_json
}
JSON
)

echo "POST $BACKEND_URL/api/alerts"
echo "firefighterIds: $FIREFIGHTER_IDS"
echo "correlationId: $CORRELATION_ID"
echo

http_code=$(curl -sS -o /tmp/send-alert-response.json -w "%{http_code}" \
  -X POST "$BACKEND_URL/api/alerts" \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: $API_KEY" \
  -d "$body")

response=$(cat /tmp/send-alert-response.json)
rm -f /tmp/send-alert-response.json

echo "status: $http_code"
echo "$response"

if echo "$response" | grep -q '"unknownFirefighterIds":\[[0-9]'; then
  echo
  echo "⚠️  hay ids que no existen en la institución — ver unknownFirefighterIds arriba." >&2
fi
if echo "$response" | grep -q '"firefightersWithoutDevice":\[[0-9]'; then
  echo "⚠️  hay ids sin ningún device token registrado — ver firefightersWithoutDevice arriba." >&2
fi
