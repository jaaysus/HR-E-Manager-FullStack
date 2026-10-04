import { useState } from "react"

import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query"

import { useAuth } from "./app/AuthProvider"

import { ProgramContext } from "./app/ProgramContext"

import { screenPermission, screenTitles, type Screen } from "./app/navigation"

import { programApi } from "./services/programApi"

import { employeesApi } from "./services/employeesApi"

import { platformApi } from "./services/platformApi"

import { api, json } from "./services/apiClient"

import type { Permission } from "./types/api"

import type { Employee, CoatTypeId } from "./types/program"

import Dashboard from "./pages/DashboardPage"

import EmployeeList from "./pages/employees/EmployeesPage"

import EmployeeForm from "./pages/employees/EmployeeForm"

import InventoryScreen from "./pages/InventoryPage"

import RequestsScreen from "./pages/RequestsPage"

import NotificationsScreen from "./pages/NotificationsPage"

import AppLayout from "./layouts/AppLayout"

import LoginPage from "./pages/LoginPage"

import ImportPage from "./pages/ImportPage"

import SettingsPage from "./pages/SettingsPage"

import UsersPage from "./pages/UsersPage"

import AccountPage from "./pages/AccountPage"

import ApiState from "./components/ApiState"

import WorkspacePage from "./pages/WorkspacePage"

import DirectoryPage from "./pages/employees/DirectoryPage"

import WorkspaceNotificationsPage from "./pages/WorkspaceNotificationsPage"

import ClothingSettingsPage from "./pages/ClothingSettingsPage"

export default function App() {
  const { user } = useAuth()

  return user ? <Workspace /> : <LoginPage />
}

