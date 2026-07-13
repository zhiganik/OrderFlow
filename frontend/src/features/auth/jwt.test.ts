import { describe, expect, it } from 'vitest'

import { decodeAuthUser, isAccessTokenExpired } from './jwt'

function base64UrlEncode(value: unknown): string {
  return btoa(JSON.stringify(value)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
}

function makeToken(payload: Record<string, unknown>): string {
  return `${base64UrlEncode({ alg: 'HS256', typ: 'JWT' })}.${base64UrlEncode(payload)}.signature`
}

describe('isAccessTokenExpired', () => {
  it('treats a token with no exp claim as expired', () => {
    expect(isAccessTokenExpired(makeToken({ sub: '1' }))).toBe(true)
  })

  it('treats a token whose exp is in the past as expired', () => {
    const exp = Math.floor(Date.now() / 1000) - 60
    expect(isAccessTokenExpired(makeToken({ exp }))).toBe(true)
  })

  it('treats a token that expires well in the future as not expired', () => {
    const exp = Math.floor(Date.now() / 1000) + 3600
    expect(isAccessTokenExpired(makeToken({ exp }))).toBe(false)
  })

  it('applies the skew window', () => {
    const exp = Math.floor(Date.now() / 1000) + 2
    expect(isAccessTokenExpired(makeToken({ exp }), 5)).toBe(true)
  })

  it('treats a malformed token as expired', () => {
    expect(isAccessTokenExpired('not-a-jwt')).toBe(true)
  })
})

describe('decodeAuthUser', () => {
  it('decodes sub/email and the role claim under its real ClaimTypes.Role URI', () => {
    const token = makeToken({
      sub: 'user-1',
      email: 'a@example.com',
      'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': 'Admin',
    })
    expect(decodeAuthUser(token)).toEqual({
      id: 'user-1',
      email: 'a@example.com',
      roles: ['Admin'],
    })
  })

  it('returns an empty roles array when there is no role claim', () => {
    const token = makeToken({ sub: 'user-1', email: 'a@example.com' })
    expect(decodeAuthUser(token)?.roles).toEqual([])
  })

  it('falls back to a "roles" array claim', () => {
    const token = makeToken({ sub: 'user-1', email: 'a@example.com', roles: ['Admin', 'Support'] })
    expect(decodeAuthUser(token)?.roles).toEqual(['Admin', 'Support'])
  })

  it('returns null when sub or email is missing', () => {
    expect(decodeAuthUser(makeToken({ email: 'a@example.com' }))).toBeNull()
    expect(decodeAuthUser(makeToken({ sub: 'user-1' }))).toBeNull()
  })

  it('returns null for a malformed token', () => {
    expect(decodeAuthUser('not-a-jwt')).toBeNull()
  })
})
