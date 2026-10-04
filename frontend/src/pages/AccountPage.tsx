import { useState } from "react"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { useAuth } from "../app/AuthProvider"
import { platformApi } from "../services/platformApi"
import { api, json } from "../services/apiClient"
import type { PersonalNotificationPreferences } from "../types/api"
import ApiState from "../components/ApiState"
export default function AccountPage() {
  const { can } = useAuth()
  return (
    <div className="page page-account grid grid-cols-[repeat(auto-fit,minmax(min(100%,18rem),1fr))] items-start gap-6">
      <ProfileForm />
      <PasswordForm />
      {can("coats.read") && <NotificationPreferences />}
    </div>
  )
}
function ProfileForm() {
  const { user } = useAuth()
  const [fullName, setFullName] = useState(user?.fullName ?? "")
  const [email, setEmail] = useState(user?.email ?? "")
  const save = useMutation({
    mutationFn: () =>
      api.request<{ fullName: string, email: string }>(
        "/auth/profile",
        json("PUT", { fullName, email }),
      ),
    onSuccess: (result) => {
      api.updateIdentity(result.fullName, result.email)
      setFullName(result.fullName)
      setEmail(result.email)
    },
  })
  return (
    <form
      className="panel min-w-0 space-y-4"
      onSubmit={(event) => {
        event.preventDefault()
        save.mutate()
      }}
    >
      <h2 className="text-lg font-semibold">Profile</h2>
      <ApiState error={save.error} />
      <fieldset disabled={save.isPending} className="space-y-4">
        <label className="field">
          Full name
          <input
            className="input"
            required
            maxLength={200}
            value={fullName}
            onChange={(event) => setFullName(event.target.value)}
          />
        </label>
        <label className="field">
          Email
          <input
            className="input"
            required
            type="email"
            maxLength={256}
            value={email}
            onChange={(event) => setEmail(event.target.value)}
          />
        </label>
      </fieldset>
      <p className="text-sm text-slate-500">
        Assigned roles: {user?.roles.join(", ")}. HR administrators manage role
        assignments in Users & access.
      </p>
      <button className="btn" disabled={save.isPending}>
        Save profile
      </button>
      {save.isSuccess && (
        <p role="status" className="text-green-700">
          Profile saved.
        </p>
      )}
    </form>
  )
}
function NotificationPreferences() {
  const query = useQuery({
    queryKey: ["account", "preferences"],
    queryFn: ({ signal }) =>
      api.request<PersonalNotificationPreferences>(
        "/auth/profile/preferences",
        { signal },
      ),
  })
  return (
    <section className="panel min-w-0 space-y-4">
      <h2 className="text-lg font-semibold">Personal notifications</h2>
      <p className="text-sm text-slate-500">
        Choose which clothing notifications appear in your feed. This does not
        change module-wide alert generation or other users’ preferences.
      </p>
      <ApiState
        pending={query.isPending}
        error={query.error}
        retry={() => void query.refetch()}
      />
      {query.data && <PreferencesForm initial={query.data} />}
    </section>
  )
}
function PreferencesForm({
  initial,
}: {
  initial: PersonalNotificationPreferences
}) {
  const cache = useQueryClient()
  const [draft, setDraft] = useState(initial)
  const save = useMutation({
    mutationFn: () =>
      api.request<PersonalNotificationPreferences>(
        "/auth/profile/preferences",
        json("PUT", draft),
      ),
    onSuccess: async (result) => {
      setDraft(result)
      await cache.invalidateQueries()
    },
  })
  return (
    <form
      className="space-y-4"
      onSubmit={(event) => {
        event.preventDefault()
        save.mutate()
      }}
    >
      <ApiState error={save.error} />
      <fieldset disabled={save.isPending} className="space-y-3">
        <label className="flex gap-2">
          <input
            type="checkbox"
            checked={draft.notifyLowStock}
            onChange={(event) =>
              setDraft({ ...draft, notifyLowStock: event.target.checked })
            }
          />
          Show low-stock notifications
        </label>
        <label className="flex gap-2">
          <input
            type="checkbox"
            checked={draft.notifyClothingActivity}
            onChange={(event) =>
              setDraft({
                ...draft,
                notifyClothingActivity: event.target.checked,
              })
            }
          />
          Show clothing requests, arrivals, and provision notifications
        </label>
      </fieldset>
      <button className="btn" disabled={save.isPending}>
        Save notification preferences
      </button>
      {save.isSuccess && (
        <p role="status" className="text-green-700">
          Notification preferences saved.
        </p>
      )}
    </form>
  )
}
function PasswordForm() {
  const { user } = useAuth()
  const [current, setCurrent] = useState("")
  const [next, setNext] = useState("")
  const change = useMutation({
    mutationFn: () => platformApi.changePassword(current, next),
    onSuccess: () => api.clearSession(),
  })
  return (
    <div className="min-w-0">
      <form
        className="panel space-y-5"
        onSubmit={(e) => {
          e.preventDefault()
          change.mutate()
        }}
      >
        <h2 className="text-lg font-semibold">Change password</h2>
        <ApiState error={change.error} />
        <label className="field">
          Current password
          <input
            className="input"
            type="password"
            required
            autoComplete="current-password"
            disabled={change.isPending}
            value={current}
            onChange={(e) => setCurrent(e.target.value)}
          />
        </label>
        <label className="field">
          New password
          <input
            className="input"
            type="password"
            required
            minLength={8}
            maxLength={128}
            autoComplete="new-password"
            disabled={change.isPending}
            value={next}
            onChange={(e) => setNext(e.target.value)}
          />
        </label>
        <p className="text-xs text-slate-500">
          After changing your password, sign in again with your new password.
        </p>
        <button className="btn" disabled={change.isPending}>
          {change.isPending ? "Changing…" : "Change password"}
        </button>
      </form>
    </div>
  )
}
