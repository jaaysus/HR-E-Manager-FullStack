import { useState } from "react"

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"

import { api, json } from "../services/apiClient"

import { useAuth } from "../app/AuthProvider"

import type { ClothingSettings } from "../types/api"

import ApiState from "../components/ApiState"

export default function ClothingSettingsPage() {
  const query = useQuery({
    queryKey: ["clothing", "settings"],

    queryFn: ({ signal }) =>
      api.request<ClothingSettings>("/clothing/settings", { signal }),
  })

  return (
    <div className="page page-settings space-y-6">
      <ApiState
        pending={query.isPending}
        error={query.error}
        retry={() => void query.refetch()}
      />
      {query.data && (
        <AlertsForm
          initial={query.data}
          reload={async () => (await query.refetch()).data}
        />
      )}
    </div>
  )
}

function AlertsForm({
  initial,

  reload,
}: {
  initial: ClothingSettings

  reload: () => Promise<ClothingSettings | undefined>
}) {
  const { can } = useAuth()

  const cache = useQueryClient()

  const [draft, setDraft] = useState(initial)

  const save = useMutation({
    mutationFn: () =>
      api.request<ClothingSettings>(
        "/clothing/settings",

        json("PUT", {
          eligibilityInterval: draft.eligibilityInterval,

          eligibilityUnit: draft.eligibilityUnit,

          lowStockAlertsEnabled: draft.lowStockAlertsEnabled,

          lowStockThreshold: draft.lowStockThreshold,

          rowVersion: draft.rowVersion,
        }),
      ),

    onSuccess: async (result) => {
      setDraft(result)

      await cache.invalidateQueries()
    },
  })

  return (
    <form
      className="panel max-w-3xl space-y-4"
      onSubmit={(event) => {
        event.preventDefault()

        save.mutate()
      }}
    >
      <h2 className="text-lg font-semibold">Clothing policy and alerts</h2>
      <p className="text-sm text-slate-500">
        Controls alert generation for the clothing module. Personal preferences
        are in My account.
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
        disabled={!can("clothing.settings.manage") || save.isPending}
        className="space-y-4"
      >
        <h3 className="font-semibold">Eligibility policy</h3>
        <div className="flex gap-4">
          <label className="field">
            Interval
            <input
              className="input"
              aria-label="Eligibility interval"
              type="number"
              min={1}
              max={10000}
              step={1}
              required
              value={draft.eligibilityInterval ?? 6}
              onChange={(e) =>
                setDraft({
                  ...draft,

                  eligibilityInterval: Number(e.target.value),
                })
              }
            />
          </label>
          <label className="field">
            Unit
            <select
              className="input"
              aria-label="Eligibility unit"
              value={draft.eligibilityUnit ?? "Months"}
              onChange={(e) =>
                setDraft({
                  ...draft,

                  eligibilityUnit: e.target
                    .value as ClothingSettings["eligibilityUnit"],
                })
              }
            >
              <option value="Minutes">Minutes</option>
              <option value="Days">Days</option>
              <option value="Months">Months</option>
            </select>
          </label>
        </div>
        <p className="text-sm text-slate-500">
          One coat per current interval. Changing the interval starts a new
          schedule when saved; the first request follows
          one interval later, or after enrollment if the employee starts in the
          future. Older missed requests are superseded. Completed allocations
          remain recorded.
        </p>
        <h3 className="font-semibold">Inventory alerts</h3>
        <label className="flex gap-2">
          <input
            type="checkbox"
            checked={draft.lowStockAlertsEnabled}
            onChange={(event) =>
              setDraft({
                ...draft,

                lowStockAlertsEnabled: event.target.checked,
              })
            }
          />
          Generate low-stock alerts
        </label>
        <label className="field max-w-xs">
          Low stock threshold (units per variant)
          <input
            className="input"
            required
            type="number"
            min={0}
            max={2147483647}
            step={1}
            value={draft.lowStockThreshold}
            onChange={(event) =>
              setDraft({
                ...draft,

                lowStockThreshold: Number(event.target.value),
              })
            }
          />
        </label>
      </fieldset>
      <p className="text-xs text-slate-500">
        An alert is generated when a variant has this many units or fewer.
      </p>
      {can("clothing.settings.manage") && (
        <button className="btn" disabled={save.isPending}>
          Save clothing settings
        </button>
      )}
      {save.isSuccess && (
        <p role="status" className="text-green-700">
          Clothing settings saved.
        </p>
      )}
    </form>
  )
}
