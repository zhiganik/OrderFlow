import { keepPreviousData, useQuery } from '@tanstack/react-query'

import { getStock } from '../api'
import { stockKeys, type StockSearchParams } from '../query-keys'

export function useStock(params: StockSearchParams) {
  return useQuery({
    queryKey: stockKeys.list(params),
    queryFn: () => getStock(params),
    placeholderData: keepPreviousData,
  })
}
