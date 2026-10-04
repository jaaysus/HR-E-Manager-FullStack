import { useState } from "react"
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query"
import { platformApi } from "../services/platformApi"
import ApiState from "../components/ApiState"
import { useAuth } from "../app/AuthProvider"
import type { Screen } from "../app/navigation"
import { notificationDestination } from "../utils/notificationNavigation"

export default function WorkspaceNotificationsPage({
  onNav,
}: {
  onNav: (screen: Screen) => void
}) {
  const { can } = useAuth()
  const [page, setPage] = useState(1)
  const cache = useQueryClient()
  const feed = useQuery({
    queryKey: ["platform", "notifications", page],
    queryFn: ({ signal }) => platformApi.notifications(page, signal),
  })
  const read = useMutation({
    mutationFn: platformApi.readNotification,
    onSuccess: () =>
      cache.invalidateQueries({ queryKey: ["platform", "notifications"] }),
  })
  return (
    <div className="page space-y-4">
      <ApiState
        pending={feed.isPending}
        error={feed.error ?? read.error}
        retry={() => void feed.refetch()}
      />
      {feed.data?.items.map((notification) => (
        <div key={notification.id} className="panel flex justify-between gap-4">
          <button
            type="button"
            className="flex-1 text-left rounded-lg enabled:cursor-pointer enabled:hover:underline focus-visible:outline-2 focus-visible:outline-blue-600"
            disabled={!notificationDestination(notification, can)}
            onClick={() => {
              const destination = notificationDestination(notification, can)
              if (destination) onNav(destination)
            }}
          >
            <p>{notification.message}</p>
            <p className="text-xs text-slate-500 mt-2">
              {new Date(notification.createdAtUtc).toLocaleString()}
            </p>
          </button>
          {!notification.readAtUtc && (
            <button
              className="btn secondary"
              disabled={read.isPending}
              onClick={() => read.mutate(notification.id)}
            >
              Mark read
            </button>
          )}
        </div>
      ))}
      {feed.data?.items.length === 0 && (
        <p>No notifications for your assigned modules.</p>
      )}
      <div className="flex justify-between">
        <button
          className="btn secondary"
          disabled={page === 1}
          onClick={() => setPage(page - 1)}
        >
          Previous
        </button>
        <button
          className="btn secondary"
          disabled={
            !feed.data || page * feed.data.pageSize >= feed.data.totalCount
          }
          onClick={() => setPage(page + 1)}
        >
          Next
        </button>
      </div>
    </div>
  )
}
