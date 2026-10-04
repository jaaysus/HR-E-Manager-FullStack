import type { ReactNode } from "react"
import type { Screen } from "../app/navigation"
import Sidebar from "./Sidebar"
import Header from "./Header"
import { brandFooter } from "../app/branding"

export default function AppLayout({
  screen,
  onNav,
  unreadCount,
  activeRequestCount,
  title,
  subtitle,
  businessDate,
  children,
}: {
  screen: Screen
  onNav: (screen: Screen) => void
  unreadCount: number
  activeRequestCount: number
  title: string
  subtitle?: string
  businessDate?: string
  children: ReactNode
}) {
  return (
    <div
      className="flex h-dvh overflow-hidden"
      style={{ background: "#f1f5f9" }}
    >
      <Sidebar
        current={screen}
        onNav={onNav}
        unreadCount={unreadCount}
        activeRequestCount={activeRequestCount}
      />
      <div className="flex-1 flex flex-col min-w-0 min-h-0">
        <div className="shrink-0">
          <Header
            title={title}
            subtitle={subtitle}
            businessDate={businessDate}
          />
        </div>
        <main key={screen} className="flex-1 min-h-0 overflow-auto">
          {children}
        </main>
        <footer
          className="shrink-0 px-8 py-3 text-center"
          style={{
            borderTop: "1px solid #e2e8f0",
            background: "#ffffff",
            fontSize: 12,
            color: "#94a3b8",
          }}
        >
          {brandFooter()}
        </footer>
      </div>
    </div>
  )
}
