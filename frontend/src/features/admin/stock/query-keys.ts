export interface StockSearchParams {
  page: number
  pageSize: number
  id?: string
  productName?: string
}

export const stockKeys = {
  all: ['stock'] as const,
  lists: () => [...stockKeys.all, 'list'] as const,
  list: (params: StockSearchParams) => [...stockKeys.lists(), params] as const,
}
