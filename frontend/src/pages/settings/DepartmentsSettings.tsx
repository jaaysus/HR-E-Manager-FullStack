import { useState } from "react"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { platformApi } from "../../services/platformApi"
import { api, json } from "../../services/apiClient"
import { useAuth } from "../../app/AuthProvider"
import type { Department } from "../../types/api"
import ApiState from "../../components/ApiState"

export default function DepartmentsSettings() {
  const { can } = useAuth()
  const cache = useQueryClient()
  const [editing, setEditing] = useState<Department | null | undefined>()
  const query = useQuery({
    queryKey: ["platform", "departments"],
    queryFn: ({ signal }) => platformApi.departments(signal),
  })
  return (
    <section className="space-y-4">
      <div className="flex justify-between gap-3">
        <div>
          <h2 className="text-lg font-semibold">Departments</h2>
          <p className="text-sm text-slate-500 mt-1">
            Manage the departments used by employee records and module rules.
          </p>
        </div>
        {can("platform.departments.manage") && (
          <button className="btn" onClick={() => setEditing(null)}>
            Add department
          </button>
        )}
      </div>
      <ApiState
        pending={query.isPending}
        error={query.error}
        retry={() => void query.refetch()}
      />
      {editing !== undefined && (
        <DepartmentForm
          key={editing ? editing.id + ":" + editing.rowVersion : "new"}
          department={editing}
          onClose={() => setEditing(undefined)}
          onRefresh={async () => {
            const refreshed = await query.refetch()
            const latest = refreshed.data?.find(
              (department) => department.id === editing?.id,
            )
            if (latest) setEditing(latest)
          }}
          onSaved={async () => {
            setEditing(undefined)
            await cache.invalidateQueries()
          }}
        />
      )}
      <div className="overflow-x-auto">
        <table className="data-table">
          <thead>
            <tr>
              <th>Code</th>
              <th>Name</th>
              <th>Status</th>
              {can("platform.departments.manage") && <th>Actions</th>}
            </tr>
          </thead>
          <tbody>
            {query.data?.map((department) => (
              <tr key={department.id}>
                <td>{department.code}</td>
                <td>{department.name}</td>
                <td>{department.isActive ? "Active" : "Inactive"}</td>
                {can("platform.departments.manage") && (
                  <td>
                    <button
                      className="btn secondary"
                      onClick={() => setEditing(department)}
                    >
                      Edit department
                    </button>
                  </td>
                )}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  )
}
function DepartmentForm({
  department,
  onSaved,
  onClose,
  onRefresh,
}: {
  department: Department | null
  onSaved: () => Promise<void>
  onClose: () => void
  onRefresh: () => Promise<void>
}) {
  const [code, setCode] = useState(department?.code ?? "")
  const [name, setName] = useState(department?.name ?? "")
  const [active, setActive] = useState(department?.isActive ?? true)
  const save = useMutation({
    mutationFn: () =>
      api.request<Department>(
        department ? `/departments/${department.id}` : "/departments",
        json(department ? "PUT" : "POST", {
          code,
          name,
          isActive: active,
          rowVersion: department?.rowVersion,
        }),
      ),
    onSuccess: onSaved,
  })
  return (
    <form
      className="rounded-lg border border-slate-200 p-4 space-y-4"
      onSubmit={(event) => {
        event.preventDefault()
        save.mutate()
      }}
    >
      <ApiState
        error={save.error}
        retry={department ? () => void onRefresh() : undefined}
      />
      <fieldset disabled={save.isPending} className="grid gap-4 md:grid-cols-3">
        <label className="field">
          Department code
          <input
            className="input"
            required
            maxLength={50}
            value={code}
            onChange={(event) => setCode(event.target.value)}
          />
        </label>
        <label className="field">
          Department name
          <input
            className="input"
            required
            maxLength={100}
            value={name}
            onChange={(event) => setName(event.target.value)}
          />
        </label>
        <label className="flex gap-2 items-center">
          <input
            type="checkbox"
            checked={active}
            onChange={(event) => setActive(event.target.checked)}
          />
          Active department
        </label>
      </fieldset>
      <p className="text-xs text-slate-500">
        A department with active employees cannot be deactivated.
      </p>
      <div className="flex gap-3">
        <button className="btn" disabled={save.isPending}>
          Save department
        </button>
        <button className="btn secondary" type="button" onClick={onClose}>
          Cancel
        </button>
      </div>
    </form>
  )
}
