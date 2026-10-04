import type { CoatType, StockMovement } from "../types/program"

export interface MovementRow extends StockMovement {
  name: string
  color: string
  size: string
}

export interface MovementFilters {
  search: string
  type: "" | StockMovement["type"]
  coatType: string
  color: string
  size: string
  from: string
  to: string
}

export const emptyMovementFilters: MovementFilters = {
  search: "",
  type: "",
  coatType: "",
  color: "",
  size: "",
  from: "",
  to: "",
}

export const movementTypeLabels = {
  arrival: "Arrival",
  allocation: "Allocation",
  adjustment: "Adjustment",
}

export function movementRows(
  movements: StockMovement[],
  coats: CoatType[],
): MovementRow[] {
  const catalog = new Map(coats.map((coat) => [coat.id, coat]))
  return movements.map((movement) => {
    const coat = catalog.get(movement.coatType)
    return {
      ...movement,
      name: movement.itemName ?? coat?.name ?? "Retired coat",
      color: movement.color ?? coat?.color ?? "",
      size: movement.size ?? coat?.size ?? "",
    }
  })
}

export function filterMovements(
  rows: MovementRow[],
  filters: MovementFilters,
): MovementRow[] {
  const search = filters.search.trim().toLocaleLowerCase()
  if (filters.from && filters.to && filters.from > filters.to) return []
  return rows.filter(
    (row) =>
      (!filters.type || row.type === filters.type) &&
      (!filters.coatType || row.coatType === filters.coatType) &&
      (!filters.color || row.color === filters.color) &&
      (!filters.size || row.size === filters.size) &&
      (!filters.from || row.date >= filters.from) &&
      (!filters.to || row.date <= filters.to) &&
      (!search ||
        [row.name, row.color, row.size, row.note, row.recipientName, row.recipientEmployeeNumber, movementTypeLabels[row.type]]
          .join(" ")
          .toLocaleLowerCase()
          .includes(search)),
  )
}
