import { api, json } from "./apiClient"
import type { InventoryItem, Movement, Page } from "../types/api"
export const inventoryApi = {
  items: (signal?: AbortSignal) =>
    api.request<InventoryItem[]>("/inventory/items", { signal }),
  movements: (id: string, page: number, signal?: AbortSignal) =>
    api.request<Page<Movement>>(
      `/inventory/items/${id}/movements?page=${page}`,
      { signal },
    ),
  postStock: (
    item: InventoryItem,
    quantity: number,
    note: string,
    reference: string,
    adjustment: boolean,
  ) =>
    api.request<InventoryItem>(
      adjustment ? "/inventory/adjustments" : "/inventory/receipts",
      json("POST", {
        inventoryItemId: item.id,
        quantity,
        note,
        reference,
        ...(adjustment ? { rowVersion: item.rowVersion } : {}),
      }),
    ),
}
