// Compare the original demo with the restored UI using identical display data.
// Only visual comparison responses are mocked here. verify-demo.py exercises SQL Server.
import { readFileSync, writeFileSync, mkdirSync } from "node:fs"
import { resolve } from "node:path"
import { pathToFileURL } from "node:url"
import { createRequire } from "node:module"
import { execFileSync } from "node:child_process"

const root = resolve(import.meta.dirname, "..")
const reference =
  process.argv[2] ||
  "C:/Users/Kuro/Documents/Code/HR E-MANAGER DEMO/HR-E-Manager"
const source = readFileSync(resolve(reference, "src/App.tsx"), "utf8")
const output = resolve(root, "artifacts/demo-comparison")
const referenceRoot = resolve(output, "reference")
mkdirSync(resolve(referenceRoot, "src"), { recursive: true })
writeFileSync(resolve(referenceRoot, "src/App.tsx"), source)
writeFileSync(
  resolve(referenceRoot, "src/main.tsx"),
  `import React from "react"; import {createRoot} from "react-dom/client"; import App from "./App"; import "../../../../frontend/src/index.css"; createRoot(document.getElementById("root")).render(<App />);`,
)
writeFileSync(
  resolve(referenceRoot, "index.html"),
  '<!doctype html><html lang="en"><head><meta charset="UTF-8"><meta name="viewport" content="width=device-width,initial-scale=1.0"></head><body><div id="root"></div><script type="module" src="/src/main.tsx"></script></body></html>',
)
const frontendRequire = createRequire(resolve(root, "frontend/package.json"))
const { createServer } = await import(
  pathToFileURL(frontendRequire.resolve("vite")).href
)
const { default: react } = await import(
  pathToFileURL(frontendRequire.resolve("@vitejs/plugin-react")).href
)
const { default: tailwind } = await import(
  pathToFileURL(frontendRequire.resolve("@tailwindcss/vite")).href
)
const playwrightPath =
  process.env.PLAYWRIGHT_MODULE ||
  execFileSync(
    "python",
    [
      "-c",
      "import pathlib,playwright;print(pathlib.Path(playwright.__file__).parent/'driver'/'package')",
    ],
    { encoding: "utf8" },
  ).trim()
const { chromium } = createRequire(import.meta.url)(playwrightPath)

const constant = (name) => {
  const match = source.match(
    new RegExp(`const ${name}[^=]*= (\\[[\\s\\S]*?\\]);`),
  )
  if (!match) throw new Error("Missing reference constant: " + name)
  return Function("return " + match[1])()
}
const employees = constant("seedEmployees")
const movements = constant("seedMovements")
const notifications = constant("seedNotifications")
const coats = constant("COAT_TYPES")
const today = new Intl.DateTimeFormat("en-CA", {
  timeZone: "Africa/Casablanca",
  year: "numeric",
  month: "2-digit",
  day: "2-digit",
}).format(new Date())
const month = Number(today.slice(5, 7))
const coatFor = (department) =>
  department === "Warehouse"
    ? "warehouse-hi-vis"
    : ["Operations", "Logistics"].includes(department)
      ? "logistics-summer"
      : department === "Maintenance"
        ? month >= 4 && month <= 9
          ? "engineering-summer"
          : "engineering-winter"
        : department === "Quality Control"
          ? "quality-winter"
          : month >= 4 && month <= 9
            ? "production-summer"
            : "production-winter"
