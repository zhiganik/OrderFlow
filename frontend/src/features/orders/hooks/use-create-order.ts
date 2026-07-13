import { useMutation, useQueryClient } from '@tanstack/react-query'

import { createOrder } from '../api'
import { orderKeys } from '../query-keys'

export function useCreateOrder() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: createOrder,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: orderKeys.lists() })
    },
  })
}
