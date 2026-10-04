import { useState } from "react"
import BrandLogo from "../components/BrandLogo"
import { branding, brandFooter } from "../app/branding"
import { useAuth } from "../app/AuthProvider"

export default function LoginPage() {
  const { login } = useAuth()
  const [email, setEmail] = useState("")
  const [password, setPassword] = useState("")
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState("")

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setError("")
    setLoading(true)
    try {
      await login(email.trim(), password)
    } catch (error) {
      setError(error instanceof Error ? error.message : "Sign-in failed.")
    } finally {
      setLoading(false)
    }
  }

  return (
    <div
      className="min-h-screen flex"
      style={{
        background: "linear-gradient(135deg, #f8fafc 0%, #e2e8f0 100%)",
      }}
    >
      {/* Left panel */}
      <div
        className="hidden lg:flex flex-col justify-between p-12"
        style={{
          width: 440,
          background: "var(--brand-sidebar, #1e293b)",
          flexShrink: 0,
        }}
      >
        <div>
          <div className="flex items-center gap-3 mb-12">
            <div>
              <BrandLogo light />
              <div
                style={{
                  fontSize: 11,
                  color: "#94a3b8",
                  letterSpacing: "0.08em",
                  marginTop: 6,
                }}
              >
                {branding.applicationName.toUpperCase()}
              </div>
            </div>
          </div>
          <h2
            style={{
              fontFamily: "var(--font-display)",
              fontSize: 32,
              fontWeight: 700,
              color: "#f1f5f9",
              lineHeight: 1.25,
              marginBottom: 16,
            }}
          >
            Streamline your
            <br />
            workwear program.
          </h2>
          <p style={{ fontSize: 14, color: "#94a3b8", lineHeight: 1.7 }}>
            Track employee eligibility, manage coat inventory, and automate
            requests — all in one place.
          </p>
        </div>
        <div className="flex flex-col gap-3">
          {[
            "Configurable eligibility tracking",
            "Real-time inventory management",
            "Bulk employee import via Excel",
          ].map((f) => (
            <div key={f} className="flex items-center gap-3">
              <div
                className="rounded-full flex items-center justify-center"
                style={{
                  width: 20,
                  height: 20,
                  background: "#16a34a18",
                  flexShrink: 0,
                }}
              >
                <svg width="11" height="11" viewBox="0 0 11 11" fill="none">
                  <path
                    d="M2 5.5L4.5 8L9 3"
                    stroke="#16a34a"
                    strokeWidth="1.5"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                  />
                </svg>
              </div>
              <span style={{ fontSize: 13, color: "#94a3b8" }}>{f}</span>
            </div>
          ))}
        </div>
      </div>

      {/* Right panel */}
      <div className="flex-1 flex items-center justify-center p-8">
        <div style={{ width: "100%", maxWidth: 400 }}>
          <div style={{ marginBottom: 32 }}>
            <h1
              style={{
                fontFamily: "var(--font-display)",
                fontSize: 26,
                fontWeight: 700,
                color: "#0f172a",
                marginBottom: 6,
              }}
            >
              Sign in to your account
            </h1>
            <p style={{ fontSize: 13.5, color: "#64748b" }}>
              Enter your HR credentials to continue.
            </p>
          </div>

          <form onSubmit={handleSubmit} className="flex flex-col gap-4">
            <div>
              <label
                style={{
                  fontSize: 13,
                  fontWeight: 500,
                  color: "#374151",
                  display: "block",
                  marginBottom: 6,
                }}
              >
                Email address
              </label>
              <input
                aria-label="Email"
                required
                disabled={loading}
                type="email"
                value={email}
                onChange={(e) => {
                  setEmail(e.target.value)
                  setError("")
                }}
                placeholder="you@company.com"
                autoComplete="email"
                className="w-full rounded-lg px-3.5 py-2.5"
                style={{
                  border: "1px solid #d1d5db",
                  fontSize: 14,
                  outline: "none",
                  color: "#0f172a",
                }}
                onFocus={(e) =>
                  (e.target.style.border =
                    "1.5px solid var(--brand-primary, #2563eb)")
                }
                onBlur={(e) => (e.target.style.border = "1px solid #d1d5db")}
              />
            </div>
            <div>
              <label
                style={{
                  fontSize: 13,
                  fontWeight: 500,
                  color: "#374151",
                  display: "block",
                  marginBottom: 6,
                }}
              >
                Password
              </label>
              <input
                aria-label="Password"
                required
                disabled={loading}
                type="password"
                value={password}
                onChange={(e) => {
                  setPassword(e.target.value)
                  setError("")
                }}
                placeholder="Enter your password"
                autoComplete="current-password"
                className="w-full rounded-lg px-3.5 py-2.5"
                style={{
                  border: "1px solid #d1d5db",
                  fontSize: 14,
                  outline: "none",
                  color: "#0f172a",
                }}
                onFocus={(e) =>
                  (e.target.style.border =
                    "1.5px solid var(--brand-primary, #2563eb)")
                }
                onBlur={(e) => (e.target.style.border = "1px solid #d1d5db")}
              />
            </div>
            {error && (
              <p
                role="alert"
                style={{ fontSize: 12.5, color: "#dc2626", marginTop: -4 }}
              >
                {error}
              </p>
            )}
            <button
              type="submit"
              disabled={loading}
              className="w-full rounded-lg py-2.5 text-white font-semibold transition-opacity"
              style={{
                background: "var(--brand-primary, #2563eb)",
                fontSize: 14,
                opacity: loading ? 0.7 : 1,
                cursor: loading ? "wait" : "pointer",
                fontFamily: "var(--font-display)",
              }}
            >
              {loading ? "Signing in…" : "Sign in"}
            </button>
          </form>

          <div
            style={{
              marginTop: 20,
              padding: "12px 14px",
              borderRadius: 8,
              background: "#eff6ff",
              border: "1px solid #bfdbfe",
            }}
          >
            <p
              style={{
                fontSize: 12,
                fontWeight: 600,
                color: "#1d4ed8",
                marginBottom: 3,
              }}
            >
              Company account
            </p>
            <p style={{ fontSize: 12, color: "#475569" }}>
              Contact your administrator if you need access.
            </p>
          </div>

          <p
            style={{
              fontSize: 12,
              color: "#94a3b8",
              marginTop: 24,
              textAlign: "center",
            }}
          >
            {brandFooter(true)}
          </p>
        </div>
      </div>
    </div>
  )
}
