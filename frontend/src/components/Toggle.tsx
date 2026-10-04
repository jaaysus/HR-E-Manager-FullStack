export default function Toggle({
  on,
  onToggle,
  disabled = false,
}: {
  on: boolean
  onToggle: () => void
  disabled?: boolean
}) {
  return (
    <button
      type="button"
      disabled={disabled}
      role="switch"
      aria-checked={on}
      aria-label="Low Stock Alerts"
      onClick={onToggle}
      className="relative flex-shrink-0"
      style={{
        width: 42,
        height: 24,
        borderRadius: 12,

        background: on ? "#2563eb" : "#e2e8f0",

        transition: "background 0.2s",

        border: "none",

        cursor: "pointer",
      }}
    >
      <div
        style={{
          width: 18,
          height: 18,
          borderRadius: "50%",
          background: "#fff",

          position: "absolute",
          top: 3,

          left: on ? 21 : 3,

          transition: "left 0.2s",

          boxShadow: "0 1px 3px rgba(0,0,0,0.2)",
        }}
      />
    </button>
  )
}
