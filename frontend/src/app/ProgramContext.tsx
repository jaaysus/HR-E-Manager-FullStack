import { createContext, useContext } from "react"

import type { CoatType, Employee, StockMovement } from "../types/program"

export interface ProgramContextValue {
  coats: CoatType[]

  businessDate: string

  timeZoneId: string

  employees: Employee[]

  movements: StockMovement[]

  busy: boolean

  lowStock: boolean

  stockFor: (coatType?: string) => number

  can: (action: string) => boolean
}

export const ProgramContext = createContext<ProgramContextValue | null>(null)

export function useProgram() {
  const context = useContext(ProgramContext)

  if (!context) throw new Error("ProgramContext is required")

  return context
}
