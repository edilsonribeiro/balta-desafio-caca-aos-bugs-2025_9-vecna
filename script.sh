#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${1:-http://localhost:5169}"
LOAD_TEST_ITERATIONS="${LOAD_TEST_ITERATIONS:-100}"
STRESS_SEARCH_ITERATIONS="${STRESS_SEARCH_ITERATIONS:-25}"

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

urlencode() {
  local raw="$1"
  jq -rn --arg v "$raw" '$v|@uri'
}

LAST_BODY=""
LAST_STATUS=""
REQUEST_STATUS=""
REQUEST_BODY=""
REQUEST_TOTAL_TIME=""
REQUEST_START_TRANSFER=""
REQUEST_READ_TIME=""
REQUEST_SIZE=""

perform_request() {
  local method="$1"
  local path="$2"
  local payload="${3:-}"
  local curl_format=$'\n%{http_code}|%{time_total}|%{time_starttransfer}|%{size_download}'
  local curl_args=(
    -sS
    -X "$method" "${BASE_URL}${path}"
    -w "$curl_format"
  )

  if [[ -n "$payload" ]]; then
    curl_args+=(-H "Content-Type: application/json" -d "$payload")
  fi

  local response
  if ! response=$(curl "${curl_args[@]}"); then
    echo "Falha ao executar ${method} ${path}." >&2
    return 1
  fi

  local metadata="${response##*$'\n'}"
  local body="${response%$'\n'*}"
  if [[ "$body" == "$metadata" ]]; then
    body=""
  fi

  IFS='|' read -r REQUEST_STATUS REQUEST_TOTAL_TIME REQUEST_START_TRANSFER REQUEST_SIZE <<<"$metadata"
  REQUEST_BODY="$body"

  if [[ -n "$REQUEST_TOTAL_TIME" && -n "$REQUEST_START_TRANSFER" ]]; then
    REQUEST_READ_TIME=$(awk -v total="$REQUEST_TOTAL_TIME" -v start="$REQUEST_START_TRANSFER" 'BEGIN { printf "%.4f", total - start }')
  else
    REQUEST_READ_TIME=""
  fi

  return 0
}

call_api() {
  local method="$1"
  local path="$2"
  local payload="${3:-}"

  if ! perform_request "$method" "$path" "$payload"; then
    exit 1
  fi

  LAST_STATUS="$REQUEST_STATUS"
  LAST_BODY="$REQUEST_BODY"

  echo "Status: $REQUEST_STATUS"
  print_body "$LAST_BODY"
  if [[ -n "$REQUEST_TOTAL_TIME" ]]; then
    printf 'Tempo total: %.4fs (leitura: %.4fs, corpo: %s bytes)\n' "$REQUEST_TOTAL_TIME" "${REQUEST_READ_TIME:-0}" "${REQUEST_SIZE:-0}"
  fi

  if [[ "$REQUEST_STATUS" =~ ^[0-9]+$ ]] && (( REQUEST_STATUS >= 400 )); then
    echo "Requisição ${method} ${path} falhou (status ${REQUEST_STATUS})." >&2
    exit 1
  fi
}