const items = coats.map((coat, index) => ({
  id: "item-" + index,
  sku: coat.id,
  name: coat.name,
  department: coat.department,
  season: coat.season,
  quantityOnHand: movements
    .filter((m) => m.coatType === coat.id)
    .reduce((sum, m) => sum + m.qty, 0),
  rowVersion: "visual",
}))
const permissions = [
  "employees.read",
  "employees.manage",
  "employees.import",
  "inventory.read",
  "inventory.manage",
  "coats.read",
  "coats.provide",
  "coats.dashboard.read",
  "platform.notifications.read",
  "platform.settings.read",
  "platform.settings.manage",
  "platform.users.manage",
]
const departments = [
  ...new Set(employees.map((e) => e.department).concat("Logistics")),
].map((name, index) => ({
  id: "department-" + index,
  name,
  code: name,
  isActive: true,
}))
const fixture = {
  businessDate: today,
  employees: employees.map((e, index) => {
    const months =
      (Number(today.slice(0, 4)) - Number(e.enrollmentDate.slice(0, 4))) * 12 +
      month -
      Number(e.enrollmentDate.slice(5, 7))
    const cycle = Math.max(0, Math.floor(months / 6))
    const requiredCoatId = coatFor(e.department)
    const fulfilled =
      e.lastProvidedCycle ?? (e.coatStatus === "provided" ? 1 : 0)
    const status = !cycle
      ? "none"
      : fulfilled >= cycle
        ? "provided"
        : items.find((i) => i.sku === requiredCoatId).quantityOnHand === 0
          ? "out_of_stock"
          : "requested"
    return {
      id: "employee-" + index,
      employeeNumber: e.id,
      fullName: e.name,
      departmentId: departments.find((d) => d.name === e.department).id,
      department: e.department,
      jobTitle: e.jobTitle,
      enrollmentDate: e.enrollmentDate,
      notes: e.notes,
      isActive: true,
      rowVersion: "visual",
      currentCycle: cycle,
      monthsEnrolled: months,
      coatStatus: status,
      requiredCoatId,
      requestId: "request-" + index,
      requestRowVersion: "visual",
    }
  }),
  items,
  movements,
  notifications,
  settings: {
    timeZoneId: "Africa/Casablanca",
    lowStockAlertsEnabled: true,
    lowStockThreshold: 15,
    rowVersion: "visual",
    companyName: "Lear Corporation",
  },
}

