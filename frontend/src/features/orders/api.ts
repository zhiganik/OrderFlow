import { apiClient } from '@/lib/api-client'
import type { PagedResult } from '@/lib/types'
import type { CreateOrderRequest, Order } from './types'

export function getOrders(page: number, pageSize: number) {
  return apiClient<PagedResult<Order>>(`/order/orders?page=${page}&pageSize=${pageSize}`)
}

export function getOrder(id: string) {
  return apiClient<Order>(`/order/orders/${id}`)
}

export function createOrder(payload: CreateOrderRequest) {
  // Idempotency key is scoped to this one call — no other endpoint needs it,
  // so it doesn't belong on the shared apiClient.
  return apiClient<Order>('/order/orders', {
    method: 'POST',
    body: JSON.stringify(payload),
    headers: { 'Idempotency-Key': crypto.randomUUID() },
  })
}

export function cancelOrder(id: string) {
  // Synchronous, unlike create — the response already carries the final
  // Canceled state, no polling needed afterward.
  return apiClient<Order>(`/order/orders/${id}/cancel`, { method: 'POST' })
}
