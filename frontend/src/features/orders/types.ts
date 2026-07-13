export type OrderStatus = 'Pending' | 'Reserved' | 'Rejected'

export interface OrderItem {
  id: string
  productName: string
  quantity: number
}

export interface Order {
  id: string
  customerId: string
  status: OrderStatus
  rejectionReason: string | null
  createdAt: string
  updatedAt: string
  items: OrderItem[]
}

export interface CreateOrderItemRequest {
  productName: string
  quantity: number
}

export interface CreateOrderRequest {
  items: CreateOrderItemRequest[]
}
