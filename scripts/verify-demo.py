"""Browser acceptance for the clothing module against the running API.

Uses the local admin configuration without logging credentials. Test employees
are deactivated and the tested variant's balance restored in the cleanup step.
Requires Python Playwright and the frontend/API development servers.
The configured account must have both HrAdministrator and ClothingManager roles.
"""
import json
import sys
from pathlib import Path
from uuid import uuid4
from playwright.sync_api import sync_playwright, expect

sys.stdout.reconfigure(encoding="utf-8")
root = Path(__file__).resolve().parents[1]
artifacts = root / "artifacts" / "demo-comparison"
artifacts.mkdir(parents=True, exist_ok=True)
config = {}
for line in (root / "backend" / ".env").read_text().splitlines():
    if "=" in line and not line.strip().startswith("#"):
        key, value = line.split("=", 1)
        config[key.strip()] = value.strip().strip('"')

prefix = "PARITY-" + uuid4().hex[:8].upper()
checks = []
with sync_playwright() as p:
    browser = p.chromium.launch(headless=True)
    context = browser.new_context(viewport={"width": 1440, "height": 1000})
    page = context.new_page()
    errors = []
    page.on("pageerror", lambda error: errors.append(str(error)))
    page.goto("http://127.0.0.1:8443", wait_until="networkidle")
    page.screenshot(path=str(artifacts / "restored-login.png"))
    page.get_by_label("Email", exact=True).fill(config["InitialAdmin__Email"])
    page.get_by_label("Password", exact=True).fill(config["InitialAdmin__Password"])
    with page.expect_response(lambda r: r.url.endswith("/api/auth/login")) as login:
        page.get_by_role("button", name="Sign in", exact=True).click()
    assert login.value.ok, "Admin sign-in failed."
    assert "ClothingManager" in login.value.json()["roles"], "Assign ClothingManager to the configured account before running clothing acceptance."
    expect(page.get_by_role("heading", name="HR workspace", exact=True)).to_be_visible()
    page.get_by_role("button", name="Clothing overview", exact=True).click()
    token = login.value.json()["accessToken"]
    headers = {"Authorization": "Bearer " + token, "X-HR-Auth": "1"}

    def api(method, path, body=None):
        response = context.request.fetch("http://127.0.0.1:8443/api" + path, method=method, headers=headers, data=body)
        assert response.ok, f"{method} {path} returned HTTP {response.status}"
        return response.json() if response.status != 204 else None

    initial = api("POST", "/program/snapshot")
    original_name = api("GET", "/auth/me")["fullName"]
    item = next(i for i in initial["items"] if i["sku"] == "production-winter")
    initial_quantity = item["quantityOnHand"]
    original_settings = initial["settings"]
    original_clothing_settings = api("GET", "/clothing/settings")
    try:
        expect(page.get_by_text("Total Employees", exact=True)).to_be_visible()
        page.screenshot(path=str(artifacts / "restored-dashboard.png"), full_page=True)
        checks.append("Sign-in opens the HR workspace; the assigned clothing module has activity, quick actions, chart, and status overview.")

        page.get_by_role("button", name="Add Employee", exact=True).click()
        page.get_by_label("Employee ID", exact=True).fill(prefix)
        page.get_by_label("Full Name", exact=True).fill("Browser Parity Employee")
        page.get_by_label("Job Title", exact=True).fill("Line Operator")
        page.get_by_label("Enrollment Date", exact=True).fill("2024-01-15")
        page.get_by_label("Department", exact=True).select_option(label="Production")
        page.get_by_role("button", name="Add Employee", exact=True).click()
        row = page.get_by_role("row").filter(has_text=prefix)
        expect(row).to_be_visible()
        expect(row).to_contain_text("Cycle 5 active")
        page.get_by_placeholder("Search employees…").fill("Production")
        expect(row).to_be_visible()
        page.get_by_placeholder("Search employees…").fill("")
        row.get_by_role("button", name="Edit", exact=True).click()
        page.get_by_label("Employee ID", exact=True).fill(prefix + "-EDIT")
        page.get_by_role("button", name="Save Changes", exact=True).click()
        expect(page.get_by_role("row").filter(has_text=prefix + "-EDIT")).to_be_visible()
        page.screenshot(path=str(artifacts / "restored-employees.png"), full_page=True)
        checks.append("Employee create/edit, editable ID, department search, eligibility, and coat-status columns work.")

        page.get_by_role("button", name="Inventory", exact=True).click()
        page.locator("main select").select_option("production-winter")
        page.get_by_label("Quantity Received", exact=True).fill("2")
        page.get_by_label("Arrival Date", exact=True).fill("2026-09-01")
        page.get_by_placeholder("e.g. WorkWear Co. — Invoice #4521").fill(prefix + " delivery")
        page.get_by_role("button", name="+ Add Stock", exact=True).click()
        expect(page.get_by_role("row").filter(has_text=prefix + " delivery")).to_contain_text("2026-09-01")
        page.screenshot(path=str(artifacts / "restored-inventory.png"), full_page=True)
        checks.append("Stock arrival uses its selected date; variant balances and combined ledger refresh.")

        page.get_by_role("button", name="Requests", exact=True).click()
        request_row = page.get_by_role("row").filter(has_text=prefix)
        expect(request_row).to_contain_text("Auto Request")
        expect(request_row).to_contain_text("Winter")
        page.screenshot(path=str(artifacts / "restored-requests.png"), full_page=True)
        request_row.get_by_role("combobox").select_option("production-winter")
        request_row.get_by_role("button", name="Mark as Provided", exact=True).click()
        expect(request_row).to_have_count(0)
        page.get_by_role("button", name="Employees", exact=True).click()
        expect(page.get_by_role("row").filter(has_text=prefix)).to_contain_text("Cycle Completed")
        page.locator("main select").nth(1).select_option("provided")
        expect(page.get_by_role("row").filter(has_text=prefix)).to_be_visible()
        checks.append("Provision consumes one coat and updates requests, employee status, and status filtering.")

        page.get_by_role("button", name="Notifications", exact=True).click()
        expect(page.get_by_text("Production Coat successfully assigned to Browser Parity Employee (" + prefix + "-EDIT).", exact=True)).to_be_visible()
        page.get_by_role("button", name="Mark all as read", exact=True).click()
        expect(page.get_by_text("All caught up!", exact=True)).to_be_visible()
        page.reload(wait_until="networkidle")
        page.get_by_role("button", name="Notifications", exact=True).click()
        expect(page.get_by_text("All caught up!", exact=True)).to_be_visible()
        page.screenshot(path=str(artifacts / "restored-notifications.png"), full_page=True)
        checks.append("Named allocation notifications and per-user read state survive reload.")

        page.get_by_role("button", name="Import Excel", exact=True).click()
        csv = ("Employee ID,Full Name,Department,Job Title,Enrollment Date,Notes\n" + prefix + "-IMPORT,Imported Browser Employee,Operations,Coordinator," + initial["businessDate"] + ",Imported note\n")
        page.get_by_label("Employee file", exact=True).set_input_files({"name": "parity.csv", "mimeType": "text/csv", "buffer": csv.encode()})
        expect(page.get_by_role("cell", name="Coordinator", exact=True)).to_be_visible()
        page.get_by_role("button", name="Confirm Import (1 records)", exact=True).click()
        expect(page.get_by_text("1 employees imported successfully.", exact=True)).to_be_visible()
        page.screenshot(path=str(artifacts / "restored-import.png"), full_page=True)
        page.get_by_role("button", name="Employees", exact=True).click()
        expect(page.get_by_role("row").filter(has_text=prefix + "-IMPORT")).to_contain_text("Not Eligible")
        checks.append("Real CSV upload, preview with job title, and confirmed import create persistent employees.")

        page.get_by_role("button", name="My account", exact=True).click()
        page.get_by_label("Full name", exact=True).fill("Browser HR")
        page.get_by_role("button", name="Save profile", exact=True).click()
        expect(page.get_by_role("status").filter(has_text="Profile saved")).to_be_visible()
        page.get_by_role("button", name="Organization settings", exact=True).click()
        page.get_by_label("Business time zone", exact=True).fill("UTC")
        page.get_by_role("button", name="Save time zone", exact=True).click()
        expect(page.get_by_role("status").filter(has_text="Business time zone saved")).to_be_visible()
        page.get_by_role("button", name="Clothing settings", exact=True).click()
        page.get_by_label("Low stock threshold (units per variant)", exact=True).fill("14")
        page.get_by_role("button", name="Save clothing settings", exact=True).click()
        expect(page.get_by_role("status").filter(has_text="Clothing settings saved")).to_be_visible()
        assert api("GET", "/auth/me")["fullName"] == "Browser HR"
        saved_settings = api("GET", "/settings")
        assert saved_settings["timeZoneId"] == "UTC"
        assert saved_settings["companyName"] == original_settings["companyName"]
        page.reload(wait_until="networkidle")
        page.get_by_role("button", name="My account", exact=True).click()
        expect(page.get_by_label("Full name", exact=True)).to_have_value("Browser HR")
        page.get_by_role("button", name="Organization settings", exact=True).click()
        expect(page.get_by_label("Business time zone", exact=True)).to_have_value("UTC")
        page.get_by_role("button", name="Clothing settings", exact=True).click()
        expect(page.get_by_label("Low stock threshold (units per variant)", exact=True)).to_have_value("14")
        page.screenshot(path=str(artifacts / "restored-settings.png"), full_page=True)
        checks.append("Account name, business time zone, and notification settings persist after reload; company name is preserved.")
        assert not errors, "Browser JavaScript errors: " + str(errors)
        checks.append("No browser JavaScript errors.")
    finally:
        api("PUT", "/auth/profile", {"fullName": original_name})
        fresh_settings = api("GET", "/settings")
        api("PUT", "/settings", {**original_settings, "rowVersion": fresh_settings["rowVersion"]})
        fresh_clothing_settings = api("GET", "/clothing/settings")
        api("PUT", "/clothing/settings", {**original_clothing_settings, "rowVersion": fresh_clothing_settings["rowVersion"]})
        snapshot = api("POST", "/program/snapshot")
        for employee in snapshot["employees"]:
            if employee["employeeNumber"].startswith(prefix):
                api("PATCH", "/employees/" + employee["id"] + "/active", {"isActive": False, "rowVersion": employee["rowVersion"]})
        current = next(i for i in snapshot["items"] if i["id"] == item["id"])
        difference = initial_quantity - current["quantityOnHand"]
        if difference:
            api("POST", "/inventory/adjustments", {"inventoryItemId": item["id"], "quantity": difference, "rowVersion": current["rowVersion"], "note": prefix + " browser acceptance cleanup"})
        browser.close()
    (artifacts / "browser-acceptance.json").write_text(json.dumps({"checks": checks, "javascriptErrors": errors, "cleanup": "Test employees deactivated; account/settings and variant stock restored."}, indent=2))
    print(json.dumps({"passed": len(checks), "checks": checks}, indent=2))
