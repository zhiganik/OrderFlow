import { z } from 'zod'

export const createOrderSchema = z.object({
  items: z
    .array(
      z.object({
        productName: z.string().min(1, 'Product name is required'),
        quantity: z.number().int().positive('Quantity must be greater than 0'),
      })
    )
    .min(1, 'Add at least one item'),
})
export type CreateOrderFormValues = z.infer<typeof createOrderSchema>
