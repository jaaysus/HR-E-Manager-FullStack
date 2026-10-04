import { api } from "./apiClient"

import type {
  Employee as EmployeeRecord,
  InventoryItem,
  Settings,
} from "../types/api"

import type {
  CoatStatus,
  CoatTypeId,
  Notification,
  StockMovement,
} from "../types/program"

export interface ProgramEmployee extends EmployeeRecord {
  monthsEnrolled: number

  currentCycle: number

  nextEligibilityAtUtc: string | null

  coatStatus: CoatStatus

  requestId: string | null

  requestRowVersion: string | null

  requiredCoatId: CoatTypeId | null
}

export interface ProgramSnapshot {
  businessDate: string

  employees: ProgramEmployee[]

  items: InventoryItem[]

  movements: StockMovement[]

  notifications: Notification[]

  settings: Settings
}

export const programApi = {
  snapshot: (signal?: AbortSignal) =>
    api.request<ProgramSnapshot>("/program/snapshot", {
      method: "POST",

      signal,
    }),
}
