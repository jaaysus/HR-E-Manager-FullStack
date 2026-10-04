import { useState } from "react"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { platformApi } from "../services/platformApi"
import type { Role, User } from "../types/api"
import ApiState from "../components/ApiState"
const roles: Role[] = ["HrAdministrator", "Viewer", "ClothingManager", "ClothingViewer", "InventoryManager"]
const roleDescriptions: Record<Role, string> = {
  HrAdministrator: "HR Administrator — employees, organization settings, and user access",
  Viewer: "HR Viewer — employee directory and workspace read access",
  ClothingManager: "Clothing Manager — employees, imports, stock, rules, alert settings, and coat provision",
  ClothingViewer: "Clothing Viewer — read-only clothing tracking",
  InventoryManager: "Inventory Manager — stock receipts and coat provision (existing role)",
}
export default function UsersPage() {
  const cache = useQueryClient()
  const [page, setPage] = useState(1)
  const [editing, setEditing] = useState<User | null | undefined>()
  const users = useQuery({
    queryKey: ["platform", "users", page],
    queryFn: ({ signal }) => platformApi.users(page, signal),
  })
  return (
    <div className="page space-y-5">
      <div className="flex justify-end">
        <button className="btn" onClick={() => setEditing(null)}>
          Add user
        </button>
      </div>
      {editing !== undefined && (
        <UserForm
          key={editing?.id ?? "new"}
          user={editing}
          onClose={() => setEditing(undefined)}
          onSaved={async () => {
            setEditing(undefined)
            await cache.invalidateQueries({ queryKey: ["platform", "users"] })
          }}
        />
      )}
      <ApiState
        pending={users.isPending}
        error={users.error}
        retry={() => void users.refetch()}
      >
        <div className="panel overflow-x-auto">
          <table className="data-table">
            <thead>
              <tr>
                <th>Name</th>
                <th>Email</th>
                <th>Roles</th>
                <th>Status</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {users.data?.map((user) => (
                <tr key={user.id}>
                  <td>{user.fullName}</td>
                  <td>{user.email}</td>
                  <td>{user.roles.join(", ")}</td>
                  <td>{user.isActive ? "Active" : "Inactive"}</td>
                  <td>
                    <button
                      className="btn secondary"
                      onClick={() => setEditing(user)}
                    >
                      Edit access
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {users.data?.length === 0 && (
            <p className="py-6 text-slate-500">No users on this page.</p>
          )}
          <div className="mt-4 flex justify-between">
            <button
              className="btn secondary"
              disabled={page <= 1}
              onClick={() => setPage(page - 1)}
            >
              Previous
            </button>
            <span className="text-sm text-slate-500">Page {page}</span>
            <button
              className="btn secondary"
              disabled={(users.data?.length ?? 0) < 25}
              onClick={() => setPage(page + 1)}
            >
              Next
            </button>
          </div>
        </div>
      </ApiState>
    </div>
  )
}
function UserForm({
  user,
  onClose,
  onSaved,
}: {
  user: User | null
  onClose: () => void
  onSaved: () => Promise<void>
}) {
  const [email, setEmail] = useState(user?.email ?? "")
  const [fullName, setName] = useState(user?.fullName ?? "")
  const [password, setPassword] = useState("")
  const [selected, setSelected] = useState<Role[]>(user?.roles ?? ["Viewer"])
  const [isActive, setActive] = useState(user?.isActive ?? true)
  const save = useMutation({
    mutationFn: () =>
      user
        ? platformApi.updateUser(user.id, {
            fullName,
            isActive,
            roles: selected,
          })
        : platformApi.createUser({
            email,
            fullName,
            password,
            roles: selected,
          }),
    onSuccess: onSaved,
  })
  return (
    <form
      className="panel space-y-4"
      onSubmit={(e) => {
        e.preventDefault()
        save.mutate()
      }}
    >
      <h2 className="font-semibold">
        {user ? "Edit user access" : "Create user"}
      </h2>
      <ApiState error={save.error} />
      <div className="grid gap-4 md:grid-cols-3">
        <label className="field">
          Full name
          <input
            className="input"
            required
            maxLength={200}
            disabled={save.isPending}
            value={fullName}
            onChange={(e) => setName(e.target.value)}
          />
        </label>
        <label className="field">
          Email
          <input
            className="input"
            required
            type="email"
            maxLength={256}
            disabled={!!user || save.isPending}
            value={email}
            onChange={(e) => setEmail(e.target.value)}
          />
        </label>
        {!user && (
          <label className="field">
            Initial password
            <input
              className="input"
              required
              type="password"
              minLength={8}
              maxLength={128}
              autoComplete="new-password"
              disabled={save.isPending}
              value={password}
              onChange={(e) => setPassword(e.target.value)}
            />
          </label>
        )}
      </div>
      <fieldset disabled={save.isPending}>
        <legend className="mb-2 text-sm text-slate-500">
          Roles (at least one)
        </legend>
        <div className="flex flex-wrap gap-5">
          {roles.map((role) => (
            <label key={role} className="flex gap-2 text-sm">
              <input
                type="checkbox"
                checked={selected.includes(role)}
                onChange={(e) =>
                  setSelected(
                    e.target.checked
                      ? [...selected, role]
                      : selected.filter((value) => value !== role),
                  )
                }
              />
              {roleDescriptions[role]}
            </label>
          ))}
        </div>
      </fieldset>
      {user && (
        <label className="flex gap-2 text-sm">
          <input
            type="checkbox"
            disabled={save.isPending}
            checked={isActive}
            onChange={(e) => setActive(e.target.checked)}
          />
          Active account
        </label>
      )}
      <p className="text-xs text-slate-500">
        Clothing tracking requires a Clothing Manager, Clothing Viewer, or
        Inventory Manager role. Assign a clothing role alongside HR Administrator
        when an administrator also needs that module.
        Changes to an existing account require that user to sign in again.
      </p>
      <div className="flex gap-3">
        <button
          className="btn"
          disabled={save.isPending || selected.length === 0}
        >
          {save.isPending ? "Saving…" : "Save user"}
        </button>
        <button
          className="btn secondary"
          type="button"
          disabled={save.isPending}
          onClick={onClose}
        >
          Cancel
        </button>
      </div>
    </form>
  )
}
