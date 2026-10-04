import { useProgram } from "../app/ProgramContext"

import type { Notification } from "../types/program"
import type { Screen } from "../app/navigation"
import { useAuth } from "../app/AuthProvider"
import { notificationDestination } from "../utils/notificationNavigation"

export default function NotificationsPage({
  notifications,

  onMarkRead,

  onMarkAllRead,
  onNav,
}: {
  notifications: Notification[]

  onMarkRead: (id: string) => void

  onMarkAllRead: () => void
  onNav: (screen: Screen) => void
}) {
  const { busy } = useProgram()
  const { can } = useAuth()

  const unread = notifications.filter((n) => !n.read).length

  return (
    <div className="page page-notifications flex flex-col gap-5">
      <div className="flex items-center justify-between">
        <div>
          <p style={{ fontSize: 13, color: "#64748b" }}>
            {unread > 0
              ? `${unread} unread notification${unread > 1 ? "s" : ""}`
              : "All caught up!"}
          </p>
        </div>
        {unread > 0 && (
          <button
            disabled={busy}
            onClick={onMarkAllRead}
            style={{ fontSize: 13, color: "#2563eb", fontWeight: 500 }}
          >
            Mark all as read
          </button>
        )}
      </div>

      <div className="flex flex-col gap-2">
        {notifications.map((n) => (
          <div
            key={n.id}
            className="flex items-start gap-4 p-4 rounded-xl"
            style={{
              background: n.read ? "#fff" : "#eff6ff",

              border: n.read ? "1px solid #f1f5f9" : "1px solid #bfdbfe",
            }}
          >
            <div
              className="flex items-center justify-center rounded-full flex-shrink-0"
              style={{
                width: 38,
                height: 38,
                marginTop: 2,

                background:
                  n.type === "warning"
                    ? "#fef3c7"
                    : n.type === "success"
                      ? "#dcfce7"
                      : "#dbeafe",
              }}
            >
              {n.type === "warning" ? (
                <svg
                  width="18"
                  height="18"
                  fill="none"
                  stroke="#d97706"
                  strokeWidth="2"
                  viewBox="0 0 24 24"
                >
                  <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z" />
                  <line x1="12" y1="9" x2="12" y2="13" />
                  <line x1="12" y1="17" x2="12.01" y2="17" />
                </svg>
              ) : n.type === "success" ? (
                <svg
                  width="18"
                  height="18"
                  fill="none"
                  stroke="#16a34a"
                  strokeWidth="2"
                  viewBox="0 0 24 24"
                >
                  <polyline points="20 6 9 17 4 12" />
                </svg>
              ) : (
                <svg
                  width="18"
                  height="18"
                  fill="none"
                  stroke="#2563eb"
                  strokeWidth="2"
                  viewBox="0 0 24 24"
                >
                  <circle cx="12" cy="12" r="10" />
                  <line x1="12" y1="8" x2="12" y2="12" />
                  <line x1="12" y1="16" x2="12.01" y2="16" />
                </svg>
              )}
            </div>
            <button
              type="button"
              className="flex-1 text-left rounded-lg enabled:cursor-pointer enabled:hover:underline focus-visible:outline-2 focus-visible:outline-blue-600"
              disabled={!notificationDestination(n, can)}
              onClick={() => {
                const destination = notificationDestination(n, can)
                if (destination) onNav(destination)
              }}
            >
              <p
                style={{
                  fontSize: 14,
                  color: "#1e293b",
                  lineHeight: 1.5,
                  fontWeight: n.read ? 400 : 500,
                }}
              >
                {n.message}
              </p>
              <p style={{ fontSize: 12, color: "#94a3b8", marginTop: 4 }}>
                {n.date}
              </p>
            </button>
            {!n.read && (
              <button
                disabled={busy}
                onClick={() => onMarkRead(n.id)}
                style={{
                  fontSize: 12,
                  color: "#2563eb",
                  fontWeight: 500,
                  flexShrink: 0,
                }}
              >
                Mark read
              </button>
            )}
          </div>
        ))}
      </div>
    </div>
  )
}

// ─── Toggle ───────────────────────────────────────────────────────────────────
