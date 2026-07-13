import type { AuthUser } from './types'

// The Identity service builds the JWT via `new JwtSecurityToken(claims: ...)`
// directly rather than going through JwtSecurityTokenHandler's outbound claim
// map, so role claims are serialized under the full ClaimTypes.Role URI
// (verified against a live token: schemas.microsoft.com, not the short "role"
// name and not the xmlsoap.org URI some .NET docs reference). "role"/"roles"
// stay as fallbacks in case that ever changes server-side.
const ROLE_CLAIM_KEYS = [
  'http://schemas.microsoft.com/ws/2008/06/identity/claims/role',
  'role',
  'roles',
]

interface JwtPayload {
  sub?: string
  email?: string
  exp?: number
  [claim: string]: unknown
}

function decodeBase64Url(input: string): string {
  const base64 = input.replace(/-/g, '+').replace(/_/g, '/')
  const padded = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), '=')
  const binary = atob(padded)
  const percentEncoded = Array.from(binary, (char) =>
    '%' + char.charCodeAt(0).toString(16).padStart(2, '0')
  ).join('')
  return decodeURIComponent(percentEncoded)
}

function decodeJwtPayload(token: string): JwtPayload | null {
  const parts = token.split('.')
  if (parts.length !== 3) {
    return null
  }
  try {
    return JSON.parse(decodeBase64Url(parts[1])) as JwtPayload
  } catch {
    return null
  }
}

function extractRoles(payload: JwtPayload): string[] {
  for (const key of ROLE_CLAIM_KEYS) {
    const value = payload[key]
    if (Array.isArray(value)) {
      return value.filter((entry): entry is string => typeof entry === 'string')
    }
    if (typeof value === 'string') {
      return [value]
    }
  }
  return []
}

export function isAccessTokenExpired(token: string, skewSeconds = 5): boolean {
  const payload = decodeJwtPayload(token)
  if (!payload?.exp) {
    return true
  }
  return payload.exp * 1000 <= Date.now() + skewSeconds * 1000
}

export function decodeAuthUser(token: string): AuthUser | null {
  const payload = decodeJwtPayload(token)
  if (!payload?.sub || !payload.email) {
    return null
  }
  return {
    id: payload.sub,
    email: payload.email,
    roles: extractRoles(payload),
  }
}
