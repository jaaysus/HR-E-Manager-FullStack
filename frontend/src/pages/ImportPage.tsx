import { useRef, useState } from "react"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { employeesApi } from "../services/employeesApi"
import icons from "../components/Icons"
import ApiState from "../components/ApiState"
import Pagination from "../components/Pagination"
export default function ImportPage() {
  const cache = useQueryClient()
  const input = useRef<HTMLInputElement>(null)
  const [id, setId] = useState("")
  const [page, setPage] = useState(1)
  const [fileError, setFileError] = useState<Error | null>(null)
  const upload = useMutation({
    mutationFn: employeesApi.upload,
    onSuccess: (batch) => {
      setId(batch.id)
      setPage(1)
    },
  })
  const preview = useQuery({
    queryKey: ["employees", "import", id, page],
    queryFn: ({ signal }) => employeesApi.preview(id, page, signal),
    enabled: !!id,
  })
  const commit = useMutation({
    mutationFn: () => employeesApi.commit(id),
    onSuccess: () => cache.invalidateQueries(),
    onError: () =>
      cache.invalidateQueries({ queryKey: ["employees", "import", id] }),
  })
  const busy = upload.isPending || commit.isPending
  const step = id ? "preview" : "upload"
  const previewData = preview.data?.rows ?? []
  function selectFile(file?: File) {
    if (!file || busy) return
    setFileError(null)
    upload.reset()
    commit.reset()
    if (!/\.(csv|xlsx|xls)$/i.test(file.name) || file.size > 10 * 1024 * 1024) {
      setFileError(new Error("Choose a CSV, XLSX, or XLS file up to 10 MB."))
      return
    }
    upload.mutate(file)
  }
  function reset() {
    setId("")
    upload.reset()
    commit.reset()
    setFileError(null)
    if (input.current) input.current.value = ""
  }
  return (
    <div className="page page-import flex flex-col gap-6">
      <input
        ref={input}
        type="file"
        accept=".csv,.xlsx,.xls"
        aria-label="Employee file"
        className="hidden"
        onChange={(e) => selectFile(e.target.files?.[0])}
      />
      <ApiState
        pending={busy || (!!id && preview.isPending)}
        error={fileError ?? upload.error ?? preview.error ?? commit.error}
        retry={id ? () => void preview.refetch() : undefined}
      />
      {step === "upload" ? (
        <>
          <div
            style={{
              background: "#fff",
              border: "2px dashed #cbd5e1",
              borderRadius: 12,
              padding: 48,
              textAlign: "center",
            }}
            onDrop={(e) => {
              e.preventDefault()
              e.currentTarget.style.borderColor = "#cbd5e1"
              e.currentTarget.style.background = "#fff"
              selectFile(e.dataTransfer.files[0])
            }}
            onDragOver={(e) => {
              e.preventDefault()
              e.currentTarget.style.borderColor = "#2563eb"
              e.currentTarget.style.background = "#eff6ff"
            }}
            onDragLeave={(e) => {
              e.currentTarget.style.borderColor = "#cbd5e1"
              e.currentTarget.style.background = "#fff"
            }}
          >
            <div
              className="mx-auto flex items-center justify-center rounded-full mb-4"
              style={{ width: 56, height: 56, background: "#eff6ff" }}
            >
              {icons.import}
            </div>
            <h3
              style={{
                fontFamily: "Outfit, sans-serif",
                fontWeight: 600,
                fontSize: 16,
                color: "#0f172a",
                marginBottom: 6,
              }}
            >
              Upload Employee Excel File
            </h3>
            <p style={{ fontSize: 13, color: "#64748b", marginBottom: 20 }}>
              Drag and drop your .xlsx or .csv file here, or click to browse
            </p>
            <button
              disabled={busy}
              onClick={() => input.current?.click()}
              className="px-6 py-2.5 rounded-lg text-white font-semibold"
              style={{ background: "#2563eb", fontSize: 13.5 }}
            >
              Select File
            </button>
            <p style={{ fontSize: 11.5, color: "#94a3b8", marginTop: 12 }}>
              Supported: .xlsx, .xls, .csv · Max 10MB
            </p>
          </div>

          {/* Column mapping */}
          <div
            style={{
              background: "#fff",
              border: "1px solid #e2e8f0",
              borderRadius: 12,
              padding: 20,
            }}
          >
            <h3
              style={{
                fontFamily: "Outfit, sans-serif",
                fontWeight: 600,
                fontSize: 14,
                color: "#0f172a",
                marginBottom: 12,
              }}
            >
              Expected Column Format
            </h3>
            <table
              style={{
                width: "100%",
                borderCollapse: "collapse",
                fontSize: 13,
              }}
            >
              <thead>
                <tr
                  style={{
                    background: "#f8fafc",
                    borderBottom: "1px solid #e2e8f0",
                  }}
                >
                  <th
                    style={{
                      padding: "8px 12px",
                      textAlign: "left",
                      color: "#64748b",
                      fontWeight: 600,
                      fontSize: 11.5,
                      textTransform: "uppercase",
                      letterSpacing: "0.04em",
                    }}
                  >
                    Column
                  </th>
                  <th
                    style={{
                      padding: "8px 12px",
                      textAlign: "left",
                      color: "#64748b",
                      fontWeight: 600,
                      fontSize: 11.5,
                      textTransform: "uppercase",
                      letterSpacing: "0.04em",
                    }}
                  >
                    Maps To
                  </th>
                  <th
                    style={{
                      padding: "8px 12px",
                      textAlign: "left",
                      color: "#64748b",
                      fontWeight: 600,
                      fontSize: 11.5,
                      textTransform: "uppercase",
                      letterSpacing: "0.04em",
                    }}
                  >
                    Required
                  </th>
                  <th
                    style={{
                      padding: "8px 12px",
                      textAlign: "left",
                      color: "#64748b",
                      fontWeight: 600,
                      fontSize: 11.5,
                      textTransform: "uppercase",
                      letterSpacing: "0.04em",
                    }}
                  >
                    Example
                  </th>
                </tr>
              </thead>
              <tbody>
                {[
                  ["A", "Employee ID", "Yes", "EMP-001"],
                  ["B", "Full Name", "Yes", "Jane Smith"],
                  ["C", "Department", "Yes", "Warehouse"],
                  ["D", "Job Title", "No", "Forklift Operator"],
                  ["E", "Enrollment Date", "Yes", "2024-03-15"],
                  ["F", "Notes", "No", "Preferred size: L"],
                ].map(([col, field, req, ex]) => (
                  <tr key={col} style={{ borderBottom: "1px solid #f1f5f9" }}>
                    <td
                      style={{
                        padding: "9px 12px",
                        fontFamily: "monospace",
                        fontSize: 12,
                        color: "#64748b",
                      }}
                    >
                      Col {col}
                    </td>
                    <td
                      style={{
                        padding: "9px 12px",
                        fontWeight: 500,
                        color: "#0f172a",
                      }}
                    >
                      {field}
                    </td>
                    <td style={{ padding: "9px 12px" }}>
                      <span
                        style={{
                          fontSize: 11.5,
                          padding: "2px 7px",
                          borderRadius: 20,
                          background: req === "Yes" ? "#dbeafe" : "#f1f5f9",
                          color: req === "Yes" ? "#2563eb" : "#94a3b8",
                          fontWeight: 500,
                        }}
                      >
                        {req}
                      </span>
                    </td>
                    <td
                      style={{
                        padding: "9px 12px",
                        fontSize: 12.5,
                        color: "#64748b",
                        fontFamily: "monospace",
                      }}
                    >
                      {ex}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      ) : (
        <>
          <div
            className="flex items-center gap-3 p-3 rounded-lg"
            style={{ background: "#f0fdf4", border: "1px solid #86efac" }}
          >
            <svg
              width="16"
              height="16"
              fill="none"
              stroke="#16a34a"
              strokeWidth="2"
              viewBox="0 0 24 24"
            >
              <polyline points="20 6 9 17 4 12" />
            </svg>
            <span style={{ fontSize: 13, color: "#15803d", fontWeight: 500 }}>
              {preview.data?.batch.fileName} —{" "}
              {preview.data?.batch.rowCount ?? 0} records detected,{" "}
              {preview.data?.batch.errorCount ?? 0} errors
            </span>
          </div>

          <div
            style={{
              background: "#fff",
              border: "1px solid #e2e8f0",
              borderRadius: 12,
              overflow: "hidden",
            }}
          >
            <div
              style={{
                padding: "14px 20px",
                borderBottom: "1px solid #e2e8f0",
              }}
            >
              <h3
                style={{
                  fontFamily: "Outfit, sans-serif",
                  fontWeight: 600,
                  fontSize: 14,
                  color: "#0f172a",
                }}
              >
                Preview — {preview.data?.batch.rowCount ?? 0} Records
              </h3>
            </div>
            <table style={{ width: "100%", borderCollapse: "collapse" }}>
              <thead>
                <tr
                  style={{
                    background: "#f8fafc",
                    borderBottom: "1px solid #e2e8f0",
                  }}
                >
                  {[
                    "Employee ID",
                    "Full Name",
                    "Department",
                    "Job Title",
                    "Enrollment Date",
                    "Validation",
                  ].map((h) => (
                    <th
                      key={h}
                      style={{
                        padding: "9px 16px",
                        textAlign: "left",
                        fontSize: 11.5,
                        fontWeight: 600,
                        color: "#64748b",
                        textTransform: "uppercase",
                        letterSpacing: "0.04em",
                      }}
                    >
                      {h}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {previewData.map((r, i) => (
                  <tr
                    key={r.rowNumber}
                    style={{
                      borderBottom:
                        i < previewData.length - 1
                          ? "1px solid #f1f5f9"
                          : "none",
                    }}
                  >
                    <td
                      style={{
                        padding: "11px 16px",
                        fontFamily: "monospace",
                        fontSize: 12.5,
                        color: "#64748b",
                      }}
                    >
                      {r.employeeNumber}
                    </td>
                    <td
                      style={{
                        padding: "11px 16px",
                        fontSize: 13.5,
                        fontWeight: 500,
                        color: "#0f172a",
                      }}
                    >
                      {r.fullName}
                    </td>
                    <td
                      style={{
                        padding: "11px 16px",
                        fontSize: 13,
                        color: "#374151",
                      }}
                    >
                      {r.department}
                    </td>
                    <td
                      style={{
                        padding: "11px 16px",
                        fontSize: 13,
                        color: "#374151",
                      }}
                    >
                      {r.jobTitle}
                    </td>
                    <td
                      style={{
                        padding: "11px 16px",
                        fontSize: 13,
                        color: "#374151",
                      }}
                    >
                      {r.enrollmentDate}
                    </td>
                    <td
                      className={
                        r.errors.length ? "text-red-600" : "text-green-700"
                      }
                      style={{ padding: "11px 16px", fontSize: 12 }}
                    >
                      {r.errors.join("; ") || "Valid"}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {preview.data && (
            <Pagination
              page={page}
              size={preview.data.pageSize}
              total={preview.data.batch.rowCount}
              onPage={setPage}
              busy={busy}
            />
          )}
          {preview.data?.batch.status === "Committed" && (
            <p role="status" className="text-sm text-green-700">
              {preview.data.batch.importedCount} employees imported
              successfully.
            </p>
          )}
          <div className="flex gap-3">
            <button
              disabled={busy}
              onClick={reset}
              className="px-6 py-2.5 rounded-lg font-medium"
              style={{
                background: "#fff",
                fontSize: 13.5,
                color: "#374151",
                border: "1px solid #e2e8f0",
              }}
            >
              ← Back
            </button>
            <button
              disabled={busy || preview.data?.batch.status !== "Validated"}
              onClick={() => commit.mutate()}
              className="px-6 py-2.5 rounded-lg text-white font-semibold"
              style={{
                background: "#2563eb",
                fontSize: 13.5,
                fontFamily: "Outfit, sans-serif",
              }}
            >
              {commit.isPending
                ? "Importing…"
                : preview.data?.batch.status === "Committed"
                  ? "Import Complete"
                  : "Confirm Import (" +
                    (preview.data?.batch.rowCount ?? 0) +
                    " records)"}
            </button>
          </div>
        </>
      )}
    </div>
  )
}
