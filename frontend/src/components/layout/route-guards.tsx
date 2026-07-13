import type { ReactNode } from 'react'
import { Navigate, Outlet, useLocation } from 'react-router-dom'

import { useAuth } from '@/features/auth/auth-context'

export function RequireAuth() {
  const { status } = useAuth()
  const location = useLocation()

  if (status === 'idle') {
    return null
  }

  if (status === 'unauthenticated') {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />
  }

  return <Outlet />
}

export function RequireAdmin() {
  const { user } = useAuth()

  if (!user?.roles.includes('Admin')) {
    return <Navigate to="/orders" replace />
  }

  return <Outlet />
}

export function PublicOnly({ children }: { children: ReactNode }) {
  const { status } = useAuth()

  if (status === 'authenticated') {
    return <Navigate to="/orders" replace />
  }

  return <>{children}</>
}
