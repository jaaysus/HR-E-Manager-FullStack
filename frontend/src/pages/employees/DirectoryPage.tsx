import { useState } from "react"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { employeesApi } from "../../services/employeesApi"
import { platformApi } from "../../services/platformApi"
import { useAuth } from "../../app/AuthProvider"
import type { Employee, EmployeeInput } from "../../types/api"
import ApiState from "../../components/ApiState"

export default function DirectoryPage() {
  const { can } = useAuth()
  const cache = useQueryClient()
  const [query, setQuery] = useState("")
  const [page, setPage] = useState(1)
  const [editing, setEditing] = useState<Employee | null | undefined>()
  const list = useQuery({ queryKey: ["employees", "directory", query, page], queryFn: ({ signal }) => employeesApi.list({ query, departmentId: "", status: "active", page }, signal) })
  const deactivate = useMutation({ mutationFn: employeesApi.setActive, onSuccess: () => cache.invalidateQueries({ queryKey: ["employees"] }) })
  return <div className="page space-y-5">
    <div className="flex justify-between gap-4"><input className="input" aria-label="Search employees" placeholder="Search employee name or ID" value={query} onChange={event => { setQuery(event.target.value); setPage(1) }} />{can("employees.manage") && <button className="btn" onClick={() => setEditing(null)}>Add Employee</button>}</div>
    {editing !== undefined && can("employees.manage") && <DirectoryForm key={editing?.id ?? "new"} employee={editing} onClose={() => setEditing(undefined)} onSaved={async () => { setEditing(undefined); await cache.invalidateQueries({ queryKey: ["employees"] }) }} />}
    <ApiState pending={list.isPending} error={list.error ?? deactivate.error} retry={() => void list.refetch()} />
    <div className="panel overflow-x-auto"><table className="data-table"><thead><tr><th>Employee ID</th><th>Name</th><th>Department</th><th>Job title</th><th>Enrollment date</th>{can("employees.manage") && <th>Actions</th>}</tr></thead><tbody>{list.data?.items.map(employee => <tr key={employee.id}><td>{employee.employeeNumber}</td><td>{employee.fullName}</td><td>{employee.department}</td><td>{employee.jobTitle}</td><td>{employee.enrollmentDate}</td>{can("employees.manage") && <td><button className="btn secondary" onClick={() => setEditing(employee)}>Edit</button><button className="btn secondary ml-2" disabled={deactivate.isPending} onClick={() => deactivate.mutate(employee)}>Deactivate</button></td>}</tr>)}</tbody></table>{list.data?.items.length === 0 && <p className="py-5 text-slate-500">No employees found.</p>}
      <div className="flex justify-between mt-4"><button className="btn secondary" disabled={page === 1} onClick={() => setPage(page - 1)}>Previous</button><span>Page {page}</span><button className="btn secondary" disabled={!list.data || page * list.data.pageSize >= list.data.totalCount} onClick={() => setPage(page + 1)}>Next</button></div>
    </div>
  </div>
}

function DirectoryForm({ employee, onClose, onSaved }: { employee: Employee | null; onClose: () => void; onSaved: () => Promise<void> }) {
  const [input, setInput] = useState<EmployeeInput>({ employeeNumber: employee?.employeeNumber ?? "", fullName: employee?.fullName ?? "", departmentId: employee?.departmentId ?? "", enrollmentDate: employee?.enrollmentDate ?? "", jobTitle: employee?.jobTitle ?? "", notes: employee?.notes ?? "" })
  const departments = useQuery({ queryKey: ["platform", "departments"], queryFn: ({ signal }) => platformApi.departments(signal) })
  const save = useMutation({ mutationFn: () => employeesApi.save(input, employee ?? undefined), onSuccess: onSaved })
  return <form className="panel space-y-4" onSubmit={event => { event.preventDefault(); save.mutate() }}>
    <h2 className="font-semibold">{employee ? "Edit Employee" : "Add Employee"}</h2><ApiState error={save.error ?? departments.error} />
    <fieldset disabled={save.isPending} className="grid gap-4 md:grid-cols-2">
      <label className="field">Employee ID<input className="input" required maxLength={50} value={input.employeeNumber} onChange={event => setInput({ ...input, employeeNumber: event.target.value })} /></label>
      <label className="field">Full name<input className="input" required maxLength={200} value={input.fullName} onChange={event => setInput({ ...input, fullName: event.target.value })} /></label>
      <label className="field">Department<select className="input" required value={input.departmentId} onChange={event => setInput({ ...input, departmentId: event.target.value })}><option value="">Select department</option>{departments.data?.map(department => <option key={department.id} value={department.id}>{department.name}</option>)}</select></label>
      <label className="field">Enrollment date<input className="input" required type="date" value={input.enrollmentDate} onChange={event => setInput({ ...input, enrollmentDate: event.target.value })} /></label>
      <label className="field">Job title<input className="input" maxLength={200} value={input.jobTitle ?? ""} onChange={event => setInput({ ...input, jobTitle: event.target.value })} /></label>
      <label className="field">Notes<textarea className="input" maxLength={2000} value={input.notes ?? ""} onChange={event => setInput({ ...input, notes: event.target.value })} /></label>
    </fieldset><div className="flex gap-3"><button className="btn" disabled={save.isPending || departments.isPending}>Save Employee</button><button className="btn secondary" type="button" onClick={onClose}>Cancel</button></div>
  </form>
}
