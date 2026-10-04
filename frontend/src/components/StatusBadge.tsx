import type { CoatStatus } from "../types/program"

import { STATUS_META } from "../utils/program"

export default function StatusBadge({ status }: { status: CoatStatus }) {
  const m = STATUS_META[status]

  return (
    <span
      className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium ${m.color}`}
    >
      {m.label}
    </span>
  )
}
