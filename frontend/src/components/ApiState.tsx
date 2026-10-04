import type { ReactNode } from "react"
import { ApiError } from "../services/apiClient"
export default function ApiState({
  pending,
  error,
  retry,
  children,
}: {
  pending?: boolean
  error?: Error | null
  retry?: () => void
  children?: ReactNode
}) {
  if (pending)
    return (
      <p role="status" className="p-6 text-center text-slate-500">
        Loading…
      </p>
    )
  if (error)
    return (
      <div
        role="alert"
        className="rounded-lg border border-red-200 bg-red-50 p-4 text-red-800"
      >
        <p>{error.message}</p>
        {error instanceof ApiError && error.status === 409 && (
          <p className="mt-1 text-sm">
            Refresh the record, review the latest values, then try again.
          </p>
        )}
        {retry && (
          <button className="btn secondary mt-3" onClick={retry}>
            Refresh
          </button>
        )}
      </div>
    )
  return <>{children}</>
}
