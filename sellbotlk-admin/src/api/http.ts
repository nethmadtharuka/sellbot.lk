const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL?.toString().replace(/\/$/, '') ??
  'http://localhost:5000'

export class ApiError extends Error {
  status: number
  body?: unknown

  constructor(message: string, status: number, body?: unknown) {
    super(message)
    this.status = status
    this.body = body
  }
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

export async function apiGet<T>(path: string, init?: RequestInit): Promise<T> {
  const url = `${API_BASE_URL}${path.startsWith('/') ? '' : '/'}${path}`
  const res = await fetch(url, {
    method: 'GET',
    headers: { Accept: 'application/json' },
    ...init,
  })

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
      Accept: 'application/json',
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(payload),
    ...init,
  })

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
      Accept: 'application/json',
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(payload),
    ...init,
  })

  const body = await readJsonSafe(res)
  if (!res.ok) {
    throw new ApiError(`PUT ${path} failed`, res.status, body)
  }
  return body as T
}

