import { useMemo, useState } from "react"
import { useProgram } from "../../app/ProgramContext"
import type { StockMovement } from "../../types/program"
import { clothingLabel } from "../../utils/clothing"
import {
  emptyMovementFilters,
  filterMovements,
  movementRows,
  movementTypeLabels,
  type MovementFilters,
} from "../../utils/stockMovements"
import { downloadStockMovements } from "../../services/stockMovementExport"
import ApiState from "../../components/ApiState"

export default function StockMovementHistory({
  movements,
}: {
  movements: StockMovement[]
}) {
  const { coats, businessDate } = useProgram()
  const [filters, setFilters] = useState<MovementFilters>(emptyMovementFilters)
  const [exporting, setExporting] = useState(false)
  const [error, setError] = useState<Error | null>(null)
  const rows = useMemo(() => movementRows(movements, coats), [movements, coats])
  const filtered = useMemo(
    () => filterMovements(rows, filters),
    [rows, filters],
  )
  const variants = Array.from(
    new Map(rows.map((row) => [row.coatType, clothingLabel(row)])).entries(),
  ).sort((a, b) => a[1].localeCompare(b[1]))
  const colors = Array.from(
    new Set(rows.map((row) => row.color).filter(Boolean)),
  ).sort()
  const sizes = Array.from(
    new Set(rows.map((row) => row.size).filter(Boolean)),
  ).sort()
  const invalidDates = Boolean(
    filters.from && filters.to && filters.from > filters.to,
  )
  const hasFilters = Object.values(filters).some(Boolean)

  function change<K extends keyof MovementFilters>(
    key: K,
    value: MovementFilters[K],
  ) {
    setFilters((current) => ({ ...current, [key]: value }))
    setError(null)
  }

  async function download() {
    if (exporting || invalidDates || filtered.length === 0) return
    setExporting(true)
    setError(null)
    try {
      await downloadStockMovements(filtered, businessDate)
    } catch (failure) {
      setError(
        failure instanceof Error
          ? failure
          : new Error("Could not download stock movements. Try again."),
      )
    } finally {
      setExporting(false)
    }
  }

  return (
    <section
      className="overflow-hidden rounded-xl border border-slate-200 bg-white"
      aria-label="Stock movement history"
    >
      <div className="space-y-4 border-b border-slate-200 p-5">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <h3 className="font-semibold text-slate-900">
              Stock Movement History
            </h3>
            <p className="mt-1 text-xs text-slate-500" role="status">
              Showing {filtered.length} of {rows.length} movements
            </p>
          </div>
          <button
            className="btn"
            disabled={exporting || invalidDates || filtered.length === 0}
            onClick={() => void download()}
          >
            {exporting ? "Preparing Excel..." : "Download Excel"}
          </button>
        </div>
        <fieldset
          disabled={exporting}
          className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4"
        >
          <label className="field sm:col-span-2">
            Search movements
            <input
              className="input"
              type="search"
              placeholder="Coat, recipient, employee ID, or note"
              value={filters.search}
              onChange={(event) => change("search", event.target.value)}
            />
          </label>
          <label className="field">
            Movement type
            <select
              className="input"
              aria-label="Movement type"
              value={filters.type}
              onChange={(event) =>
                change("type", event.target.value as MovementFilters["type"])
              }
            >
              <option value="">All types</option>
              {Object.entries(movementTypeLabels).map(([value, label]) => (
                <option key={value} value={value}>
                  {label}
                </option>
              ))}
            </select>
          </label>
          <label className="field">
            Coat variant
            <select
              className="input"
              aria-label="Coat variant"
              value={filters.coatType}
              onChange={(event) => change("coatType", event.target.value)}
            >
              <option value="">All variants</option>
              {variants.map(([value, label]) => (
                <option key={value} value={value}>
                  {label}
                </option>
              ))}
            </select>
          </label>
          <label className="field">
            Color
            <select
              className="input"
              aria-label="Color"
              value={filters.color}
              onChange={(event) => change("color", event.target.value)}
            >
              <option value="">All colors</option>
              {colors.map((value) => (
                <option key={value}>{value}</option>
              ))}
            </select>
          </label>
          <label className="field">
            Size
            <select
              className="input"
              aria-label="Size"
              value={filters.size}
              onChange={(event) => change("size", event.target.value)}
            >
              <option value="">All sizes</option>
              {sizes.map((value) => (
                <option key={value}>{value}</option>
              ))}
            </select>
          </label>
          <label className="field">
            From date
            <input
              className="input"
              type="date"
              value={filters.from}
              aria-invalid={invalidDates}
              onChange={(event) => change("from", event.target.value)}
            />
          </label>
          <label className="field">
            To date
            <input
              className="input"
              type="date"
              value={filters.to}
              aria-invalid={invalidDates}
              onChange={(event) => change("to", event.target.value)}
            />
          </label>
        </fieldset>
        <div className="flex flex-wrap items-center justify-between gap-2 text-xs text-slate-500">
          <p>Excel includes all movements matching these filters.</p>
          <button
            className="font-medium text-blue-600 disabled:text-slate-400"
            disabled={!hasFilters || exporting}
            onClick={() => {
              setFilters(emptyMovementFilters)
              setError(null)
            }}
          >
            Clear filters
          </button>
        </div>
        {invalidDates && (
          <p role="alert" className="text-sm text-red-600">
            From date must be on or before To date.
          </p>
        )}
        <ApiState error={error} />
      </div>
      <div className="overflow-x-auto">
        <table className="w-full min-w-[950px] text-left text-sm">
          <thead className="border-b border-slate-200 bg-slate-50 text-xs uppercase text-slate-500">
            <tr>
              {[
                "Date",
                "Name",
                "Color",
                "Size",
                "Type",
                "Quantity",
                "Received by",
                "Note",
              ].map((label) => (
                <th className="px-4 py-3 font-semibold" key={label}>
                  {label}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {filtered.map((row) => (
              <tr
                className="border-b border-slate-100 last:border-0"
                key={row.id}
              >
                <td className="whitespace-nowrap px-4 py-3 text-slate-600">
                  {row.date}
                </td>
                <td className="px-4 py-3 font-medium text-slate-700">
                  {row.name}
                </td>
                <td className="px-4 py-3 text-slate-600">{row.color || "—"}</td>
                <td className="px-4 py-3 text-slate-600">{row.size || "—"}</td>
                <td className="px-4 py-3">
                  <span
                    className={`rounded-full px-2 py-1 text-xs font-medium ${
                      row.type === "arrival"
                        ? "bg-green-100 text-green-700"
                        : row.type === "allocation"
                          ? "bg-red-100 text-red-700"
                          : "bg-amber-100 text-amber-700"
                    }`}
                  >
                    {movementTypeLabels[row.type]}
                  </span>
                </td>
                <td
                  className={`px-4 py-3 font-semibold ${
                    row.qty > 0 ? "text-green-600" : "text-red-600"
                  }`}
                >
                  {row.qty > 0 ? "+" : ""}
                  {row.qty}
                </td>
                <td className="px-4 py-3 text-slate-700">
                  {row.recipientName || (row.type === "adjustment" ? "—" : "Not recorded")}
                  {row.recipientEmployeeNumber && (
                    <div className="mt-1 text-xs text-slate-500">
                      Employee ID: {row.recipientEmployeeNumber}
                    </div>
                  )}
                </td>
                <td className="max-w-sm break-words px-4 py-3 text-slate-600">
                  {row.note}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        {filtered.length === 0 && (
          <p className="p-8 text-center text-sm text-slate-500">
            {rows.length === 0
              ? "No stock movements yet."
              : "No movements match these filters."}
          </p>
        )}
      </div>
    </section>
  )
}
