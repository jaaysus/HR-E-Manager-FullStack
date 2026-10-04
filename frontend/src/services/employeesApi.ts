import { api, json } from "./apiClient"
import type {
  Employee,
  EmployeeInput,
  ImportPreview,
  ImportSummary,
  Page,
} from "../types/api"
export const employeesApi = {
  detail: (id: string) => api.request<Employee>("/employees/" + id),
  list: (
    filters: {
      query: string
      departmentId: string
      status: string
      page: number
    },
    signal?: AbortSignal,
  ) =>
    api.request<Page<Employee>>(
      "/employees?" +
        new URLSearchParams(
          Object.entries(filters)
            .filter(([, value]) => value !== "")
            .map(([key, value]) => [key, String(value)]),
        ),
      { signal },
    ),
  save: (input: EmployeeInput, employee?: Employee) =>
    employee
      ? api.request<Employee>(
          `/employees/${employee.id}`,
          json("PUT", { ...input, rowVersion: employee.rowVersion }),
        )
      : api.request<Employee>("/employees", json("POST", input)),
  setActive: (employee: Employee) =>
    api.request<Employee>(
      `/employees/${employee.id}/active`,
      json("PATCH", {
        isActive: false,
        rowVersion: employee.rowVersion,
      }),
    ),
  history: (page: number, signal?: AbortSignal) =>
    api.request<Page<ImportSummary>>(`/employee-imports?page=${page}`, {
      signal,
    }),
  preview: (id: string, page: number, signal?: AbortSignal) =>
    api.request<ImportPreview>(`/employee-imports/${id}?page=${page}`, {
      signal,
    }),
  upload: (file: File) => {
    const body = new FormData()
    body.append("file", file)
    return api.request<ImportSummary>("/employee-imports", {
      method: "POST",
      body,
    })
  },
  commit: (id: string) =>
    api.request<ImportSummary>(`/employee-imports/${id}/commit`, {
      method: "POST",
    }),
}
