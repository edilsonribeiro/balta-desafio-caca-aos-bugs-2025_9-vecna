#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${1:-https://bug-store-h2f9degcamh8ggcd.brazilsouth-01.azurewebsites.net}"

if ! command -v jq >/dev/null 2>&1; then
  echo "Este script depende de 'jq'. Instale-o e execute novamente." >&2
  exit 1
fi

RUN_ID="$(date +%s)"

log() {
  printf '\n\033[1m%s\033[0m\n' "$1"
}

print_body() {
  local body="$1"
  if [[ -z "$body" ]]; then
    echo "(sem corpo)"
    return
  fi

  local parsed
  if parsed=$(printf '%s' "$body" | jq . 2>/dev/null); then
    printf '%s\n' "$parsed"
  else
    printf '%s\n' "$body"
  fi
}

LAST_BODY=""
LAST_STATUS=""

call_api() {
  local method="$1"
  local path="$2"
  local payload="${3:-}"

  local response
  if [[ -n "$payload" ]]; then
    if ! response=$(curl -sS -X "$method" "${BASE_URL}${path}" \
      -H "Content-Type: application/json" \
      -d "$payload" \
      -w '\n%{http_code}'); then
      echo "Falha ao executar ${method} ${path}." >&2
      exit 1
    fi
  else
    if ! response=$(curl -sS -X "$method" "${BASE_URL}${path}" -w '\n%{http_code}'); then
      echo "Falha ao executar ${method} ${path}." >&2
      exit 1
    fi
  fi

  local status="${response##*$'\n'}"
  local body="${response%$'\n'*}"
  if [[ "$body" == "$status" ]]; then
    body=""
  fi

  LAST_STATUS="$status"
  LAST_BODY="$body"

  echo "Status: $status"
  print_body "$body"

  if [[ "$status" =~ ^[0-9]+$ ]] && (( status >= 400 )); then
    echo "Requisição ${method} ${path} falhou (status ${status})." >&2
    exit 1
  fi
}

delete_product() {
  local path="$1"

  if ! response=$(curl -sS -X DELETE "${BASE_URL}${path}" -w '\n%{http_code}'); then
    echo "Falha ao executar DELETE ${path}." >&2
    exit 1
  fi

  local status="${response##*$'\n'}"
  local body="${response%$'\n'*}"
  if [[ "$body" == "$status" ]]; then
    body=""
  fi

  LAST_STATUS="$status"
  LAST_BODY="$body"

  echo "Status: $status"
  print_body "$body"

  if [[ "$status" == "409" ]]; then
    echo "Produto associado a pedidos. Mantendo registro para preservar o histórico."
    return 0
  fi

  if [[ "$status" =~ ^[0-9]+$ ]] && (( status >= 400 )); then
    echo "Requisição DELETE ${path} falhou (status ${status})." >&2
    exit 1
  fi
}

extract_from_last() {
  local jq_filter="$1"
  local description="$2"
  local value

  if ! value=$(printf '%s' "$LAST_BODY" | jq -re "$jq_filter" 2>/dev/null); then
    echo "Não foi possível extrair ${description} da resposta anterior:" >&2
    print_body "$LAST_BODY"
    exit 1
  fi

  printf '%s' "$value"
}

log "Ping raiz"
call_api GET "/"

log "Criar cliente"
CREATE_CUSTOMER_PAYLOAD='{
  "name": "Bruce Wayne",
  "email": "bruce@wayneenterprises.com",
  "phone": "+55 11 99999-9999",
  "birthDate": "1980-02-19T00:00:00Z"
}'
call_api POST "/v1/customers" "${CREATE_CUSTOMER_PAYLOAD}"
CUSTOMER_ID=$(extract_from_last '.id' 'o id do cliente')

log "Criar clientes em massa"
for i in $(seq 1 15); do
  BULK_CUSTOMER_PAYLOAD=$(jq -n --arg name "Cliente ${RUN_ID}_$i" \
      --arg email "cliente${RUN_ID}_$i@example.com" \
      --arg phone "+55 11 9$(printf '%04d' "$i")-0000" \
      --arg birth "$(printf '1990-01-%02dT00:00:00Z' "$(( (i % 28) + 1 ))")" \
      '{name: $name, email: $email, phone: $phone, birthDate: $birth}')
  call_api POST "/v1/customers" "$BULK_CUSTOMER_PAYLOAD"
done

log "Listar clientes - página 1 (sort name asc)"
call_api GET "/v1/customers?page=1&pageSize=5&sortBy=name&sortOrder=asc"
printf 'Total clientes: %s\n' "$(printf '%s' "$LAST_BODY" | jq '.total')"

log "Listar clientes - página 2 (sort email desc)"
call_api GET "/v1/customers?page=2&pageSize=5&sortBy=email&sortOrder=desc"
printf 'Total clientes: %s\n' "$(printf '%s' "$LAST_BODY" | jq '.total')"

log "Atualizar cliente"
UPDATE_CUSTOMER_PAYLOAD='{
  "name": "Batman",
  "email": "batman@wayneenterprises.com",
  "phone": "+55 11 90000-0000",
  "birthDate": "1980-02-19T00:00:00Z"
}'
call_api PUT "/v1/customers/${CUSTOMER_ID}" "${UPDATE_CUSTOMER_PAYLOAD}"

