import { QueryClient } from "@tanstack/react-query"
import { ApiError } from "../services/apiClient"
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      retry: (count, error) =>
        !(
          error instanceof ApiError &&
          [401, 403, 404, 409].includes(error.status)
        ) && count < 1,
    },
    mutations: { retry: false },
  },
})
