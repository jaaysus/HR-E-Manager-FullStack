import { useAuth } from "../app/AuthProvider"
import type { Screen } from "../app/navigation"

export default function WorkspacePage({
  onNav,
}: {
  onNav: (screen: Screen) => void
}) {
  const { user, can } = useAuth()
  const features = [
    {
      screen: "employees" as Screen,
      title: "Employees",
      description: "Manage employee records, departments, and enrollment.",
      permission: "employees.read" as const,
    },
    {
      screen: "dashboard" as Screen,
      title: "Clothing tracking",
      description:
        "Track clothing, eligibility, stock arrivals, and provision.",
      permission: "coats.dashboard.read" as const,
    },
    {
      screen: "users" as Screen,
      title: "Users & access",
      description: "Create accounts and assign HR and clothing roles.",
      permission: "platform.users.manage" as const,
    },
    {
      screen: "settings" as Screen,
      title: "Organization settings",
      description:
        "Manage the business time zone and departments.",
      permission: "platform.settings.read" as const,
    },
  ]
  return (
    <div className="page space-y-6">
      <div>
        <h2 className="text-xl font-semibold">Welcome, {user?.fullName}</h2>
        <p className="mt-2 text-sm text-slate-500">
          Your assigned roles determine which modules you can open.
        </p>
      </div>
      <div className="grid gap-5 md:grid-cols-2">
        {features
          .filter((feature) => can(feature.permission))
          .map((feature) => (
            <button
              key={feature.screen}
              onClick={() => onNav(feature.screen)}
              className="panel text-left hover:border-blue-400 transition-colors"
            >
              <h3 className="font-semibold text-lg">{feature.title}</h3>
              <p className="mt-2 text-sm text-slate-500">
                {feature.description}
              </p>
              <span className="mt-4 inline-block text-sm font-semibold text-blue-600">
                Open module →
              </span>
            </button>
          ))}
      </div>
      {!can("coats.dashboard.read") && (
        <p className="text-sm text-slate-500">
          For clothing tracking access, ask an HR administrator to assign a
          Clothing Manager or Clothing Viewer role.
        </p>
      )}
    </div>
  )
}
