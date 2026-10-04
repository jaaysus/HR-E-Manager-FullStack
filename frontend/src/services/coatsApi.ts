import { api, json } from "./apiClient"
import type { CoatRequest, Dashboard, Page } from "../types/api"
export const coatsApi = {
  dashboard: (year: number | undefined, signal?: AbortSignal) =>
    api.request<Dashboard>(`/dashboard${year ? "?year=" + year : ""}`, {
      signal,
    }),
  requests: (
    status: string,
    departmentId: string,
    page: number,
    signal?: AbortSignal,
  ) =>
    api.request<Page<CoatRequest>>(
      "/coat-requests?" +
        new URLSearchParams({
          ...(status ? { status } : {}),
          ...(departmentId ? { departmentId } : {}),
          page: String(page),
        }),
      { signal },
    ),
  mutate: (request: CoatRequest, action: "provide" | "cancel") =>
    api.request<CoatRequest>(
      `/coat-requests/${request.id}/${action}`,
      json("POST", { rowVersion: request.rowVersion }),
    ),
  assign: (request: CoatRequest, inventoryItemId: string) =>
    api.request<void>(
      `/coat-requests/${request.id}/assign-item`,
      json("POST", { inventoryItemId, rowVersion: request.rowVersion }),
    ),
  run: () =>
    api.request<{ created: number }>("/coat-requests/run-due-cycle", {
      method: "POST",
    }),
}
