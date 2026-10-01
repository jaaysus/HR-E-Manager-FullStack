import { useState, useMemo } from "react";

// ─── Types ────────────────────────────────────────────────────────────────────
type CoatStatus = "none" | "eligible" | "requested" | "provided" | "out_of_stock";
type CoatTypeId = "production-winter" | "production-summer" | "warehouse-hi-vis" | "logistics-summer" | "engineering-winter" | "engineering-summer" | "quality-winter";

interface CoatType {
  id: CoatTypeId;
  name: string;
  department: string;
  season: string;
  color: string;
  softColor: string;
  icon: string;
}

interface Employee {
  id: string;
  name: string;
  department: string;
  jobTitle: string;
  enrollmentDate: string;
  coatStatus: CoatStatus;
  /** The six-month cycle most recently fulfilled for this employee. */
  lastProvidedCycle?: number;
  notes: string;
}

interface StockMovement {
  id: string;
  date: string;
  type: "arrival" | "allocation";
  qty: number;
  coatType: CoatTypeId;
  note: string;
}

interface Notification {
  id: string;
  message: string;
  date: string;
  read: boolean;
  type: "info" | "warning" | "success";
}

type Screen =
  | "login"
  | "dashboard"
  | "employees"
  | "add-employee"
  | "import"
  | "inventory"
  | "requests"
  | "notifications"
  | "settings";

// ─── Seed Data ────────────────────────────────────────────────────────────────
const seedEmployees: Employee[] = [
  { id: "EMP-001", name: "Margaret Holloway", department: "Operations", jobTitle: "Line Supervisor", enrollmentDate: "2024-01-15", coatStatus: "provided", notes: "" },
  { id: "EMP-002", name: "James Okafor", department: "Warehouse", jobTitle: "Forklift Operator", enrollmentDate: "2024-02-01", coatStatus: "provided", notes: "" },
  { id: "EMP-003", name: "Sofia Hernandez", department: "Production", jobTitle: "Assembly Tech", enrollmentDate: "2024-03-10", coatStatus: "requested", notes: "Preferred size: M" },
  { id: "EMP-004", name: "Daniel Park", department: "Warehouse", jobTitle: "Inventory Clerk", enrollmentDate: "2024-03-22", coatStatus: "eligible", notes: "" },
  { id: "EMP-005", name: "Amara Diallo", department: "Quality Control", jobTitle: "QC Inspector", enrollmentDate: "2024-04-05", coatStatus: "eligible", notes: "" },
  { id: "EMP-006", name: "Ryan Kowalski", department: "Maintenance", jobTitle: "Technician", enrollmentDate: "2024-05-01", coatStatus: "none", notes: "" },
  { id: "EMP-007", name: "Priya Nair", department: "Operations", jobTitle: "Shift Lead", enrollmentDate: "2024-01-08", coatStatus: "provided", notes: "" },
  { id: "EMP-008", name: "Carlos Mendes", department: "Production", jobTitle: "Machine Operator", enrollmentDate: "2024-02-20", coatStatus: "provided", notes: "" },
  { id: "EMP-009", name: "Fatima Al-Hassan", department: "Warehouse", jobTitle: "Receiving Clerk", enrollmentDate: "2024-03-15", coatStatus: "requested", notes: "Preferred size: S" },
  { id: "EMP-010", name: "Thomas Grant", department: "Quality Control", jobTitle: "Senior Inspector", enrollmentDate: "2024-04-18", coatStatus: "none", notes: "" },
  { id: "EMP-011", name: "Yuki Tanaka", department: "Maintenance", jobTitle: "Electrical Tech", enrollmentDate: "2024-01-25", coatStatus: "provided", notes: "" },
  { id: "EMP-012", name: "Lucia Fernandez", department: "Production", jobTitle: "Line Operator", enrollmentDate: "2024-06-01", coatStatus: "none", notes: "" },
  { id: "EMP-013", name: "Mohammed Al-Rashid", department: "Operations", jobTitle: "Logistics Coord.", enrollmentDate: "2024-05-12", coatStatus: "none", notes: "" },
  { id: "EMP-014", name: "Emma Blackwood", department: "Warehouse", jobTitle: "Stock Controller", enrollmentDate: "2024-02-10", coatStatus: "out_of_stock", notes: "Waiting for next shipment" },
  { id: "EMP-015", name: "Nathan Rivers", department: "Quality Control", jobTitle: "QC Analyst", enrollmentDate: "2024-03-30", coatStatus: "eligible", notes: "" },
];

const COAT_TYPES: CoatType[] = [
  { id: "production-winter", name: "Production Coat", department: "Production", season: "Winter", color: "#dc2626", softColor: "#fef2f2", icon: "🧥" },
  { id: "production-summer", name: "Production Coat", department: "Production", season: "Summer", color: "#ef4444", softColor: "#fff1f2", icon: "🧥" },
  { id: "warehouse-hi-vis", name: "Hi-Visibility Coat", department: "Warehouse", season: "All-season", color: "#ca8a04", softColor: "#fefce8", icon: "🦺" },
  { id: "logistics-summer", name: "Logistics Coat", department: "Logistics", season: "Summer", color: "#ea580c", softColor: "#fff7ed", icon: "🧥" },
  { id: "engineering-winter", name: "Engineering Coat", department: "Maintenance", season: "Winter", color: "#2563eb", softColor: "#eff6ff", icon: "🧥" },
  { id: "engineering-summer", name: "Engineering Coat", department: "Maintenance", season: "Summer", color: "#0ea5e9", softColor: "#f0f9ff", icon: "🧥" },
  { id: "quality-winter", name: "Quality Coat", department: "Quality Control", season: "Winter", color: "#16a34a", softColor: "#f0fdf4", icon: "🧥" },
];

const seedMovements: StockMovement[] = [
  { id: "MOV-001", date: "2026-09-01", type: "arrival", qty: 14, coatType: "production-winter", note: "Seasonal production delivery" },
  { id: "MOV-002", date: "2026-09-01", type: "arrival", qty: 22, coatType: "production-summer", note: "Seasonal production delivery" },
  { id: "MOV-003", date: "2026-09-02", type: "arrival", qty: 0, coatType: "warehouse-hi-vis", note: "Hi-vis variant awaiting delivery" },
  { id: "MOV-004", date: "2026-09-03", type: "arrival", qty: 9, coatType: "logistics-summer", note: "Logistics replenishment" },
  { id: "MOV-005", date: "2026-09-04", type: "arrival", qty: 7, coatType: "engineering-winter", note: "Engineering winter delivery" },
  { id: "MOV-006", date: "2026-09-04", type: "arrival", qty: 13, coatType: "engineering-summer", note: "Engineering summer delivery" },
  { id: "MOV-007", date: "2026-09-05", type: "arrival", qty: 5, coatType: "quality-winter", note: "Quality winter delivery" },
];

const seedNotifications: Notification[] = [
  { id: "N-001", message: "5 coat requests were created automatically at their six-month anniversary.", date: "2024-09-14", read: false, type: "info" },
  { id: "N-002", message: "Inventory is running low — only 14 coats remaining.", date: "2024-09-13", read: false, type: "warning" },
  { id: "N-003", message: "Coat successfully assigned to Sofia Hernandez (EMP-003).", date: "2024-09-12", read: true, type: "success" },
  { id: "N-004", message: "Coat successfully assigned to Fatima Al-Hassan (EMP-009).", date: "2024-09-12", read: true, type: "success" },
  { id: "N-005", message: "New stock of 20 coats arrived from ProGear Ltd.", date: "2024-09-01", read: true, type: "success" },
  { id: "N-006", message: "Emma Blackwood (EMP-014) marked as out-of-stock — follow up required.", date: "2024-08-30", read: true, type: "warning" },
];

// ─── Helpers ──────────────────────────────────────────────────────────────────
function monthsSince(dateStr: string): number {
  const start = new Date(dateStr);
  const now = new Date();
  return (
    (now.getFullYear() - start.getFullYear()) * 12 +
    (now.getMonth() - start.getMonth())
  );
}

function isEligible(emp: Employee): boolean {
  return getRequestCycle(emp.enrollmentDate) > 0;
}

function getRequestCycle(enrollmentDate: string): number {
  return Math.floor(monthsSince(enrollmentDate) / 6);
}

function getEmployeeRequestStatus(emp: Employee): CoatStatus {
  const currentCycle = getRequestCycle(emp.enrollmentDate);
  if (currentCycle === 0) return "none";

  // Legacy fulfilled records are treated as completing the first cycle.
  const fulfilledCycle = emp.lastProvidedCycle ?? (emp.coatStatus === "provided" ? 1 : 0);
  if (fulfilledCycle >= currentCycle) return "provided";
  return "requested";
}

const STATUS_META: Record<CoatStatus, { label: string; color: string }> = {
  none: { label: "Not Eligible", color: "bg-slate-100 text-slate-500" },
  eligible: { label: "Eligible", color: "bg-blue-100 text-blue-700" },
  requested: { label: "Auto Request", color: "bg-amber-100 text-amber-700" },
  provided: { label: "Cycle Completed", color: "bg-green-100 text-green-700" },
  out_of_stock: { label: "Out of Stock", color: "bg-red-100 text-red-700" },
};

