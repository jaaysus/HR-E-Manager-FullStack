import assert from "node:assert/strict"
import test from "node:test"
import { readFile } from "node:fs/promises"
import ts from "typescript"

async function loadTs(path) {
  const source = await readFile(new URL(path, import.meta.url), "utf8")
  const { outputText } = ts.transpileModule(source, {
    compilerOptions: {
      target: ts.ScriptTarget.ES2022,
      module: ts.ModuleKind.ESNext,
    },
  })
  return import(
    "data:text/javascript;base64," + Buffer.from(outputText).toString("base64")
  )
}
const { ApiClient, ApiError } = await loadTs("../src/services/apiClient.ts")
const { navItems, screenPermission } = await loadTs("../src/app/navigation.ts")
const session = (token = "first", expired = false) => ({
  accessToken: token,
  expiresAtUtc: new Date(Date.now() + (expired ? -1000 : 60_000)).toISOString(),
  refreshTokenExpiresAtUtc: new Date(Date.now() + 3600_000).toISOString(),
  email: "user@example.test",
  fullName: "Test User",
  roles: ["Viewer"],
  permissions: ["employees.read"],
})
const response = (data, status = 200) =>
  new Response(data === undefined ? null : JSON.stringify(data), {
    status,
    headers: { "Content-Type": "application/json" },
  })
const deferred = () => {
  let resolve
  const promise = new Promise((done) => {
    resolve = done
  })
  return { promise, resolve }
}

test("transport is called without binding the API client as its receiver", async () => {
  let calls = 0
  const client = new ApiClient("/api", async function (url) {
    if (this !== undefined) throw new TypeError("Illegal invocation")
    calls++
    return response(url.endsWith("/login") ? session() : { ok: true })
  })
  await client.login("user", "password")
  assert.deepEqual(await client.request("/employees"), { ok: true })
  assert.equal(calls, 2)
})

test("a fresh client restores from cookies and duplicate startup calls share one refresh", async () => {
  const gate = deferred()
  let refreshes = 0
  const client = new ApiClient("/api", async (url, init) => {
    assert.equal(url, "/api/auth/refresh")
    assert.equal(init.method, "POST")
    assert.equal(init.credentials, "include")
    assert.equal(init.headers.get("X-HR-Auth"), "1")
    assert.equal(init.headers.has("Authorization"), false)
    assert.equal(init.body, undefined)
    refreshes++
    await gate.promise
    return response(session())
  })
  const first = client.restoreSession()
  const second = client.restoreSession()
  assert.equal(client.getSession(), null)
  gate.resolve()
  await Promise.all([first, second])
  assert.equal(refreshes, 1)
  assert.equal(client.getSession().email, "user@example.test")
  assert.equal("refreshToken" in client.getSession(), false)
})

test("expired cookie leaves the client signed out and a failed connection can be retried", async () => {
  let attempts = 0
  const client = new ApiClient("/api", async () => {
    attempts++
    if (attempts === 1) throw new TypeError("Failed to fetch")
    if (attempts === 2) return response({ title: "Expired session" }, 401)
    return response(session())
  })
  await assert.rejects(client.restoreSession(), (error) => error.status === 0)
  assert.equal(client.getSession(), null)
  await assert.rejects(client.restoreSession(), (error) => error.status === 401)
  assert.equal(client.getSession(), null)
  await client.restoreSession()
  assert.ok(client.getSession())
})

test("logout waits for startup cookie rotation before revoking the session", async () => {
  const gate = deferred()
  const calls = []
  const client = new ApiClient("/api", async (url, init) => {
    calls.push(url)
    if (url.endsWith("/refresh")) {
      await gate.promise
      return response(session())
    }
    assert.equal(init.credentials, "include")
    assert.equal(init.headers.get("X-HR-Auth"), "1")
    return response(undefined, 204)
  })
  const restoring = client.restoreSession()
  const logout = client.logout()
  assert.deepEqual(calls, ["/api/auth/refresh"])
  gate.resolve()
  await Promise.all([restoring, logout])
  assert.deepEqual(calls, ["/api/auth/refresh", "/api/auth/logout"])
  assert.equal(client.getSession(), null)
})

test("concurrent expired requests share one rotating refresh and use the new bearer token", async () => {
  let refreshes = 0
  const gate = deferred()
  const headers = []
  const client = new ApiClient("/api", async (url, init) => {
    if (url.endsWith("/login")) return response(session("old", true))
    if (url.endsWith("/refresh")) {
      refreshes++
      await gate.promise
      return response(session("new"))
    }
    headers.push(init.headers.get("Authorization"))
    return response({ persisted: true })
  })
  await client.login("user", "password")
  const first = client.request("/employees")
  const second = client.request("/inventory/items")
  gate.resolve()
  assert.deepEqual(await Promise.all([first, second]), [
    { persisted: true },
    { persisted: true },
  ])
  assert.equal(refreshes, 1)
  assert.deepEqual(headers, ["Bearer new", "Bearer new"])
})

