import { useState, useMemo } from "react"

import { useProgram } from "../../app/ProgramContext"

import type { Employee, StockMovement, CoatStatus } from "../../types/program"

import { getRequestDisplayStatus } from "../../utils/program"

import Badge from "../../components/StatusBadge"

export default function EmployeesPage({
  employees,

  movements,

  onAdd,

  onEdit,
}: {
  employees: Employee[]

  movements: StockMovement[]

  onAdd: () => void

  onEdit: (emp: Employee) => void
}) {
  const { can, businessDate, timeZoneId } = useProgram()

  const [search, setSearch] = useState("")

  const [deptFilter, setDeptFilter] = useState("All")

  const [statusFilter, setStatusFilter] = useState<"All" | CoatStatus>("All")

  const depts = [
    "All",

    ...Array.from(new Set(employees.map((e) => e.department))).sort(),
  ]

  const filtered = useMemo(() => {
    return employees.filter((e) => {
      const matchSearch =
        search === "" ||
        e.name.toLowerCase().includes(search.toLowerCase()) ||
        e.id.toLowerCase().includes(search.toLowerCase()) ||
        e.department.toLowerCase().includes(search.toLowerCase())

      const matchDept = deptFilter === "All" || e.department === deptFilter

      const matchStatus =
        statusFilter === "All" ||
        getRequestDisplayStatus(e, movements) === statusFilter

      return matchSearch && matchDept && matchStatus
    })
  }, [employees, movements, search, deptFilter, statusFilter])

  return (
    <div className="page flex flex-col gap-5">
      {/* Toolbar */}
      <div className="flex items-center gap-3">
        <div className="relative flex-1" style={{ maxWidth: 320 }}>
          <svg
            className="absolute left-3 top-1/2 -translate-y-1/2"
            width="15"
            height="15"
            fill="none"
            stroke="#94a3b8"
            strokeWidth="2"
            viewBox="0 0 24 24"
          >
            <circle cx="11" cy="11" r="8" />
            <path d="M21 21l-4.35-4.35" />
          </svg>
          <input
            type="text"
            placeholder="Search employees…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="w-full rounded-lg pl-9 pr-3.5 py-2"
            style={{
              border: "1px solid #e2e8f0",

              fontSize: 13.5,

              outline: "none",

              color: "#0f172a",

              background: "#fff",
            }}
          />
        </div>

        <select
          value={deptFilter}
          onChange={(e) => setDeptFilter(e.target.value)}
          className="rounded-lg px-3 py-2"
          style={{
            border: "1px solid #e2e8f0",

            fontSize: 13,

            color: "#374151",

            background: "#fff",

            outline: "none",
          }}
        >
          {depts.map((d) => (
            <option key={d}>{d}</option>
          ))}
        </select>

        <select
          value={statusFilter}
          onChange={(e) => setStatusFilter(e.target.value as any)}
          className="rounded-lg px-3 py-2"
          style={{
            border: "1px solid #e2e8f0",

            fontSize: 13,

            color: "#374151",

            background: "#fff",

            outline: "none",
          }}
        >
          <option value="All">All Statuses</option>
          <option value="none">Not Eligible</option>
          <option value="requested">Auto Request</option>
          <option value="provided">Cycle Completed</option>
          <option value="out_of_stock">Out of Stock</option>
          {employees.some((e) => e.coatStatus === "cancelled") && (
            <option value="cancelled">Cancelled</option>
          )}
        </select>

        <div className="flex-1" />
        <button
          disabled={!can("add-employee")}
          onClick={onAdd}
          className="flex items-center gap-2 px-4 py-2 rounded-lg text-white font-semibold"
          style={{ background: "#2563eb", fontSize: 13 }}
        >
          <svg
            width="14"
            height="14"
            fill="none"
            stroke="currentColor"
            strokeWidth="2.5"
            viewBox="0 0 24 24"
          >
            <path d="M12 5v14M5 12h14" />
          </svg>
          Add Employee
        </button>
      </div>

      {/* Table */}
      <div
        style={{
          background: "#ffffff",

          border: "1px solid #e2e8f0",

          borderRadius: 12,

          overflow: "hidden",
        }}
      >
        <table style={{ width: "100%", borderCollapse: "collapse" }}>
          <thead>
            <tr
              style={{
                borderBottom: "1px solid #e2e8f0",

                background: "#f8fafc",
              }}
            >
              {[
                "Employee ID",

                "Full Name",

                "Department",

                "Enrollment Date",

                "Months",

                "Coat Status",

                "Eligibility",

                "",
              ].map((h) => (
                <th
                  key={h}
                  style={{
                    padding: "10px 16px",

                    textAlign: "left",

                    fontSize: 11.5,

                    fontWeight: 600,

                    color: "#64748b",

                    letterSpacing: "0.04em",

                    textTransform: "uppercase",

                    whiteSpace: "nowrap",
                  }}
                >
                  {h}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {filtered.map((emp, i) => {
              const months = emp.monthsEnrolled ?? 0

              const eligible = (emp.currentCycle ?? 0) > 0

              return (
                <tr
                  key={emp.id}
                  style={{
                    borderBottom:
                      i < filtered.length - 1 ? "1px solid #f1f5f9" : "none",
                  }}
                  onMouseEnter={(e) =>
                    (e.currentTarget.style.background = "#f8fafc")
                  }
                  onMouseLeave={(e) =>
                    (e.currentTarget.style.background = "transparent")
                  }
                >
                  <td
                    style={{
                      padding: "12px 16px",

                      fontSize: 12.5,

                      color: "#64748b",

                      fontFamily: "monospace",
                    }}
                  >
                    {emp.id}
                  </td>
                  <td
                    style={{
                      padding: "12px 16px",

                      fontSize: 13.5,

                      fontWeight: 500,

                      color: "#0f172a",
                    }}
                  >
                    {emp.name}
                  </td>
                  <td
                    style={{
                      padding: "12px 16px",

                      fontSize: 13,

                      color: "#374151",
                    }}
                  >
                    {emp.department}
                  </td>
                  <td
                    style={{
                      padding: "12px 16px",

                      fontSize: 13,

                      color: "#374151",
                    }}
                  >
                    {emp.enrollmentDate}
                  </td>
                  <td
                    style={{
                      padding: "12px 16px",

                      fontSize: 13,

                      color: eligible ? "#16a34a" : "#374151",

                      fontWeight: eligible ? 600 : 400,
                    }}
                  >
                    {months}mo
                  </td>
                  <td style={{ padding: "12px 16px" }}>
                    <Badge status={getRequestDisplayStatus(emp, movements)} />
                  </td>
                  <td style={{ padding: "12px 16px" }}>
                    <span
                      style={{
                        fontSize: 12,

                        fontWeight: 500,

                        padding: "2px 8px",

                        borderRadius: 20,

                        background: eligible ? "#dcfce7" : "#f1f5f9",

                        color: eligible ? "#16a34a" : "#94a3b8",
                      }}
                    >
                      {eligible
                        ? `Cycle ${emp.currentCycle ?? 0} active`
                        : emp.enrollmentDate > businessDate
                          ? `Enrollment starts ${emp.enrollmentDate}`
                          : "Waiting for eligibility interval"}
                    </span>
                    {emp.nextEligibilityAtUtc && (
                      <p className="mt-2 text-xs text-slate-500">
                        Next eligibility:{" "}
                        {new Intl.DateTimeFormat(undefined, {
                          timeZone: timeZoneId,
                          dateStyle: "short",
                          timeStyle: "medium",
                        }).format(new Date(emp.nextEligibilityAtUtc))}
                      </p>
                    )}
                  </td>
                  <td style={{ padding: "12px 16px" }}>
                    <button
                      disabled={!can("add-employee")}
                      onClick={() => onEdit(emp)}
                      style={{
                        fontSize: 12.5,

                        color: "#2563eb",

                        fontWeight: 500,
                      }}
                    >
                      Edit
                    </button>
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
        <div
          style={{
            padding: "10px 16px",

            borderTop: "1px solid #f1f5f9",

            fontSize: 12,

            color: "#94a3b8",
          }}
        >
          Showing {filtered.length} of {employees.length} employees
        </div>
      </div>
    </div>
  )
}

// ─── Add/Edit Employee ────────────────────────────────────────────────────────
