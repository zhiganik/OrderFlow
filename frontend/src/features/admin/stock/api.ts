import { apiClient } from '@/lib/api-client'
import type { PagedResult } from '@/lib/types'
import type { StockSearchParams } from './query-keys'
import type { StockItem, UpsertStockItemRequest } from './types'

export function getStock(params: StockSearchParams) {
  const query = new URLSearchParams({
    page: String(params.page),
    pageSize: String(params.pageSize),
  })
  if (params.id) {
    query.set('id', params.id)
  }
  if (params.productName) {
    query.set('productName', params.productName)
  }
  return apiClient<PagedResult<StockItem>>(`/inventory/api/stock?${query.toString()}`)
}

export function upsertStock(payload: UpsertStockItemRequest) {
  return apiClient<StockItem>('/inventory/api/stock', {
    method: 'POST',
    body: JSON.stringify(payload),
  })
}
