export default function CoatIcon({
  color = "currentColor",
}: {
  color?: string
}) {
  return (
    <svg
      width="30"
      height="30"
      viewBox="0 0 24 24"
      fill="none"
      stroke={color}
      strokeWidth="1.7"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      <path d="m8 3-5 3-2 7 4 1 1-4v11h12V10l1 4 4-1-2-7-5-3-4 3-4-3Z" />
      <path d="M12 6v15M8 16H6m10 0h2" />
    </svg>
  )
}
