import { useState } from "react"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { useAuth } from "../app/AuthProvider"
import { platformApi } from "../services/platformApi"
import type { OrganizationPreferences } from "../types/api"
import ApiState from "../components/ApiState"
import DepartmentsSettings from "./settings/DepartmentsSettings"

export default function SettingsPage() {
  const query = useQuery({
    queryKey: ["platform", "settings"],
    queryFn: ({ signal }) => platformApi.settings(signal),
  })
  return (
    <div className="page page-settings space-y-6">
      <section className="panel space-y-6">
        <ApiState
          pending={query.isPending}
          error={query.error}
          retry={() => void query.refetch()}
        />
        {query.data && (
          <BusinessTimeZoneForm
            initial={query.data}
            reload={async () => (await query.refetch()).data}
          />
        )}
        <DepartmentsSettings />
      </section>
    </div>
  )
}
function BusinessTimeZoneForm({
  initial,
  reload,
}: {
  initial: OrganizationPreferences
  reload: () => Promise<OrganizationPreferences | undefined>
}) {
  const { can } = useAuth()
  const cache = useQueryClient()
  const [draft, setDraft] = useState(initial)
  const save = useMutation({
    mutationFn: () => platformApi.saveSettings(draft),
    onSuccess: async (result) => {
      setDraft(result)
      await cache.invalidateQueries({ queryKey: ["platform", "settings"] })
      await cache.invalidateQueries({ queryKey: ["program"] })
    },
  })
  return (
    <form
      className="space-y-4 border-b border-slate-200 pb-6"
      onSubmit={(event) => {
        event.preventDefault()
        save.mutate()
      }}
    >
      <p className="text-sm text-slate-500">
        Set the time zone used for business dates across the app.
      </p>
      <ApiState
        error={save.error}
        retry={() => {
          void reload().then((result) => {
            if (result) {
              setDraft(result)
              save.reset()
            }
          })
        }}
      />
      <fieldset
        disabled={!can("platform.settings.manage") || save.isPending}
        className="grid gap-4 md:grid-cols-2"
      >
        <label className="field">
          Business time zone
          <input
            className="input"
            required
            list="business-time-zones"
            placeholder="Africa/Casablanca"
            value={draft.timeZoneId}
            onChange={(event) =>
              setDraft({ ...draft, timeZoneId: event.target.value })
            }
          />
          <datalist id="business-time-zones">
            {[
              "Africa/Casablanca",
              "UTC",
              "Europe/Paris",
              "Europe/London",
              "America/New_York",
              "Asia/Dubai",
            ].map((zone) => (
              <option key={zone} value={zone} />
            ))}
          </datalist>
        </label>
      </fieldset>
      {can("platform.settings.manage") ? (
        <button className="btn" disabled={save.isPending}>
          Save time zone
        </button>
      ) : (
        <p className="text-sm text-slate-500">
          HR administrators manage the business time zone.
        </p>
      )}
      {save.isSuccess && (
        <p role="status" className="text-green-700">
          Business time zone saved.
        </p>
      )}
    </form>
  )
}