delete_product() {
  local path="$1"

  if ! perform_request DELETE "$path"; then
    exit 1
  fi

  LAST_STATUS="$REQUEST_STATUS"
  LAST_BODY="$REQUEST_BODY"

  echo "Status: $REQUEST_STATUS"
  print_body "$LAST_BODY"
  if [[ -n "$REQUEST_TOTAL_TIME" ]]; then
    printf 'Tempo total: %.4fs (leitura: %.4fs, corpo: %s bytes)\n' "$REQUEST_TOTAL_TIME" "${REQUEST_READ_TIME:-0}" "${REQUEST_SIZE:-0}"
  fi

  if [[ "$REQUEST_STATUS" == "409" ]]; then
    echo "Produto associado a pedidos. Mantendo registro para preservar o histórico."
    return 0
  fi

  if [[ "$REQUEST_STATUS" =~ ^[0-9]+$ ]] && (( REQUEST_STATUS >= 400 )); then
    echo "Requisição DELETE ${path} falhou (status ${REQUEST_STATUS})." >&2
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

load_test() {
  local method="$1"
  local path="$2"
  local iterations="${3:-100}"
  local payload="${4:-}"

  log "Teste de carga ${method} ${path} (${iterations} execuções)"

  local sum_total="0"
  local sum_read="0"
  local min_total=""
  local max_total=""
  local min_read=""
  local max_read=""

  for ((i = 1; i <= iterations; i++)); do
    if ! perform_request "$method" "$path" "$payload"; then
      echo "Falha na iteração ${i}." >&2
      exit 1
    fi

    if [[ "$REQUEST_STATUS" =~ ^[0-9]+$ ]] && (( REQUEST_STATUS >= 400 )); then
      echo "Iteração ${i} retornou status ${REQUEST_STATUS}. Interrompendo teste de carga." >&2
      exit 1
    fi

    if [[ -z "$REQUEST_TOTAL_TIME" || -z "$REQUEST_READ_TIME" ]]; then
      echo "Curl não reportou métricas na iteração ${i}." >&2
      exit 1
    fi

    sum_total=$(awk -v sum="$sum_total" -v val="$REQUEST_TOTAL_TIME" 'BEGIN { printf "%.6f", sum + val }')
    sum_read=$(awk -v sum="$sum_read" -v val="$REQUEST_READ_TIME" 'BEGIN { printf "%.6f", sum + val }')

    if [[ -z "$min_total" ]]; then
      min_total="$REQUEST_TOTAL_TIME"
      max_total="$REQUEST_TOTAL_TIME"
      min_read="$REQUEST_READ_TIME"
      max_read="$REQUEST_READ_TIME"
    else
      min_total=$(awk -v current="$REQUEST_TOTAL_TIME" -v existing="$min_total" 'BEGIN { if (current < existing) printf "%.6f", current; else printf "%.6f", existing }')
      max_total=$(awk -v current="$REQUEST_TOTAL_TIME" -v existing="$max_total" 'BEGIN { if (current > existing) printf "%.6f", current; else printf "%.6f", existing }')
      min_read=$(awk -v current="$REQUEST_READ_TIME" -v existing="$min_read" 'BEGIN { if (current < existing) printf "%.6f", current; else printf "%.6f", existing }')
      max_read=$(awk -v current="$REQUEST_READ_TIME" -v existing="$max_read" 'BEGIN { if (current > existing) printf "%.6f", current; else printf "%.6f", existing }')
    fi
  done

  local avg_total
  local avg_read
  avg_total=$(awk -v sum="$sum_total" -v count="$iterations" 'BEGIN { printf "%.4f", sum / count }')
  avg_read=$(awk -v sum="$sum_read" -v count="$iterations" 'BEGIN { printf "%.4f", sum / count }')

  printf 'Tempo total médio: %.4fs | min: %.4fs | max: %.4fs\n' "$avg_total" "$min_total" "$max_total"
  printf 'Tempo leitura médio: %.4fs | min: %.4fs | max: %.4fs\n' "$avg_read" "$min_read" "$max_read"
}

run_heavy_searches() {
  if (( STRESS_SEARCH_ITERATIONS <= 0 )); then
    return
  fi

  local iterations="$STRESS_SEARCH_ITERATIONS"
  local -a heavy_pages=(1 5 10 25 50)

  local -a customer_terms=("bruce" "Cliente" "50% off")
  local -a product_terms=("gadget" "Batarang" "Smoke Pellet")
  local -a order_terms=("bruce" "batarang" "smoke")

  log "Buscas pesadas - clientes"
  for term in "${customer_terms[@]}"; do
    local encoded_term
    encoded_term=$(urlencode "$term")
    load_test GET "/v1/customers?term=${encoded_term}&page=1&pageSize=100&sortBy=name&sortOrder=desc" "$iterations"
    load_test GET "/v1/customers?term=${encoded_term}&page=1&pageSize=100&sortBy=email&sortOrder=asc" "$iterations"
  done

  local encoded_heavy_customer_term
  encoded_heavy_customer_term=$(urlencode "${customer_terms[0]}")
  for page in "${heavy_pages[@]}"; do
    load_test GET "/v1/customers?page=${page}&pageSize=100&sortBy=name&sortOrder=desc" "$iterations"
    load_test GET "/v1/customers?term=${encoded_heavy_customer_term}&page=${page}&pageSize=100&sortBy=birthDate&sortOrder=desc" "$iterations"
  done

  log "Buscas pesadas - produtos"
  for term in "${product_terms[@]}"; do
    local encoded_term
    encoded_term=$(urlencode "$term")
    load_test GET "/v1/products?term=${encoded_term}&page=1&pageSize=100&sortBy=price&sortOrder=desc" "$iterations"
    load_test GET "/v1/products?term=${encoded_term}&page=1&pageSize=100&sortBy=slug&sortOrder=asc" "$iterations"
  done

  local encoded_heavy_product_term
  encoded_heavy_product_term=$(urlencode "${product_terms[0]}")
  for page in "${heavy_pages[@]}"; do
    load_test GET "/v1/products?page=${page}&pageSize=100&sortBy=price&sortOrder=desc" "$iterations"
    load_test GET "/v1/products?term=${encoded_heavy_product_term}&page=${page}&pageSize=100&sortBy=title&sortOrder=desc" "$iterations"
  done

  log "Buscas pesadas - pedidos"
  for term in "${order_terms[@]}"; do
    local encoded_term
    encoded_term=$(urlencode "$term")
    load_test GET "/v1/orders?term=${encoded_term}&page=1&pageSize=100&sortBy=total&sortOrder=desc" "$iterations"
    load_test GET "/v1/orders?term=${encoded_term}&page=1&pageSize=100&sortBy=updatedAt&sortOrder=desc" "$iterations"
  done

  local encoded_heavy_order_term
  encoded_heavy_order_term=$(urlencode "${order_terms[0]}")
  for page in "${heavy_pages[@]}"; do
    load_test GET "/v1/orders?page=${page}&pageSize=100&sortBy=total&sortOrder=desc" "$iterations"
    load_test GET "/v1/orders?term=${encoded_heavy_order_term}&page=${page}&pageSize=100&sortBy=createdAt&sortOrder=asc" "$iterations"
  done
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
call_api GET "/v1/reports/sales-by-customer/${CUSTOMER_ID}"

log "Relatório de vendas por cliente (período completo)"
call_api GET "/v1/reports/sales-by-customer/${CUSTOMER_ID}?startDate=2020-01-01T00:00:00Z&endDate=2100-01-01T00:00:00Z"

log "Relatório de faturamento diário"
call_api GET "/v1/reports/revenue-by-period?groupBy=day"

log "Relatório de faturamento mensal"
call_api GET "/v1/reports/revenue-by-period?groupBy=month&startDate=2020-01-01T00:00:00Z&endDate=2100-01-01T00:00:00Z"

log "Relatório de faturamento anual"
call_api GET "/v1/reports/revenue-by-period?groupBy=year"

if (( LOAD_TEST_ITERATIONS > 0 )); then
  load_test GET "/v1/products?page=1&pageSize=5&sortBy=price&sortOrder=asc" "$LOAD_TEST_ITERATIONS"
  load_test GET "/v1/customers/${CUSTOMER_ID}" "$LOAD_TEST_ITERATIONS"
  load_test GET "/v1/orders/${ORDER_ID}" "$LOAD_TEST_ITERATIONS"
fi

run_heavy_searches

log "Remover produto"
delete_product "/v1/products/${PRODUCT_ID}"

log "Remover segundo produto"
delete_product "/v1/products/${SECOND_PRODUCT_ID}"

log "Remover cliente"
call_api DELETE "/v1/customers/${CUSTOMER_ID}"