const server = await createServer({
  root: referenceRoot,
  configFile: false,
  plugins: [react(), tailwind()],
  resolve: {
    alias: {
      react: resolve(root, "frontend/node_modules/react"),
      "react-dom": resolve(root, "frontend/node_modules/react-dom"),
    },
  },
  server: {
    port: 8444,
    host: "127.0.0.1",
    strictPort: true,
    fs: { allow: [root] },
  },
})
let browser
try {
  await server.listen()
  browser = await chromium.launch({ headless: true })
  const original = await browser.newPage({
    viewport: { width: 1440, height: 1000 },
  })
  const restored = await browser.newPage({
    viewport: { width: 1440, height: 1000 },
  })
  const errors = []
  for (const page of [original, restored])
    page.on("pageerror", (e) => errors.push(e.message))
  await restored.route("**/api/**", async (route) => {
    const path = new URL(route.request().url()).pathname
    let body
    if (path === "/api/auth/refresh")
      return route.fulfill({
        status: 401,
        json: { title: "No visual session" },
      })
    if (path === "/api/auth/login")
      body = {
        accessToken: "visual",
        expiresAtUtc: "2099-01-01T00:00:00Z",
        refreshTokenExpiresAtUtc: "2099-01-01T00:00:00Z",
        email: "sarah.mitchell@company.com",
        fullName: "Sarah Mitchell",
        roles: ["HrAdministrator", "ClothingManager"],
        permissions,
      }
    else if (path === "/api/program/snapshot") body = fixture
    else if (path === "/api/departments") body = departments
    else if (path === "/api/settings") body = fixture.settings
    else if (path === "/api/notifications/unread-count") body = { unreadCount: fixture.notifications.filter(n => !n.read).length }
    else
      return route.fulfill({
        status: 404,
        json: { title: "Unused visual route" },
      })
    return route.fulfill({ json: body })
  })
  await original.goto("http://127.0.0.1:8444", { waitUntil: "networkidle" })
  await restored.goto("http://127.0.0.1:8443", { waitUntil: "networkidle" })
  await original.screenshot({ path: resolve(output, "reference-login.png") })
  await restored.screenshot({ path: resolve(output, "matched-login.png") })
  const demoEmail = source.match(
    /email\.trim\(\)\.toLowerCase\(\) !== "([^"]+)"/,
  )[1]
  const demoPassword = source.match(/password !== "([^"]+)"/)[1]
  for (const page of [original, restored]) {
    await page.locator('input[type="email"]').fill(demoEmail)
    await page.locator('input[type="password"]').fill(demoPassword)
    await page.getByRole("button", { name: "Sign in", exact: true }).click()
    if (page === restored) await page.getByRole("button", { name: "Clothing overview", exact: true }).click()
    await page
      .getByRole("heading", { name: "Dashboard", exact: true })
      .waitFor()
    await page.getByText("Total Employees", { exact: true }).waitFor()
  }
  const results = []
  const measure = (page) =>
    page.evaluate(() => ({
      heading: document.querySelector("h1").textContent.trim(),
      tableHeaders: [...document.querySelectorAll("main th")].map((e) =>
        e.textContent.trim(),
      ),
      boxes: [
        ...(
          document.querySelector("main > form > fieldset") ??
          document.querySelector("main").children[0]
        ).children,
      ]
        .filter((e) => e.getBoundingClientRect().width > 0)
        .map((e) => {
          const b = e.getBoundingClientRect()
          return { x: b.x, y: b.y, width: b.width, height: b.height }
        }),
    }))
  for (const [screen, label] of [
    ["dashboard", "Dashboard"],
    ["employees", "Employees"],
    ["inventory", "Inventory"],
    ["requests", "Requests"],
    ["notifications", "Notifications"],
    ["import", "Import Excel"],
    ["add-employee", "Add Employee"],
  ]) {
    if (screen === "add-employee") {
      await original
        .locator("nav button")
        .filter({ hasText: /^Employees/ })
        .click()
      await restored
        .getByRole("button", { name: "Employees", exact: true })
        .click()
      await original
        .locator("main")
        .getByRole("button", { name: "Add Employee", exact: true })
        .click()
      await restored
        .locator("main")
        .getByRole("button", { name: "Add Employee", exact: true })
        .click()
      await original
        .getByRole("heading", { name: "Add New Employee", exact: true })
        .waitFor()
      await restored
        .getByRole("heading", { name: "Add New Employee", exact: true })
        .waitFor()
      await original.locator('input[placeholder="EMP-001"]').fill("EMP-016")
      await restored.locator('input[placeholder="EMP-001"]').fill("EMP-016")
    } else if (screen !== "dashboard") {
      await original
        .locator("nav button")
        .filter({ hasText: new RegExp("^" + label) })
        .click()
      await restored.getByRole("button", { name: label, exact: true }).click()
      await restored
        .locator('nav button[aria-current="page"]')
        .filter({ hasText: label })
        .waitFor()
    }
    await original.mouse.move(1430, 20)
    await restored.mouse.move(1430, 20)
    await Promise.all([
      original.screenshot({
        path: resolve(output, "reference-" + screen + ".png"),
        fullPage: true,
      }),
      restored.screenshot({
        path: resolve(output, "matched-" + screen + ".png"),
        fullPage: true,
      }),
    ])
    const [before, after] = await Promise.all([
      measure(original),
      measure(restored),
    ])
    results.push({
      screen,
      before,
      after,
      tableHeadersMatch:
        JSON.stringify(before.tableHeaders) ===
        JSON.stringify(after.tableHeaders),
      layoutMatches:
        JSON.stringify(before.boxes) === JSON.stringify(after.boxes),
    })
  }
  if (errors.length) throw new Error("Browser errors: " + errors.join(", "))
  writeFileSync(
    resolve(output, "visual-comparison.json"),
    JSON.stringify(
      {
        businessDate: today,
        data: "Identical demo fixtures; API responses mocked only for this visual comparison.",
        results,
        errors,
      },
      null,
      2,
    ),
  )
  console.log(
    JSON.stringify(
      { screensCompared: results.length, browserErrors: errors.length, output },
      null,
      2,
    ),
  )
  const mismatches = results.filter(
    (result) => !result.layoutMatches || !result.tableHeadersMatch,
  )
  if (mismatches.length)
    throw new Error(
      "Screen layouts differ: " +
        mismatches.map((result) => result.screen).join(", "),
    )
} finally {
  await browser?.close()
  await server.close()
}
