import { useState } from "react"

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"

import { api, json } from "../../services/apiClient"

import { useAuth } from "../../app/AuthProvider"

import type { InventoryItem } from "../../types/api"

import ApiState from "../../components/ApiState"

import CoatIcon from "../../components/CoatIcon"

import { clothingAccent } from "../../utils/clothing"

export default function CoatVariants() {
  const { can } = useAuth()

  const cache = useQueryClient()

  const query = useQuery({
    queryKey: ["inventory", "variants"],

    queryFn: () => api.request<InventoryItem[]>("/inventory/items"),
  })

  const [editing, setEditing] = useState<InventoryItem | null>()

  const [color, setColor] = useState("")

  const [size, setSize] = useState("")

  const [name, setName] = useState("")

  const save = useMutation({
    mutationFn: () =>
      api.request(
        `/inventory/items${editing ? `/${editing.id}` : ""}`,

        json(editing ? "PUT" : "POST", {
          color,

          size,

          name,

          rowVersion: editing?.rowVersion,

          isActive: true,
        }),
      ),

    onSuccess: async () => {
      setEditing(undefined)

      await cache.invalidateQueries()
    },
  })

  const remove = useMutation({
    mutationFn: (item: InventoryItem) =>
      api.request(
        `/inventory/items/${item.id}?rowVersion=${encodeURIComponent(item.rowVersion)}`,

        { method: "DELETE" },
      ),

    onSuccess: () => cache.invalidateQueries(),
  })

  const busy = save.isPending || remove.isPending

  const form = (
    <form
      className="space-y-3"
      onSubmit={(event) => {
        event.preventDefault()

        save.mutate()
      }}
    >
      <h3 className="font-semibold text-slate-900">
        {editing ? "Edit coat variant" : "New coat variant"}
      </h3>
      <fieldset disabled={busy} className="space-y-3">
        <label className="field">
          Name
          <input
            className="input"
            required
            maxLength={150}
            value={name}
            onChange={(event) => setName(event.target.value)}
          />
        </label>
        <label className="field">
          Color
          <input
            className="input"
            required
            maxLength={50}
            placeholder="e.g. Navy blue"
            value={color}
            onChange={(event) => setColor(event.target.value)}
          />
        </label>
        <label className="field">
          Size
          <input
            className="input"
            required
            maxLength={30}
            placeholder="e.g. M, XL, or 42"
            value={size}
            onChange={(event) => setSize(event.target.value)}
          />
        </label>
        <div className="flex flex-wrap gap-2">
          <button className="btn">Save coat variant</button>
          <button
            type="button"
            className="px-3 py-2 text-sm text-slate-600"
            onClick={() => setEditing(undefined)}
          >
            Cancel
          </button>
        </div>
      </fieldset>
    </form>
  )

  const items = query.data ?? []

  return (
    <section className="space-y-4">
      <div className="flex items-center justify-between gap-4">
        <div>
          <h2 className="text-lg font-semibold text-slate-900">
            Coat Variants
          </h2>
          <p className="text-sm text-slate-500">
            Track the clothing received and provided.
          </p>
        </div>
        <span className="text-sm text-slate-500">
          {items.filter((item) => item.isActive !== false).length} active
          variants
        </span>
      </div>
      <ApiState
        pending={query.isPending}
        error={query.error ?? save.error ?? remove.error}
        retry={() => {
          save.reset()

          remove.reset()

          void query.refetch()
        }}
      />
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
        {items.map((item) => (
          <article
            key={item.id}
            aria-label={`Coat variant ${item.name}`}
            style={
              item.isActive === false
                ? undefined
                : {
                    borderTopColor: clothingAccent(item.color),
                    background: `color-mix(in srgb, ${clothingAccent(item.color)} 6%, white)`,
                  }
            }
            className={`rounded-xl border border-t-4 p-4 ${
              item.isActive === false
                ? "border-slate-200 bg-slate-50"
                : "border-blue-200 border-t-blue-600 bg-blue-50"
            }`}
          >
            {editing?.id === item.id ? (
              form
            ) : (
              <>
                <div className="flex items-center justify-between">
                  <CoatIcon color={clothingAccent(item.color)} />
                  <span
                    className={`text-2xl font-extrabold ${
                      item.isActive === false
                        ? "text-slate-400"
                        : "text-blue-600"
                    }`}
                  >
                    {item.quantityOnHand}
                  </span>
                </div>
                <h3 className="mt-3 font-semibold text-slate-900 break-words">
                  {item.name}
                </h3>
                <p className="mt-1 text-xs font-semibold text-blue-600 break-words">
                  <span
                    className="inline-block h-2.5 w-2.5 rounded-full mr-1.5"
                    style={{ background: clothingAccent(item.color) }}
                    aria-hidden="true"
                  />
                  {item.color || "Color not set"} ·{" "}
                  {item.size ? `Size ${item.size}` : "Size not set"}
                </p>
                <p
                  className={`mt-2 text-xs font-semibold ${
                    item.isActive === false
                      ? "text-slate-500"
                      : item.quantityOnHand === 0
                        ? "text-red-600"
                        : "text-slate-600"
                  }`}
                >
                  {item.isActive === false
                    ? "Retired"
                    : item.quantityOnHand === 0
                      ? "OUT OF STOCK"
                      : `${item.quantityOnHand} ready to issue`}
                </p>
                {can("inventory.manage") && (
                  <div className="mt-4 flex gap-4 border-t border-slate-200 pt-3 text-sm font-medium">
                    <button
                      className="text-blue-700"
                      disabled={busy}
                      onClick={() => {
                        setEditing(item)

                        setColor(item.color ?? "")

                        setSize(item.size ?? "")

                        setName(item.name)

                        save.reset()

                        remove.reset()
                      }}
                    >
                      {item.isActive === false ? "Restore / edit" : "Edit"}
                    </button>
                    {item.isActive !== false && (
                      <button
                        className="text-red-600 disabled:text-slate-400"
                        disabled={busy || item.quantityOnHand !== 0}
                        title={
                          item.quantityOnHand !== 0
                            ? "Stock must be zero before deleting this variant"
                            : "Retire this variant and keep its history"
                        }
                        onClick={() => remove.mutate(item)}
                      >
                        Delete
                      </button>
                    )}
                  </div>
                )}
              </>
            )}
          </article>
        ))}
        {can("inventory.manage") &&
          (editing === null ? (
            <div className="rounded-xl border border-t-4 border-blue-200 border-t-blue-600 bg-white p-4">
              {form}
            </div>
          ) : (
            <button
              className="flex min-h-48 flex-col items-center justify-center gap-3 rounded-xl border-2 border-dashed border-slate-300 bg-white p-6 text-blue-600 transition-colors hover:border-blue-400 hover:bg-blue-50 disabled:opacity-50"
              disabled={busy}
              onClick={() => {
                setEditing(null)

                setColor("")

                setSize("")

                setName("")

                save.reset()

                remove.reset()
              }}
            >
              <span
                className="flex h-10 w-10 items-center justify-center rounded-full bg-blue-50 text-2xl"
                aria-hidden="true"
              >
                +
              </span>
              <span className="font-semibold">Add coat variant</span>
            </button>
          ))}
      </div>
      {items.length === 0 && !query.isPending && (
        <p className="text-sm text-slate-500">No coat variants yet.</p>
      )}
    </section>
  )
}
