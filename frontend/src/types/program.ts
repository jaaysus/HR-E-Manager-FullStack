export type CoatStatus = "none" | "eligible" | "requested" | "provided" | "out_of_stock" | "cancelled"

export type CoatTypeId = string

export interface CoatType {
  id: CoatTypeId

  name: string

  color: string

  size: string
}

export interface Employee {
  recordId?: string

  rowVersion?: string

  requestId?: string | null

  requestRowVersion?: string | null

  requiredCoatId?: CoatTypeId | null

  monthsEnrolled?: number

  currentCycle?: number

  nextEligibilityAtUtc?: string | null

  id: string

  name: string

  department: string

  jobTitle: string

  enrollmentDate: string

  coatStatus: CoatStatus

  notes: string
}

export interface StockMovement {
  recipientName?: string | null

  recipientEmployeeNumber?: string | null

  itemName?: string

  color?: string

  size?: string

  id: string

  date: string

  type: "arrival" | "allocation" | "adjustment"

  qty: number

  coatType: CoatTypeId

  note: string
}

export interface Notification {
  eventType: string

  relatedEntityType: string | null

  id: string

  message: string

  date: string

  read: boolean

  type: "info" | "warning" | "success"
}
