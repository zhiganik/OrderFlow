import { useState } from 'react'
import { Link } from 'react-router-dom'

import { PaginationControls } from '@/components/pagination-controls'
import { Button } from '@/components/ui/button'
import { OrderTable } from '../components/order-table'
import { useOrders } from '../hooks/use-orders'

export function OrderListPage() {
  const [page, setPage] = useState(1)
  const { data, isLoading, isError } = useOrders(page)

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">Your orders</h1>
        <Button asChild>
          <Link to="/orders/new">New order</Link>
        </Button>
      </div>
      {isLoading && <p className="text-muted-foreground">Loading…</p>}
      {isError && <p className="text-destructive">Failed to load orders.</p>}
      {data && (
        <>
          <OrderTable orders={data.items} />
          <PaginationControls page={data.page} totalPages={data.totalPages} onPageChange={setPage} />
        </>
      )}
    </div>
  )
}
