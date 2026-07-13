import { keepPreviousData, useQuery } from '@tanstack/react-query'

import { getOrders } from '../api'
import { orderKeys } from '../query-keys'

export function useOrders(page: number, pageSize = 20) {
  return useQuery({
    queryKey: orderKeys.list(page, pageSize),
    queryFn: () => getOrders(page, pageSize),
    placeholderData: keepPreviousData,
  })
}
