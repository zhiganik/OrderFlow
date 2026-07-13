import { Link, useParams } from 'react-router-dom'

import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { OrderStatusBadge } from '../components/order-status-badge'
import { useOrder } from '../hooks/use-order'

export function OrderDetailPage() {
  const { orderId } = useParams<{ orderId: string }>()
  const { data: order, isLoading, isError } = useOrder(orderId!)

  if (isLoading) {
    return <p className="text-muted-foreground">Loading…</p>
  }

  if (isError || !order) {
    return (
      <div className="space-y-4">
        <p className="text-destructive">Order not found.</p>
        <Button asChild variant="outline">
          <Link to="/orders">Back to orders</Link>
        </Button>
      </div>
    )
  }

  return (
    <div className="space-y-4">
      <Button asChild variant="ghost" size="sm" className="w-fit">
        <Link to="/orders">&larr; Back to orders</Link>
      </Button>
      <Card>
        <CardHeader className="flex flex-row items-center justify-between">
          <CardTitle className="font-mono text-base">{order.id}</CardTitle>
          <OrderStatusBadge status={order.status} />
        </CardHeader>
        <CardContent className="space-y-4">
          {order.status === 'Pending' && (
            <p className="text-sm text-muted-foreground">Waiting for stock reservation…</p>
          )}
          {order.status === 'Rejected' && order.rejectionReason && (
            <p className="text-sm text-destructive">Rejected: {order.rejectionReason}</p>
          )}
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Product</TableHead>
                <TableHead>Quantity</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {order.items.map((item) => (
                <TableRow key={item.id}>
                  <TableCell>{item.productName}</TableCell>
                  <TableCell>{item.quantity}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
          <p className="text-xs text-muted-foreground">
            Created {new Date(order.createdAt).toLocaleString()} · Updated{' '}
            {new Date(order.updatedAt).toLocaleString()}
          </p>
        </CardContent>
      </Card>
    </div>
  )
}
