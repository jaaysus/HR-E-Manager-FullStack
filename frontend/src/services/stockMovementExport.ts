import type { MovementRow } from "../utils/stockMovements"
import { movementTypeLabels } from "../utils/stockMovements"

export async function downloadStockMovements(
  rows: MovementRow[],
  businessDate: string,
) {
  const { default: ExcelJS } = await import("exceljs")
  const workbook = new ExcelJS.Workbook()
  workbook.creator = "HR E-Tracker"
  const sheet = workbook.addWorksheet("Stock movements", {
    views: [{ state: "frozen", ySplit: 1 }],
  })
  sheet.columns = [
    { header: "Date", key: "date", width: 14 },
    { header: "Name", key: "name", width: 28 },
    { header: "Color", key: "color", width: 18 },
    { header: "Size", key: "size", width: 12 },
    { header: "Type", key: "type", width: 16 },
    { header: "Quantity", key: "quantity", width: 14 },
    { header: "Received by", key: "recipientName", width: 28 },
    { header: "Recipient employee ID", key: "recipientEmployeeNumber", width: 24 },
    { header: "Note", key: "note", width: 55 },
  ]
  for (const row of rows) {
    sheet.addRow({
      date: new Date(`${row.date}T00:00:00Z`),
      name: row.name,
      color: row.color,
      size: row.size,
      type: movementTypeLabels[row.type],
      quantity: row.qty,
      recipientName: row.recipientName ?? "",
      recipientEmployeeNumber: row.recipientEmployeeNumber ?? "",
      note: row.note,
    })
  }
  sheet.getColumn("date").numFmt = "yyyy-mm-dd"
  sheet.getColumn("quantity").numFmt = "+0;-0;0"
  sheet.getColumn("note").alignment = { wrapText: true, vertical: "top" }
  sheet.getRow(1).font = { bold: true, color: { argb: "FFFFFFFF" } }
  sheet.getRow(1).fill = {
    type: "pattern",
    pattern: "solid",
    fgColor: { argb: "FF1E3A5F" },
  }
  sheet.getRow(1).height = 24
  sheet.autoFilter = { from: "A1", to: `I${sheet.rowCount}` }
  const data = await workbook.xlsx.writeBuffer()
  const url = URL.createObjectURL(
    new Blob([new Uint8Array(data)], {
      type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    }),
  )
  const link = document.createElement("a")
  link.href = url
  link.download = `stock-movements-${businessDate}.xlsx`
  document.body.appendChild(link)
  link.click()
  link.remove()
  window.setTimeout(() => URL.revokeObjectURL(url), 1000)
}
