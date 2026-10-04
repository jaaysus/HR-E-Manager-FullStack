export type Permission = "platform.users.manage" | "platform.departments.read" | "platform.departments.manage" | "platform.notifications.read" | "platform.settings.read" | "platform.settings.manage" | "clothing.settings.manage" | "employees.read" | "employees.manage" | "employees.import" | "inventory.read" | "inventory.manage" | "inventory.rules.manage" | "coats.read" | "coats.provide" | "coats.manage" | "coats.dashboard.read"

export type Role = "HrAdministrator" | "InventoryManager" | "Viewer" | "ClothingManager" | "ClothingViewer"

export interface AuthUser {
  email: string

  fullName: string

  roles: Role[]

  permissions: Permission[]
}

export interface User extends AuthUser {
  id: string

  isActive: boolean
}

export interface Session extends AuthUser {
  accessToken: string

  expiresAtUtc: string

  refreshTokenExpiresAtUtc: string
}

export interface Page<T> {
  items: T[]

  totalCount: number

  page: number

  pageSize: number
}

export interface Department {
  id: string

  code: string

  name: string

  isActive: boolean

  rowVersion: string
}

export interface OrganizationPreferences {
  companyName: string

  timeZoneId: string

  rowVersion: string

  businessDate?: string
}

export interface ClothingSettings {
  lowStockAlertsEnabled: boolean

  lowStockThreshold: number

  rowVersion: string

  eligibilityInterval: number

  eligibilityUnit: "Minutes" | "Days" | "Months"
}

export interface PersonalNotificationPreferences {
  notifyLowStock: boolean

  notifyClothingActivity: boolean
}

export interface Employee {
  id: string

  employeeNumber: string

  fullName: string

  departmentId: string

  department: string

  jobTitle: string | null

  enrollmentDate: string

  notes: string | null

  isActive: boolean

  rowVersion: string
}

export type EmployeeInput = Pick<Employee, "employeeNumber" | "fullName" | "departmentId" | "jobTitle" | "enrollmentDate" | "notes">

export interface InventoryItem {
  color: string

  size: string

  isActive?: boolean

  id: string

  sku: string

  name: string

  department: string

  quantityOnHand: number

  rowVersion: string
}

export interface Movement {
  id: string

  type: number

  quantity: number

  occurredAtUtc: string

  reference: string | null

  note: string
}

export type RequestStatus = 0 | 1 | 2 | 3

export interface CoatRequest {
  id: string

  employeeNumber: string

  employeeName: string

  cycleNumber: number

  dueDate: string

  itemSku: string | null

  itemName: string | null

  status: RequestStatus

  providedAtUtc: string | null

  rowVersion: string

  inventoryItemId: string | null

  notes: string | null
}

export interface Notification {
  relatedEntityType: string | null

  relatedEntityId: string | null

  id: string

  type: string

  message: string

  createdAtUtc: string

  readAtUtc: string | null

  resolvedAtUtc: string | null
}

export interface Settings {
  companyName: string

  timeZoneId: string

  lowStockAlertsEnabled: boolean

  lowStockThreshold: number

  rowVersion: string
}

export interface Dashboard {
  businessDate: string

  timeZoneId: string

  reportYear: number

  activeEmployees: number

  inactiveEmployees: number

  pendingRequests: number

  outOfStockRequests: number

  providedRequests: number

  cancelledRequests: number

  quantityOnHand: number

  lowStockItems: number

  unreadNotifications: number

  monthlyIssues: {
    year: number

    month: number

    quantity: number
  }[]
}

export interface ImportSummary {
  id: string

  fileName: string

  status: string

  rowCount: number

  errorCount: number

  importedCount: number

  createdAtUtc: string
}

export interface ImportPreview {
  batch: ImportSummary

  rows: {
    rowNumber: number

    employeeNumber: string

    fullName: string

    department: string

    enrollmentDate: string | null

    jobTitle: string | null

    notes: string | null

    errors: string[]
  }[]

  page: number

  pageSize: number
}
