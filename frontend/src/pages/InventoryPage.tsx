import { useState } from "react"

import { useProgram } from "../app/ProgramContext"

import type { StockMovement, CoatTypeId } from "../types/program"

import CoatVariants from "./inventory/CoatVariants"

import StockMovementHistory from "./inventory/StockMovementHistory"

import { clothingLabel } from "../utils/clothing"

export default function InventoryPage({
  movements,

  stock,

  onAddStock,
}: {
  movements: StockMovement[]

  stock: number

  onAddStock: (
    coatType: CoatTypeId,

    qty: number,

    note: string,

    date: string,
  ) => Promise<boolean>
}) {
  const {
    coats: COAT_TYPES,

    busy,

    businessDate,

    lowStock,

    can,

    stockFor,
  } = useProgram()

  const [date, setDate] = useState(businessDate)

  const [qty, setQty] = useState("")

  const [note, setNote] = useState("")

  const [coatType, setCoatType] = useState<CoatTypeId>(COAT_TYPES[0]?.id ?? "")

  async function handleAdd() {
    const q = Number(qty)

    if (
      Number.isInteger(q) &&
      q > 0 &&
      (await onAddStock(coatType, q, note || "Manual stock arrival", date))
    ) {
      setQty("")

      setNote("")
    }
  }

  return (
    <div className="page flex flex-col gap-6">
      {/* Stock card */}
      <div className="grid grid-cols-3 gap-4">
        <div
          className="col-span-1 rounded-xl p-6 text-white"
          style={{
            background: "linear-gradient(135deg, #1e3a5f 0%, #2563eb 100%)",
          }}
        >
          <div
            style={{
              fontSize: 12,

              opacity: 0.75,

              marginBottom: 8,

              letterSpacing: "0.05em",

              textTransform: "uppercase",
            }}
          >
            Current Stock
          </div>
          <div
            style={{
              fontFamily: "Outfit, sans-serif",

              fontSize: 52,

              fontWeight: 700,

              lineHeight: 1,
            }}
          >
            {stock}
          </div>
          <div style={{ fontSize: 13, opacity: 0.8, marginTop: 6 }}>
            coats across {COAT_TYPES.length} variants
          </div>
          {lowStock && (
            <div
              className="mt-4 flex items-center gap-2 rounded-lg px-3 py-2"
              style={{ background: "#ffffff20", fontSize: 12 }}
            >
              <span>⚠</span> Low stock — reorder recommended
            </div>
          )}
        </div>

        {/* Add stock form */}
        <div
          className="col-span-2 rounded-xl p-6"
          style={{ background: "#fff", border: "1px solid #e2e8f0" }}
        >
          <h3
            style={{
              fontFamily: "Outfit, sans-serif",

              fontWeight: 600,

              fontSize: 15,

              color: "#0f172a",

              marginBottom: 16,
            }}
          >
            Record Stock Arrival
          </h3>
          <form
            onSubmit={(e) => {
              e.preventDefault()

              void handleAdd()
            }}
          >
            <fieldset disabled={busy || !can("inventory-write")}>
              <div className="grid grid-cols-2 gap-4">
                <div className="col-span-2">
                  <label
                    style={{
                      fontSize: 12.5,

                      fontWeight: 500,

                      color: "#374151",

                      display: "block",

                      marginBottom: 5,
                    }}
                  >
                    Coat variant
                  </label>
                  <select
                    value={coatType}
                    onChange={(e) => setCoatType(e.target.value as CoatTypeId)}
                    className="w-full rounded-lg px-3 py-2.5"
                    style={{
                      border: "1px solid #d1d5db",

                      fontSize: 13.5,

                      color: "#0f172a",

                      background: "#fff",
                    }}
                  >
                    {COAT_TYPES.map((coat) => (
                      <option key={coat.id} value={coat.id}>
                        {clothingLabel(coat)}
                      </option>
                    ))}
                  </select>
                </div>
                <div>
                  <label
                    style={{
                      fontSize: 12.5,

                      fontWeight: 500,

                      color: "#374151",

                      display: "block",

                      marginBottom: 5,
                    }}
                  >
                    Quantity Received
                  </label>
                  <input
                    type="number"
                    required
                    min={1}
                    max={2147483647}
                    step={1}
                    aria-label="Quantity Received"
                    value={qty}
                    onChange={(e) => setQty(e.target.value)}
                    placeholder="e.g. 25"
                    className="w-full rounded-lg px-3 py-2.5"
                    style={{
                      border: "1px solid #d1d5db",

                      fontSize: 13.5,

                      outline: "none",

                      color: "#0f172a",
                    }}
                  />
                </div>
                <div>
                  <label
                    style={{
                      fontSize: 12.5,

                      fontWeight: 500,

                      color: "#374151",

                      display: "block",

                      marginBottom: 5,
                    }}
                  >
                    Arrival Date
                  </label>
                  <input
                    type="date"
                    required
                    aria-label="Arrival Date"
                    value={date}
                    max={businessDate}
                    onChange={(e) => setDate(e.target.value)}
                    className="w-full rounded-lg px-3 py-2.5"
                    style={{
                      border: "1px solid #d1d5db",

                      fontSize: 13.5,

                      outline: "none",

                      color: "#0f172a",
                    }}
                  />
                </div>
                <div className="col-span-2">
                  <label
                    style={{
                      fontSize: 12.5,

                      fontWeight: 500,

                      color: "#374151",

                      display: "block",

                      marginBottom: 5,
                    }}
                  >
                    Supplier / Note
                  </label>
                  <input
                    type="text"
                    value={note}
                    onChange={(e) => setNote(e.target.value)}
                    placeholder="e.g. WorkWear Co. — Invoice #4521"
                    className="w-full rounded-lg px-3 py-2.5"
                    style={{
                      border: "1px solid #d1d5db",

                      fontSize: 13.5,

                      outline: "none",

                      color: "#0f172a",
                    }}
                  />
                </div>
              </div>
              <button
                type="submit"
                className="mt-4 px-6 py-2.5 rounded-lg text-white font-semibold"
                style={{
                  background: "#16a34a",

                  fontSize: 13.5,

                  fontFamily: "Outfit, sans-serif",
                }}
              >
                {busy ? "Saving…" : "+ Add Stock"}
              </button>
            </fieldset>
          </form>
        </div>
      </div>

      <CoatVariants />

      <StockMovementHistory movements={movements} />
    </div>
  )
}

// ─── Requests ─────────────────────────────────────────────────────────────────
