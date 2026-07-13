import { useQuery } from '@tanstack/react-query'

import { getOrder } from '../api'
import { orderKeys } from '../query-keys'
import type { Order, OrderStatus } from '../types'

// Order status resolves asynchronously via RabbitMQ events on the backend —
// there's no push mechanism, so we poll while it's still Pending and stop
// once it lands on a terminal state.
export function getOrderPollInterval(status: OrderStatus | undefined): number | false {
  return status === 'Pending' ? 1500 : false
}

export function useOrder(id: string) {
  return useQuery({
    queryKey: orderKeys.detail(id),
    queryFn: () => getOrder(id),
    refetchInterval: (query) => getOrderPollInterval((query.state.data as Order | undefined)?.status),
  })
}
