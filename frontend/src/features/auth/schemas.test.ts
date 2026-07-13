import { describe, expect, it } from 'vitest'

import { loginSchema, registerSchema } from './schemas'

describe('loginSchema', () => {
  it('accepts a valid email and any non-empty password', () => {
    expect(loginSchema.safeParse({ email: 'a@example.com', password: 'x' }).success).toBe(true)
  })

  it('rejects an invalid email', () => {
    expect(loginSchema.safeParse({ email: 'not-an-email', password: 'x' }).success).toBe(false)
  })

  it('rejects an empty password', () => {
    expect(loginSchema.safeParse({ email: 'a@example.com', password: '' }).success).toBe(false)
  })
})

describe('registerSchema', () => {
  it('accepts a valid email and an 8+ character password', () => {
    expect(registerSchema.safeParse({ email: 'a@example.com', password: 'longenough' }).success).toBe(
      true
    )
  })

  it('rejects a password shorter than 8 characters', () => {
    expect(registerSchema.safeParse({ email: 'a@example.com', password: 'short1' }).success).toBe(
      false
    )
  })

  it('rejects an invalid email', () => {
    expect(
      registerSchema.safeParse({ email: 'not-an-email', password: 'longenough' }).success
    ).toBe(false)
  })
})
