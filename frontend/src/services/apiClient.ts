import type { Session } from "../types/api"

export class ApiError extends Error {
  constructor(
    public status: number,
    message: string,
    public errors: Record<string, string[]> = {},
  ) {
    super(message)
  }
}

// Access tokens stay in memory; the browser manages the HttpOnly refresh cookie.
export class ApiClient {
  private session: Session | null = null
  private generation = 0
  private refreshing: Promise<void> | null = null
  private listeners = new Set<() => void>()
  constructor(
    private baseUrl: string,
    private transport: typeof fetch = fetch,
  ) {}
  getSession = () => this.session
  subscribe = (listener: () => void) => {
    this.listeners.add(listener)
    return () => {
      this.listeners.delete(listener)
    }
  }
  private publish() {
    this.listeners.forEach((listener) => listener())
  }
  clearSession() {
    this.generation++
    this.session = null
    this.refreshing = null
    this.publish()
  }

  updateIdentity(fullName: string, email?: string) {
    if (this.session) {
      this.session = { ...this.session, fullName, ...(email ? { email } : {}) }
      this.publish()
    }
  }

  async login(email: string, password: string) {
    if (this.refreshing) await this.refreshing.catch(() => {})
    this.clearSession()
    const generation = this.generation
    const session = await this.send<Session>("/auth/login", {
      method: "POST",
      body: JSON.stringify({ email, password }),
    })
    if (generation !== this.generation)
      throw new ApiError(401, "Sign-in was cancelled.")
    this.session = session
    this.publish()
  }

  private async send<T,>(
    path: string,
    init: RequestInit,
    token?: string,
  ): Promise<T> {
    const headers = new Headers(init.headers)
    if (path.startsWith("/auth/")) headers.set("X-HR-Auth", "1")
    if (init.body && !(init.body instanceof FormData))
      headers.set("Content-Type", "application/json")
    if (token) headers.set("Authorization", `Bearer ${token}`)
    let response: Response
    try {
      // Call fetch as a function; using the client as its receiver can cause
      // browsers to reject the call with "Illegal invocation".
      const transport = this.transport
      response = await transport(this.baseUrl + path, {
        ...init,
        headers,
        cache: "no-store",
        credentials: "include",
      })
    } catch (error) {
      if (init.signal?.aborted) throw error
      throw new ApiError(
        0,
        "Cannot reach the server. Check your connection and try again.",
      )
    }
    if (!response.ok) {
      const problem = await response.json().catch(() => ({}))
      const errors = problem.errors ?? {}
      const detail = Object.values(errors).flat().join(" ")
      throw new ApiError(
        response.status,
        detail ||
          problem.detail ||
          problem.title ||
          (response.status === 403
            ? "You do not have permission for this action."
            : response.status === 409
              ? "This record changed. Refresh it before retrying."
              : response.status === 429
                ? "Too many attempts. Please wait and try again."
                : "The request failed."),
        errors,
      )
    }
    if (response.status === 204) return undefined as T
    return response.json()
  }

  private refresh() {
    if (this.refreshing) return this.refreshing
    const generation = this.generation
    const operation = this.send<Session>("/auth/refresh", {
      method: "POST",
    })
      .then((session) => {
        if (generation !== this.generation)
          throw new ApiError(401, "Session changed.")
        this.session = session
        this.publish()
      })
      .catch((error) => {
        if (generation === this.generation) this.clearSession()
        throw error
      })
      .finally(() => {
        if (this.refreshing === operation) this.refreshing = null
      })
    this.refreshing = operation
    return operation
  }

  async restoreSession() {
    if (!this.session) await this.refresh()
  }

  async request<T,>(path: string, init: RequestInit = {}): Promise<T> {
    const generation = this.generation
    if (!this.session) throw new ApiError(401, "Please sign in.")
    if (Date.parse(this.session.expiresAtUtc) <= Date.now() + 30_000)
      await this.refresh()
    if (generation !== this.generation)
      throw new ApiError(401, "Session changed.")
    const token = this.session?.accessToken
    try {
      const result = await this.send<T>(path, init, token)
      if (generation !== this.generation)
        throw new ApiError(401, "Session changed.")
      return result
    } catch (error) {
      if (
        !(error instanceof ApiError) ||
        error.status !== 401 ||
        generation !== this.generation
      )
        throw error
      // Another request may have already refreshed the rejected access token.
      if (this.session?.accessToken === token) await this.refresh()
      if (generation !== this.generation)
        throw new ApiError(401, "Session changed.")
      try {
        const result = await this.send<T>(path, init, this.session?.accessToken)
        if (generation !== this.generation)
          throw new ApiError(401, "Session changed.")
        return result
      } catch (retryError) {
        if (
          retryError instanceof ApiError &&
          retryError.status === 401 &&
          generation === this.generation
        )
          this.clearSession()
        throw retryError
      }
    }
  }

  async logout() {
    // Let rotation finish before deleting its cookie, so a late response cannot
    // restore a refresh cookie after logout.
    if (this.refreshing) await this.refreshing.catch(() => {})
    const generation = this.generation
    try {
      await this.send<void>(
        "/auth/logout",
        { method: "POST" },
        this.session?.accessToken,
      )
    } finally {
      if (generation === this.generation) this.clearSession()
    }
  }
}
export const api = new ApiClient(
  (import.meta.env?.VITE_API_BASE_URL || "/api").replace(/\/$/, ""),
)
export const json = (method: string, data: unknown): RequestInit => ({
  method,
  body: JSON.stringify(data),
})
