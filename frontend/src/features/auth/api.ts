import { apiClient } from '@/lib/api-client'
import type { AuthResult, LoginRequest, RefreshRequest, RegisterRequest } from './types'

// skipAuth: true on all four — none of these calls carry a bearer token (login/
// register happen before we have one, refresh proves identity via the refresh
// token in the body, and a 401 from any of them means "try again from scratch",
// not "refresh and retry").

export function login(request: LoginRequest) {
  return apiClient<AuthResult>('/identity/login', {
    method: 'POST',
    body: JSON.stringify(request),
    skipAuth: true,
  })
}

export function register(request: RegisterRequest) {
  return apiClient<AuthResult>('/identity/register', {
    method: 'POST',
    body: JSON.stringify(request),
    skipAuth: true,
  })
}

export function refresh(request: RefreshRequest) {
  return apiClient<AuthResult>('/identity/refresh', {
    method: 'POST',
    body: JSON.stringify(request),
    skipAuth: true,
  })
}

export function logout(request: RefreshRequest) {
  return apiClient<void>('/identity/logout', {
    method: 'POST',
    body: JSON.stringify(request),
    skipAuth: true,
  })
}
