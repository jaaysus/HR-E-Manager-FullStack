"""Verify module navigation with role fixtures; backend stays stopped.

API authorization is verified separately by AuthenticationTests.
"""
import json
from datetime import datetime, timedelta, timezone
from pathlib import Path
from playwright.sync_api import sync_playwright, expect

output = Path(__file__).resolve().parents[1] / "artifacts" / "rbac"
output.mkdir(parents=True, exist_ok=True)
shared = ["employees.read", "platform.departments.read", "platform.settings.read", "platform.notifications.read"]
clothing = ["coats.dashboard.read", "coats.read", "inventory.read"]
profiles = {
    "HrAdministrator": shared + ["employees.manage", "employees.import", "platform.users.manage", "platform.settings.manage", "platform.departments.manage"],
    "Viewer": shared,
    "ClothingViewer": shared + clothing,
    "ClothingManager": shared + clothing + ["inventory.manage", "inventory.rules.manage", "coats.manage", "coats.provide", "employees.manage", "employees.import", "clothing.settings.manage"],
    "CombinedRoles": shared + clothing + ["platform.users.manage", "platform.settings.manage", "platform.departments.manage", "inventory.manage", "inventory.rules.manage", "clothing.settings.manage", "employees.manage", "employees.import", "coats.provide"],
}
checks = []
with sync_playwright() as playwright:
    browser = playwright.chromium.launch(headless=True)
    for role, permissions in profiles.items():
        page = browser.new_page(viewport={"width": 1440, "height": 650})
        errors, calls = [], []
        page.on("pageerror", lambda error: errors.append(str(error)))
        session = {"email": "role@example.test", "fullName": "Role Test", "roles": [role], "permissions": permissions, "accessToken": "fixture", "expiresAtUtc": (datetime.now(timezone.utc) + timedelta(hours=1)).isoformat(), "refreshTokenExpiresAtUtc": "2099-01-01T00:00:00Z"}
        if role == "CombinedRoles": session["roles"] = ["HrAdministrator", "InventoryManager", "ClothingManager"]
        settings = {"companyName": "Lear Corporation", "timeZoneId": "Africa/Casablanca", "businessDate": "2026-10-04", "rowVersion": "fixture"}
        clothing_settings = {"lowStockAlertsEnabled": True, "lowStockThreshold": 15, "eligibilityInterval": 6, "eligibilityUnit": "Months", "rowVersion": "fixture"}
        active_request_count = 2
        preferences = {"notifyLowStock": True, "notifyClothingActivity": True}
        departments = [{"id": "department", "code": "PROD", "name": "Production", "isActive": True, "rowVersion": "fixture"}]
        items = [{"id": "item", "sku": "production-winter", "name": "Production Coat", "color": "Blue", "size": "M", "isActive": True, "quantityOnHand": 10, "rowVersion": "fixture"}]
        movements = [{"id": str(index), "date": "2026-10-04", "type": "arrival", "qty": 1, "coatType": "production-winter", "note": "Long-page sidebar check"} for index in range(120)]
        def route_api(route):
            path = route.request.url.split("/api", 1)[1].split("?", 1)[0]
            calls.append(path)
            if path == "/auth/refresh": body = session
            elif path == "/coat-requests/active-count": body = {"activeCount": active_request_count}
            elif path == "/notifications/unread-count": body = {"unreadCount": 0}
            elif path == "/departments":
                if route.request.method == "POST": departments.append({"id": "new-department", **route.request.post_data_json, "rowVersion": "fixture"})
                body = departments[-1] if route.request.method == "POST" else departments
            elif path == "/users": body = [{"id": "test-user", "email": session["email"], "fullName": session["fullName"], "roles": [role], "isActive": True}]
            elif path == "/settings":
                if route.request.method == "PUT": settings.update(route.request.post_data_json)
                body = settings
            elif path == "/clothing/settings":
                if route.request.method == "PUT": clothing_settings.update(route.request.post_data_json)
                body = clothing_settings
            elif path == "/inventory/items":
                if route.request.method == "POST":
                    items.append({"id": "new-item", **route.request.post_data_json, "quantityOnHand": 0, "rowVersion": "fixture", "isActive": True})
                    body = items[-1]
                else: body = items
            elif path == "/auth/profile/preferences":
                if route.request.method == "PUT": preferences.update(route.request.post_data_json)
                body = preferences
            elif path == "/auth/profile": body = route.request.post_data_json
            elif path == "/program/snapshot": body = {"businessDate": "2026-10-04", "employees": [], "items": items, "movements": movements, "notifications": [], "settings": {**settings, **clothing_settings}}
            else: body = {"items": [], "totalCount": 0, "page": 1, "pageSize": 25}
            route.fulfill(json=body)
        page.route("**/api/**", route_api)
        page.goto("http://127.0.0.1:8443", wait_until="networkidle")
        expect(page.get_by_role("heading", name="HR workspace", exact=True)).to_be_visible()
        assert "/program/snapshot" not in calls, "Workspace must not load the clothing module"
        nav = page.locator("nav")
        clothing_access = "coats.dashboard.read" in permissions
        expect(nav.get_by_role("button", name="Clothing overview", exact=True)).to_have_count(1 if clothing_access else 0)
        expect(nav.get_by_role("button", name="Users & access", exact=True)).to_have_count(1 if "platform.users.manage" in permissions else 0)
        expect(nav.get_by_role("button", name="Clothing settings", exact=True)).to_have_count(1 if clothing_access else 0)
        page.screenshot(path=str(output / f"{role}-workspace.png"))
        if clothing_access:
            requests_button = nav.get_by_role("button", name="Requests", exact=True)
            expect(requests_button).to_contain_text("2")
            expect(requests_button).to_have_attribute("title", "2 active requests")
            active_request_count = 0
            page.reload(wait_until="networkidle")
            expect(requests_button).not_to_contain_text("2")
            active_request_count = 2
            page.reload(wait_until="networkidle")
            expect(requests_button).to_contain_text("2")
            nav.get_by_role("button", name="Inventory", exact=True).click()
            expect(page.get_by_role("heading", name="Inventory Management", exact=True)).to_be_visible()
            if "inventory.manage" in permissions:
                page.get_by_role("button", name="Add coat variant", exact=True).click()
                variant_form = page.locator("form").filter(has=page.get_by_role("heading", name="New coat variant", exact=True))
                variant_form.get_by_label("Name", exact=True).fill("Testing coat")
                variant_form.get_by_label("Color", exact=True).fill("Navy blue")
                variant_form.get_by_label("Size", exact=True).fill("XL")
                page.get_by_role("button", name="Save coat variant", exact=True).click()
                expect(page.get_by_role("article", name="Coat variant Testing coat", exact=True)).to_be_visible()
                card = page.get_by_role("article", name="Coat variant Testing coat", exact=True)
                expect(card).to_contain_text("Navy blue")
                expect(card).to_contain_text("Size XL")
                expect(card.locator("svg")).to_have_count(1)
                assert "??" not in card.inner_text()
                card.scroll_into_view_if_needed()
                page.screenshot(path=str(output / f"{role}-coat-cards.png"))
            else:
                expect(page.get_by_role("button", name="Add coat variant", exact=True)).to_have_count(0)
            if role == "ClothingViewer": expect(page.get_by_role("button", name="+ Add Stock", exact=True)).to_be_disabled()
            else: expect(page.get_by_role("button", name="+ Add Stock", exact=True)).to_be_enabled()
            assert "/program/snapshot" in calls
            page.locator("main").evaluate("element => element.scrollTop = element.scrollHeight")
            assert page.locator("main").evaluate("element => element.scrollTop") > 0
            expect(nav.get_by_role("button", name="HR workspace", exact=True)).to_have_count(1)
            expect(page.get_by_role("button", name="Sign out", exact=True)).to_be_in_viewport()
            assert page.locator("aside").bounding_box()["y"] == 0
            assert page.evaluate("document.documentElement.scrollHeight <= window.innerHeight")
            page.screenshot(path=str(output / f"{role}-sidebar-scrolled.png"))
            nav.get_by_role("button", name="Clothing settings", exact=True).click()
            expect(page.get_by_role("heading", name="Clothing settings", exact=True)).to_be_visible()
            assert page.locator("main").evaluate("element => element.scrollTop") == 0
            if "clothing.settings.manage" in permissions:
                page.get_by_label("Low stock threshold (units per variant)", exact=True).fill("12")
                page.get_by_role("button", name="Save clothing settings", exact=True).click()
                expect(page.get_by_role("status").filter(has_text="Clothing settings saved")).to_be_visible()
                assert clothing_settings["lowStockThreshold"] == 12
                page.get_by_label("Eligibility interval", exact=True).fill("1")
                page.get_by_label("Eligibility unit", exact=True).select_option("Minutes")
                page.get_by_role("button", name="Save clothing settings", exact=True).click()
                expect(page.get_by_role("status").filter(has_text="Clothing settings saved")).to_be_visible()
                assert clothing_settings["eligibilityInterval"] == 1
                assert clothing_settings["eligibilityUnit"] == "Minutes"
            else:
                expect(page.get_by_label("Low stock threshold (units per variant)", exact=True)).to_be_disabled()
                expect(page.get_by_label("Eligibility interval", exact=True)).to_be_disabled()
            page.screenshot(path=str(output / f"{role}-clothing-settings.png"))
        else:
            assert "/coat-requests/active-count" not in calls
            nav.get_by_role("button", name="Employees", exact=True).click()
            expect(page.get_by_role("columnheader", name="Department", exact=True)).to_be_visible()
            assert "/program/snapshot" not in calls
        if role == "HrAdministrator":
            nav.get_by_role("button", name="Users & access", exact=True).click()
            page.get_by_role("button", name="Add user", exact=True).click()
            expect(page.get_by_role("checkbox", name="Clothing Manager", exact=False)).to_be_visible()
            expect(page.get_by_role("checkbox", name="Clothing Viewer", exact=False)).to_be_visible()
        nav.get_by_role("button", name="Organization settings", exact=True).click()
        expect(page.get_by_label("Business time zone", exact=True)).to_be_visible()
        expect(page.get_by_label("Low stock threshold (units per variant)", exact=True)).to_have_count(0)
        expect(page.get_by_label("Full name", exact=True)).to_have_count(0)
        if "platform.settings.manage" in permissions:
            page.get_by_label("Business time zone", exact=True).fill("UTC")
            page.get_by_role("button", name="Save time zone", exact=True).click()
            expect(page.get_by_role("status").filter(has_text="Business time zone saved")).to_be_visible()
            page.get_by_role("button", name="Add department", exact=True).click()
            page.get_by_label("Department code", exact=True).fill("HR")
            page.get_by_label("Department name", exact=True).fill("Human Resources")
            page.get_by_role("button", name="Save department", exact=True).click()
            expect(page.get_by_role("cell", name="Human Resources", exact=True)).to_be_visible()
        else: expect(page.get_by_label("Business time zone", exact=True)).to_be_disabled()
        page.screenshot(path=str(output / f"{role}-organization-settings.png"))
        nav.get_by_role("button", name="My account", exact=True).click()
        page.get_by_label("Full name", exact=True).fill("Updated profile")
        page.get_by_role("button", name="Save profile", exact=True).click()
        expect(page.get_by_role("status").filter(has_text="Profile saved")).to_be_visible()
        if clothing_access:
            page.get_by_label("Show low-stock notifications", exact=True).uncheck()
            page.get_by_role("button", name="Save notification preferences", exact=True).click()
            expect(page.get_by_role("status").filter(has_text="Notification preferences saved")).to_be_visible()
            assert preferences["notifyLowStock"] is False
        page.screenshot(path=str(output / f"{role}-account.png"))
        assert not errors, errors
        checks.append(f"{role}: scoped settings, separate saves, department/variant controls and minute eligibility, account preferences, and sidebar scrolling; no JavaScript errors")
        page.close()
    browser.close()
(output / "browser-checks.json").write_text(json.dumps({"checks": checks, "api": "Role fixtures; actual authorization covered by backend integration tests"}, indent=2))
print(json.dumps({"passed": len(checks), "checks": checks}, indent=2))
