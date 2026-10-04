import type { Screen } from "../app/navigation"
import { navItems } from "../app/navigation"
import icons from "../components/Icons"
import BrandLogo from "../components/BrandLogo"
import { branding } from "../app/branding"
import { useAuth } from "../app/AuthProvider"

export default function Sidebar({
  current,
  onNav,
  unreadCount,
  activeRequestCount,
}: {
  current: Screen
  onNav: (s: Screen) => void
  unreadCount: number
  activeRequestCount: number
}) {
  const { can } = useAuth()
  return (
    <aside
      className="flex shrink-0 flex-col h-full overflow-hidden"
      style={{
        width: 220,
        background: "var(--brand-sidebar, #1e293b)",
      }}
    >
      {/* Application identity */}
      <div
        className="shrink-0 px-6 py-5 border-b"
        style={{ borderColor: "#334155" }}
      >
        <div className="flex items-center gap-2.5">
          <div>
            <BrandLogo light compact />
            <div
              style={{
                fontSize: 10,
                color: "#94a3b8",
                letterSpacing: "0.08em",
                marginTop: 4,
              }}
            >
              {branding.applicationName.toUpperCase()}
            </div>
          </div>
        </div>
      </div>

      {/* Nav */}
      <nav
        aria-label="Main navigation"
        className="flex-1 min-h-0 overflow-y-auto px-3 py-4 flex flex-col gap-0.5"
      >
        {navItems
          .filter((item) => !item.permission || can(item.permission))
          .sort(
            (left, right) =>
              ["Workspace", "Employees", "Clothing tracking"].indexOf(
                left.module,
              ) -
              ["Workspace", "Employees", "Clothing tracking"].indexOf(
                right.module,
              ),
          )
          .map((item, index, visible) => {
            const active = current === item.id
            const count =
              item.id === "notifications"
                ? unreadCount
                : item.id === "requests"
                  ? activeRequestCount
                  : 0
            return (
              <div key={item.id}>
                {(index === 0 || visible[index - 1].module !== item.module) && (
                  <div className="px-3 pb-2 pt-4 text-xs font-semibold uppercase tracking-wide text-slate-500">
                    {item.module}
                  </div>
                )}
                <button
                  key={item.id}
                  aria-label={item.label}
                  aria-current={active ? "page" : undefined}
                  title={
                    item.id === "requests" && count > 0
                      ? `${count} active request${count === 1 ? "" : "s"}`
                      : undefined
                  }
                  onClick={() => onNav(item.id)}
                  className="flex items-center gap-3 px-3 py-2.5 rounded-lg w-full text-left transition-all relative"
                  style={{
                    background: active
                      ? "var(--brand-primary, #2563eb)"
                      : "transparent",
                    color: active ? "#ffffff" : "#94a3b8",
                    fontSize: 13.5,
                    fontWeight: active ? 600 : 400,
                  }}
                  onMouseEnter={(e) => {
                    if (!active) e.currentTarget.style.background = "#334155"
                  }}
                  onMouseLeave={(e) => {
                    if (!active)
                      e.currentTarget.style.background = "transparent"
                  }}
                >
                  {icons[item.icon]}
                  <span style={{ fontFamily: "Inter, sans-serif" }}>
                    {item.label}
                  </span>
                  {count > 0 && (
                    <span
                      className="ml-auto flex items-center justify-center rounded-full text-white text-xs font-bold"
                      style={{
                        background: "#ef4444",
                        minWidth: 18,
                        height: 18,
                        fontSize: 10,
                        padding: "0 4px",
                      }}
                    >
                      {count}
                    </span>
                  )}
                </button>
              </div>
            )
          })}
      </nav>

    </aside>
  )
}
