import { Trash2 } from 'lucide-react'
import { useFieldArray, type Control } from 'react-hook-form'

import { Button } from '@/components/ui/button'
import { FormControl, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form'
import { Input } from '@/components/ui/input'
import type { CreateOrderFormValues } from '../schemas'

export function OrderItemsForm({ control }: { control: Control<CreateOrderFormValues> }) {
  const { fields, append, remove } = useFieldArray({ control, name: 'items' })

  return (
    <div className="space-y-4">
      {fields.map((field, index) => (
        <div key={field.id} className="flex items-end gap-2">
          <FormField
            control={control}
            name={`items.${index}.productName`}
            render={({ field }) => (
              <FormItem className="flex-1">
                {index === 0 && <FormLabel>Product name</FormLabel>}
                <FormControl>
                  <Input placeholder="Product name" {...field} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />
          <FormField
            control={control}
            name={`items.${index}.quantity`}
            render={({ field }) => (
              <FormItem className="w-28">
                {index === 0 && <FormLabel>Quantity</FormLabel>}
                <FormControl>
                  <Input
                    type="number"
                    min={1}
                    {...field}
                    onChange={(event) => field.onChange(event.target.valueAsNumber)}
                  />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />
          <Button
            type="button"
            variant="outline"
            size="icon"
            onClick={() => remove(index)}
            disabled={fields.length === 1}
            aria-label="Remove item"
          >
            <Trash2 className="size-4" />
          </Button>
        </div>
      ))}
      <Button
        type="button"
        variant="outline"
        size="sm"
        onClick={() => append({ productName: '', quantity: 1 })}
      >
        Add item
      </Button>
    </div>
  )
}
