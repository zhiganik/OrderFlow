import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { useNavigate } from 'react-router-dom'
import { toast } from 'sonner'

import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Form } from '@/components/ui/form'
import { ApiError } from '@/lib/api-client'
import { OrderItemsForm } from '../components/order-items-form'
import { useCreateOrder } from '../hooks/use-create-order'
import { createOrderSchema, type CreateOrderFormValues } from '../schemas'

export function OrderCreatePage() {
  const navigate = useNavigate()
  const createOrder = useCreateOrder()

  const form = useForm<CreateOrderFormValues>({
    resolver: zodResolver(createOrderSchema),
    defaultValues: { items: [{ productName: '', quantity: 1 }] },
  })

  async function onSubmit(values: CreateOrderFormValues) {
    try {
      const order = await createOrder.mutateAsync(values)
      toast.success('Order created')
      navigate(`/orders/${order.id}`)
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : 'Failed to create order.')
    }
  }

  return (
    <div className="max-w-xl space-y-4">
      <h1 className="text-xl font-semibold">New order</h1>
      <Card>
        <CardHeader>
          <CardTitle>Items</CardTitle>
        </CardHeader>
        <CardContent>
          <Form {...form}>
            <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
              <OrderItemsForm control={form.control} />
              <Button type="submit" disabled={form.formState.isSubmitting}>
                {form.formState.isSubmitting ? 'Placing order…' : 'Place order'}
              </Button>
            </form>
          </Form>
        </CardContent>
      </Card>
    </div>
  )
}
