import React from "react"
import ReactDOM from "react-dom/client"
import App from "./App"
import "./index.css"
import { QueryClientProvider } from "@tanstack/react-query"
import { queryClient } from "./app/queryClient"
import AuthProvider from "./app/AuthProvider"
import { initializeBranding } from "./app/branding"

async function bootstrap() {
  await initializeBranding()
  ReactDOM.createRoot(document.getElementById("root")!).render(
    <React.StrictMode>
      <QueryClientProvider client={queryClient}>
        <AuthProvider>
          <App />
        </AuthProvider>
      </QueryClientProvider>
    </React.StrictMode>,
  )
}
void bootstrap()
