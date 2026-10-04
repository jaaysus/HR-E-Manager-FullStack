"""Check stock-history filters and the downloaded XLSX against browser results."""
from datetime import datetime, timedelta, timezone
from pathlib import Path
from zipfile import ZipFile
import xml.etree.ElementTree as ET
from playwright.sync_api import sync_playwright, expect

output = Path(__file__).resolve().parents[1] / "artifacts" / "stock-history"
output.mkdir(parents=True, exist_ok=True)
ns = {"s": "http://schemas.openxmlformats.org/spreadsheetml/2006/main"}


def workbook_rows(path):
    with ZipFile(path) as book:
        strings = ET.fromstring(book.read("xl/sharedStrings.xml"))
        shared = ["".join(item.itertext()) for item in strings]
        sheet = ET.fromstring(book.read("xl/worksheets/sheet1.xml"))
        rows = []
        for row in sheet.findall("s:sheetData/s:row", ns):
            values = []
            for cell in row.findall("s:c", ns):
                assert cell.find("s:f", ns) is None, "Exported text must not become formulas"
                value = cell.findtext("s:v", default="", namespaces=ns)
                values.append(shared[int(value)] if cell.get("t") == "s" else value)
            rows.append(values)
        return rows


with sync_playwright() as playwright:
    browser = playwright.chromium.launch(headless=True)
    page = browser.new_page(viewport={"width": 1440, "height": 1000}, accept_downloads=True)
    errors = []
    page.on("pageerror", lambda error: errors.append(str(error)))
    session = {
        "email": "viewer@example.test", "fullName": "Clothing Viewer", "roles": ["ClothingViewer"],
        "permissions": ["platform.settings.read", "platform.notifications.read", "coats.dashboard.read", "coats.read", "inventory.read"],
        "accessToken": "fixture", "expiresAtUtc": (datetime.now(timezone.utc) + timedelta(hours=1)).isoformat(),
        "refreshTokenExpiresAtUtc": "2099-01-01T00:00:00Z",
    }
    settings = {"companyName": "Test", "timeZoneId": "Africa/Casablanca", "businessDate": "2026-10-04", "lowStockAlertsEnabled": False, "lowStockThreshold": 0, "rowVersion": "fixture"}
    items = [
        {"id": "a", "sku": "coat-a", "name": "Work Coat", "color": "Blue", "size": "M", "quantityOnHand": 9, "rowVersion": "fixture", "isActive": True},
        {"id": "b", "sku": "coat-b", "name": "Work Coat", "color": "Red", "size": "XL", "quantityOnHand": 4, "rowVersion": "fixture", "isActive": True},
    ]
    records = [
        ("1", "2026-10-01", "arrival", "coat-a", "Blue", "M", 10, 'Invoice 42 & "delivery"'),
        ("2", "2026-10-02", "allocation", "coat-a", "Blue", "M", -1, "Employee issue"),
        ("3", "2026-10-03", "adjustment", "coat-a", "Blue", "M", -2, "=SUM(1,2)"),
        ("4", "2026-10-03", "arrival", "coat-b", "Red", "XL", 4, "Invoice 42 second"),
        ("5", "2026-10-04", "arrival", "coat-a", "Blue", "M", 2, "Invoice 42"),
        ("6", "2026-10-04", "allocation", "retired", "Yellow", "L", -1, "Earlier coat"),
    ]
    movements = [{"id": id, "date": date, "type": type, "coatType": coat, "itemName": "Retired Coat" if coat == "retired" else "Work Coat", "color": color, "size": size, "qty": qty, "note": note} for id, date, type, coat, color, size, qty, note in reversed(records)]

    def route_api(route):
        path = route.request.url.split("/api", 1)[1].split("?", 1)[0]
        if path == "/auth/refresh": body = session
        elif path == "/settings": body = settings
        elif path == "/notifications/unread-count": body = {"unreadCount": 0}
        elif path == "/coat-requests/active-count": body = {"activeCount": 0}
        elif path == "/inventory/items": body = items
        elif path == "/program/snapshot": body = {"businessDate": "2026-10-04", "employees": [], "items": items, "movements": movements, "notifications": [], "settings": settings}
        else: body = {}
        route.fulfill(json=body)

    page.route("**/api/**", route_api)
    page.goto("http://127.0.0.1:8443", wait_until="networkidle")
    page.locator("nav").get_by_role("button", name="Inventory", exact=True).click()
    history = page.get_by_role("region", name="Stock movement history")
    history.scroll_into_view_if_needed()
    expect(history.get_by_role("status")).to_have_text("Showing 6 of 6 movements")
    history.get_by_label("Movement type", exact=True).select_option("arrival")
    history.get_by_label("Coat variant", exact=True).select_option("coat-a")
    history.get_by_label("Color", exact=True).select_option("Blue")
    history.get_by_label("Size", exact=True).select_option("M")
    history.get_by_label("From date", exact=True).fill("2026-10-01")
    history.get_by_label("To date", exact=True).fill("2026-10-04")
    history.get_by_label("Search movements", exact=True).fill("invoice 42")
    expect(history.get_by_role("status")).to_have_text("Showing 2 of 6 movements")
    expect(history.locator("tbody tr")).to_have_count(2)
    with page.expect_download() as download:
        history.get_by_role("button", name="Download Excel", exact=True).click()
    artifact = output / download.value.suggested_filename
    download.value.save_as(artifact)
    assert artifact.suffix == ".xlsx"
    rows = workbook_rows(artifact)
    assert rows[0] == ["Date", "Name", "Color", "Size", "Type", "Quantity", "Note"]
    assert len(rows) == 3
    assert [r[1:6] for r in rows[1:]] == [["Work Coat", "Blue", "M", "Arrival", "2"], ["Work Coat", "Blue", "M", "Arrival", "10"]]
    assert [(datetime(1899, 12, 30) + timedelta(days=float(row[0]))).date().isoformat() for row in rows[1:]] == ["2026-10-04", "2026-10-01"]
    assert rows[2][6] == 'Invoice 42 & "delivery"'
    page.screenshot(path=str(output / "filtered-history.png"))
    history.get_by_label("To date", exact=True).fill("2026-09-30")
    expect(history.get_by_role("alert")).to_contain_text("From date must be on or before To date")
    expect(history.get_by_role("button", name="Download Excel", exact=True)).to_be_disabled()
    history.get_by_role("button", name="Clear filters", exact=True).click()
    expect(history.get_by_role("status")).to_have_text("Showing 6 of 6 movements")
    history.get_by_label("Movement type", exact=True).select_option("adjustment")
    with page.expect_download() as download:
        history.get_by_role("button", name="Download Excel", exact=True).click()
    adjusted = output / "adjustment.xlsx"
    download.value.save_as(adjusted)
    rows = workbook_rows(adjusted)
    assert len(rows) == 2 and rows[1][5:] == ["-2", "=SUM(1,2)"]
    history.get_by_role("button", name="Clear filters", exact=True).click()
    history.get_by_label("Coat variant", exact=True).select_option("retired")
    expect(history.get_by_role("status")).to_have_text("Showing 1 of 6 movements")
    expect(history.locator("tbody")).to_contain_text("Retired Coat")
    history.get_by_label("Search movements", exact=True).fill("does not exist")
    expect(history.get_by_text("No movements match these filters.")).to_be_visible()
    expect(history.get_by_role("button", name="Download Excel", exact=True)).to_be_disabled()
    assert not errors, errors
    browser.close()
    print("Passed: combined filters, inclusive dates, XLSX content/date/quantity types, literal formula text, retired coats, reset, invalid dates, empty results, and read-only export.")
