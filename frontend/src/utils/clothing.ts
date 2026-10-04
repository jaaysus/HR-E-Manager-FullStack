export function clothingLabel(coat: {
  name: string
  color?: string
  size?: string
}) {
  return [coat.name, coat.color, coat.size].filter(Boolean).join(" · ")
}

export function clothingAccent(color?: string) {
  const value = color?.trim() ?? ""
  if (value && typeof CSS !== "undefined" && CSS.supports("color", value))
    return value
  if (value.toLowerCase() === "navy blue") return "#1e3a5f"
  return "#2563eb"
}
