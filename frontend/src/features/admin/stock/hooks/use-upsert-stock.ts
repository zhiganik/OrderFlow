import { useMutation, useQueryClient } from '@tanstack/react-query'

import { upsertStock } from '../api'
import { stockKeys } from '../query-keys'

export function useUpsertStock() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: upsertStock,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: stockKeys.lists() })
    },
  })
}
