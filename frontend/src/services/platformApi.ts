import { api, json } from "./apiClient"
import type {
  Department,
  Notification,
  Page,
  Role,
  OrganizationPreferences,
  User,
} from "../types/api"
export const platformApi = {
  unreadCount: (signal?: AbortSignal) =>
    api.request<{ unreadCount: number }>("/notifications/unread-count", {
      signal,
    }),
  departments: (signal?: AbortSignal) =>
    api.request<Department[]>("/departments", { signal }),
  notifications: (page: number, signal?: AbortSignal) =>
    api.request<Page<Notification>>(`/notifications?page=${page}`, { signal }),
  readNotification: (id: string) =>
    api.request<void>(`/notifications/${id}/read`, { method: "POST" }),
  readAll: () =>
    api.request<void>("/notifications/read-all", { method: "POST" }),
  settings: (signal?: AbortSignal) =>
    api.request<OrganizationPreferences>("/settings", { signal }),
  saveSettings: (settings: OrganizationPreferences) =>
    api.request<OrganizationPreferences>("/settings", json("PUT", settings)),
  users: (page: number, signal?: AbortSignal) =>
    api.request<User[]>(`/users?page=${page}&pageSize=25`, { signal }),
  createUser: (input: {
    email: string
    fullName: string
    password: string
    roles: Role[]
  }) => api.request<User>("/users", json("POST", input)),
  updateUser: (
    id: string,
    input: {
      fullName: string
      isActive: boolean
      roles: Role[]
    },
  ) => api.request<User>(`/users/${id}`, json("PUT", input)),
  changePassword: (currentPassword: string, newPassword: string) =>
    api.request<void>(
      "/auth/change-password",
      json("POST", { currentPassword, newPassword }),
    ),
}
