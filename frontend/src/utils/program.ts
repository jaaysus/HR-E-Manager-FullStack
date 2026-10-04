import type { CoatStatus, Employee, StockMovement } from "../types/program"

export function isEligible(emp: Employee): boolean {
  return (emp.currentCycle ?? 0) > 0
}

export function getEmployeeRequestStatus(emp: Employee): CoatStatus {
  return emp.coatStatus
}

export const STATUS_META: Record<CoatStatus, {
  label: string
  color: string
}> =
  {
    none: { label: "Not Eligible", color: "bg-slate-100 text-slate-500" },

    eligible: { label: "Eligible", color: "bg-blue-100 text-blue-700" },

    requested: { label: "Auto Request", color: "bg-amber-100 text-amber-700" },

    provided: {
      label: "Cycle Completed",

      color: "bg-green-100 text-green-700",
    },

    out_of_stock: { label: "Out of Stock", color: "bg-red-100 text-red-700" },

    cancelled: { label: "Cancelled", color: "bg-slate-100 text-slate-500" },
  }

export function getRequestDisplayStatus(
  employee: Employee,

  _movements: StockMovement[],
): CoatStatus {
  return employee.coatStatus
}
