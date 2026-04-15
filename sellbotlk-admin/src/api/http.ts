const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL?.toString().replace(/\/$/, '') ??
  'http://localhost:5028'

export class ApiError extends Error {
  status: number
  body?: unknown

  constructor(message: string, status: number, body?: unknown) {
    super(message)
    this.status = status
    this.body = body
  }
}

export function getToken(): string | null {
  return localStorage.getItem('token')
}

export function setToken(token: string) {
  localStorage.setItem('token', token)
}

export function clearToken() {
  localStorage.removeItem('token')
}

function authHeaders(): Record<string, string> {
  const token = getToken()
  const headers: Record<string, string> = { Accept: 'application/json' }
  if (token) headers['Authorization'] = `Bearer ${token}`
  return headers
}

async function readJsonSafe(res: Response): Promise<unknown> {
  const contentType = res.headers.get('content-type') ?? ''
  if (!contentType.includes('application/json')) return undefined
  try {
    return await res.json()
  } catch {
    return undefined
  }
}

function handleUnauthorized(res: Response) {
  if (res.status === 401) {
    clearToken()
    window.location.href = '/login'
  }
}

export async function apiGet<T>(path: string, init?: RequestInit): Promise<T> {
  const url = `${API_BASE_URL}${path.startsWith('/') ? '' : '/'}${path}`
  const res = await fetch(url, {
    method: 'GET',
    headers: authHeaders(),
    ...init,
  })

  handleUnauthorized(res)
  const body = await readJsonSafe(res)
  if (!res.ok) {
    throw new ApiError(`GET ${path} failed`, res.status, body)
  }
  return body as T
}

export async function apiPost<T>(
  path: string,
  payload: unknown,
  init?: RequestInit,
): Promise<T> {
  const url = `${API_BASE_URL}${path.startsWith('/') ? '' : '/'}${path}`
  const res = await fetch(url, {
    method: 'POST',
    headers: {
      ...authHeaders(),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(payload),
    ...init,
  })

  handleUnauthorized(res)
  const body = await readJsonSafe(res)
  if (!res.ok) {
    throw new ApiError(`POST ${path} failed`, res.status, body)
  }
  return body as T
}

export async function apiPut<T>(
  path: string,
  payload: unknown,
  init?: RequestInit,
): Promise<T> {
  const url = `${API_BASE_URL}${path.startsWith('/') ? '' : '/'}${path}`
  const res = await fetch(url, {
    method: 'PUT',
    headers: {
      ...authHeaders(),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(payload),
    ...init,
  })

  handleUnauthorized(res)
  const body = await readJsonSafe(res)
  if (!res.ok) {
    throw new ApiError(`PUT ${path} failed`, res.status, body)
  }
  return body as T
}

