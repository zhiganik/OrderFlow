import { Badge } from '@/components/ui/badge'
import type { OrderStatus } from '../types'

const VARIANT: Record<OrderStatus, 'default' | 'secondary' | 'destructive' | 'outline'> = {
  Pending: 'secondary',
  Reserved: 'default',
  Rejected: 'destructive',
  Canceled: 'outline',
}

export function OrderStatusBadge({ status }: { status: OrderStatus }) {
  return <Badge variant={VARIANT[status]}>{status}</Badge>
}
