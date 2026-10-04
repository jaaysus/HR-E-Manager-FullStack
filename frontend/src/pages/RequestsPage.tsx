import { useState } from "react"

import { useProgram } from "../app/ProgramContext"

import type { Employee, StockMovement } from "../types/program"

import Badge from "../components/StatusBadge"

import { clothingLabel } from "../utils/clothing"

export default function RequestsPage({
  employees,

  stock,

  onMarkProvided,
}: {
  employees: Employee[]

  stock: number

  movements: StockMovement[]

  onMarkProvided: (id: string, sku?: string) => void
}) {
  const { coats, busy, can, stockFor } = useProgram()

  const [selected, setSelected] = useState<Record<string, string>>({})

  const requests = employees.filter(
    (e) => e.coatStatus === "requested" || e.coatStatus === "out_of_stock",
  )

  return (
    <div className="page space-y-5">
      <div className="grid grid-cols-2 gap-4">
        <div className="panel">
          <p>Active Requests</p>
          <strong className="text-2xl">{requests.length}</strong>
          <p className="text-sm text-slate-500">
            Created by the eligibility policy
          </p>
        </div>
        <div className="panel">
          <p>Available Stock</p>
          <strong className="text-2xl">{stock}</strong>
        </div>
      </div>
      <section className="panel space-y-4">
        <h2 className="font-semibold">Clothing requests</h2>
        <p className="text-sm text-slate-500">
          Choose the coat variant the employee takes. The allocation records
          that variant and deducts one unit from stock.
        </p>
        {requests.length === 0 ? (
          <p>
            No active requests. New requests follow the eligibility policy in
            Clothing settings.
          </p>
        ) : (
          <table className="w-full text-left text-sm">
            <thead>
              <tr>
                <th>Employee</th>
                <th>Coat variant taken</th>
                <th>Cycle</th>
                <th>Status</th>
                <th>Action</th>
              </tr>
            </thead>
            <tbody>
              {requests.map((e) => {
                const sku = selected[e.id] ?? e.requiredCoatId ?? ""

                return (
                  <tr key={e.id} className="border-t border-slate-100">
                    <td className="py-4">
                      {e.name}
                      <p className="text-slate-500">{e.id}</p>
                    </td>
                    <td>
                      <select
                        className="input"
                        aria-label={`Coat variant for ${e.id}`}
                        disabled={busy || !can("provide")}
                        value={sku}
                        onChange={(event) =>
                          setSelected({
                            ...selected,

                            [e.id]: event.target.value,
                          })
                        }
                      >
                        <option value="">Choose a coat variant</option>
                        {coats.map((c) => (
                          <option
                            key={c.id}
                            value={c.id}
                            disabled={stockFor(c.id) < 1}
                          >
                            {clothingLabel(c)} - {stockFor(c.id)} in stock
                          </option>
                        ))}
                      </select>
                    </td>
                    <td>{e.currentCycle}</td>
                    <td>
                      <Badge status={e.coatStatus} />
                    </td>
                    <td>
                      <button
                        className="btn"
                        disabled={
                          busy || !can("provide") || !sku || stockFor(sku) < 1
                        }
                        onClick={() => onMarkProvided(e.id, sku)}
                      >
                        Mark as Provided
                      </button>
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        )}
      </section>
    </div>
  )
}
