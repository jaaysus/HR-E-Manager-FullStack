import { useAuth } from "../app/AuthProvider"

export default function Header({
  title,
  subtitle,
  businessDate,
}: {
  title: string
  subtitle?: string
  businessDate?: string
}) {
  const { user, logout } = useAuth()
  return (
    <div
      className="flex flex-wrap items-center justify-between gap-4 px-8 py-4"
      style={{ background: "#ffffff", borderBottom: "1px solid #e2e8f0" }}
    >
      <div>
        <h1
          style={{
            fontFamily: "var(--font-display)",
            fontSize: 20,
            fontWeight: 700,
            color: "#0f172a",
          }}
        >
          {title}
        </h1>
        {subtitle && (
          <p style={{ fontSize: 13, color: "#64748b", marginTop: 2 }}>
            {subtitle}
          </p>
        )}
      </div>
      <div className="ml-auto flex min-w-0 items-center gap-3">
        <div className="hidden xl:block" style={{ fontSize: 12, color: "#94a3b8" }}>
          {new Intl.DateTimeFormat("en-GB", {
            timeZone: "UTC",
            weekday: "short",
            day: "numeric",
            month: "short",
            year: "numeric",
          }).format(businessDate ? new Date(`${businessDate}T12:00:00Z`) : new Date())}
        </div>
        <div
          className="flex shrink-0 items-center justify-center rounded-full text-white text-sm font-semibold"
          style={{
            width: 34,
            height: 34,
            background: "var(--brand-primary, #2563eb)",
          }}
        >
          {user?.fullName
            .split(" ")
            .map((part) => part[0])
            .slice(0, 2)
            .join("")}
        </div>
        <div className="min-w-0 max-w-72 break-words">
          <div className="text-sm font-semibold text-slate-900">
            {user?.fullName}
          </div>
          <div className="text-xs text-slate-500">
            {user?.roles
              .map((role) =>
                role === "HrAdministrator"
                  ? "HR Administrator"
                  : role === "InventoryManager"
                    ? "Inventory Manager"
                    : role === "ClothingManager"
                      ? "Clothing Manager"
                      : role === "ClothingViewer"
                        ? "Clothing Viewer"
                        : "HR Viewer",
              )
              .join(", ")}
          </div>
        </div>
        <button
          type="button"
          aria-label="Sign out"
          title="Sign out"
          className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg text-slate-500 transition-colors hover:bg-slate-100 hover:text-slate-900 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-600"
          onClick={() => void logout().catch(() => {})}
        >
          <svg
            aria-hidden="true"
            width="20"
            height="20"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            strokeLinecap="round"
            strokeLinejoin="round"
          >
            <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4" />
            <path d="m16 17 5-5-5-5M21 12H9" />
          </svg>
        </button>
      </div>
    </div>
  )
}
