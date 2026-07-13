import { env } from './env'

export class ApiError extends Error {
  status: number
  title?: string
  detail?: string
  fieldErrors?: Record<string, string[]>

  constructor(
    status: number,
    message: string,
    opts?: { title?: string; detail?: string; fieldErrors?: Record<string, string[]> }
  ) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.title = opts?.title
    this.detail = opts?.detail
    this.fieldErrors = opts?.fieldErrors
  }
}

// Registered once by AuthProvider on mount. A module-level indirection (rather
// than importing the auth context here) avoids a circular import between this
// client and features/auth, since features/auth/api.ts itself calls apiClient.
type AccessTokenGetter = () => string | null
type UnauthorizedHandler = () => void
type RefreshHandler = () => Promise<string | null>

let accessTokenGetter: AccessTokenGetter = () => null
let unauthorizedHandler: UnauthorizedHandler = () => {}
let refreshHandler: RefreshHandler = async () => null

export function setAccessTokenGetter(getter: AccessTokenGetter) {
  accessTokenGetter = getter
}

export function setUnauthorizedHandler(handler: UnauthorizedHandler) {
  unauthorizedHandler = handler
}

export function setRefreshHandler(handler: RefreshHandler) {
  refreshHandler = handler
}

// Dedupes concurrent 401s (e.g. several queries firing at once) into a single
// refresh call instead of one per failed request.
let refreshPromise: Promise<string | null> | null = null

function refreshAccessTokenOnce(): Promise<string | null> {
  refreshPromise ??= refreshHandler().finally(() => {
    refreshPromise = null
  })
  return refreshPromise
}

async function parseErrorBody(response: Response): Promise<ApiError> {
  let body: unknown = null
  try {
    body = await response.json()
  } catch {
    // No JSON body (e.g. a plain-text 401 or empty response) — fall through.
  }

  if (body && typeof body === 'object') {
    const record = body as Record<string, unknown>

    // RFC7807 ProblemDetails — validation failures, idempotency conflicts,
    // and unhandled-exception responses from GlobalExceptionHandler.
    if ('title' in record || 'errors' in record) {
      const fieldErrors = record.errors as Record<string, string[]> | undefined
      const title = typeof record.title === 'string' ? record.title : undefined
      const detail = typeof record.detail === 'string' ? record.detail : undefined
      return new ApiError(response.status, detail ?? title ?? response.statusText, {
        title,
        detail,
        fieldErrors,
      })
    }

    // Identity's AuthController shape for login/register/refresh outcome failures.
    if (typeof record.message === 'string') {
      return new ApiError(response.status, record.message)
    }
  }

  return new ApiError(
    response.status,
    response.statusText || `Request failed with status ${response.status}`
  )
}

export interface ApiClientOptions extends RequestInit {
  // Skips attaching the bearer token AND skips the 401-refresh-retry dance.
  // Used for login/register (no token yet) and refresh (401 there means the
  // refresh token itself is dead, not that we need to refresh again).
  skipAuth?: boolean
}

function performFetch(path: string, options: ApiClientOptions): Promise<Response> {
  const headers = new Headers(options.headers)
  if (options.body && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json')
  }
  if (!options.skipAuth) {
    const token = accessTokenGetter()
    if (token) {
      headers.set('Authorization', `Bearer ${token}`)
    }
  }

  return fetch(`${env.apiBaseUrl}${path}`, { ...options, headers })
}

export async function apiClient<T>(path: string, options: ApiClientOptions = {}): Promise<T> {
  let response = await performFetch(path, options)

  if (response.status === 401 && !options.skipAuth) {
    const newToken = await refreshAccessTokenOnce()
    if (newToken) {
      response = await performFetch(path, options)
    } else {
      unauthorizedHandler()
      throw await parseErrorBody(response)
    }
  }

  if (!response.ok) {
    throw await parseErrorBody(response)
  }

  if (response.status === 204) {
    return undefined as T
  }

  return (await response.json()) as T
}
