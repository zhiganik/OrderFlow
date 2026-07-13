import { Link } from 'react-router-dom'

import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import type { Order } from '../types'
import { OrderStatusBadge } from './order-status-badge'

export function OrderTable({ orders, showCustomer = false }: { orders: Order[]; showCustomer?: boolean }) {
  const columnCount = showCustomer ? 5 : 4

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>Order</TableHead>
          {showCustomer && <TableHead>Customer</TableHead>}
          <TableHead>Status</TableHead>
          <TableHead>Items</TableHead>
          <TableHead>Created</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {orders.map((order) => (
          <TableRow key={order.id}>
            <TableCell>
              <Link to={`/orders/${order.id}`} className="underline underline-offset-4">
                {order.id.slice(0, 8)}
              </Link>
            </TableCell>
            {showCustomer && <TableCell>{order.customerId.slice(0, 8)}</TableCell>}
            <TableCell>
              <OrderStatusBadge status={order.status} />
            </TableCell>
            <TableCell>{order.items.reduce((sum, item) => sum + item.quantity, 0)} units</TableCell>
            <TableCell>{new Date(order.createdAt).toLocaleString()}</TableCell>
          </TableRow>
        ))}
        {orders.length === 0 && (
          <TableRow>
            <TableCell colSpan={columnCount} className="text-center text-muted-foreground">
              No orders yet.
            </TableCell>
          </TableRow>
        )}
      </TableBody>
    </Table>
  )
}
