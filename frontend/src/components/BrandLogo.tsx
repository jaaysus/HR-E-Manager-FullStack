import { useState } from "react"
import { branding } from "../app/branding"

export default function BrandLogo({
  light = false,
  compact = false,
}: {
  light?: boolean
  compact?: boolean
}) {
  const [imageFailed, setImageFailed] = useState(false)
  const logo = branding.logo
  const imageUrl = light ? logo.lightImageUrl || logo.imageUrl : logo.imageUrl
  const color = light ? logo.lightColor : logo.color
  return (
    <div className="flex items-start" aria-label={branding.companyName}>
      {imageUrl && !imageFailed ? (
        <img
          src={imageUrl}
          alt={branding.companyName}
          style={{
            height: compact ? 28 : 34,
            maxWidth: compact ? 170 : 250,
            objectFit: "contain",
          }}
          onError={() => setImageFailed(true)}
        />
      ) : (
        <span
          style={{
            color,
            fontFamily: "Arial, Helvetica, sans-serif",
            fontSize: compact ? 28 : 34,
            fontWeight: 800,
            fontStyle: logo.italic ? "italic" : "normal",
            letterSpacing: logo.letterSpacing,
            lineHeight: 0.9,
          }}
        >
          {logo.text}
        </span>
      )}
      {logo.trademark && (
        <sup
          style={{
            color,
            fontSize: compact ? 7 : 8,
            fontWeight: 700,
            marginLeft: 3,
            marginTop: -2,
          }}
        >
          {logo.trademark}
        </sup>
      )}
    </div>
  )
}
