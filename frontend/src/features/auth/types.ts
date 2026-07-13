export interface LoginRequest {
  email: string
  password: string
}

export interface RegisterRequest {
  email: string
  password: string
}

export interface RefreshRequest {
  refreshToken: string
}

export interface AuthResult {
  accessToken: string
  refreshToken: string
  expiresAt: string
}

export interface AuthUser {
  id: string
  email: string
  roles: string[]
}
