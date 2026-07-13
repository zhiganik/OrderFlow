import { useState } from 'react'

import { PaginationControls } from '@/components/pagination-controls'
import { OrderTable } from '@/features/orders/components/order-table'
import { useOrders } from '@/features/orders/hooks/use-orders'

export function AdminOrdersPage() {
  const [page, setPage] = useState(1)
  const { data, isLoading, isError } = useOrders(page)

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">All orders</h1>
      {isLoading && <p className="text-muted-foreground">Loading…</p>}
      {isError && <p className="text-destructive">Failed to load orders.</p>}
      {data && (
        <>
          <OrderTable orders={data.items} showCustomer />
          <PaginationControls page={data.page} totalPages={data.totalPages} onPageChange={setPage} />
        </>
      )}
    </div>
  )
}
