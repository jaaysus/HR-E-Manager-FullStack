import { screenPermission, type Screen } from "../app/navigation"
import type { Permission } from "../types/api"

export function notificationDestination(
  notification: {
    type: string
    eventType?: string
    relatedEntityType?: string | null
  },
  can: (permission: Permission) => boolean,
): Screen | undefined {
  const type = notification.eventType ?? notification.type
  let screen: Screen | undefined
  if (type === "ConfigurationRequired") screen = "clothing-settings"
  else if (
    notification.relatedEntityType === "InventoryItem" ||
    ["LowStock", "StockArrival"].includes(type)
  )
    screen = "inventory"
  else if (
    notification.relatedEntityType === "CoatRequest" ||
    ["RequestDue", "RequestProvided", "RequestCancelled"].includes(type)
  )
    screen = "requests"
  else if (notification.relatedEntityType === "Employee") screen = "employees"
  else if (notification.relatedEntityType === "OrganizationSettings")
    screen = "settings"
  if (!screen) return undefined
  const permission = screenPermission(screen)
  return !permission || can(permission) ? screen : undefined
}
