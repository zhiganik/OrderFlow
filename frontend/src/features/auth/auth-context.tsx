import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from 'react'
import { useQueryClient } from '@tanstack/react-query'

import { setAccessTokenGetter, setRefreshHandler, setUnauthorizedHandler } from '@/lib/api-client'
import * as authApi from './api'
import { decodeAuthUser, isAccessTokenExpired } from './jwt'
import type { AuthResult, AuthUser } from './types'

const STORAGE_KEY = 'orderflow.auth'

interface StoredAuth {
  accessToken: string
  refreshToken: string
}

function readStoredAuth(): StoredAuth | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    return raw ? (JSON.parse(raw) as StoredAuth) : null
  } catch {
    return null
  }
}

function writeStoredAuth(auth: StoredAuth | null) {
  if (auth) {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(auth))
  } else {
    localStorage.removeItem(STORAGE_KEY)
  }
}

type AuthStatus = 'idle' | 'authenticated' | 'unauthenticated'

interface AuthState {
  accessToken: string | null
  refreshToken: string | null
  user: AuthUser | null
  status: AuthStatus
}

function initialAuthState(): AuthState {
  const stored = readStoredAuth()
  if (!stored) {
    return { accessToken: null, refreshToken: null, user: null, status: 'unauthenticated' }
  }

  if (!isAccessTokenExpired(stored.accessToken)) {
    const user = decodeAuthUser(stored.accessToken)
    if (user) {
      return { accessToken: stored.accessToken, refreshToken: stored.refreshToken, user, status: 'authenticated' }
    }
  }

  // Access token missing/expired/undecodable but we have a refresh token —
  // try to refresh once on mount instead of forcing a re-login.
  return { accessToken: null, refreshToken: stored.refreshToken, user: null, status: 'idle' }
}

interface AuthContextValue {
  user: AuthUser | null
  status: AuthStatus
  login: (email: string, password: string) => Promise<void>
  register: (email: string, password: string) => Promise<void>
  logout: () => Promise<void>
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<AuthState>(initialAuthState)
  const stateRef = useRef(state)
  stateRef.current = state
  const queryClient = useQueryClient()

  // Orders/stock queries aren't keyed by user or role, so switching who's
  // logged in in the same tab (logout -> different user, or a role grant
  // that requires re-login) must drop the cache — otherwise a query started
  // under the old identity stays "fresh" and silently serves stale/wrong
  // data (e.g. an empty order list cached from before a user became Admin).
  const applyAuthResult = useCallback(
    (result: AuthResult) => {
      const user = decodeAuthUser(result.accessToken)
      const previousUserId = stateRef.current.user?.id
      writeStoredAuth({ accessToken: result.accessToken, refreshToken: result.refreshToken })
      setState({
        accessToken: result.accessToken,
        refreshToken: result.refreshToken,
        user,
        status: user ? 'authenticated' : 'unauthenticated',
      })
      if (user?.id !== previousUserId) {
        queryClient.clear()
      }
    },
    [queryClient]
  )

  const clearAuth = useCallback(() => {
    writeStoredAuth(null)
    setState({ accessToken: null, refreshToken: null, user: null, status: 'unauthenticated' })
    queryClient.clear()
  }, [queryClient])

  // Guards against concurrent callers issuing duplicate /refresh requests
  // with the same (soon-to-be-stale) refresh token — the mount effect below
  // and api-client's 401-retry path can both invoke this independently (e.g.
  // React StrictMode double-firing the mount effect in dev).
  const refreshPromiseRef = useRef<Promise<string | null> | null>(null)

  const doRefresh = useCallback((): Promise<string | null> => {
    refreshPromiseRef.current ??= (async () => {
      const refreshToken = stateRef.current.refreshToken
      if (!refreshToken) {
        clearAuth()
        return null
      }
      try {
        const result = await authApi.refresh({ refreshToken })
        applyAuthResult(result)
        return result.accessToken
      } catch {
        clearAuth()
        return null
      }
    })().finally(() => {
      refreshPromiseRef.current = null
    })
    return refreshPromiseRef.current
  }, [applyAuthResult, clearAuth])

  // Wire the API client's module-level hooks synchronously during render,
  // not in a useEffect: descendants always render after their ancestors, but
  // effects fire child-before-parent — a descendant's useQuery can kick off
  // its first fetch during render (e.g. on a hard reload), which would race
  // an effect-based registration and 401 before it ever gets a token.
  setAccessTokenGetter(() => stateRef.current.accessToken)
  setUnauthorizedHandler(clearAuth)
  setRefreshHandler(doRefresh)

  useEffect(() => {
    if (stateRef.current.status === 'idle') {
      void doRefresh()
    }
    // Mount-only: refresh once if we started in the "expired token, have a
    // refresh token" state.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const login = useCallback(
    async (email: string, password: string) => {
      const result = await authApi.login({ email, password })
      applyAuthResult(result)
    },
    [applyAuthResult]
  )

  const register = useCallback(
    async (email: string, password: string) => {
      const result = await authApi.register({ email, password })
      applyAuthResult(result)
    },
    [applyAuthResult]
  )

  const logout = useCallback(async () => {
    const refreshToken = stateRef.current.refreshToken
    if (refreshToken) {
      try {
        await authApi.logout({ refreshToken })
      } catch {
        // Best-effort — clear local state regardless of server outcome.
      }
    }
    clearAuth()
  }, [clearAuth])

  const value = useMemo<AuthContextValue>(
    () => ({ user: state.user, status: state.status, login, register, logout }),
    [state.user, state.status, login, register, logout]
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext)
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider')
  }
  return context
}