function Workspace() {
  const { can } = useAuth()

  const cache = useQueryClient()

  const [screen, setScreen] = useState<Screen>("home")

  const hasClothing = can("coats.dashboard.read")

  const clothingScreen =
    hasClothing &&
    [
      "dashboard",

      "employees",

      "add-employee",

      "inventory",

      "requests",

      "notifications",
    ].includes(screen)

  const permitted = !screenPermission(screen) || can(screenPermission(screen)!)

  const [editing, setEditing] = useState<Employee>()

  const snapshot = useQuery({
    queryKey: ["program"],

    queryFn: ({ signal }) => programApi.snapshot(signal),

    refetchInterval: 10_000,

    enabled: clothingScreen && permitted,
  })

  const departments = useQuery({
    queryKey: ["platform", "departments"],

    queryFn: ({ signal }) => platformApi.departments(signal),

    enabled:
      can("platform.departments.read") &&
      ["employees", "add-employee"].includes(screen),
  })

  const activeRequests = useQuery({
    queryKey: ["clothing", "requests", "active-count"],
    queryFn: ({ signal }) =>
      api.request<{ activeCount: number }>("/coat-requests/active-count", {
        signal,
      }),
    enabled: can("coats.read"),
    refetchInterval: 10_000,
  })
  const unread = useQuery({
    queryKey: ["platform", "notifications", "unread"],

    queryFn: ({ signal }) => platformApi.unreadCount(signal),

    enabled: can("platform.notifications.read"),

    refetchInterval: 30_000,
  })

  const organization = useQuery({
    queryKey: ["platform", "settings"],

    queryFn: ({ signal }) => platformApi.settings(signal),

    enabled: can("platform.settings.read"),

    refetchInterval: 60_000,
  })

  const action = useMutation({
    mutationFn: (work: () => Promise<unknown>) => work(),

    onSuccess: () => cache.invalidateQueries(),

    onError: () => cache.invalidateQueries(),
  })

  async function perform(work: () => Promise<unknown>) {
    if (action.isPending) return false

    try {
      await action.mutateAsync(work)

      return true
    } catch {
      return false
    }
  }

  function navigate(next: Screen) {
    const permission = screenPermission(next)

    if (permission && !can(permission)) return

    action.reset()

    setEditing(undefined)

    setScreen(next)
  }

  function allowed(name: string) {
    const permission =
      name === "inventory-write"
        ? "inventory.manage"
        : name === "provide"
          ? "coats.provide"
          : screenPermission(name as Screen)

    return !permission || can(permission as Permission)
  }

  const data = hasClothing ? snapshot.data : undefined

  const employees: Employee[] =
    data?.employees.map((e) => ({
      ...e,

      recordId: e.id,

      id: e.employeeNumber,

      name: e.fullName,

      jobTitle: e.jobTitle ?? "",

      notes: e.notes ?? "",
    })) ?? []

  const stock =
    data?.items.reduce((sum, item) => sum + item.quantityOnHand, 0) ?? 0

  async function saveEmployee(employee: Employee) {
    const department = departments.data?.find(
      (d) => d.name === employee.department,
    )

    if (!department) {
      await perform(() =>
        Promise.reject(new Error("Choose a valid department.")),
      )

      return
    }

    const input = {
      employeeNumber: employee.id,

      fullName: employee.name,

      departmentId: department.id,

      enrollmentDate: employee.enrollmentDate,

      jobTitle: employee.jobTitle,

      notes: employee.notes,
    }

    const saved = data?.employees.find((e) => e.id === employee.recordId)

    const current = saved
      ? { ...saved, rowVersion: employee.rowVersion ?? saved.rowVersion }
      : undefined

    if (await perform(() => employeesApi.save(input, current)))
      navigate("employees")
  }

  async function receive(
    coatType: CoatTypeId,

    quantity: number,

    note: string,

    arrivalDate: string,
  ) {
    const item = data?.items.find((i) => i.sku === coatType)

    return perform(() =>
      item
        ? api.request(
            "/inventory/receipts",

            json("POST", {
              inventoryItemId: item.id,

              quantity,

              note,

              arrivalDate,
            }),
          )
        : Promise.reject(new Error("Coat variant unavailable.")),
    )
  }

  function provide(id: string, sku?: string) {
    const employee = employees.find((e) => e.id === id)

    void perform(() =>
      employee?.requestId
        ? api.request(
            `/coat-requests/${employee.requestId}/provide`,

            json("POST", {
              rowVersion: employee.requestRowVersion,

              inventoryItemId: data?.items.find((i) => i.sku === sku)?.id,
            }),
          )
        : Promise.reject(new Error("Refresh the request and try again.")),
    )
  }

  const meta = {
    ...screenTitles[screen],

    ...(screen === "add-employee" && editing ? { title: "Edit Employee" } : {}),
  }

  return (
    <AppLayout
      screen={screen}
      onNav={navigate}
      unreadCount={unread.data?.unreadCount ?? 0}
      activeRequestCount={
        can("coats.read") ? (activeRequests.data?.activeCount ?? 0) : 0
      }
      title={meta.title}
      subtitle={meta.subtitle}
      businessDate={organization.data?.businessDate}
    >
      {!permitted && (
        <div role="alert" className="p-8">
          Your roles do not allow this page.{" "}
          <button className="btn" onClick={() => navigate("home")}>
            Return to workspace
          </button>
        </div>
      )}
      {permitted && screen === "home" && <WorkspacePage onNav={navigate} />}
      {permitted && screen === "settings" && <SettingsPage />}
      {permitted && screen === "clothing-settings" && <ClothingSettingsPage />}
      {permitted && screen === "users" && <UsersPage />}
      {permitted && screen === "account" && <AccountPage />}
      {permitted && screen === "import" && <ImportPage />}
      {permitted && screen === "employees" && !hasClothing && <DirectoryPage />}
      {permitted && screen === "notifications" && !hasClothing && (
        <WorkspaceNotificationsPage onNav={navigate} />
      )}
      {clothingScreen && permitted && (
        <ApiState
          pending={snapshot.isPending}
          error={snapshot.error}
          retry={() => void snapshot.refetch()}
        />
      )}
      {action.error && (
        <div className="mx-8 mt-4">
          <ApiState
            error={action.error}
            retry={() => {
              action.reset()

              void snapshot.refetch().then((result) => {
                const latest = result.data?.employees.find(
                  (e) => e.id === editing?.recordId,
                )

                if (latest)
                  setEditing({
                    ...latest,

                    recordId: latest.id,

                    id: latest.employeeNumber,

                    name: latest.fullName,

                    jobTitle: latest.jobTitle ?? "",

                    notes: latest.notes ?? "",
                  })
              })
            }}
          />
        </div>
      )}
      {data && clothingScreen && permitted && (
        <ProgramContext.Provider
          value={{
            coats: data.items.map((item) => ({
              id: item.sku,

              name: item.name,

              color: item.color,

              size: item.size,
            })),

            businessDate: data.businessDate,

            timeZoneId: data.settings.timeZoneId,

            employees,

            movements: data.movements,

            busy: action.isPending,

            lowStock:
              data.settings.lowStockAlertsEnabled &&
              data.items.some(
                (item) =>
                  item.quantityOnHand <= data.settings.lowStockThreshold,
              ),

            stockFor: (coatType) =>
              coatType
                ? (data.items.find((i) => i.sku === coatType)?.quantityOnHand ??
                  0)
                : stock,

            can: allowed,
          }}
        >
          {screen === "dashboard" && (
            <Dashboard
              employees={employees}
              notifications={data.notifications}
              stock={stock}
              onNav={navigate}
            />
          )}
          {screen === "employees" && (
            <EmployeeList
              employees={employees}
              movements={data.movements}
              onAdd={() => navigate("add-employee")}
              onEdit={(employee) => {
                action.reset()

                setEditing(employee)

                setScreen("add-employee")
              }}
            />
          )}
          {screen === "add-employee" && allowed("add-employee") && (
            <EmployeeForm
              key={
                editing ? editing.recordId + ":" + editing.rowVersion : "new"
              }
              employee={editing}
              onSave={(employee) => void saveEmployee(employee)}
              onCancel={() => navigate("employees")}
            />
          )}
          {screen === "inventory" && (
            <InventoryScreen
              movements={data.movements}
              stock={stock}
              onAddStock={receive}
            />
          )}
          {screen === "requests" && (
            <RequestsScreen
              employees={employees}
              movements={data.movements}
              stock={stock}
              onMarkProvided={provide}
            />
          )}
          {screen === "notifications" && (
            <NotificationsScreen
              onNav={navigate}
              notifications={data.notifications}
              onMarkRead={(id) =>
                void perform(() => platformApi.readNotification(id))
              }
              onMarkAllRead={() => void perform(() => platformApi.readAll())}
            />
          )}
        </ProgramContext.Provider>
      )}
    </AppLayout>
  )
}
