import { describe, expect, it } from 'vitest'

import { getOrderPollInterval } from './use-order'

describe('getOrderPollInterval', () => {
  it('polls every 1.5s while an order is Pending', () => {
    expect(getOrderPollInterval('Pending')).toBe(1500)
  })

  it('stops polling once an order reaches a terminal state', () => {
    expect(getOrderPollInterval('Reserved')).toBe(false)
    expect(getOrderPollInterval('Rejected')).toBe(false)
  })

  it('stops polling when there is no data yet', () => {
    expect(getOrderPollInterval(undefined)).toBe(false)
  })
})
