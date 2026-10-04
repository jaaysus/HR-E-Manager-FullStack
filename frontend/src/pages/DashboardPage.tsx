import { useProgram } from "../app/ProgramContext"

import { useAuth } from "../app/AuthProvider"

import { notificationDestination } from "../utils/notificationNavigation"

import type { Screen } from "../app/navigation"

import type { Employee, Notification, CoatStatus } from "../types/program"

import {
  isEligible,
  getEmployeeRequestStatus,
  STATUS_META,
} from "../utils/program"

function KpiCard({
  label,

  value,

  sub,

  color,

  icon,
}: {
  label: string

  value: string | number

  sub?: string

  color: string

  icon: React.ReactNode
}) {
  return (
    <div
      className="rounded-xl p-5 flex flex-col gap-3"
      style={{ background: "#ffffff", border: "1px solid #e2e8f0" }}
    >
      <div className="flex items-start justify-between">
        <div
          className="flex items-center justify-center rounded-lg"
          style={{ width: 40, height: 40, background: color + "18" }}
        >
          <span style={{ color }}>{icon}</span>
        </div>
      </div>
      <div>
        <div
          style={{
            fontFamily: "Outfit, sans-serif",

            fontSize: 28,

            fontWeight: 700,

            color: "#0f172a",

            lineHeight: 1,
          }}
        >
          {value}
        </div>
        <div style={{ fontSize: 13, color: "#64748b", marginTop: 4 }}>
          {label}
        </div>
        {sub && (
          <div
            style={{
              fontSize: 11,

              color: color,

              marginTop: 3,

              fontWeight: 500,
            }}
          >
            {sub}
          </div>
        )}
      </div>
    </div>
  )
}

// ─── Mini Bar Chart ───────────────────────────────────────────────────────────

function MiniBarChart() {
  const { movements, businessDate } = useProgram()

  const dates = Array.from({ length: 6 }, (_, i) => {
    const d = new Date(businessDate + "T12:00:00Z")

    d.setUTCDate(1)

    d.setUTCMonth(d.getUTCMonth() - 5 + i)

    return d
  })

  const months = dates.map((d) =>
    new Intl.DateTimeFormat("en", { month: "short", timeZone: "UTC" }).format(
      d,
    ),
  )

  const values = dates.map(
    (d) =>
      movements.filter(
        (m) =>
          m.type === "allocation" &&
          m.date.startsWith(d.toISOString().slice(0, 7)),
      ).length,
  )

  const max = Math.max(1, ...values)

  return (
    <div
      style={{
        background: "#ffffff",

        border: "1px solid #e2e8f0",

        borderRadius: 12,

        padding: 20,
      }}
    >
      <div
        style={{
          fontFamily: "Outfit, sans-serif",

          fontWeight: 600,

          fontSize: 14,

          color: "#0f172a",

          marginBottom: 4,
        }}
      >
        Coats Issued per Month
      </div>
      <div style={{ fontSize: 12, color: "#94a3b8", marginBottom: 16 }}>
        {months[0]} – {months[5]} {dates[5].getUTCFullYear()}
      </div>
      <div className="flex items-end gap-2" style={{ height: 80 }}>
        {months.map((m, i) => (
          <div key={m} className="flex flex-col items-center gap-1 flex-1">
            <div
              className="w-full rounded-sm transition-all"
              style={{
                height: Math.max(4, (values[i] / max) * 70),

                background: i === months.length - 1 ? "#2563eb" : "#bfdbfe",

                borderRadius: 3,
              }}
            />
            <span style={{ fontSize: 10, color: "#94a3b8" }}>{m}</span>
          </div>
        ))}
      </div>
    </div>
  )
}

