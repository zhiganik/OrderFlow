import { z } from 'zod'

export const upsertStockSchema = z.object({
  productName: z.string().min(1, 'Product name is required'),
  quantityAvailable: z.number().int().min(0, 'Quantity cannot be negative'),
})
export type UpsertStockFormValues = z.infer<typeof upsertStockSchema>
