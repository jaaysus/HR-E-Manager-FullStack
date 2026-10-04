export interface Branding {
  companyName: string
  displayName: string
  applicationName: string
  version: string
  pageTitle: string
  logo: {
    text: string
    imageUrl: string
    lightImageUrl: string
    trademark: string
    color: string
    lightColor: string
    italic: boolean
    letterSpacing: string
  }
  theme: {
    primary: string
    primaryHover: string
    sidebar: string
    bodyFont: string
    displayFont: string
  }
}

const defaults: Branding = {
  companyName: "Company",
  displayName: "Company",
  applicationName: "HR E-Tracker",
  version: "",
  pageTitle: "HR E-Tracker",
  logo: {
    text: "Company",
    imageUrl: "",
    lightImageUrl: "",
    trademark: "",
    color: "#2563eb",
    lightColor: "#ffffff",
    italic: false,
    letterSpacing: "normal",
  },
  theme: {
    primary: "#2563eb",
    primaryHover: "#1d4ed8",
    sidebar: "#1e293b",
    bodyFont: "Inter, sans-serif",
    displayFont: "Outfit, sans-serif",
  },
}

export let branding: Branding = defaults

// Public branding is loaded at startup, so deployed identities can be swapped
// without rebuilding the app. Missing or invalid settings use neutral defaults.
export async function initializeBranding() {
  try {
    const response = await fetch(`${import.meta.env.BASE_URL}branding.json`, {
      cache: "no-store",
    })
    if (!response.ok) throw new Error("Branding file could not be loaded.")
    const config = await response.json()
    if (!config || typeof config !== "object" || Array.isArray(config))
      throw new Error("Invalid branding file.")
    const text = (value: unknown, fallback: string) =>
      typeof value === "string" ? value : fallback
    const color = (value: unknown, fallback: string) =>
      typeof value === "string" && /^#(?:[\da-f]{3}|[\da-f]{6})$/i.test(value)
        ? value
        : fallback
    const logo = config.logo ?? {}
    const theme = config.theme ?? {}
    branding = {
      companyName: text(config.companyName, defaults.companyName),
      displayName: text(config.displayName, defaults.displayName),
      applicationName: text(config.applicationName, defaults.applicationName),
      version: text(config.version, defaults.version),
      pageTitle: text(
        config.pageTitle,
        text(config.applicationName, defaults.pageTitle),
      ),
      logo: {
        text: text(logo.text, text(config.displayName, defaults.logo.text)),
        imageUrl: text(logo.imageUrl, ""),
        lightImageUrl: text(logo.lightImageUrl, ""),
        trademark: text(logo.trademark, ""),
        color: color(logo.color, defaults.logo.color),
        lightColor: color(logo.lightColor, defaults.logo.lightColor),
        italic: logo.italic === true,
        letterSpacing: text(logo.letterSpacing, defaults.logo.letterSpacing),
      },
      theme: {
        primary: color(theme.primary, defaults.theme.primary),
        primaryHover: color(theme.primaryHover, defaults.theme.primaryHover),
        sidebar: color(theme.sidebar, defaults.theme.sidebar),
        bodyFont: text(theme.bodyFont, defaults.theme.bodyFont),
        displayFont: text(theme.displayFont, defaults.theme.displayFont),
      },
    }
  } catch (error) {
    console.warn("Using default application branding.", error)
  }
  document.title = branding.pageTitle
  const socialTitle = document.querySelector('meta[property="og:title"]')
  socialTitle?.setAttribute("content", branding.pageTitle)
  const style = document.documentElement.style
  style.setProperty("--brand-primary", branding.theme.primary)
  style.setProperty("--brand-primary-hover", branding.theme.primaryHover)
  style.setProperty("--brand-sidebar", branding.theme.sidebar)
  style.setProperty("--brand-body-font", branding.theme.bodyFont)
  style.setProperty("--brand-display-font", branding.theme.displayFont)
}

export function brandFooter(includeVersion = false) {
  return `${branding.displayName}${branding.logo.trademark} ${branding.applicationName}${
    includeVersion && branding.version ? ` v${branding.version}` : ""
  }`
}
