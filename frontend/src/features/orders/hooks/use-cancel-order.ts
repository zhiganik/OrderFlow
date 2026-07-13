import { useMutation, useQueryClient } from '@tanstack/react-query'

import { cancelOrder } from '../api'
import { orderKeys } from '../query-keys'

export function useCancelOrder() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: cancelOrder,
    onSuccess: (order) => {
      // Refetch rather than trust the mutation response's `items` directly —
      // the cancel endpoint's response can come back with an empty items
      // array (a backend read-path inconsistency), so a fresh GET is the
      // reliable source for the full order shape.
      queryClient.invalidateQueries({ queryKey: orderKeys.detail(order.id) })
      queryClient.invalidateQueries({ queryKey: orderKeys.lists() })
    },
  })
}
