import {
  createContext,
  useContext,
  useEffect,
  useState,
  useSyncExternalStore,
  type ReactNode,
} from "react"
import { api } from "../services/apiClient"
import { queryClient } from "./queryClient"
import type { Permission, Session } from "../types/api"
const AuthContext = createContext<{
  user: Session | null
  can: (permission: Permission) => boolean
  login: (email: string, password: string) => Promise<void>
  logout: () => Promise<void>
} | null>(null)
export default function AuthProvider({ children }: { children: ReactNode }) {
  const user = useSyncExternalStore(api.subscribe, api.getSession, () => null)
  const [restoring, setRestoring] = useState(true)
  const [restoreError, setRestoreError] = useState("")
  const [attempt, setAttempt] = useState(0)
  useEffect(() => {
    let active = true
    setRestoring(true)
    setRestoreError("")
    api
      .restoreSession()
      .catch((error) => {
        if (active && error.status !== 401)
          setRestoreError(
            error instanceof Error
              ? error.message
              : "Could not restore your session.",
          )
      })
      .finally(() => {
        if (active) setRestoring(false)
      })
    return () => {
      active = false
    }
  }, [attempt])
  useEffect(
    () =>
      api.subscribe(() => {
        if (!api.getSession()) queryClient.clear()
      }),
    [],
  )
  async function login(email: string, password: string) {
    queryClient.clear()
    await api.login(email, password)
  }
  async function logout() {
    await api.logout()
  }
  if (restoring || restoreError)
    return (
      <div className="min-h-screen flex flex-col items-center justify-center gap-4">
        {restoring ? (
          <p role="status">Restoring your session…</p>
        ) : (
          <>
            <p role="alert">{restoreError}</p>
            <button
              className="btn"
              onClick={() => setAttempt((value) => value + 1)}
            >
              Try again
            </button>
          </>
        )}
      </div>
    )
  return (
    <AuthContext.Provider
      value={{
        user,
        can: (permission) => user?.permissions?.includes(permission) ?? false,
        login,
        logout,
      }}
    >
      {children}
    </AuthContext.Provider>
  )
}
export function useAuth() {
  const auth = useContext(AuthContext)
  if (!auth) throw new Error("AuthProvider is required")
  return auth
}
