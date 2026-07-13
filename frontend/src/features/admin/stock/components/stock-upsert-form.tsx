import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { toast } from 'sonner'

import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog'
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form'
import { Input } from '@/components/ui/input'
import { ApiError } from '@/lib/api-client'
import { useUpsertStock } from '../hooks/use-upsert-stock'
import { upsertStockSchema, type UpsertStockFormValues } from '../schemas'

export function StockUpsertForm() {
  const [open, setOpen] = useState(false)
  const upsertStock = useUpsertStock()

  const form = useForm<UpsertStockFormValues>({
    resolver: zodResolver(upsertStockSchema),
    defaultValues: { productName: '', quantityAvailable: 0 },
  })

  async function onSubmit(values: UpsertStockFormValues) {
    try {
      await upsertStock.mutateAsync(values)
      toast.success(`Stock updated for ${values.productName}`)
      form.reset({ productName: '', quantityAvailable: 0 })
      setOpen(false)
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : 'Failed to update stock.')
    }
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button>Add / update stock</Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Add or update stock</DialogTitle>
          <DialogDescription>
            Upserts by product name — creates it if new, overwrites the quantity if it already
            exists.
          </DialogDescription>
        </DialogHeader>
        <Form {...form}>
          <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
            <FormField
              control={form.control}
              name="productName"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Product name</FormLabel>
                  <FormControl>
                    <Input {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
            <FormField
              control={form.control}
              name="quantityAvailable"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Quantity available</FormLabel>
                  <FormControl>
                    <Input
                      type="number"
                      min={0}
                      {...field}
                      onChange={(event) => field.onChange(event.target.valueAsNumber)}
                    />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
            <DialogFooter>
              <Button type="submit" disabled={form.formState.isSubmitting}>
                {form.formState.isSubmitting ? 'Saving…' : 'Save'}
              </Button>
            </DialogFooter>
          </form>
        </Form>
      </DialogContent>
    </Dialog>
  )
}
