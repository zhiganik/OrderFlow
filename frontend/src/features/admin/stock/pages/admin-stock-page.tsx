import { useState } from 'react'

import { PaginationControls } from '@/components/pagination-controls'
import { Input } from '@/components/ui/input'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { StockUpsertForm } from '../components/stock-upsert-form'
import { useStock } from '../hooks/use-stock'

export function AdminStockPage() {
  const [page, setPage] = useState(1)
  const [productName, setProductName] = useState('')
  const { data, isLoading, isError } = useStock({
    page,
    pageSize: 20,
    productName: productName || undefined,
  })

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">Stock</h1>
        <StockUpsertForm />
      </div>
      <Input
        placeholder="Filter by product name…"
        value={productName}
        onChange={(event) => {
          setProductName(event.target.value)
          setPage(1)
        }}
        className="max-w-sm"
      />
      {isLoading && <p className="text-muted-foreground">Loading…</p>}
      {isError && <p className="text-destructive">Failed to load stock.</p>}
      {data && (
        <>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Product</TableHead>
                <TableHead>Quantity available</TableHead>
                <TableHead>Created</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {data.items.map((item) => (
                <TableRow key={item.id}>
                  <TableCell>{item.productName}</TableCell>
                  <TableCell>{item.quantityAvailable}</TableCell>
                  <TableCell>{new Date(item.createdAt).toLocaleString()}</TableCell>
                </TableRow>
              ))}
              {data.items.length === 0 && (
                <TableRow>
                  <TableCell colSpan={3} className="text-center text-muted-foreground">
                    No stock items found.
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
          <PaginationControls page={data.page} totalPages={data.totalPages} onPageChange={setPage} />
        </>
      )}
    </div>
  )
}
