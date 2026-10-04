import type icons from "../components/Icons"
import type { Permission } from "../types/api"
export type Screen = "home" | "dashboard" | "employees" | "add-employee" | "import" | "inventory" | "requests" | "notifications" | "settings" | "clothing-settings" | "users" | "account"
export const navItems: {
  id: Screen
  label: string
  icon: keyof typeof icons
  module: string
  permission?: Permission
}[] = [
  { id: "home", label: "HR workspace", icon: "dashboard", module: "Workspace" },
  {
    id: "account",
    label: "My account",
    icon: "employees",
    module: "Workspace",
  },
  {
    id: "dashboard",
    label: "Clothing overview",
    icon: "dashboard",
    module: "Clothing tracking",
    permission: "coats.dashboard.read",
  },
  {
    id: "employees",
    label: "Employees",
    icon: "employees",
    module: "Employees",
    permission: "employees.read",
  },
  {
    id: "import",
    label: "Import Excel",
    icon: "import",
    module: "Employees",
    permission: "employees.import",
  },
  {
    id: "inventory",
    label: "Inventory",
    icon: "inventory",
    module: "Clothing tracking",
    permission: "inventory.read",
  },
  {
    id: "requests",
    label: "Requests",
    icon: "requests",
    module: "Clothing tracking",
    permission: "coats.read",
  },
  {
    id: "clothing-settings",
    label: "Clothing settings",
    icon: "settings",
    module: "Clothing tracking",
    permission: "coats.read",
  },
  {
    id: "notifications",
    label: "Notifications",
    icon: "notifications",
    module: "Workspace",
    permission: "platform.notifications.read",
  },
  {
    id: "settings",
    label: "Organization settings",
    icon: "settings",
    module: "Workspace",
    permission: "platform.settings.read",
  },
  {
    id: "users",
    label: "Users & access",
    icon: "employees",
    module: "Workspace",
    permission: "platform.users.manage",
  },
]
export const screenPermission = (screen: Screen): Permission | undefined =>
  screen === "add-employee"
    ? "employees.manage"
    : navItems.find((item) => item.id === screen)?.permission
export const screenTitles: Record<Screen, {
  title: string
  subtitle?: string
}> = {
  home: {
    title: "HR workspace",
    subtitle: "Choose a feature to get started",
  },
  account: {
    title: "My account",
    subtitle: "Your profile, password, and personal notification preferences",
  },
  "clothing-settings": {
    title: "Clothing settings",
    subtitle: "Clothing alerts and configurable eligibility policy",
  },
  dashboard: {
    title: "Dashboard",
    subtitle: "Overview of your coat tracking program",
  },
  employees: {
    title: "Employees",
    subtitle: "Manage employee records and enrollment",
  },
  "add-employee": {
    title: "Add Employee",
    subtitle: "Create or update an employee record",
  },
  import: {
    title: "Import from Excel",
    subtitle: "Bulk-upload employees from a spreadsheet",
  },
  inventory: {
    title: "Inventory Management",
    subtitle: "Track coat stock and movements",
  },
  requests: {
    title: "Coat Requests",
    subtitle: "Requests created by the eligibility policy",
  },
  notifications: {
    title: "Notifications",
    subtitle: "Stay on top of eligibility and inventory alerts",
  },
  settings: {
    title: "Organization settings",
    subtitle: "Business time zone and departments",
  },
  users: {
    title: "Users & access",
    subtitle: "Manage accounts and role assignments",
  },
}
