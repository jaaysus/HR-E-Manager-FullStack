import { useState } from "react"
import { useQuery } from "@tanstack/react-query"
import { platformApi } from "../../services/platformApi"
import ApiState from "../../components/ApiState"

import { useProgram } from "../../app/ProgramContext"

import type { Employee } from "../../types/program"

export default function EmployeeForm({
  employee,

  onSave,

  onCancel,
}: {
  employee?: Employee

  onSave: (emp: Employee) => void

  onCancel: () => void
}) {
  const { busy, employees } = useProgram()

  const [form, setForm] = useState<Employee>(
    employee ?? {
      id:
        "EMP-" +
        String(
          Math.max(
            0,
            ...employees.map((e) => Number(e.id.replace(/^EMP-/, "")) || 0),
          ) + 1,
        ).padStart(3, "0"),

      name: "",

      department: "",

      jobTitle: "",

      enrollmentDate: "",

      coatStatus: "none",

      notes: "",
    },
  )

  function set(k: keyof Employee, v: string) {
    setForm((f) => ({ ...f, [k]: v }))
  }

  const departments = useQuery({ queryKey: ["platform", "departments"], queryFn: ({ signal }) => platformApi.departments(signal) })
  const depts = departments.data?.filter(department => department.isActive).map(department => department.name) ?? []

  return (
    <div className="page page-form">
      <div
        style={{
          background: "#fff",
          border: "1px solid #e2e8f0",
          borderRadius: 12,
          padding: 28,
        }}
      >
        <h2
          style={{
            fontFamily: "Outfit, sans-serif",
            fontWeight: 700,
            fontSize: 18,
            color: "#0f172a",
            marginBottom: 6,
          }}
        >
          {employee ? "Edit Employee" : "Add New Employee"}
        </h2>
        <p style={{ fontSize: 13, color: "#64748b", marginBottom: 24 }}>
          {employee
            ? `Editing record for ${employee.name}`
            : "Fill in the employee details below."}
        </p>

        <form
          onSubmit={(e) => {
            e.preventDefault()
            onSave(form)
          }}
        >
          <ApiState pending={departments.isPending} error={departments.error} retry={() => void departments.refetch()} />
          <fieldset disabled={busy || departments.isPending || !!departments.error}>
            <div className="grid grid-cols-2 gap-4">
              {[
                {
                  label: "Employee ID",
                  key: "id",
                  type: "text",
                  placeholder: "EMP-001",
                },

                {
                  label: "Full Name",
                  key: "name",
                  type: "text",
                  placeholder: "Jane Smith",
                },

                {
                  label: "Job Title",
                  key: "jobTitle",
                  type: "text",
                  placeholder: "Line Operator",
                },

                {
                  label: "Enrollment Date",
                  key: "enrollmentDate",
                  type: "date",
                  placeholder: "",
                },
              ].map((f) => (
                <div key={f.key}>
                  <label
                    style={{
                      fontSize: 12.5,
                      fontWeight: 500,
                      color: "#374151",
                      display: "block",
                      marginBottom: 5,
                    }}
                  >
                    {f.label}
                  </label>
                  <input
                    required={f.key !== "jobTitle"}
                    aria-label={f.label}
                    type={f.type}
                    value={(form as any)[f.key]}
                    onChange={(e) =>
                      set(f.key as keyof Employee, e.target.value)
                    }
                    placeholder={f.placeholder}
                    className="w-full rounded-lg px-3 py-2.5"
                    style={{
                      border: "1px solid #d1d5db",
                      fontSize: 13.5,
                      outline: "none",
                      color: "#0f172a",
                    }}
                  />
                </div>
              ))}

              <div>
                <label
                  style={{
                    fontSize: 12.5,
                    fontWeight: 500,
                    color: "#374151",
                    display: "block",
                    marginBottom: 5,
                  }}
                >
                  Department
                </label>
                <select
                  required
                  aria-label="Department"
                  value={form.department}
                  onChange={(e) => set("department", e.target.value)}
                  className="w-full rounded-lg px-3 py-2.5"
                  style={{
                    border: "1px solid #d1d5db",
                    fontSize: 13.5,
                    outline: "none",
                    color: "#0f172a",
                    background: "#fff",
                  }}
                >
                  <option value="">Select department…</option>
                  {depts.map((d) => (
                    <option key={d}>{d}</option>
                  ))}
                </select>
              </div>

              <div>
                <label
                  style={{
                    fontSize: 12.5,
                    fontWeight: 500,
                    color: "#374151",
                    display: "block",
                    marginBottom: 5,
                  }}
                >
                  Request schedule
                </label>
                <div
                  className="rounded-lg px-3 py-2.5"
                  style={{
                    border: "1px solid #d1d5db",
                    fontSize: 13.5,
                    color: "#64748b",
                    background: "#f8fafc",
                  }}
                >
                  Created by the eligibility policy
                </div>
              </div>

              <div className="col-span-2">
                <label
                  style={{
                    fontSize: 12.5,
                    fontWeight: 500,
                    color: "#374151",
                    display: "block",
                    marginBottom: 5,
                  }}
                >
                  Notes
                </label>
                <textarea
                  value={form.notes}
                  onChange={(e) => set("notes", e.target.value)}
                  placeholder="e.g. Preferred size: L"
                  rows={3}
                  className="w-full rounded-lg px-3 py-2.5"
                  style={{
                    border: "1px solid #d1d5db",
                    fontSize: 13.5,
                    outline: "none",
                    color: "#0f172a",
                    resize: "vertical",
                  }}
                />
              </div>
            </div>

            <div className="flex gap-3 mt-6">
              <button
                type="submit"
                className="px-6 py-2.5 rounded-lg text-white font-semibold"
                style={{
                  background: "#2563eb",
                  fontSize: 13.5,
                  fontFamily: "Outfit, sans-serif",
                }}
              >
                {busy ? "Saving…" : employee ? "Save Changes" : "Add Employee"}
              </button>
              <button
                type="button"
                onClick={onCancel}
                className="px-6 py-2.5 rounded-lg font-medium"
                style={{
                  background: "#f1f5f9",
                  fontSize: 13.5,
                  color: "#374151",
                }}
              >
                Cancel
              </button>
            </div>
          </fieldset>
        </form>
      </div>
    </div>
  )
}
