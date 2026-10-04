export default function Pagination({
  page,
  total,
  size = 25,
  onPage,
  busy,
}: {
  page: number
  total: number
  size?: number
  onPage: (page: number) => void
  busy?: boolean
}) {
  return (
    <div className="flex items-center justify-between gap-4 py-4 text-sm text-slate-500">
      <span>
        {total} records · Page {page} of {Math.max(1, Math.ceil(total / size))}
      </span>
      <div className="flex gap-2">
        <button
          className="btn secondary"
          disabled={busy || page <= 1}
          onClick={() => onPage(page - 1)}
        >
          Previous
        </button>
        <button
          className="btn secondary"
          disabled={busy || page * size >= total}
          onClick={() => onPage(page + 1)}
        >
          Next
        </button>
      </div>
    </div>
  )
}