test("a 401 rotates once and a second 401 clears the session", async () => {
  let refreshes = 0
  let requests = 0
  const client = new ApiClient("/api", async (url) => {
    if (url.endsWith("/login")) return response(session())
    if (url.endsWith("/refresh")) {
      refreshes++
      return response(session("new"))
    }
    requests++
    return response({ title: "Session revoked" }, 401)
  })
  await client.login("user", "password")
  await assert.rejects(
    client.request("/employees"),
    (error) => error.status === 401,
  )
  assert.equal(refreshes, 1)
  assert.equal(requests, 2)
  assert.equal(client.getSession(), null)
})

test("403 and 409 mutations are never replayed or refreshed", async () => {
  for (const status of [403, 409]) {
    let writes = 0
    const client = new ApiClient("/api", async (url) => {
      if (url.endsWith("/login")) return response(session())
      writes++
      return response({ detail: "Forbidden or stale record" }, status)
    })
    await client.login("user", "password")
    await assert.rejects(
      client.request("/inventory/adjustments", { method: "POST" }),
      (error) => error.status === status,
    )
    assert.equal(writes, 1)
    assert.ok(client.getSession())
  }
})

test("late refresh cannot restore a cleared session or send a pending mutation", async () => {
  const gate = deferred()
  let writes = 0
  const client = new ApiClient("/api", async (url) => {
    if (url.endsWith("/login")) return response(session("old", true))
    if (url.endsWith("/refresh")) {
      await gate.promise
      return response(session("new"))
    }
    writes++
    return response({})
  })
  await client.login("user", "password")
  const pending = client.request("/inventory/receipts", { method: "POST" })
  const rejected = assert.rejects(pending, (error) => error.status === 401)
  client.clearSession()
  gate.resolve()
  await rejected
  assert.equal(client.getSession(), null)
  assert.equal(writes, 0)
})

test("late data from a previous login is discarded", async () => {
  const gate = deferred()
  const client = new ApiClient("/api", async (url) => {
    if (url.endsWith("/login")) return response(session())
    await gate.promise
    return response({ employeeName: "previous session" })
  })
  await client.login("user", "password")
  const pending = client.request("/employees")
  const rejected = assert.rejects(pending, (error) => error.status === 401)
  client.clearSession()
  await client.login("different user", "password")
  gate.resolve()
  await rejected
  assert.ok(client.getSession())
})

test("logout revokes the server session and always clears local credentials", async () => {
  let logoutHeader
  const client = new ApiClient("/api", async (url, init) => {
    if (url.endsWith("/login")) return response(session())
    logoutHeader = init.headers.get("Authorization")
    return response({ title: "Server unavailable" }, 503)
  })
  await client.login("user", "password")
  await assert.rejects(client.logout())
  assert.equal(logoutHeader, "Bearer first")
  assert.equal(client.getSession(), null)
})

test("multipart uploads keep their boundary and validation problems expose field errors", async () => {
  const body = new FormData()
  body.append("file", new Blob(["Employee ID,Full Name"]), "employees.csv")
  const client = new ApiClient("/api", async (url, init) => {
    if (url.endsWith("/login")) return response(session())
    assert.equal(init.body, body)
    assert.equal(init.headers.has("Content-Type"), false)
    return response({ errors: { file: ["Missing required columns."] } }, 400)
  })
  await client.login("user", "password")
  await assert.rejects(
    client.request("/employee-imports", { method: "POST", body }),
    (error) => {
      assert.ok(error instanceof ApiError)
      assert.equal(error.message, "Missing required columns.")
      assert.deepEqual(error.errors, { file: ["Missing required columns."] })
      return true
    },
  )
})

test("workspace features carry explicit permissions and employee forms require write access", () => {
  const modules = new Set(navItems.map((item) => item.module))
  assert.deepEqual(
    modules,
    new Set(["Employees", "Clothing tracking", "Workspace"]),
  )
  const viewerPermissions = [
    "employees.read",
    "platform.notifications.read",
    "platform.settings.read",
  ]
  assert.deepEqual(
    navItems
      .filter((item) => viewerPermissions.includes(item.permission))
      .map((item) => item.id),
    [
      "employees",
      "notifications",
      "settings",
    ],
  )
  assert.equal(screenPermission("add-employee"), "employees.manage")
  assert.equal(screenPermission("users"), "platform.users.manage")
})