log "Criar produto"
CREATE_PRODUCT_PAYLOAD='{
  "title": "Batarang",
  "description": "Arma de arremesso personalizada",
  "slug": "batarang",
  "price": 2500.00
}'
call_api POST "/v1/products" "${CREATE_PRODUCT_PAYLOAD}"
PRODUCT_ID=$(extract_from_last '.id' 'o id do produto')

log "Criar segundo produto"
CREATE_SECOND_PRODUCT_PAYLOAD=$(jq -n \
  --arg title "Smoke Pellet ${RUN_ID}" \
  --arg desc "Pelotas de fumaça para fuga rápida" \
  --arg slug "smoke-pellet-${RUN_ID}" \
  --argjson price 175.50 \
  '{title: $title, description: $desc, slug: $slug, price: $price}')
call_api POST "/v1/products" "${CREATE_SECOND_PRODUCT_PAYLOAD}"
SECOND_PRODUCT_ID=$(extract_from_last '.id' 'o id do segundo produto')

log "Criar produtos em massa"
for i in $(seq 1 20); do
  BULK_PRODUCT_PAYLOAD=$(jq -n --arg title "Gadget ${RUN_ID}_$i" \
      --arg desc "Descrição aleatória $i" \
      --arg slug "gadget-${RUN_ID}-$i" \
      --argjson price $((i * 25)) \
      '{title: $title, description: $desc, slug: $slug, price: $price}')
  call_api POST "/v1/products" "$BULK_PRODUCT_PAYLOAD"
done

log "Listar produtos - página 1 (sort price asc)"
call_api GET "/v1/products?page=1&pageSize=5&sortBy=price&sortOrder=asc"
printf 'Total produtos: %s\n' "$(printf '%s' "$LAST_BODY" | jq '.total')"

log "Listar produtos - página 3 (sort slug desc)"
call_api GET "/v1/products?page=3&pageSize=5&sortBy=slug&sortOrder=desc"
printf 'Total produtos: %s\n' "$(printf '%s' "$LAST_BODY" | jq '.total')"

log "Atualizar produto"
UPDATE_PRODUCT_PAYLOAD='{
  "title": "Batarang v2",
  "description": "Versão aprimorada com rastreador",
  "slug": "batarang-v2",
  "price": 3200.00
}'
call_api PUT "/v1/products/${PRODUCT_ID}" "${UPDATE_PRODUCT_PAYLOAD}"

log "Criar pedido"
CREATE_ORDER_PAYLOAD=$(cat <<EOF
{
  "customerId": "${CUSTOMER_ID}",
  "lines": [
    { "productId": "${PRODUCT_ID}", "quantity": 3 }
  ]
}
EOF
)
call_api POST "/v1/orders" "${CREATE_ORDER_PAYLOAD}"
ORDER_ID=$(extract_from_last '.id' 'o id do pedido')

log "Criar pedido adicional com múltiplos itens"
CREATE_SECOND_ORDER_PAYLOAD=$(cat <<EOF
{
  "customerId": "${CUSTOMER_ID}",
  "lines": [
    { "productId": "${PRODUCT_ID}", "quantity": 1 },
    { "productId": "${SECOND_PRODUCT_ID}", "quantity": 2 }
  ]
}
EOF
)
call_api POST "/v1/orders" "${CREATE_SECOND_ORDER_PAYLOAD}"
SECOND_ORDER_ID=$(extract_from_last '.id' 'o id do segundo pedido')

log "Buscar pedido por ID"
call_api GET "/v1/orders/${ORDER_ID}"

log "Buscar segundo pedido por ID"
call_api GET "/v1/orders/${SECOND_ORDER_ID}"

log "Listar pedidos - sort total asc"
call_api GET "/v1/orders?page=1&pageSize=5&sortBy=total&sortOrder=asc"
printf 'Total pedidos: %s\n' "$(printf '%s' "$LAST_BODY" | jq '.total')"

log "Listar pedidos - sort createdAt desc"
call_api GET "/v1/orders?page=1&pageSize=5&sortBy=createdAt&sortOrder=desc"
printf 'Total pedidos: %s\n' "$(printf '%s' "$LAST_BODY" | jq '.total')"

log "Relatório de vendas por cliente"
call_api GET "/v1/reports/sales-by-customer"

log "Relatório de vendas por cliente (período completo)"
call_api GET "/v1/reports/sales-by-customer?startDate=2020-01-01T00:00:00Z&endDate=2100-01-01T00:00:00Z"

log "Relatório de faturamento diário"
call_api GET "/v1/reports/revenue-by-period?groupBy=day"

log "Relatório de faturamento mensal"
call_api GET "/v1/reports/revenue-by-period?groupBy=month&startDate=2020-01-01T00:00:00Z&endDate=2100-01-01T00:00:00Z"

log "Relatório de faturamento anual"
call_api GET "/v1/reports/revenue-by-period?groupBy=year"

log "Remover produto"
delete_product "/v1/products/${PRODUCT_ID}"

log "Remover segundo produto"
delete_product "/v1/products/${SECOND_PRODUCT_ID}"

log "Remover cliente"
call_api DELETE "/v1/customers/${CUSTOMER_ID}"