export default function DashboardPage({
  employees,

  notifications,

  stock,

  onNav,
}: {
  employees: Employee[]

  notifications: Notification[]

  stock: number

  onNav: (s: Screen) => void
}) {
  const eligible = employees.filter((e) => isEligible(e)).length

  const pending = employees.filter(
    (e) => getEmployeeRequestStatus(e) === "requested",
  ).length

  const recent = notifications.slice(0, 4)

  const { can, lowStock } = useProgram()

  const { can: canAccess } = useAuth()

  return (
    <div className="page flex flex-col gap-6">
      {/* KPI grid */}
      <div className="grid grid-cols-4 gap-4">
        <KpiCard
          label="Total Employees"
          value={employees.length}
          sub="Active this month"
          color="#2563eb"
          icon={
            <svg
              width="20"
              height="20"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
              viewBox="0 0 24 24"
            >
              <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2" />
              <circle cx="9" cy="7" r="4" />
              <path d="M23 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75" />
            </svg>
          }
        />
        <KpiCard
          label="Employees in Request Cycle"
          value={eligible}
          sub="Automatic eligibility schedule"
          color="#7c3aed"
          icon={
            <svg
              width="20"
              height="20"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
              viewBox="0 0 24 24"
            >
              <circle cx="12" cy="12" r="10" />
              <polyline points="12 6 12 12 16 14" />
            </svg>
          }
        />
        <KpiCard
          label="Auto-Created Requests"
          value={pending}
          sub="Awaiting provision"
          color="#d97706"
          icon={
            <svg
              width="20"
              height="20"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
              viewBox="0 0 24 24"
            >
              <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
              <polyline points="14 2 14 8 20 8" />
            </svg>
          }
        />
        <KpiCard
          label="Available Inventory"
          value={stock}
          sub={lowStock ? "⚠ Low stock alert" : "Units in stock"}
          color={lowStock ? "#dc2626" : "#16a34a"}
          icon={
            <svg
              width="20"
              height="20"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
              viewBox="0 0 24 24"
            >
              <path d="M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z" />
            </svg>
          }
        />
      </div>

      <div className="grid gap-4" style={{ gridTemplateColumns: "1fr 340px" }}>
        {/* Recent notifications */}
        <div
          style={{
            background: "#ffffff",

            border: "1px solid #e2e8f0",

            borderRadius: 12,

            padding: 20,
          }}
        >
          <div className="flex items-center justify-between mb-4">
            <h3
              style={{
                fontFamily: "Outfit, sans-serif",

                fontWeight: 600,

                fontSize: 14,

                color: "#0f172a",
              }}
            >
              Recent Activity
            </h3>
            <button
              onClick={() => onNav("notifications")}
              style={{ fontSize: 12, color: "#2563eb", fontWeight: 500 }}
            >
              View all →
            </button>
          </div>
          <div className="flex flex-col gap-2">
            {recent.map((n) => (
              <button
                key={n.id}
                type="button"
                disabled={!notificationDestination(n, canAccess)}
                onClick={() => {
                  const destination = notificationDestination(n, canAccess)

                  if (destination) onNav(destination)
                }}
                className="flex items-start gap-3 p-3 rounded-lg text-left enabled:cursor-pointer enabled:hover:ring-1 enabled:hover:ring-blue-300 focus-visible:outline-2 focus-visible:outline-blue-600"
                style={{ background: n.read ? "#f8fafc" : "#eff6ff" }}
              >
                <div
                  className="rounded-full flex-shrink-0 flex items-center justify-center mt-0.5"
                  style={{
                    width: 28,

                    height: 28,

                    background:
                      n.type === "warning"
                        ? "#fef3c7"
                        : n.type === "success"
                          ? "#dcfce7"
                          : "#dbeafe",
                  }}
                >
                  <div
                    style={{
                      width: 8,

                      height: 8,

                      borderRadius: "50%",

                      background:
                        n.type === "warning"
                          ? "#d97706"
                          : n.type === "success"
                            ? "#16a34a"
                            : "#2563eb",
                    }}
                  />
                </div>
                <div className="flex-1 min-w-0">
                  <p
                    style={{ fontSize: 13, color: "#1e293b", lineHeight: 1.5 }}
                  >
                    {n.message}
                  </p>
                  <p style={{ fontSize: 11, color: "#94a3b8", marginTop: 2 }}>
                    {n.date}
                  </p>
                </div>
                {!n.read && (
                  <div
                    style={{
                      width: 7,

                      height: 7,

                      borderRadius: "50%",

                      background: "#2563eb",

                      flexShrink: 0,

                      marginTop: 4,
                    }}
                  />
                )}
              </button>
            ))}
          </div>
        </div>

        {/* Right column: chart + quick actions */}
        <div className="flex flex-col gap-4">
          <MiniBarChart />

          <div
            style={{
              background: "#ffffff",

              border: "1px solid #e2e8f0",

              borderRadius: 12,

              padding: 20,
            }}
          >
            <h3
              style={{
                fontFamily: "Outfit, sans-serif",

                fontWeight: 600,

                fontSize: 14,

                color: "#0f172a",

                marginBottom: 12,
              }}
            >
              Quick Actions
            </h3>
            <div className="flex flex-col gap-2">
              {[
                {
                  label: "Add Employee",

                  screen: "add-employee",

                  color: "#2563eb",
                },

                {
                  label: "Import from Excel",

                  screen: "import",

                  color: "#7c3aed",
                },

                {
                  label: "Add Stock Arrival",

                  screen: "inventory",

                  color: "#16a34a",
                },

                {
                  label: "View Coat Requests",

                  screen: "requests",

                  color: "#d97706",
                },
              ]

                .filter((a) => can(a.screen))

                .map((a) => (
                  <button
                    key={a.label}
                    onClick={() => onNav(a.screen as Screen)}
                    className="w-full text-left px-3.5 py-2.5 rounded-lg font-medium transition-colors"
                    style={{
                      fontSize: 13,

                      color: a.color,

                      background: a.color + "10",

                      border: `1px solid ${a.color}20`,
                    }}
                    onMouseEnter={(e) =>
                      (e.currentTarget.style.background = a.color + "1e")
                    }
                    onMouseLeave={(e) =>
                      (e.currentTarget.style.background = a.color + "10")
                    }
                  >
                    {a.label}
                  </button>
                ))}
            </div>
          </div>
        </div>
      </div>

      {/* Status breakdown */}
      <div
        style={{
          background: "#ffffff",

          border: "1px solid #e2e8f0",

          borderRadius: 12,

          padding: 20,
        }}
      >
        <h3
          style={{
            fontFamily: "Outfit, sans-serif",

            fontWeight: 600,

            fontSize: 14,

            color: "#0f172a",

            marginBottom: 16,
          }}
        >
          Employee Coat Status Overview
        </h3>
        <div className="flex gap-8">
          {([
            "none",

            "requested",

            "provided",

            "out_of_stock",

            ...(employees.some((e) => e.coatStatus === "cancelled")
              ? ["cancelled"]
              : []),
          ] as CoatStatus[]).map((s) => {
            const count = employees.filter((e) => e.coatStatus === s).length

            const meta = STATUS_META[s]

            const pct = employees.length
              ? Math.round((count / employees.length) * 100)
              : 0

            return (
              <div key={s} className="flex flex-col gap-1 flex-1">
                <div className="flex items-center justify-between mb-1">
                  <span style={{ fontSize: 12, color: "#64748b" }}>
                    {meta.label}
                  </span>
                  <span
                    style={{ fontSize: 12, fontWeight: 600, color: "#0f172a" }}
                  >
                    {count}
                  </span>
                </div>
                <div
                  style={{ height: 6, borderRadius: 3, background: "#f1f5f9" }}
                >
                  <div
                    style={{
                      height: "100%",

                      borderRadius: 3,

                      width: `${pct}%`,

                      background:
                        s === "provided"
                          ? "#16a34a"
                          : s === "requested"
                            ? "#d97706"
                            : s === "out_of_stock"
                              ? "#dc2626"
                              : "#94a3b8",
                    }}
                  />
                </div>
                <span style={{ fontSize: 11, color: "#94a3b8" }}>{pct}%</span>
              </div>
            )
          })}
        </div>
      </div>
    </div>
  )
}

// ─── Employees ────────────────────────────────────────────────────────────────
