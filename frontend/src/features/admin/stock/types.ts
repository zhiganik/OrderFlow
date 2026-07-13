export interface StockItem {
  id: string
  productName: string
  quantityAvailable: number
  createdAt: string
}

export interface UpsertStockItemRequest {
  productName: string
  quantityAvailable: number
}