function Badge({ status }: { status: CoatStatus }) {
  const m = STATUS_META[status];
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium ${m.color}`}>
      {m.label}
    </span>
  );
}

function getCoatType(employee: Employee): CoatType {
  const isSummer = new Date().getMonth() >= 3 && new Date().getMonth() <= 8;
  if (employee.department === "Warehouse") return COAT_TYPES[2];
  if (employee.department === "Logistics" || employee.department === "Operations") return COAT_TYPES[3];
  if (employee.department === "Maintenance") return isSummer ? COAT_TYPES[5] : COAT_TYPES[4];
  if (employee.department === "Quality Control") return COAT_TYPES[6];
  return isSummer ? COAT_TYPES[1] : COAT_TYPES[0];
}

function calcStock(movements: StockMovement[], coatType?: CoatTypeId): number {
  return movements
    .filter(m => !coatType || m.coatType === coatType)
    .reduce((acc, m) => acc + m.qty, 0);
}

function getRequestDisplayStatus(employee: Employee, movements: StockMovement[]): CoatStatus {
  const status = getEmployeeRequestStatus(employee);
  return status === "requested" && calcStock(movements, getCoatType(employee).id) === 0 ? "out_of_stock" : status;
}

// ─── Nav Icons (inline SVG) ───────────────────────────────────────────────────
const icons = {
  dashboard: (
    <svg width="18" height="18" fill="none" stroke="currentColor" strokeWidth="1.8" viewBox="0 0 24 24">
      <rect x="3" y="3" width="7" height="7" rx="1" /><rect x="14" y="3" width="7" height="7" rx="1" />
      <rect x="3" y="14" width="7" height="7" rx="1" /><rect x="14" y="14" width="7" height="7" rx="1" />
    </svg>
  ),
  employees: (
    <svg width="18" height="18" fill="none" stroke="currentColor" strokeWidth="1.8" viewBox="0 0 24 24">
      <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2" /><circle cx="9" cy="7" r="4" />
      <path d="M23 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75" />
    </svg>
  ),
  inventory: (
    <svg width="18" height="18" fill="none" stroke="currentColor" strokeWidth="1.8" viewBox="0 0 24 24">
      <path d="M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z" />
      <polyline points="3.27 6.96 12 12.01 20.73 6.96" /><line x1="12" y1="22.08" x2="12" y2="12" />
    </svg>
  ),
  requests: (
    <svg width="18" height="18" fill="none" stroke="currentColor" strokeWidth="1.8" viewBox="0 0 24 24">
      <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
      <polyline points="14 2 14 8 20 8" /><line x1="16" y1="13" x2="8" y2="13" /><line x1="16" y1="17" x2="8" y2="17" /><polyline points="10 9 9 9 8 9" />
    </svg>
  ),
  notifications: (
    <svg width="18" height="18" fill="none" stroke="currentColor" strokeWidth="1.8" viewBox="0 0 24 24">
      <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9" /><path d="M13.73 21a2 2 0 0 1-3.46 0" />
    </svg>
  ),
  settings: (
    <svg width="18" height="18" fill="none" stroke="currentColor" strokeWidth="1.8" viewBox="0 0 24 24">
      <circle cx="12" cy="12" r="3" />
      <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83-2.83l.06-.06A1.65 1.65 0 0 0 4.68 15a1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 2.83-2.83l.06.06A1.65 1.65 0 0 0 9 4.68a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 2.83l-.06.06A1.65 1.65 0 0 0 19.4 9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z" />
    </svg>
  ),
  import: (
    <svg width="18" height="18" fill="none" stroke="currentColor" strokeWidth="1.8" viewBox="0 0 24 24">
      <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" /><polyline points="17 8 12 3 7 8" /><line x1="12" y1="3" x2="12" y2="15" />
    </svg>
  ),
};

// ─── Sidebar ──────────────────────────────────────────────────────────────────
const navItems = [
  { id: "dashboard", label: "Dashboard", icon: icons.dashboard },
  { id: "employees", label: "Employees", icon: icons.employees },
  { id: "import", label: "Import Excel", icon: icons.import },
  { id: "inventory", label: "Inventory", icon: icons.inventory },
  { id: "requests", label: "Requests", icon: icons.requests },
  { id: "notifications", label: "Notifications", icon: icons.notifications },
  { id: "settings", label: "Settings", icon: icons.settings },
];

function LearLogo({ light = false, compact = false }: { light?: boolean; compact?: boolean }) {
  const color = light ? "#ffffff" : "#005daa";
  return (
    <div className="flex items-start" aria-label="Lear Corporation">
      <span
        style={{
          color,
          fontFamily: "Arial, Helvetica, sans-serif",
          fontSize: compact ? 28 : 34,
          fontWeight: 800,
          fontStyle: "italic",
          letterSpacing: "-0.09em",
          lineHeight: 0.9,
        }}
      >
        LEAR
      </span>
      <sup style={{ color, fontSize: compact ? 7 : 8, fontWeight: 700, marginLeft: 3, marginTop: -2 }}>®</sup>
    </div>
  );
}

function Sidebar({
  current,
  onNav,
  unreadCount,
}: {
  current: Screen;
  onNav: (s: Screen) => void;
  unreadCount: number;
}) {
  return (
    <aside className="flex flex-col" style={{ width: 220, minHeight: "100vh", background: "#1e293b" }}>
      {/* Lear brand */}
      <div className="px-6 py-5 border-b" style={{ borderColor: "#334155" }}>
        <div className="flex items-center gap-2.5">
          <div>
            <LearLogo light compact />
            <div style={{ fontSize: 10, color: "#94a3b8", letterSpacing: "0.08em", marginTop: 4 }}>HR E-TRACKER</div>
          </div>
        </div>
      </div>

      {/* Nav */}
      <nav className="flex-1 px-3 py-4 flex flex-col gap-0.5">
        {navItems.map((item) => {
          const active = current === item.id;
          return (
            <button
              key={item.id}
              onClick={() => onNav(item.id as Screen)}
              className="flex items-center gap-3 px-3 py-2.5 rounded-lg w-full text-left transition-all relative"
              style={{
                background: active ? "#2563eb" : "transparent",
                color: active ? "#ffffff" : "#94a3b8",
                fontSize: 13.5,
                fontWeight: active ? 600 : 400,
              }}
              onMouseEnter={(e) => {
                if (!active) e.currentTarget.style.background = "#334155";
              }}
              onMouseLeave={(e) => {
                if (!active) e.currentTarget.style.background = "transparent";
              }}
            >
              {item.icon}
              <span style={{ fontFamily: "Inter, sans-serif" }}>{item.label}</span>
              {item.id === "notifications" && unreadCount > 0 && (
                <span
                  className="ml-auto flex items-center justify-center rounded-full text-white text-xs font-bold"
                  style={{ background: "#ef4444", minWidth: 18, height: 18, fontSize: 10, padding: "0 4px" }}
                >
                  {unreadCount}
                </span>
              )}
            </button>
          );
        })}
      </nav>

      {/* User */}
      <div className="px-4 py-4 border-t" style={{ borderColor: "#334155" }}>
        <div className="flex items-center gap-3">
          <div
            className="flex items-center justify-center rounded-full text-white font-semibold text-sm"
            style={{ width: 32, height: 32, background: "#475569", flexShrink: 0 }}
          >
            HR
          </div>
          <div>
            <div style={{ fontSize: 13, fontWeight: 600, color: "#f1f5f9" }}>Sarah Mitchell</div>
            <div style={{ fontSize: 11, color: "#64748b" }}>HR Administrator</div>
          </div>
        </div>
      </div>
    </aside>
  );
}

// ─── Header ───────────────────────────────────────────────────────────────────
function Header({ title, subtitle }: { title: string; subtitle?: string }) {
  return (
    <div
      className="flex items-center justify-between px-8 py-4"
      style={{ background: "#ffffff", borderBottom: "1px solid #e2e8f0" }}
    >
      <div>
        <h1 style={{ fontFamily: "Outfit, sans-serif", fontSize: 20, fontWeight: 700, color: "#0f172a" }}>
          {title}
        </h1>
        {subtitle && <p style={{ fontSize: 13, color: "#64748b", marginTop: 2 }}>{subtitle}</p>}
      </div>
      <div className="flex items-center gap-3">
        <div style={{ fontSize: 12, color: "#94a3b8" }}>Mon, 15 Sep 2024</div>
        <div
          className="flex items-center justify-center rounded-full text-white text-sm font-semibold"
          style={{ width: 34, height: 34, background: "#2563eb" }}
        >
          SM
        </div>
      </div>
    </div>
  );
}

// ─── KPI Card ─────────────────────────────────────────────────────────────────
function KpiCard({
  label,
  value,
  sub,
  color,
  icon,
}: {
  label: string;
  value: string | number;
  sub?: string;
  color: string;
  icon: React.ReactNode;
}) {
  return (
    <div
      className="rounded-xl p-5 flex flex-col gap-3"
      style={{ background: "#ffffff", border: "1px solid #e2e8f0" }}
    >
      <div className="flex items-start justify-between">
        <div
          className="flex items-center justify-center rounded-lg"
          style={{ width: 40, height: 40, background: color + "18" }}
        >
          <span style={{ color }}>{icon}</span>
        </div>
      </div>
      <div>
        <div style={{ fontFamily: "Outfit, sans-serif", fontSize: 28, fontWeight: 700, color: "#0f172a", lineHeight: 1 }}>
          {value}
        </div>
        <div style={{ fontSize: 13, color: "#64748b", marginTop: 4 }}>{label}</div>
        {sub && <div style={{ fontSize: 11, color: color, marginTop: 3, fontWeight: 500 }}>{sub}</div>}
      </div>
    </div>
  );
}

// ─── Mini Bar Chart ───────────────────────────────────────────────────────────
function MiniBarChart() {
  const months = ["Apr", "May", "Jun", "Jul", "Aug", "Sep"];
  const values = [2, 1, 4, 3, 5, 5];
  const max = Math.max(...values);
  return (
    <div style={{ background: "#ffffff", border: "1px solid #e2e8f0", borderRadius: 12, padding: 20 }}>
      <div style={{ fontFamily: "Outfit, sans-serif", fontWeight: 600, fontSize: 14, color: "#0f172a", marginBottom: 4 }}>
        Coats Issued per Month
      </div>
      <div style={{ fontSize: 12, color: "#94a3b8", marginBottom: 16 }}>April – September 2024</div>
      <div className="flex items-end gap-2" style={{ height: 80 }}>
        {months.map((m, i) => (
          <div key={m} className="flex flex-col items-center gap-1 flex-1">
            <div
              className="w-full rounded-sm transition-all"
              style={{
                height: Math.max(4, (values[i] / max) * 70),
                background: i === months.length - 1 ? "#2563eb" : "#bfdbfe",
                borderRadius: 3,
              }}
            />
            <span style={{ fontSize: 10, color: "#94a3b8" }}>{m}</span>
          </div>
        ))}
      </div>
    </div>
  );
}

// ─── Login Screen ─────────────────────────────────────────────────────────────
function LoginScreen({ onLogin }: { onLogin: () => void }) {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError("");
    if (email.trim().toLowerCase() !== "admin@mail.com" || password !== "Lear123456@") {
      setError("Invalid email address or password.");
      return;
    }
    setLoading(true);
    setTimeout(() => { setLoading(false); onLogin(); }, 900);
  }

  return (
    <div
      className="min-h-screen flex"
      style={{ background: "linear-gradient(135deg, #f8fafc 0%, #e2e8f0 100%)" }}
    >
      {/* Left panel */}
      <div
        className="hidden lg:flex flex-col justify-between p-12"
        style={{ width: 440, background: "#1e293b", flexShrink: 0 }}
      >
        <div>
          <div className="flex items-center gap-3 mb-12">
            <div>
              <LearLogo light />
              <div style={{ fontSize: 11, color: "#94a3b8", letterSpacing: "0.08em", marginTop: 6 }}>HR E-TRACKER</div>
            </div>
          </div>
          <h2 style={{ fontFamily: "Outfit, sans-serif", fontSize: 32, fontWeight: 700, color: "#f1f5f9", lineHeight: 1.25, marginBottom: 16 }}>
            Streamline your<br />workwear program.
          </h2>
          <p style={{ fontSize: 14, color: "#94a3b8", lineHeight: 1.7 }}>
            Track employee eligibility, manage coat inventory, and automate requests — all in one place.
          </p>
        </div>
        <div className="flex flex-col gap-3">
          {["Automated 6-month eligibility tracking", "Real-time inventory management", "Bulk employee import via Excel"].map(f => (
            <div key={f} className="flex items-center gap-3">
              <div className="rounded-full flex items-center justify-center" style={{ width: 20, height: 20, background: "#16a34a18", flexShrink: 0 }}>
                <svg width="11" height="11" viewBox="0 0 11 11" fill="none">
                  <path d="M2 5.5L4.5 8L9 3" stroke="#16a34a" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
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
            <h1 style={{ fontFamily: "Outfit, sans-serif", fontSize: 26, fontWeight: 700, color: "#0f172a", marginBottom: 6 }}>
              Sign in to your account
            </h1>
            <p style={{ fontSize: 13.5, color: "#64748b" }}>Enter your HR credentials to continue.</p>
          </div>

          <form onSubmit={handleSubmit} className="flex flex-col gap-4">
            <div>
              <label style={{ fontSize: 13, fontWeight: 500, color: "#374151", display: "block", marginBottom: 6 }}>
                Email address
              </label>
              <input
                type="email"
                value={email}
                onChange={e => { setEmail(e.target.value); setError(""); }}
                placeholder="admin@mail.com"
                autoComplete="email"
                className="w-full rounded-lg px-3.5 py-2.5"
                style={{ border: "1px solid #d1d5db", fontSize: 14, outline: "none", color: "#0f172a" }}
                onFocus={e => (e.target.style.border = "1.5px solid #2563eb")}
                onBlur={e => (e.target.style.border = "1px solid #d1d5db")}
              />
            </div>
            <div>
              <label style={{ fontSize: 13, fontWeight: 500, color: "#374151", display: "block", marginBottom: 6 }}>
                Password
              </label>
              <input
                type="password"
                value={password}
                onChange={e => { setPassword(e.target.value); setError(""); }}
                placeholder="Enter your password"
                autoComplete="current-password"
                className="w-full rounded-lg px-3.5 py-2.5"
                style={{ border: "1px solid #d1d5db", fontSize: 14, outline: "none", color: "#0f172a" }}
                onFocus={e => (e.target.style.border = "1.5px solid #2563eb")}
                onBlur={e => (e.target.style.border = "1px solid #d1d5db")}
              />
            </div>
            {error && (
              <p role="alert" style={{ fontSize: 12.5, color: "#dc2626", marginTop: -4 }}>
                {error}
              </p>
            )}
            <button
              type="submit"
              disabled={loading}
              className="w-full rounded-lg py-2.5 text-white font-semibold transition-opacity"
              style={{ background: "#2563eb", fontSize: 14, opacity: loading ? 0.7 : 1, cursor: loading ? "wait" : "pointer", fontFamily: "Outfit, sans-serif" }}
            >
              {loading ? "Signing in…" : "Sign in"}
            </button>
          </form>

          <div style={{ marginTop: 20, padding: "12px 14px", borderRadius: 8, background: "#eff6ff", border: "1px solid #bfdbfe" }}>
            <p style={{ fontSize: 12, fontWeight: 600, color: "#1d4ed8", marginBottom: 3 }}>Mock account</p>
            <p style={{ fontSize: 12, color: "#475569" }}>Use the administrator credentials supplied for this demo.</p>
          </div>

          <p style={{ fontSize: 12, color: "#94a3b8", marginTop: 24, textAlign: "center" }}>
            Lear® HR E-Tracker v2.4
          </p>
        </div>
      </div>
    </div>
  );
}

// ─── Dashboard ────────────────────────────────────────────────────────────────
function Dashboard({
  employees,
  notifications,
  stock,
  onNav,
}: {
  employees: Employee[];
  notifications: Notification[];
  stock: number;
  onNav: (s: Screen) => void;
}) {
  const eligible = employees.filter(e => isEligible(e)).length;
  const pending = employees.filter(e => getEmployeeRequestStatus(e) === "requested").length;
  const recent = notifications.slice(0, 4);

  return (
    <div className="flex flex-col gap-6 p-8">
      {/* KPI grid */}
      <div className="grid grid-cols-4 gap-4">
        <KpiCard
          label="Total Employees"
          value={employees.length}
          sub="Active this month"
          color="#2563eb"
          icon={
            <svg width="20" height="20" fill="none" stroke="currentColor" strokeWidth="2" viewBox="0 0 24 24">
              <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2" /><circle cx="9" cy="7" r="4" />
              <path d="M23 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75" />
            </svg>
          }
        />
        <KpiCard
          label="Employees in Request Cycle"
          value={eligible}
          sub="Automatic every 6 months"
          color="#7c3aed"
          icon={
            <svg width="20" height="20" fill="none" stroke="currentColor" strokeWidth="2" viewBox="0 0 24 24">
              <circle cx="12" cy="12" r="10" /><polyline points="12 6 12 12 16 14" />
            </svg>
          }
        />
        <KpiCard
          label="Auto-Created Requests"
          value={pending}
          sub="Awaiting provision"
          color="#d97706"
          icon={
            <svg width="20" height="20" fill="none" stroke="currentColor" strokeWidth="2" viewBox="0 0 24 24">
              <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
              <polyline points="14 2 14 8 20 8" />
            </svg>
          }
        />
        <KpiCard
          label="Available Inventory"
          value={stock}
          sub={stock < 15 ? "⚠ Low stock alert" : "Units in stock"}
          color={stock < 15 ? "#dc2626" : "#16a34a"}
          icon={
            <svg width="20" height="20" fill="none" stroke="currentColor" strokeWidth="2" viewBox="0 0 24 24">
              <path d="M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z" />
            </svg>
          }
        />
      </div>

      <div className="grid gap-4" style={{ gridTemplateColumns: "1fr 340px" }}>
        {/* Recent notifications */}
        <div style={{ background: "#ffffff", border: "1px solid #e2e8f0", borderRadius: 12, padding: 20 }}>
          <div className="flex items-center justify-between mb-4">
            <h3 style={{ fontFamily: "Outfit, sans-serif", fontWeight: 600, fontSize: 14, color: "#0f172a" }}>
              Recent Activity
            </h3>
            <button
              onClick={() => onNav("notifications")}
              style={{ fontSize: 12, color: "#2563eb", fontWeight: 500 }}
            >
              View all →
            </button>
          </div>
          <div className="flex flex-col gap-2">
            {recent.map(n => (
              <div
                key={n.id}
                className="flex items-start gap-3 p-3 rounded-lg"
                style={{ background: n.read ? "#f8fafc" : "#eff6ff" }}
              >
                <div
                  className="rounded-full flex-shrink-0 flex items-center justify-center mt-0.5"
                  style={{
                    width: 28, height: 28,
                    background: n.type === "warning" ? "#fef3c7" : n.type === "success" ? "#dcfce7" : "#dbeafe",
                  }}
                >
                  <div
                    style={{
                      width: 8, height: 8, borderRadius: "50%",
                      background: n.type === "warning" ? "#d97706" : n.type === "success" ? "#16a34a" : "#2563eb",
                    }}
                  />
                </div>
                <div className="flex-1 min-w-0">
                  <p style={{ fontSize: 13, color: "#1e293b", lineHeight: 1.5 }}>{n.message}</p>
                  <p style={{ fontSize: 11, color: "#94a3b8", marginTop: 2 }}>{n.date}</p>
                </div>
                {!n.read && (
                  <div style={{ width: 7, height: 7, borderRadius: "50%", background: "#2563eb", flexShrink: 0, marginTop: 4 }} />
                )}
              </div>
            ))}
          </div>
        </div>

        {/* Right column: chart + quick actions */}
        <div className="flex flex-col gap-4">
          <MiniBarChart />

          <div style={{ background: "#ffffff", border: "1px solid #e2e8f0", borderRadius: 12, padding: 20 }}>
            <h3 style={{ fontFamily: "Outfit, sans-serif", fontWeight: 600, fontSize: 14, color: "#0f172a", marginBottom: 12 }}>
              Quick Actions
            </h3>
            <div className="flex flex-col gap-2">
              {[
                { label: "Add Employee", screen: "add-employee", color: "#2563eb" },
                { label: "Import from Excel", screen: "import", color: "#7c3aed" },
                { label: "Add Stock Arrival", screen: "inventory", color: "#16a34a" },
                { label: "View Coat Requests", screen: "requests", color: "#d97706" },
              ].map(a => (
                <button
                  key={a.label}
                  onClick={() => onNav(a.screen as Screen)}
                  className="w-full text-left px-3.5 py-2.5 rounded-lg font-medium transition-colors"
                  style={{ fontSize: 13, color: a.color, background: a.color + "10", border: `1px solid ${a.color}20` }}
                  onMouseEnter={e => (e.currentTarget.style.background = a.color + "1e")}
                  onMouseLeave={e => (e.currentTarget.style.background = a.color + "10")}
                >
                  {a.label}
                </button>
              ))}
            </div>
          </div>
        </div>
      </div>

      {/* Status breakdown */}
      <div style={{ background: "#ffffff", border: "1px solid #e2e8f0", borderRadius: 12, padding: 20 }}>
        <h3 style={{ fontFamily: "Outfit, sans-serif", fontWeight: 600, fontSize: 14, color: "#0f172a", marginBottom: 16 }}>
          Employee Coat Status Overview
        </h3>
        <div className="flex gap-8">
          {(["none", "requested", "provided", "out_of_stock"] as CoatStatus[]).map(s => {
            const count = employees.filter(e => getEmployeeRequestStatus(e) === s).length;
            const meta = STATUS_META[s];
            const pct = Math.round((count / employees.length) * 100);
            return (
              <div key={s} className="flex flex-col gap-1 flex-1">
                <div className="flex items-center justify-between mb-1">
                  <span style={{ fontSize: 12, color: "#64748b" }}>{meta.label}</span>
                  <span style={{ fontSize: 12, fontWeight: 600, color: "#0f172a" }}>{count}</span>
                </div>
                <div style={{ height: 6, borderRadius: 3, background: "#f1f5f9" }}>
                  <div
                    style={{
                      height: "100%", borderRadius: 3,
                      width: `${pct}%`,
                      background: s === "provided" ? "#16a34a" : s === "requested" ? "#d97706" : s === "out_of_stock" ? "#dc2626" : "#94a3b8",
                    }}
                  />
                </div>
                <span style={{ fontSize: 11, color: "#94a3b8" }}>{pct}%</span>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}

// ─── Employees ────────────────────────────────────────────────────────────────
function EmployeeList({
  employees,
  movements,
  onAdd,
  onEdit,
}: {
  employees: Employee[];
  movements: StockMovement[];
  onAdd: () => void;
  onEdit: (emp: Employee) => void;
}) {
  const [search, setSearch] = useState("");
  const [deptFilter, setDeptFilter] = useState("All");
  const [statusFilter, setStatusFilter] = useState<"All" | CoatStatus>("All");

  const depts = ["All", ...Array.from(new Set(employees.map(e => e.department))).sort()];

  const filtered = useMemo(() => {
    return employees.filter(e => {
      const matchSearch =
        search === "" ||
        e.name.toLowerCase().includes(search.toLowerCase()) ||
        e.id.toLowerCase().includes(search.toLowerCase()) ||
        e.department.toLowerCase().includes(search.toLowerCase());
      const matchDept = deptFilter === "All" || e.department === deptFilter;
      const matchStatus = statusFilter === "All" || getRequestDisplayStatus(e, movements) === statusFilter;
      return matchSearch && matchDept && matchStatus;
    });
  }, [employees, movements, search, deptFilter, statusFilter]);

  return (
    <div className="flex flex-col gap-5 p-8">
      {/* Toolbar */}
      <div className="flex items-center gap-3">
        <div className="relative flex-1" style={{ maxWidth: 320 }}>
          <svg className="absolute left-3 top-1/2 -translate-y-1/2" width="15" height="15" fill="none" stroke="#94a3b8" strokeWidth="2" viewBox="0 0 24 24">
            <circle cx="11" cy="11" r="8" /><path d="M21 21l-4.35-4.35" />
          </svg>
          <input
            type="text"
            placeholder="Search employees…"
            value={search}
            onChange={e => setSearch(e.target.value)}
            className="w-full rounded-lg pl-9 pr-3.5 py-2"
            style={{ border: "1px solid #e2e8f0", fontSize: 13.5, outline: "none", color: "#0f172a", background: "#fff" }}
          />
        </div>

        <select
          value={deptFilter}
          onChange={e => setDeptFilter(e.target.value)}
          className="rounded-lg px-3 py-2"
          style={{ border: "1px solid #e2e8f0", fontSize: 13, color: "#374151", background: "#fff", outline: "none" }}
        >
          {depts.map(d => <option key={d}>{d}</option>)}
        </select>

        <select
          value={statusFilter}
          onChange={e => setStatusFilter(e.target.value as any)}
          className="rounded-lg px-3 py-2"
          style={{ border: "1px solid #e2e8f0", fontSize: 13, color: "#374151", background: "#fff", outline: "none" }}
        >
          <option value="All">All Statuses</option>
          <option value="none">Not Eligible</option>
          <option value="requested">Auto Request</option>
          <option value="provided">Cycle Completed</option>
          <option value="out_of_stock">Out of Stock</option>
        </select>

        <div className="flex-1" />
        <button
          onClick={onAdd}
          className="flex items-center gap-2 px-4 py-2 rounded-lg text-white font-semibold"
          style={{ background: "#2563eb", fontSize: 13 }}
        >
          <svg width="14" height="14" fill="none" stroke="currentColor" strokeWidth="2.5" viewBox="0 0 24 24">
            <path d="M12 5v14M5 12h14" />
          </svg>
          Add Employee
        </button>
      </div>

      {/* Table */}
      <div style={{ background: "#ffffff", border: "1px solid #e2e8f0", borderRadius: 12, overflow: "hidden" }}>
        <table style={{ width: "100%", borderCollapse: "collapse" }}>
          <thead>
            <tr style={{ borderBottom: "1px solid #e2e8f0", background: "#f8fafc" }}>
              {["Employee ID", "Full Name", "Department", "Enrollment Date", "Months", "Coat Status", "Eligibility", ""].map(h => (
                <th
                  key={h}
                  style={{ padding: "10px 16px", textAlign: "left", fontSize: 11.5, fontWeight: 600, color: "#64748b", letterSpacing: "0.04em", textTransform: "uppercase", whiteSpace: "nowrap" }}
                >
                  {h}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {filtered.map((emp, i) => {
              const months = monthsSince(emp.enrollmentDate);
              const eligible = months >= 6;
              return (
                <tr
                  key={emp.id}
                  style={{ borderBottom: i < filtered.length - 1 ? "1px solid #f1f5f9" : "none" }}
                  onMouseEnter={e => (e.currentTarget.style.background = "#f8fafc")}
                  onMouseLeave={e => (e.currentTarget.style.background = "transparent")}
                >
                  <td style={{ padding: "12px 16px", fontSize: 12.5, color: "#64748b", fontFamily: "monospace" }}>{emp.id}</td>
                  <td style={{ padding: "12px 16px", fontSize: 13.5, fontWeight: 500, color: "#0f172a" }}>{emp.name}</td>
                  <td style={{ padding: "12px 16px", fontSize: 13, color: "#374151" }}>{emp.department}</td>
                  <td style={{ padding: "12px 16px", fontSize: 13, color: "#374151" }}>{emp.enrollmentDate}</td>
                  <td style={{ padding: "12px 16px", fontSize: 13, color: months >= 6 ? "#16a34a" : "#374151", fontWeight: months >= 6 ? 600 : 400 }}>
                    {months}mo
                  </td>
                  <td style={{ padding: "12px 16px" }}>
                    <Badge status={getRequestDisplayStatus(emp, movements)} />
                  </td>
                  <td style={{ padding: "12px 16px" }}>
                    <span
                      style={{
                        fontSize: 12, fontWeight: 500, padding: "2px 8px", borderRadius: 20,
                        background: eligible ? "#dcfce7" : "#f1f5f9",
                        color: eligible ? "#16a34a" : "#94a3b8",
                      }}
                    >
                      {eligible ? `Cycle ${getRequestCycle(emp.enrollmentDate)} active` : "First request at 6 months"}
                    </span>
                  </td>
                  <td style={{ padding: "12px 16px" }}>
                    <button
                      onClick={() => onEdit(emp)}
                      style={{ fontSize: 12.5, color: "#2563eb", fontWeight: 500 }}
                    >
                      Edit
                    </button>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
        <div style={{ padding: "10px 16px", borderTop: "1px solid #f1f5f9", fontSize: 12, color: "#94a3b8" }}>
          Showing {filtered.length} of {employees.length} employees
        </div>
      </div>
    </div>
  );
}

// ─── Add/Edit Employee ────────────────────────────────────────────────────────
function EmployeeForm({
  employee,
  onSave,
  onCancel,
}: {
  employee?: Employee;
  onSave: (emp: Employee) => void;
  onCancel: () => void;
}) {
  const [form, setForm] = useState<Employee>(
    employee ?? {
      id: `EMP-${String(Math.floor(Math.random() * 900) + 100).padStart(3, "0")}`,
      name: "",
      department: "",
      jobTitle: "",
      enrollmentDate: "",
      coatStatus: "none",
      notes: "",
    }
  );

  function set(k: keyof Employee, v: string) {
    setForm(f => ({ ...f, [k]: v }));
  }

  const depts = ["Operations", "Warehouse", "Production", "Quality Control", "Maintenance", "Logistics"];

  return (
    <div className="p-8" style={{ maxWidth: 640 }}>
      <div style={{ background: "#fff", border: "1px solid #e2e8f0", borderRadius: 12, padding: 28 }}>
        <h2 style={{ fontFamily: "Outfit, sans-serif", fontWeight: 700, fontSize: 18, color: "#0f172a", marginBottom: 6 }}>
          {employee ? "Edit Employee" : "Add New Employee"}
        </h2>
        <p style={{ fontSize: 13, color: "#64748b", marginBottom: 24 }}>
          {employee ? `Editing record for ${employee.name}` : "Fill in the employee details below."}
        </p>

        <div className="grid grid-cols-2 gap-4">
          {[
            { label: "Employee ID", key: "id", type: "text", placeholder: "EMP-001" },
            { label: "Full Name", key: "name", type: "text", placeholder: "Jane Smith" },
            { label: "Job Title", key: "jobTitle", type: "text", placeholder: "Line Operator" },
            { label: "Enrollment Date", key: "enrollmentDate", type: "date", placeholder: "" },
          ].map(f => (
            <div key={f.key}>
              <label style={{ fontSize: 12.5, fontWeight: 500, color: "#374151", display: "block", marginBottom: 5 }}>
                {f.label}
              </label>
              <input
                type={f.type}
                value={(form as any)[f.key]}
                onChange={e => set(f.key as keyof Employee, e.target.value)}
                placeholder={f.placeholder}
                className="w-full rounded-lg px-3 py-2.5"
                style={{ border: "1px solid #d1d5db", fontSize: 13.5, outline: "none", color: "#0f172a" }}
              />
            </div>
          ))}

          <div>
            <label style={{ fontSize: 12.5, fontWeight: 500, color: "#374151", display: "block", marginBottom: 5 }}>
              Department
            </label>
            <select
              value={form.department}
              onChange={e => set("department", e.target.value)}
              className="w-full rounded-lg px-3 py-2.5"
              style={{ border: "1px solid #d1d5db", fontSize: 13.5, outline: "none", color: "#0f172a", background: "#fff" }}
            >
              <option value="">Select department…</option>
              {depts.map(d => <option key={d}>{d}</option>)}
            </select>
          </div>

          <div>
            <label style={{ fontSize: 12.5, fontWeight: 500, color: "#374151", display: "block", marginBottom: 5 }}>
              Request schedule
            </label>
            <div className="rounded-lg px-3 py-2.5" style={{ border: "1px solid #d1d5db", fontSize: 13.5, color: "#64748b", background: "#f8fafc" }}>
              Created automatically every 6 months
            </div>
          </div>

          <div className="col-span-2">
            <label style={{ fontSize: 12.5, fontWeight: 500, color: "#374151", display: "block", marginBottom: 5 }}>
              Notes
            </label>
            <textarea
              value={form.notes}
              onChange={e => set("notes", e.target.value)}
              placeholder="e.g. Preferred size: L"
              rows={3}
              className="w-full rounded-lg px-3 py-2.5"
              style={{ border: "1px solid #d1d5db", fontSize: 13.5, outline: "none", color: "#0f172a", resize: "vertical" }}
            />
          </div>
        </div>

        <div className="flex gap-3 mt-6">
          <button
            onClick={() => onSave(form)}
            className="px-6 py-2.5 rounded-lg text-white font-semibold"
            style={{ background: "#2563eb", fontSize: 13.5, fontFamily: "Outfit, sans-serif" }}
          >
            {employee ? "Save Changes" : "Add Employee"}
          </button>
          <button
            onClick={onCancel}
            className="px-6 py-2.5 rounded-lg font-medium"
            style={{ background: "#f1f5f9", fontSize: 13.5, color: "#374151" }}
          >
            Cancel
          </button>
        </div>
      </div>
    </div>
  );
}

// ─── Import Screen ────────────────────────────────────────────────────────────
function ImportScreen() {
  const [step, setStep] = useState<"upload" | "preview">("upload");
  const previewData = [
    { id: "EMP-016", name: "Laura Bennett", dept: "Production", title: "Line Operator", date: "2024-09-10" },
    { id: "EMP-017", name: "Kevin Osei", dept: "Warehouse", title: "Stock Clerk", date: "2024-09-10" },
    { id: "EMP-018", name: "Anastasia Volkov", dept: "Maintenance", title: "Technician", date: "2024-09-10" },
  ];

  return (
    <div className="flex flex-col gap-6 p-8" style={{ maxWidth: 800 }}>
      {step === "upload" ? (
        <>
          <div
            style={{ background: "#fff", border: "2px dashed #cbd5e1", borderRadius: 12, padding: 48, textAlign: "center" }}
            onDragOver={e => { e.preventDefault(); e.currentTarget.style.borderColor = "#2563eb"; e.currentTarget.style.background = "#eff6ff"; }}
            onDragLeave={e => { e.currentTarget.style.borderColor = "#cbd5e1"; e.currentTarget.style.background = "#fff"; }}
          >
            <div
              className="mx-auto flex items-center justify-center rounded-full mb-4"
              style={{ width: 56, height: 56, background: "#eff6ff" }}
            >
              {icons.import}
            </div>
            <h3 style={{ fontFamily: "Outfit, sans-serif", fontWeight: 600, fontSize: 16, color: "#0f172a", marginBottom: 6 }}>
              Upload Employee Excel File
            </h3>
            <p style={{ fontSize: 13, color: "#64748b", marginBottom: 20 }}>
              Drag and drop your .xlsx or .csv file here, or click to browse
            </p>
            <button
              onClick={() => setStep("preview")}
              className="px-6 py-2.5 rounded-lg text-white font-semibold"
              style={{ background: "#2563eb", fontSize: 13.5 }}
            >
              Select File
            </button>
            <p style={{ fontSize: 11.5, color: "#94a3b8", marginTop: 12 }}>Supported: .xlsx, .xls, .csv · Max 10MB</p>
          </div>

          {/* Column mapping */}
          <div style={{ background: "#fff", border: "1px solid #e2e8f0", borderRadius: 12, padding: 20 }}>
            <h3 style={{ fontFamily: "Outfit, sans-serif", fontWeight: 600, fontSize: 14, color: "#0f172a", marginBottom: 12 }}>
              Expected Column Format
            </h3>
            <table style={{ width: "100%", borderCollapse: "collapse", fontSize: 13 }}>
              <thead>
                <tr style={{ background: "#f8fafc", borderBottom: "1px solid #e2e8f0" }}>
                  <th style={{ padding: "8px 12px", textAlign: "left", color: "#64748b", fontWeight: 600, fontSize: 11.5, textTransform: "uppercase", letterSpacing: "0.04em" }}>Column</th>
                  <th style={{ padding: "8px 12px", textAlign: "left", color: "#64748b", fontWeight: 600, fontSize: 11.5, textTransform: "uppercase", letterSpacing: "0.04em" }}>Maps To</th>
                  <th style={{ padding: "8px 12px", textAlign: "left", color: "#64748b", fontWeight: 600, fontSize: 11.5, textTransform: "uppercase", letterSpacing: "0.04em" }}>Required</th>
                  <th style={{ padding: "8px 12px", textAlign: "left", color: "#64748b", fontWeight: 600, fontSize: 11.5, textTransform: "uppercase", letterSpacing: "0.04em" }}>Example</th>
                </tr>
              </thead>
              <tbody>
                {[
                  ["A", "Employee ID", "Yes", "EMP-001"],
                  ["B", "Full Name", "Yes", "Jane Smith"],
                  ["C", "Department", "Yes", "Warehouse"],
                  ["D", "Job Title", "No", "Forklift Operator"],
                  ["E", "Enrollment Date", "Yes", "2024-03-15"],
                  ["F", "Notes", "No", "Preferred size: L"],
                ].map(([col, field, req, ex]) => (
                  <tr key={col} style={{ borderBottom: "1px solid #f1f5f9" }}>
                    <td style={{ padding: "9px 12px", fontFamily: "monospace", fontSize: 12, color: "#64748b" }}>Col {col}</td>
                    <td style={{ padding: "9px 12px", fontWeight: 500, color: "#0f172a" }}>{field}</td>
                    <td style={{ padding: "9px 12px" }}>
                      <span style={{ fontSize: 11.5, padding: "2px 7px", borderRadius: 20, background: req === "Yes" ? "#dbeafe" : "#f1f5f9", color: req === "Yes" ? "#2563eb" : "#94a3b8", fontWeight: 500 }}>
                        {req}
                      </span>
                    </td>
                    <td style={{ padding: "9px 12px", fontSize: 12.5, color: "#64748b", fontFamily: "monospace" }}>{ex}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      ) : (
        <>
          <div
            className="flex items-center gap-3 p-3 rounded-lg"
            style={{ background: "#f0fdf4", border: "1px solid #86efac" }}
          >
            <svg width="16" height="16" fill="none" stroke="#16a34a" strokeWidth="2" viewBox="0 0 24 24">
              <polyline points="20 6 9 17 4 12" />
            </svg>
            <span style={{ fontSize: 13, color: "#15803d", fontWeight: 500 }}>
              employees_sept2024.xlsx — 3 records detected, 0 errors
            </span>
          </div>

          <div style={{ background: "#fff", border: "1px solid #e2e8f0", borderRadius: 12, overflow: "hidden" }}>
            <div style={{ padding: "14px 20px", borderBottom: "1px solid #e2e8f0" }}>
              <h3 style={{ fontFamily: "Outfit, sans-serif", fontWeight: 600, fontSize: 14, color: "#0f172a" }}>
                Preview — 3 Records
              </h3>
            </div>
            <table style={{ width: "100%", borderCollapse: "collapse" }}>
              <thead>
                <tr style={{ background: "#f8fafc", borderBottom: "1px solid #e2e8f0" }}>
                  {["Employee ID", "Full Name", "Department", "Job Title", "Enrollment Date"].map(h => (
                    <th key={h} style={{ padding: "9px 16px", textAlign: "left", fontSize: 11.5, fontWeight: 600, color: "#64748b", textTransform: "uppercase", letterSpacing: "0.04em" }}>{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {previewData.map((r, i) => (
                  <tr key={r.id} style={{ borderBottom: i < previewData.length - 1 ? "1px solid #f1f5f9" : "none" }}>
                    <td style={{ padding: "11px 16px", fontFamily: "monospace", fontSize: 12.5, color: "#64748b" }}>{r.id}</td>
                    <td style={{ padding: "11px 16px", fontSize: 13.5, fontWeight: 500, color: "#0f172a" }}>{r.name}</td>
                    <td style={{ padding: "11px 16px", fontSize: 13, color: "#374151" }}>{r.dept}</td>
                    <td style={{ padding: "11px 16px", fontSize: 13, color: "#374151" }}>{r.title}</td>
                    <td style={{ padding: "11px 16px", fontSize: 13, color: "#374151" }}>{r.date}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="flex gap-3">
            <button
              onClick={() => setStep("upload")}
              className="px-6 py-2.5 rounded-lg font-medium"
              style={{ background: "#fff", fontSize: 13.5, color: "#374151", border: "1px solid #e2e8f0" }}
            >
              ← Back
            </button>
            <button
              className="px-6 py-2.5 rounded-lg text-white font-semibold"
              style={{ background: "#2563eb", fontSize: 13.5, fontFamily: "Outfit, sans-serif" }}
            >
              Confirm Import (3 records)
            </button>
          </div>
        </>
      )}
    </div>
  );
}

// ─── Inventory ────────────────────────────────────────────────────────────────
function InventoryScreen({
  movements,
  stock,
  onAddStock,
}: {
  movements: StockMovement[];
  stock: number;
  onAddStock: (coatType: CoatTypeId, qty: number, note: string) => void;
}) {
  const [qty, setQty] = useState("");
  const [note, setNote] = useState("");
  const [coatType, setCoatType] = useState<CoatTypeId>("production-winter");

  function handleAdd() {
    const q = parseInt(qty);
    if (q > 0) { onAddStock(coatType, q, note || "Manual stock arrival"); setQty(""); setNote(""); }
  }

  return (
    <div className="flex flex-col gap-6 p-8">
      {/* Stock card */}
      <div className="grid grid-cols-3 gap-4">
        <div
          className="col-span-1 rounded-xl p-6 text-white"
          style={{ background: "linear-gradient(135deg, #1e3a5f 0%, #2563eb 100%)" }}
        >
          <div style={{ fontSize: 12, opacity: 0.75, marginBottom: 8, letterSpacing: "0.05em", textTransform: "uppercase" }}>Current Stock</div>
          <div style={{ fontFamily: "Outfit, sans-serif", fontSize: 52, fontWeight: 700, lineHeight: 1 }}>{stock}</div>
          <div style={{ fontSize: 13, opacity: 0.8, marginTop: 6 }}>coats across 7 variants</div>
          {stock < 15 && (
            <div
              className="mt-4 flex items-center gap-2 rounded-lg px-3 py-2"
              style={{ background: "#ffffff20", fontSize: 12 }}
            >
              <span>⚠</span> Low stock — reorder recommended
            </div>
          )}
        </div>

        {/* Add stock form */}
        <div
          className="col-span-2 rounded-xl p-6"
          style={{ background: "#fff", border: "1px solid #e2e8f0" }}
        >
          <h3 style={{ fontFamily: "Outfit, sans-serif", fontWeight: 600, fontSize: 15, color: "#0f172a", marginBottom: 16 }}>
            Record Stock Arrival
          </h3>
          <div className="grid grid-cols-2 gap-4">
            <div className="col-span-2">
              <label style={{ fontSize: 12.5, fontWeight: 500, color: "#374151", display: "block", marginBottom: 5 }}>Coat variant</label>
              <select value={coatType} onChange={e => setCoatType(e.target.value as CoatTypeId)} className="w-full rounded-lg px-3 py-2.5" style={{ border: "1px solid #d1d5db", fontSize: 13.5, color: "#0f172a", background: "#fff" }}>
                {COAT_TYPES.map(coat => <option key={coat.id} value={coat.id}>{coat.icon} {coat.department} — {coat.name} ({coat.season})</option>)}
              </select>
            </div>
            <div>
              <label style={{ fontSize: 12.5, fontWeight: 500, color: "#374151", display: "block", marginBottom: 5 }}>
                Quantity Received
              </label>
              <input
                type="number"
                value={qty}
                onChange={e => setQty(e.target.value)}
                placeholder="e.g. 25"
                className="w-full rounded-lg px-3 py-2.5"
                style={{ border: "1px solid #d1d5db", fontSize: 13.5, outline: "none", color: "#0f172a" }}
              />
            </div>
            <div>
              <label style={{ fontSize: 12.5, fontWeight: 500, color: "#374151", display: "block", marginBottom: 5 }}>
                Arrival Date
              </label>
              <input
                type="date"
                defaultValue="2024-09-15"
                className="w-full rounded-lg px-3 py-2.5"
                style={{ border: "1px solid #d1d5db", fontSize: 13.5, outline: "none", color: "#0f172a" }}
              />
            </div>
            <div className="col-span-2">
              <label style={{ fontSize: 12.5, fontWeight: 500, color: "#374151", display: "block", marginBottom: 5 }}>
                Supplier / Note
              </label>
              <input
                type="text"
                value={note}
                onChange={e => setNote(e.target.value)}
                placeholder="e.g. WorkWear Co. — Invoice #4521"
                className="w-full rounded-lg px-3 py-2.5"
                style={{ border: "1px solid #d1d5db", fontSize: 13.5, outline: "none", color: "#0f172a" }}
              />
            </div>
          </div>
          <button
            onClick={handleAdd}
            className="mt-4 px-6 py-2.5 rounded-lg text-white font-semibold"
            style={{ background: "#16a34a", fontSize: 13.5, fontFamily: "Outfit, sans-serif" }}
          >
            + Add Stock
          </button>
        </div>
      </div>

      <div>
        <div className="flex items-center justify-between" style={{ marginBottom: 12 }}>
          <div>
            <h3 style={{ fontFamily: "Outfit, sans-serif", fontWeight: 700, fontSize: 16, color: "#0f172a" }}>Coat Variant Inventory</h3>
            <p style={{ fontSize: 12.5, color: "#64748b", marginTop: 2 }}>Inventory is managed by coat style, department and season.</p>
          </div>
          <span style={{ fontSize: 12, color: "#64748b" }}>7 industrial variants</span>
        </div>
        <div className="grid grid-cols-4 gap-3">
          {COAT_TYPES.map(coat => {
            const units = calcStock(movements, coat.id);
            return <div key={coat.id} className="rounded-xl p-4" style={{ background: coat.softColor, border: `1px solid ${coat.color}28`, borderTop: `4px solid ${coat.color}` }}>
              <div className="flex items-center justify-between"><span style={{ fontSize: 25 }}>{coat.icon}</span><span style={{ fontSize: 22, fontWeight: 800, color: coat.color }}>{units}</span></div>
              <div style={{ fontSize: 13, fontWeight: 700, color: "#0f172a", marginTop: 10 }}>{coat.name}</div>
              <div style={{ fontSize: 11.5, color: coat.color, fontWeight: 600, marginTop: 2 }}>{coat.department} · {coat.season}</div>
              <div style={{ fontSize: 11, color: units === 0 ? "#dc2626" : "#64748b", marginTop: 7, fontWeight: 600 }}>{units === 0 ? "OUT OF STOCK" : `${units} ready to issue`}</div>
            </div>;
          })}
        </div>
      </div>

      {/* Movement table */}
      <div style={{ background: "#fff", border: "1px solid #e2e8f0", borderRadius: 12, overflow: "hidden" }}>
        <div style={{ padding: "14px 20px", borderBottom: "1px solid #e2e8f0" }}>
          <h3 style={{ fontFamily: "Outfit, sans-serif", fontWeight: 600, fontSize: 14, color: "#0f172a" }}>
            Stock Movement History
          </h3>
        </div>
        <table style={{ width: "100%", borderCollapse: "collapse" }}>
          <thead>
            <tr style={{ background: "#f8fafc", borderBottom: "1px solid #e2e8f0" }}>
              {["Date", "Coat Variant", "Type", "Quantity", "Note"].map(h => (
                <th key={h} style={{ padding: "9px 16px", textAlign: "left", fontSize: 11.5, fontWeight: 600, color: "#64748b", textTransform: "uppercase", letterSpacing: "0.04em" }}>{h}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {movements.map((m, i) => {
              return (
                <tr key={m.id} style={{ borderBottom: i < movements.length - 1 ? "1px solid #f1f5f9" : "none" }}>
                  <td style={{ padding: "11px 16px", fontSize: 13, color: "#374151" }}>{m.date}</td>
                  <td style={{ padding: "11px 16px", fontSize: 12.5, color: "#374151" }}>{COAT_TYPES.find(c => c.id === m.coatType)?.icon} {COAT_TYPES.find(c => c.id === m.coatType)?.name}</td>
                  <td style={{ padding: "11px 16px" }}>
                    <span
                      style={{
                        fontSize: 11.5, padding: "2px 8px", borderRadius: 20, fontWeight: 500,
                        background: m.type === "arrival" ? "#dcfce7" : "#fee2e2",
                        color: m.type === "arrival" ? "#16a34a" : "#dc2626",
                      }}
                    >
                      {m.type === "arrival" ? "Arrival" : "Allocation"}
                    </span>
                  </td>
                  <td style={{ padding: "11px 16px", fontSize: 13, fontWeight: 600, color: m.type === "arrival" ? "#16a34a" : "#dc2626" }}>
                    {m.type === "arrival" ? "+" : ""}{m.qty}
                  </td>
                  <td style={{ padding: "11px 16px", fontSize: 13, color: "#374151" }}>{m.note}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}

// ─── Requests ─────────────────────────────────────────────────────────────────
function RequestsScreen({
  employees,
  stock,
  movements,
  onMarkProvided,
}: {
  employees: Employee[];
  stock: number;
  movements: StockMovement[];
  onMarkProvided: (id: string) => void;
}) {
  const activeRequests = employees.filter(e => {
    const status = getRequestDisplayStatus(e, movements);
    return status === "requested" || status === "out_of_stock";
  });

  return (
    <div className="flex flex-col gap-5 p-8">
      <div className="grid grid-cols-3 gap-4">
        {[
          { label: "Active Requests", value: activeRequests.length, color: "#2563eb", sub: "Created by the 6-month schedule" },
          { label: "Awaiting Provision", value: activeRequests.filter(e => getRequestDisplayStatus(e, movements) === "requested").length, color: "#d97706", sub: "Ready for coat allocation" },
          { label: "Available Stock", value: stock, color: stock < 10 ? "#dc2626" : "#16a34a", sub: stock < 10 ? "Low — reorder soon" : "Ready to allocate" },
        ].map(k => (
          <div key={k.label} style={{ background: "#fff", border: "1px solid #e2e8f0", borderRadius: 12, padding: 18 }}>
            <div style={{ fontFamily: "Outfit, sans-serif", fontSize: 26, fontWeight: 700, color: k.color }}>{k.value}</div>
            <div style={{ fontSize: 13, fontWeight: 500, color: "#0f172a", marginTop: 3 }}>{k.label}</div>
            <div style={{ fontSize: 12, color: "#94a3b8", marginTop: 2 }}>{k.sub}</div>
          </div>
        ))}
      </div>

      <div style={{ background: "#fff", border: "1px solid #e2e8f0", borderRadius: 12, overflow: "hidden" }}>
        <div style={{ padding: "14px 20px", borderBottom: "1px solid #e2e8f0" }}>
          <h3 style={{ fontFamily: "Outfit, sans-serif", fontWeight: 600, fontSize: 14, color: "#0f172a" }}>
            Automatically Created Requests
          </h3>
        </div>
        {activeRequests.length === 0 ? (
          <div style={{ padding: 40, textAlign: "center", color: "#94a3b8", fontSize: 14 }}>
            No active requests. The next request is created automatically at each six-month anniversary.
          </div>
        ) : (
          <table style={{ width: "100%", borderCollapse: "collapse" }}>
            <thead>
              <tr style={{ background: "#f8fafc", borderBottom: "1px solid #e2e8f0" }}>
                {["Employee", "Required Coat", "Enrollment Date", "Months", "Status", "Action"].map(h => (
                  <th key={h} style={{ padding: "9px 16px", textAlign: "left", fontSize: 11.5, fontWeight: 600, color: "#64748b", textTransform: "uppercase", letterSpacing: "0.04em" }}>{h}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {activeRequests.map((emp, i) => (
                <tr key={emp.id} style={{ borderBottom: i < activeRequests.length - 1 ? "1px solid #f1f5f9" : "none" }}>
                  <td style={{ padding: "12px 16px" }}>
                    <div style={{ fontSize: 13.5, fontWeight: 500, color: "#0f172a" }}>{emp.name}</div>
                    <div style={{ fontSize: 11.5, color: "#94a3b8" }}>{emp.id}</div>
                  </td>
                  <td style={{ padding: "12px 16px" }}>
                    {(() => { const coat = getCoatType(emp); const units = calcStock(movements, coat.id); return <div>
                      <div style={{ fontSize: 13, fontWeight: 600, color: coat.color }}>{coat.icon} {coat.name}</div>
                      <div style={{ fontSize: 11.5, color: "#64748b", marginTop: 2 }}>{coat.department} · {coat.season} · {units} in stock</div>
                    </div>; })()}
                  </td>
                  <td style={{ padding: "12px 16px", fontSize: 13, color: "#374151" }}>{emp.enrollmentDate}</td>
                  <td style={{ padding: "12px 16px", fontSize: 13, fontWeight: 600, color: "#16a34a" }}>
                    {monthsSince(emp.enrollmentDate)}mo
                  </td>
                  <td style={{ padding: "12px 16px" }}>
                    <Badge status={getRequestDisplayStatus(emp, movements)} />
                  </td>
                  <td style={{ padding: "12px 16px" }}>
                    {getRequestDisplayStatus(emp, movements) === "out_of_stock" ? (
                      <span style={{ fontSize: 12.5, color: "#94a3b8" }}>Out of stock</span>
                    ) : (
                      <button
                        onClick={() => onMarkProvided(emp.id)}
                        disabled={calcStock(movements, getCoatType(emp).id) === 0}
                        className="px-3.5 py-1.5 rounded-lg text-white font-medium transition-opacity"
                        style={{
                          background: calcStock(movements, getCoatType(emp).id) === 0 ? "#94a3b8" : "#16a34a",
                          fontSize: 12.5,
                          cursor: calcStock(movements, getCoatType(emp).id) === 0 ? "not-allowed" : "pointer",
                          opacity: calcStock(movements, getCoatType(emp).id) === 0 ? 0.6 : 1,
                        }}
                      >
                        Mark as Provided
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}

// ─── Notifications ────────────────────────────────────────────────────────────
function NotificationsScreen({
  notifications,
  onMarkRead,
  onMarkAllRead,
}: {
  notifications: Notification[];
  onMarkRead: (id: string) => void;
  onMarkAllRead: () => void;
}) {
  const unread = notifications.filter(n => !n.read).length;

  return (
    <div className="flex flex-col gap-5 p-8" style={{ maxWidth: 760 }}>
      <div className="flex items-center justify-between">
        <div>
          <p style={{ fontSize: 13, color: "#64748b" }}>
            {unread > 0 ? `${unread} unread notification${unread > 1 ? "s" : ""}` : "All caught up!"}
          </p>
        </div>
        {unread > 0 && (
          <button
            onClick={onMarkAllRead}
            style={{ fontSize: 13, color: "#2563eb", fontWeight: 500 }}
          >
            Mark all as read
          </button>
        )}
      </div>

      <div className="flex flex-col gap-2">
        {notifications.map(n => (
          <div
            key={n.id}
            className="flex items-start gap-4 p-4 rounded-xl"
            style={{
              background: n.read ? "#fff" : "#eff6ff",
              border: n.read ? "1px solid #f1f5f9" : "1px solid #bfdbfe",
            }}
          >
            <div
              className="flex items-center justify-center rounded-full flex-shrink-0"
              style={{
                width: 38, height: 38, marginTop: 2,
                background: n.type === "warning" ? "#fef3c7" : n.type === "success" ? "#dcfce7" : "#dbeafe",
              }}
            >
              {n.type === "warning" ? (
                <svg width="18" height="18" fill="none" stroke="#d97706" strokeWidth="2" viewBox="0 0 24 24">
                  <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z" />
                  <line x1="12" y1="9" x2="12" y2="13" /><line x1="12" y1="17" x2="12.01" y2="17" />
                </svg>
              ) : n.type === "success" ? (
                <svg width="18" height="18" fill="none" stroke="#16a34a" strokeWidth="2" viewBox="0 0 24 24">
                  <polyline points="20 6 9 17 4 12" />
                </svg>
              ) : (
                <svg width="18" height="18" fill="none" stroke="#2563eb" strokeWidth="2" viewBox="0 0 24 24">
                  <circle cx="12" cy="12" r="10" /><line x1="12" y1="8" x2="12" y2="12" /><line x1="12" y1="16" x2="12.01" y2="16" />
                </svg>
              )}
            </div>
            <div className="flex-1">
              <p style={{ fontSize: 14, color: "#1e293b", lineHeight: 1.5, fontWeight: n.read ? 400 : 500 }}>{n.message}</p>
              <p style={{ fontSize: 12, color: "#94a3b8", marginTop: 4 }}>{n.date}</p>
            </div>
            {!n.read && (
              <button
                onClick={() => onMarkRead(n.id)}
                style={{ fontSize: 12, color: "#2563eb", fontWeight: 500, flexShrink: 0 }}
              >
                Mark read
              </button>
            )}
          </div>
        ))}
      </div>
    </div>
  );
}

// ─── Toggle ───────────────────────────────────────────────────────────────────
function Toggle({ on, onToggle }: { on: boolean; onToggle: () => void }) {
    return (
      <button
        onClick={onToggle}
        className="relative flex-shrink-0"
        style={{
          width: 42, height: 24, borderRadius: 12,
          background: on ? "#2563eb" : "#e2e8f0",
          transition: "background 0.2s",
          border: "none",
          cursor: "pointer",
        }}
      >
        <div
          style={{
            width: 18, height: 18, borderRadius: "50%", background: "#fff",
            position: "absolute", top: 3,
            left: on ? 21 : 3,
            transition: "left 0.2s",
            boxShadow: "0 1px 3px rgba(0,0,0,0.2)",
          }}
        />
      </button>
    );
}

// ─── Settings ─────────────────────────────────────────────────────────────────
function SettingsScreen() {
  // The recurring interval is policy-controlled and intentionally fixed at six months.
  const [eligibilityMonths, setEligibilityMonths] = useState(6);
  const [lowStockAlert, setLowStockAlert] = useState(true);
  const [lowStockThreshold, setLowStockThreshold] = useState(15);
  const [saved, setSaved] = useState(false);

  function handleSave() {
    setSaved(true);
    setTimeout(() => setSaved(false), 2500);
  }

  return (
    <div className="flex flex-col gap-5 p-8" style={{ maxWidth: 660 }}>
      {/* Eligibility rules */}
      <div style={{ background: "#fff", border: "1px solid #e2e8f0", borderRadius: 12, padding: 24 }}>
        <h3 style={{ fontFamily: "Outfit, sans-serif", fontWeight: 600, fontSize: 15, color: "#0f172a", marginBottom: 16 }}>
          Automatic Request Schedule
        </h3>
        <div className="flex flex-col gap-4">
          <div className="flex items-center justify-between">
            <div>
              <div style={{ fontSize: 13.5, fontWeight: 500, color: "#0f172a" }}>Six-month recurring cycle</div>
              <div style={{ fontSize: 12, color: "#64748b", marginTop: 2 }}>A coat request is created automatically at 6 months, then every 6 months after.</div>
            </div>
            <div className="rounded-lg px-3 py-2" style={{ background: "#eff6ff", color: "#005daa", fontSize: 13, fontWeight: 700 }}>
              Every 6 months
            </div>
            <div style={{ display: "none" }}>
              <button
                onClick={() => setEligibilityMonths(m => Math.max(1, m - 1))}
                style={{ width: 28, height: 28, borderRadius: 6, border: "1px solid #e2e8f0", background: "#f8fafc", fontSize: 16, cursor: "pointer" }}
              >−</button>
              <span style={{ fontSize: 16, fontWeight: 700, color: "#0f172a", minWidth: 32, textAlign: "center" }}>{eligibilityMonths}</span>
              <button
                onClick={() => setEligibilityMonths(m => m + 1)}
                style={{ width: 28, height: 28, borderRadius: 6, border: "1px solid #e2e8f0", background: "#f8fafc", fontSize: 16, cursor: "pointer" }}
              >+</button>
              <span style={{ fontSize: 13, color: "#64748b" }}>months</span>
            </div>
          </div>
        </div>
      </div>

      {/* Notifications */}
      <div style={{ background: "#fff", border: "1px solid #e2e8f0", borderRadius: 12, padding: 24 }}>
        <h3 style={{ fontFamily: "Outfit, sans-serif", fontWeight: 600, fontSize: 15, color: "#0f172a", marginBottom: 16 }}>
          Notification Preferences
        </h3>
        <div className="flex flex-col gap-4">
          {[
            { label: "Low Stock Alerts", sub: "Notify when inventory drops below threshold", on: lowStockAlert, toggle: () => setLowStockAlert(v => !v) },
          ].map(item => (
            <div key={item.label} className="flex items-center justify-between py-1">
              <div>
                <div style={{ fontSize: 13.5, fontWeight: 500, color: "#0f172a" }}>{item.label}</div>
                <div style={{ fontSize: 12, color: "#64748b", marginTop: 2 }}>{item.sub}</div>
              </div>
              <Toggle on={item.on} onToggle={item.toggle} />
            </div>
          ))}
          {lowStockAlert && (
            <div className="flex items-center justify-between py-1 pl-4" style={{ borderLeft: "2px solid #e2e8f0" }}>
              <div style={{ fontSize: 13, color: "#374151" }}>Low stock threshold (units)</div>
              <input
                type="number"
                value={lowStockThreshold}
                onChange={e => setLowStockThreshold(Number(e.target.value))}
                className="rounded-lg px-3 py-1.5"
                style={{ border: "1px solid #d1d5db", fontSize: 13.5, outline: "none", color: "#0f172a", width: 80, textAlign: "center" }}
              />
            </div>
          )}
        </div>
      </div>

      {/* Account */}
      <div style={{ background: "#fff", border: "1px solid #e2e8f0", borderRadius: 12, padding: 24 }}>
        <h3 style={{ fontFamily: "Outfit, sans-serif", fontWeight: 600, fontSize: 15, color: "#0f172a", marginBottom: 16 }}>
          Account Settings
        </h3>
        <div className="grid grid-cols-2 gap-4">
          {[
            { label: "Full Name", value: "Sarah Mitchell" },
            { label: "Email", value: "sarah.mitchell@company.com" },
            { label: "Role", value: "HR Administrator" },
            { label: "Company", value: "Lear Corporation" },
          ].map(f => (
            <div key={f.label}>
              <label style={{ fontSize: 12.5, fontWeight: 500, color: "#374151", display: "block", marginBottom: 5 }}>{f.label}</label>
              <input
                defaultValue={f.value}
                className="w-full rounded-lg px-3 py-2.5"
                style={{ border: "1px solid #d1d5db", fontSize: 13.5, outline: "none", color: "#0f172a" }}
              />
            </div>
          ))}
        </div>
      </div>

      <div className="flex items-center gap-3">
        <button
          onClick={handleSave}
          className="px-6 py-2.5 rounded-lg text-white font-semibold"
          style={{ background: "#2563eb", fontSize: 13.5, fontFamily: "Outfit, sans-serif" }}
        >
          Save Settings
        </button>
        {saved && (
          <span style={{ fontSize: 13, color: "#16a34a", fontWeight: 500 }}>
            ✓ Settings saved
          </span>
        )}
      </div>
    </div>
  );
}

// ─── Root App ─────────────────────────────────────────────────────────────────
export default function App() {
  const [loggedIn, setLoggedIn] = useState(false);
  const [screen, setScreen] = useState<Screen>("dashboard");
  const [employees, setEmployees] = useState<Employee[]>(seedEmployees);
  const [movements, setMovements] = useState<StockMovement[]>(seedMovements);
  const [notifications, setNotifications] = useState<Notification[]>(seedNotifications);
  const [editingEmployee, setEditingEmployee] = useState<Employee | undefined>(undefined);

  const stock = calcStock(movements);
  const unreadCount = notifications.filter(n => !n.read).length;

  function handleNav(s: Screen) {
    setScreen(s);
    setEditingEmployee(undefined);
  }

  function handleSaveEmployee(emp: Employee) {
    setEmployees(prev => {
      const idx = prev.findIndex(e => e.id === emp.id);
      if (idx >= 0) {
        const next = [...prev];
        next[idx] = emp;
        return next;
      }
      return [...prev, emp];
    });
    setScreen("employees");
    setEditingEmployee(undefined);
  }

  function handleAddStock(coatType: CoatTypeId, qty: number, note: string) {
    const id = `MOV-${String(movements.length + 1).padStart(3, "0")}`;
    setMovements(prev => [...prev, { id, date: new Date().toISOString().slice(0, 10), type: "arrival", qty, coatType, note }]);
  }

  function handleMarkProvided(empId: string) {
    const emp = employees.find(e => e.id === empId);
    const coat = emp && getCoatType(emp);
    if (!emp || !coat || calcStock(movements, coat.id) <= 0) return;
    setEmployees(prev =>
      prev.map(e => e.id === empId ? {
        ...e,
        coatStatus: "provided",
        lastProvidedCycle: getRequestCycle(e.enrollmentDate),
      } : e)
    );
    const movId = `MOV-${String(movements.length + 1).padStart(3, "0")}`;
    setMovements(prev => [...prev, {
      id: movId, date: new Date().toISOString().slice(0, 10), type: "allocation", qty: -1, coatType: coat.id,
      note: `${coat.name} issued to ${emp.name}`,
    }]);
    const notifId = `N-${String(notifications.length + 1).padStart(3, "0")}`;
    setNotifications(prev => [{
      id: notifId,
      message: `${coat.name} successfully assigned to ${emp.name}.`,
      date: new Date().toISOString().slice(0, 10),
      read: false,
      type: "success",
    }, ...prev]);
  }

  function handleMarkRead(id: string) {
    setNotifications(prev => prev.map(n => n.id === id ? { ...n, read: true } : n));
  }

  function handleMarkAllRead() {
    setNotifications(prev => prev.map(n => ({ ...n, read: true })));
  }

  if (!loggedIn) {
    return <LoginScreen onLogin={() => setLoggedIn(true)} />;
  }

  const screenTitles: Record<Screen, { title: string; subtitle?: string }> = {
    login: { title: "Login" },
    dashboard: { title: "Dashboard", subtitle: "Overview of your coat tracking program" },
    employees: { title: "Employees", subtitle: "Manage employee records and coat eligibility" },
    "add-employee": { title: editingEmployee ? "Edit Employee" : "Add Employee", subtitle: "Create or update an employee record" },
    import: { title: "Import from Excel", subtitle: "Bulk-upload employees from a spreadsheet" },
    inventory: { title: "Inventory Management", subtitle: "Track coat stock and movements" },
    requests: { title: "Coat Requests", subtitle: "Automatically created every six months from enrollment" },
    notifications: { title: "Notifications", subtitle: "Stay on top of eligibility and inventory alerts" },
    settings: { title: "Settings", subtitle: "Configure rules and preferences" },
  };

  const meta = screenTitles[screen];

  return (
    <div className="flex min-h-screen" style={{ background: "#f1f5f9" }}>
      <Sidebar current={screen} onNav={handleNav} unreadCount={unreadCount} />

      <div className="flex-1 flex flex-col min-w-0">
        <Header title={meta.title} subtitle={meta.subtitle} />

        <main className="flex-1 overflow-auto">
          {screen === "dashboard" && (
            <Dashboard employees={employees} notifications={notifications} stock={stock} onNav={handleNav} />
          )}
          {screen === "employees" && (
            <EmployeeList
              employees={employees}
              movements={movements}
              onAdd={() => { setEditingEmployee(undefined); setScreen("add-employee"); }}
              onEdit={emp => { setEditingEmployee(emp); setScreen("add-employee"); }}
            />
          )}
          {screen === "add-employee" && (
            <EmployeeForm employee={editingEmployee} onSave={handleSaveEmployee} onCancel={() => setScreen("employees")} />
          )}
          {screen === "import" && <ImportScreen />}
          {screen === "inventory" && (
            <InventoryScreen movements={movements} stock={stock} onAddStock={handleAddStock} />
          )}
          {screen === "requests" && (
            <RequestsScreen employees={employees} stock={stock} movements={movements} onMarkProvided={handleMarkProvided} />
          )}
          {screen === "notifications" && (
            <NotificationsScreen notifications={notifications} onMarkRead={handleMarkRead} onMarkAllRead={handleMarkAllRead} />
          )}
          {screen === "settings" && <SettingsScreen />}
        </main>

        <footer className="px-8 py-3 text-center" style={{ borderTop: "1px solid #e2e8f0", background: "#ffffff", fontSize: 12, color: "#94a3b8" }}>
          Demo application — no sensitive data is stored or processed.
        </footer>
      </div>
    </div>
  );
}
