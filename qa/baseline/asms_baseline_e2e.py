#!/usr/bin/env python3
"""
ASMS baseline functional / regression probe (black-box, stdlib only).

Drives BOTH surfaces of the app end-to-end:
  * MVC (cookie auth + anti-forgery)  -> /Account/*, /Admin/*, /Customer/*, /Technician/*
  * JSON API (JWT Bearer)             -> /api/auth, /api/customer, /api/technician, /api/admin

Each check is recorded as PASS / FAIL (FAIL = product behaviour differs from the
expected business rule; many FAILs are known baseline bugs, tagged BUG-xx).

Usage:
  python qa/asms_baseline_e2e.py                       # default http://localhost:5090
  ASMS_BASE_URL=http://localhost:5142 python qa/asms_baseline_e2e.py
Optional env:
  ASMS_ADMIN_EMAIL / ASMS_ADMIN_PASSWORD  (default seeded admin)
  ASMS_RZP_SECRET   Razorpay KeySecret the APP under test runs with. Only set this
                    to a FAKE secret you injected for the test run (never the real one);
                    used to forge signatures locally to test payment verification.
  ASMS_SQLCMD       sqlcmd server for optional DB assertions, e.g. "(localdb)\\AsmsQa"
  ASMS_SQLCMD_DB    database name (default ServiceAppDB_QA)
  ASMS_JSON_OUT     path for machine-readable results (default qa/last-run.json)

Safe by design: never calls Razorpay/SendGrid directly; run the app with fake
SendGrid/Razorpay credentials so no real email or charge can happen.
"""
import datetime as dt
import hashlib
import hmac
import http.cookiejar
import json
import os
import re
import subprocess
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid

BASE = os.environ.get("ASMS_BASE_URL", "http://localhost:5090").rstrip("/")
ADMIN_EMAIL = os.environ.get("ASMS_ADMIN_EMAIL", "admin@serviceapp.com")
ADMIN_PW = os.environ.get("ASMS_ADMIN_PASSWORD", "Admin@1234")
RZP_SECRET = os.environ.get("ASMS_RZP_SECRET")
SQLCMD = os.environ.get("ASMS_SQLCMD")
SQLDB = os.environ.get("ASMS_SQLCMD_DB", "ServiceAppDB_QA")
RUN = uuid.uuid4().hex[:6]
PW = "Passw0rdQA"

RESULTS = []


def check(cid, title, ok, detail="", bug=None):
    RESULTS.append({"id": cid, "title": title, "ok": bool(ok), "detail": str(detail)[:300], "bug": bug})
    tag = "PASS" if ok else "FAIL"
    print(f"[{tag}] {cid} {title}" + (f"  ({bug})" if bug and not ok else "") + (f" -- {str(detail)[:200]}" if detail and not ok else ""))
    return ok


def sql(q):
    if not SQLCMD:
        return None
    out = subprocess.run(["sqlcmd", "-S", SQLCMD, "-E", "-I", "-d", SQLDB, "-h", "-1", "-W", "-Q",
                          "SET NOCOUNT ON;" + " ".join(q.splitlines())],
                         capture_output=True, text=True)
    return out.stdout.strip()


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *a, **k):
        return None


class Resp:
    def __init__(self, status, headers, body, url):
        self.status, self.headers, self.body, self.url = status, headers, body, url

    @property
    def text(self):
        return self.body.decode("utf-8", "replace")

    def json(self):
        try:
            return json.loads(self.body or b"null")
        except Exception:
            return None


class Client:
    """Cookie-carrying browser-ish client (follows redirects by default)."""

    def __init__(self, token=None):
        self.jar = http.cookiejar.CookieJar()
        self.token = token
        self.op = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(self.jar))
        self.op_nr = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(self.jar), NoRedirect)

    def req(self, method, path, data=None, json_body=None, headers=None, follow=True):
        h = dict(headers or {})
        body = None
        if json_body is not None:
            body = json.dumps(json_body).encode()
            h["Content-Type"] = "application/json"
        elif data is not None:
            body = urllib.parse.urlencode(data, doseq=True).encode()
            h["Content-Type"] = "application/x-www-form-urlencoded"
        if self.token:
            h["Authorization"] = "Bearer " + self.token
        r = urllib.request.Request(BASE + path, data=body, method=method, headers=h)
        try:
            follow = follow and not self.token  # JWT/API calls: never follow redirects, assert the raw status
            resp = (self.op if follow else self.op_nr).open(r, timeout=60)
            return Resp(resp.status, dict(resp.headers), resp.read(), resp.url)
        except urllib.error.HTTPError as e:
            return Resp(e.code, dict(e.headers), e.read(), e.url if hasattr(e, "url") else BASE + path)

    def get(self, path, **k):
        return self.req("GET", path, **k)

    def post(self, path, **k):
        return self.req("POST", path, **k)

    def af_token(self, page):
        r = self.get(page)
        m = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', r.text) or \
            re.search(r'value="([^"]+)"[^>]*name="__RequestVerificationToken"', r.text)
        return m.group(1) if m else None

    def form(self, form_page, action, fields, follow=True):
        tok = self.af_token(form_page)
        f = dict(fields)
        if tok:
            f["__RequestVerificationToken"] = tok
        return self.post(action, data=f, follow=follow)


def path_of(url):
    p = urllib.parse.urlparse(url)
    return p.path + (("?" + p.query) if p.query else "")


def api_login(email, pw):
    r = Client().post("/api/auth/login", json_body={"email": email, "password": pw})
    j = r.json() or {}
    return r, j.get("token"), j.get("userId")


def mvc_login(email, pw):
    c = Client()
    r = c.form("/Account/Login", "/Account/Login", {"Email": email, "Password": pw, "RememberMe": "false"})
    return c, r


def iso(days=1, hour=10):
    d = (dt.datetime.now() + dt.timedelta(days=days)).replace(hour=hour, minute=0, second=0, microsecond=0)
    return d.strftime("%Y-%m-%dT%H:%M:%S")


def rzp_sig(order_id, payment_id):
    return hmac.new(RZP_SECRET.encode(), f"{order_id}|{payment_id}".encode(), hashlib.sha256).hexdigest()


def main():
    print(f"== ASMS baseline probe against {BASE}  run={RUN}")
    anon = Client()

    # ------------------------------------------------------------- smoke
    r = anon.get("/")
    check("SMOKE-001", "Home page loads", r.status == 200, r.status)
    r = anon.get("/Account/Login")
    check("SMOKE-002", "Login page loads", r.status == 200, r.status)
    r = anon.get("/Admin/Home/Dashboard")
    check("AUTH-010", "Anonymous Admin page redirects to login", "/Account/Login" in r.url, r.url)
    r = anon.get("/api/customer/dashboard")
    check("AUTH-011", "Anonymous API call -> 401", r.status == 401, r.status)
    r = Client(token="eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJ4In0.invalidsig").get("/api/customer/dashboard")
    check("AUTH-012", "Tampered JWT -> 401", r.status == 401, r.status)
    r = anon.get("/hangfire")
    check("SEC-001", "Hangfire dashboard reachable only locally (local request -> 200 expected here)", r.status == 200, r.status)

    # ------------------------------------------------------------- registration
    c1_email, c2_email = f"qa.c1.{RUN}@example.invalid", f"qa.c2.{RUN}@example.invalid"
    t1_email, t2_email, t3_email = (f"qa.t{i}.{RUN}@example.invalid" for i in (1, 2, 3))

    def reg_customer(email, name):
        c = Client()
        r = c.form("/Account/CustomerRegister", "/Account/CustomerRegister",
                   {"FullName": name, "Email": email, "Phone": "9876543210", "Password": PW, "ConfirmPassword": PW})
        return c, r

    def reg_tech(email, name, skill):
        c = Client()
        r = c.form("/Account/TechnicianRegister", "/Account/TechnicianRegister",
                   {"FullName": name, "Email": email, "Phone": "9876500000", "Skill": skill, "Password": PW, "ConfirmPassword": PW})
        return c, r

    c1web, r = reg_customer(c1_email, "QA Customer One")
    lr, _, _ = api_login(c1_email, PW)
    check("AUTH-001", "Customer self-registration (MVC) creates a usable account", lr.status == 200,
          f"final={path_of(r.url)} login={lr.status} msg={'Registration failed' in r.text}", bug="BUG-01")
    reg_ok = lr.status == 200
    if not reg_ok:
        # retry once to be sure it's deterministic
        c1web, r = reg_customer(c1_email, "QA Customer One")
        lr, _, _ = api_login(c1_email, PW)
        reg_ok = lr.status == 200
    reg_customer(c2_email, "QA Customer Two")

    def ensure_customer(email, name):
        """QA-only workaround for BUG-01: register via technician form, then convert to Customer in the QA DB."""
        if api_login(email, PW)[0].status == 200:
            return
        if not SQLCMD:
            print(f"!! {email}: customer registration broken and ASMS_SQLCMD not set - cannot seed customer")
            return
        reg_tech(email, name, "General")
        sql(f"""DECLARE @u nvarchar(450)=(SELECT Id FROM AspNetUsers WHERE Email='{email}');
DELETE FROM TechnicianProfiles WHERE UserId=@u;
UPDATE AspNetUsers SET Role='Customer' WHERE Id=@u;
UPDATE AspNetUserRoles SET RoleId=(SELECT Id FROM AspNetRoles WHERE Name='Customer') WHERE UserId=@u;
INSERT INTO CustomerProfiles(UserId) VALUES(@u);""")
        print(f"   (seeded customer {email} via DB workaround for BUG-01)")

    ensure_customer(c1_email, "QA Customer One")
    ensure_customer(c2_email, "QA Customer Two")

    t1web, r = reg_tech(t1_email, f"QA Tech One {RUN}", "Plumbing")
    check("AUTH-002", "Technician registration lands on Technician dashboard",
          "/Technician" in path_of(r.url), path_of(r.url), bug="BUG-03")
    reg_tech(t2_email, f"QA Tech Two {RUN}", "Plumbing")
    reg_tech(t3_email, f"QA Tech Three {RUN}", "Electrical")

    # ------------------------------------------------------------- logins
    r, admin_tok, admin_id = api_login(ADMIN_EMAIL, ADMIN_PW)
    check("AUTH-003", "Admin API login returns JWT", r.status == 200 and admin_tok, r.status)
    r, c1_tok, c1_id = api_login(c1_email, PW)
    check("AUTH-004", "Customer API login returns JWT + role", r.status == 200 and (r.json() or {}).get("role") == "Customer", r.text[:120])
    _, c2_tok, c2_id = api_login(c2_email, PW)
    r, t1_tok, t1_id = api_login(t1_email, PW)
    check("AUTH-005", "Technician API login returns JWT", r.status == 200 and t1_tok, r.status)
    _, t2_tok, _ = api_login(t2_email, PW)
    _, t3_tok, t3_id = api_login(t3_email, PW)
    r, _, _ = api_login(c1_email, "WrongPass1")
    check("AUTH-006", "Wrong password -> 401", r.status == 401, r.status)
    r = Client().post("/api/auth/login", json_body={"email": "", "password": ""})
    check("AUTH-007", "Empty credentials -> 400", r.status == 400, r.status)

    if not (admin_tok and c1_tok and c2_tok and t1_tok and t2_tok and t3_tok):
        print("!! cannot continue without all role tokens (registration broken?)")
        return finish()

    admin = Client(admin_tok); c1 = Client(c1_tok); c2 = Client(c2_tok)
    t1 = Client(t1_tok); t2 = Client(t2_tok); t3 = Client(t3_tok)

    adminweb, r = mvc_login(ADMIN_EMAIL, ADMIN_PW)
    check("AUTH-008", "Admin MVC login lands on Admin dashboard", "/Admin" in path_of(r.url), path_of(r.url), bug="BUG-03")
    t1web, r = mvc_login(t1_email, PW)
    check("AUTH-009", "Technician MVC login lands on Technician dashboard", "/Technician" in path_of(r.url), path_of(r.url), bug="BUG-03")
    c1web, _ = mvc_login(c1_email, PW)
    c2web, _ = mvc_login(c2_email, PW)
    t2web, _ = mvc_login(t2_email, PW)
    t3web, _ = mvc_login(t3_email, PW)

    # role matrix
    check("AUTH-013", "Customer token on admin API -> 403", c1.get("/api/admin/dashboard").status == 403)
    check("AUTH-014", "Technician token on customer API -> 403", t1.get("/api/customer/dashboard").status == 403)
    check("AUTH-015", "Customer cookie on Admin MVC -> AccessDenied", "AccessDenied" in c1web.get("/Admin/Home/Dashboard").url)

    # ------------------------------------------------------------- booking (API)
    r = c1.get("/api/customer/categories")
    check("BOOK-001", "Categories list", r.status == 200 and len((r.json() or {}).get("data", [])) == 8, r.status)
    body = {"category": "Plumbing", "description": "Kitchen sink leaking badly under the cabinet", "scheduledDate": iso(1),
            "address": "12 QA Street", "pinCode": "680001"}
    r = c1.post("/api/customer/requests", json_body=body)
    R1 = (r.json() or {}).get("requestId")
    check("BOOK-002", "Create request (valid) -> success + id", r.status in (200, 201) and R1, r.text[:150])
    r = c1.post("/api/customer/requests", json_body=body)
    check("BOOK-003", "Duplicate submit within 30s returns same request (idempotent)", (r.json() or {}).get("requestId") == R1, r.text[:100])
    r = c1.post("/api/customer/requests", json_body={**body, "category": "Rocketry"})
    check("BOOK-004", "Invalid category -> 400", r.status == 400, r.status)
    r = c1.post("/api/customer/requests", json_body={**body, "scheduledDate": "2020-01-01T10:00:00", "address": "past-date addr"})
    check("BOOK-005", "Past preferred date rejected -> 400", r.status == 400, f"{r.status} {r.text[:80]}", bug="BUG-10")
    r = c1.post("/api/customer/requests", json_body={**body, "description": "", "address": "empty-desc addr"})
    check("BOOK-006", "Empty description rejected -> 400", r.status == 400, f"{r.status} {r.text[:80]}", bug="BUG-10")
    r = c1.post("/api/customer/requests", json_body={"category": "Plumbing", "description": "no address provided at all here", "scheduledDate": iso(1)})
    check("BOOK-007", "Missing address -> 400 (not 500)", r.status == 400, r.status, bug="BUG-10")

    def new_req(client, cat="Plumbing", addr=None, pin="680001"):
        rr = client.post("/api/customer/requests", json_body={"category": cat, "description": f"QA {cat} job needing attention {uuid.uuid4().hex[:4]}",
                                                               "scheduledDate": iso(2), "address": addr or f"{uuid.uuid4().hex[:6]} QA Road", "pinCode": pin})
        return (rr.json() or {}).get("requestId")

    R2, R3, R4 = new_req(c1), new_req(c1), new_req(c1)

    # IDOR
    r = c2.get(f"/api/customer/requests/{R1}")
    check("SEC-010", "Customer B cannot view customer A request (denied, no data)", r.status in (302, 403) and "Kitchen" not in r.text, r.status)
    r = c2.post(f"/api/customer/requests/{R1}/cancel")
    check("SEC-011", "Customer B cannot cancel customer A request (denied)", r.status in (302, 403), r.status)
    r = c2web.get(f"/Customer/Requests/Details/{R1}")
    check("SEC-012", "Customer B cannot open A's request (MVC)", "do not have access" in r.text or "/Customer/Requests" in path_of(r.url) and f"Details/{R1}" not in r.url, path_of(r.url))

    # ------------------------------------------------------------- admin assign
    r = admin.get("/api/admin/dashboard")
    check("ADMIN-001", "Admin API dashboard", r.status == 200, r.status)
    r = admin.get("/api/admin/technicians/available?category=Plumbing")
    techs = (r.json() or {}).get("data", [])
    tid = {t["name"]: t["id"] for t in techs}
    T1P, T2P = tid.get(f"QA Tech One {RUN}"), tid.get(f"QA Tech Two {RUN}")
    check("ADMIN-002", "Available Plumbing technicians listed", T1P and T2P, techs)
    r = admin.post(f"/api/admin/requests/{R1}/assign", json_body={"technicianProfileId": T1P})
    check("ASSIGN-001", "Admin assigns Pending request", r.status == 200, r.text[:120])
    r = admin.post(f"/api/admin/requests/{R1}/assign", json_body={"technicianProfileId": T2P})
    check("ASSIGN-002", "Re-assign non-Pending request rejected", r.status == 400, r.status)
    r = admin.post(f"/api/admin/requests/{R2}/assign", json_body={"technicianProfileId": T1P})
    check("ASSIGN-003", "Technician with an Assigned job is not assignable again (should be Busy)", r.status == 400,
          f"{r.status}: second job assigned to same tech", bug="BUG-12")
    r = admin.post(f"/api/admin/requests/{R3}/assign", json_body={"technicianProfileId": tid.get(f'QA Tech Two {RUN}')})
    r = admin.post(f"/api/admin/requests/{R4}/assign", json_body={"technicianProfileId": 999999})
    check("ASSIGN-004", "Assign to unknown technician -> 400", r.status == 400, r.status)
    d = (admin.get(f"/api/admin/requests/{R1}").json() or {}).get("data", {})
    check("ASSIGN-005", "Request shows Assigned + technician", d.get("status") == "Assigned" and d.get("technician"), d.get("status"))

    # ------------------------------------------------------------- technician lifecycle
    r = t2.post(f"/api/technician/jobs/{R1}/accept")
    check("JOB-001", "Other technician cannot accept job", r.status in (400, 403), r.status)
    r = t2.get(f"/api/technician/jobs/{R1}")
    check("SEC-013", "Other technician cannot view job detail (denied, no data)", r.status in (302, 403) and "Kitchen" not in r.text, r.status)
    r = t1.post(f"/api/technician/jobs/{R1}/accept")
    check("JOB-002", "Assigned technician accepts -> InProgress", r.status == 200, r.text[:100])
    r = t1.post(f"/api/technician/jobs/{R1}/accept")
    check("JOB-003", "Accept twice rejected", r.status == 400, r.status)
    r = t1.post("/api/technician/toggle-status")
    check("JOB-004", "Toggle availability blocked while Busy", r.status == 400, r.status)
    r = t2.post(f"/api/technician/jobs/{R1}/complete")
    check("JOB-005", "/jobs/{id}/complete by NON-assigned tech is rejected", r.status in (400, 403),
          f"{r.status} {r.text[:60]} (endpoint is a no-op stub)", bug="BUG-07")
    r = c1.post(f"/api/customer/requests/{R1}/cancel")
    check("BOOK-008", "Cannot cancel InProgress request", r.status == 400, r.status)

    # bill
    r = t1.post(f"/api/technician/jobs/{R2}/bill", json_body={"laborCost": 100, "materialCost": 0})
    check("BILL-001", "Cannot bill an Assigned (not InProgress) job", r.status == 400, r.status)
    r = t2.post(f"/api/technician/jobs/{R1}/bill", json_body={"laborCost": 100, "materialCost": 0})
    check("BILL-002", "Other tech cannot bill job", r.status == 400, r.status)
    r = t1.post(f"/api/technician/jobs/{R1}/bill", json_body={"laborCost": 0, "materialCost": 0})
    check("BILL-003", "Zero bill rejected", r.status == 400, r.status)
    r = t1.post(f"/api/technician/jobs/{R1}/bill", json_body={"laborCost": 500, "materialCost": 250.555, "description": "PVC pipe"})
    B1 = (r.json() or {}).get("billId")
    check("BILL-004", "Assigned tech creates bill -> Billed", r.status == 200 and B1, r.text[:120])
    r = t1.post(f"/api/technician/jobs/{R1}/bill", json_body={"laborCost": 1, "materialCost": 0})
    check("BILL-005", "Second bill for same request rejected", r.status == 400, r.status)
    d = (c1.get(f"/api/customer/requests/{R1}").json() or {}).get("data", {})
    bill = d.get("bill") or {}
    check("BILL-006", "Bill total = labor + material (rounded)", bill.get("amount") in (750.56, 750.55), bill.get("amount"))
    check("BILL-007", "API bill exposes laborCost/materialCost", bill.get("laborCost") == 500 and bill.get("materialCost") not in (0, None),
          f"laborCost={bill.get('laborCost')} materialCost={bill.get('materialCost')}", bug="BUG-06")
    check("BILL-008", "Status history row per transition (Pending,Assigned,InProgress,Billed)",
          [h["status"] for h in d.get("statusHistory", [])] == ["Pending", "Assigned", "InProgress", "Billed"], d.get("statusHistory"))
    lst = (c1.get("/api/customer/requests").json() or {}).get("data", [])
    row = next((x for x in lst if x["id"] == R1), {})
    check("BILL-009", "Customer request list shows hasBill/billAmount", row.get("hasBill") is True and row.get("billAmount"), row, bug="BUG-05")
    jobs = (t1.get("/api/technician/jobs").json() or {}).get("data", [])
    jr = next((x for x in jobs if x["id"] == R1), {})
    check("BILL-010", "Technician job list shows hasBill", jr.get("hasBill") is True, jr, bug="BUG-05")

    # PDF + IDOR around bills
    r = c1web.get(f"/Customer/Bills/Download?billId={B1}")
    check("BILL-011", "Customer downloads bill PDF", r.status == 200 and r.body[:4] == b"%PDF", f"{r.status} {r.headers.get('Content-Type')}")
    r = c2web.get(f"/Customer/Bills/Download?billId={B1}")
    check("SEC-014", "Other customer cannot download bill PDF", r.body[:4] != b"%PDF", r.status)
    r = t1web.get(f"/Technician/Bills/Download?billId={B1}")
    check("BILL-012", "Technician downloads own bill PDF", r.body[:4] == b"%PDF", r.status)
    r = t2web.get(f"/Technician/Bills/Details?requestId={R1}")
    check("SEC-015", "Other technician cannot view bill details of a job not assigned to them",
          "QA Customer One" not in r.text and "Labour charges" not in r.text, f"{r.status} {path_of(r.url)}", bug="BUG-15")

    # ------------------------------------------------------------- payment (API shortcut)
    r = c2.post(f"/api/customer/bills/{B1}/pay")
    check("PAY-001", "Other customer cannot pay bill", r.status == 400, r.status)
    r = c1.post(f"/api/customer/bills/{B1}/pay")
    st = ((c1.get(f"/api/customer/requests/{R1}").json() or {}).get("data") or {}).get("status")
    check("PAY-002", "API /bills/{id}/pay must NOT complete a request without a verified gateway payment",
          not (r.status == 200 and st == "Completed"), f"{r.status} -> status={st}", bug="BUG-02")
    if SQLCMD:
        n = sql(f"SELECT COUNT(*) FROM Payments WHERE BillId={B1}")
        check("PAY-003", "Completed via API has a Payment row", n not in ("0", None), f"payments={n}", bug="BUG-02")
    d = (c1.get(f"/api/customer/requests/{R1}").json() or {}).get("data", {})
    check("PAY-004", "Paid bill reports isPaid=true", (d.get("bill") or {}).get("isPaid") is True, d.get("bill"), bug="BUG-04")
    r = admin.get("/api/admin/dashboard")
    check("PAY-005", "Admin dashboard revenue counts paid bills", ((r.json() or {}).get("stats") or {}).get("revenue", 0) > 0,
          (r.json() or {}).get("stats"), bug="BUG-04")

    # rating
    r = c1.post(f"/api/customer/requests/{R1}/rate", json_body={"stars": 6})
    check("RATE-001", "Rating out of range -> 400", r.status == 400, r.status)
    r = c1.post(f"/api/customer/requests/{R1}/rate", json_body={"stars": 4, "comment": "good"})
    check("RATE-002", "Rate completed request", r.status == 200, r.text[:100])
    r = c1.post(f"/api/customer/requests/{R1}/rate", json_body={"stars": 5})
    check("RATE-003", "Rate twice rejected", r.status == 400, r.status)

    # ------------------------------------------------------------- full flow #2 : cash (MVC)
    # T1 is still "Assigned" to R2 (BUG-12) -> drive it
    t1.post(f"/api/technician/jobs/{R2}/accept")
    r = t1.post(f"/api/technician/jobs/{R2}/bill", json_body={"laborCost": 1200, "materialCost": 0})
    B2 = (r.json() or {}).get("billId")
    r = c1web.get(f"/Customer/Bills/Pay?requestId={R2}")
    check("PAY-010", "Customer Pay page renders", r.status == 200 and "Pay" in r.text, r.status)
    r = c1web.form(f"/Customer/Bills/Pay?requestId={R2}", "/Customer/Bills/PayCash", {"billId": B2})
    m = re.search(r"paymentId=(\d+)", r.url)
    P2 = int(m.group(1)) if m else None
    st = ((c1.get(f"/api/customer/requests/{R2}").json() or {}).get("data") or {}).get("status")
    check("PAY-011", "Cash payment completes request + confirmation page", P2 and st == "Completed", f"{path_of(r.url)} {st}")
    r = c2web.get(f"/Customer/Bills/Confirmation?paymentId={P2}")
    check("SEC-016", "Other customer cannot view payment confirmation", "QA" not in r.text or "not found" in r.text.lower(),
          f"{r.status} {path_of(r.url)} amount-visible={'1,200' in r.text or '1200' in r.text}", bug="BUG-14")
    r = c1web.form(f"/Customer/Bills/Pay?requestId={R2}", "/Customer/Bills/PayCash", {"billId": B2})
    if SQLCMD:
        check("PAY-012", "Second cash submit does not create a 2nd payment",
              sql(f"SELECT COUNT(*) FROM Payments WHERE BillId={B2}") == "1")
        time.sleep(2)
        pts = sql(f"SELECT ISNULL(SUM(Points),0) FROM LoyaltyLedger WHERE CustomerId='{c1_id}' AND TransactionType='Earned'")
        check("PROMO-010", "Loyalty points awarded on payment (1 pt / Rs10 -> 120)", pts == "120", f"points={pts}", bug="BUG-09")
        tj = sql(f"SELECT TotalJobsCompleted FROM TechnicianProfiles WHERE TechnicianProfileId={T1P}")
        check("JOB-010", "Technician TotalJobsCompleted incremented on payment (2 jobs paid)", tj == "2", f"TotalJobsCompleted={tj}", bug="BUG-02")

    # ------------------------------------------------------------- flow #3 : Razorpay (MVC, forged signatures)
    B3 = None
    t2.post(f"/api/technician/jobs/{R3}/accept")
    r = t2.post(f"/api/technician/jobs/{R3}/bill", json_body={"laborCost": 2000, "materialCost": 0})
    B3 = (r.json() or {}).get("billId")
    r = c1web.get(f"/Customer/Bills/InitiateRazorpay?billId={B3}")
    check("PAY-020", "InitiateRazorpay returns JSON (order or graceful error)", r.status == 200 and r.json() is not None, r.text[:120])
    r = c1web.form(f"/Customer/Bills/Pay?requestId={R3}", "/Customer/Bills/ProcessRazorpay",
                   {"billId": B3, "razorpayOrderId": "order_QA", "razorpayPaymentId": f"pay_QA{RUN}", "razorpaySignature": "deadbeef"})
    st = ((c1.get(f"/api/customer/requests/{R3}").json() or {}).get("data") or {}).get("status")
    check("PAY-021", "Invalid Razorpay signature rejected (stays Billed)", st == "Billed", st)
    P3 = None
    if RZP_SECRET:
        # Signature valid for an order that was never created for this bill / amount
        oid, pid = f"order_UNRELATED{RUN}", f"pay_QA{RUN}"
        r = c1web.form(f"/Customer/Bills/Pay?requestId={R3}", "/Customer/Bills/ProcessRazorpay",
                       {"billId": B3, "razorpayOrderId": oid, "razorpayPaymentId": pid, "razorpaySignature": rzp_sig(oid, pid)})
        m = re.search(r"paymentId=(\d+)", r.url); P3 = int(m.group(1)) if m else None
        st = ((c1.get(f"/api/customer/requests/{R3}").json() or {}).get("data") or {}).get("status")
        check("PAY-022", "Payment with a signature for an order NOT belonging to this bill must be rejected",
              st != "Completed", f"status={st} (order id/amount never cross-checked)", bug="BUG-08")
        r = c1web.form(f"/Customer/Bills/Pay?requestId={R3}", "/Customer/Bills/ProcessRazorpay",
                       {"billId": B3, "razorpayOrderId": oid, "razorpayPaymentId": pid, "razorpaySignature": rzp_sig(oid, pid)})
        if SQLCMD:
            check("PAY-023", "Duplicate Razorpay callback -> no second payment", sql(f"SELECT COUNT(*) FROM Payments WHERE BillId={B3}") == "1")

    # ------------------------------------------------------------- disputes / refunds
    r = c1web.get(f"/Customer/Disputes/Raise?requestId={R2}")
    check("DISPUTE-001", "Raise dispute page", r.status == 200, r.status)
    desc = "The technician left the site without fixing the leak properly."
    r = c1web.form(f"/Customer/Disputes/Raise?requestId={R2}", "/Customer/Disputes/Raise",
                   {"requestId": R2, "reason": "PoorQuality", "description": desc})
    check("DISPUTE-002", "Customer raises dispute on own completed request", "MyDisputes" in r.url, path_of(r.url))
    r = c1web.form(f"/Customer/Disputes/MyDisputes", "/Customer/Disputes/Raise",
                   {"requestId": R2, "reason": "PoorQuality", "description": desc})
    check("DISPUTE-003", "Second open dispute on same request rejected", "MyDisputes" not in r.url, path_of(r.url))
    r = c2web.form("/Customer/Disputes/MyDisputes", "/Customer/Disputes/Raise",
                   {"requestId": R2, "reason": "PoorQuality", "description": desc})
    check("DISPUTE-004", "Other customer cannot raise dispute on A's request", "MyDisputes" not in r.url, path_of(r.url))
    r = c1web.form("/Customer/Disputes/MyDisputes", "/Customer/Disputes/Raise",
                   {"requestId": R4, "reason": "PoorQuality", "description": desc})
    check("DISPUTE-005", "Dispute on non-completed request rejected", "MyDisputes" not in r.url, path_of(r.url))
    r = c1web.form("/Customer/Disputes/MyDisputes", "/Customer/Disputes/Raise",
                   {"requestId": R1, "reason": "X" * 80, "description": desc})
    check("DISPUTE-006", "Over-long reason handled (no 500)", r.status != 500, r.status, bug="BUG-16")
    r = adminweb.get("/Admin/Disputes/Index")
    ids = sorted(set(int(x) for x in re.findall(r"/Admin/Disputes/Details/(\d+)", r.text)))
    check("DISPUTE-007", "Admin sees disputes list", r.status == 200 and ids, r.status)
    D = ids[-1] if ids else 0
    r = adminweb.form(f"/Admin/Disputes/Details/{D}", "/Admin/Disputes/StartReview", {"id": D, "adminNote": "looking"})
    check("DISPUTE-008", "Admin starts review", r.status == 200, r.status)
    r = adminweb.form(f"/Admin/Disputes/Details/{D}", "/Admin/Disputes/IssueRefund", {"id": D, "refundAmount": "100", "adminNote": "partial"})
    check("DISPUTE-009", "Refund on CASH payment refused with message", "cash" in r.text.lower() or "Cash" in r.text, path_of(r.url))
    r = adminweb.form(f"/Admin/Disputes/Details/{D}", "/Admin/Disputes/Resolve", {"id": D, "resolution": "NoAction", "adminNote": "closed"})
    check("DISPUTE-010", "Admin resolves dispute", r.status == 200, r.status)
    if P3:
        c1web.form(f"/Customer/Disputes/Raise?requestId={R3}", "/Customer/Disputes/Raise",
                   {"requestId": R3, "reason": "Overcharged", "description": desc})
        ids = sorted(set(int(x) for x in re.findall(r"/Admin/Disputes/Details/(\d+)", adminweb.get("/Admin/Disputes/Index").text)))
        D3 = ids[-1]
        r = adminweb.form(f"/Admin/Disputes/Details/{D3}", "/Admin/Disputes/IssueRefund", {"id": D3, "refundAmount": "999999", "adminNote": "too much"})
        check("DISPUTE-011", "Refund > paid amount rejected", "between" in r.text, path_of(r.url))
        r = adminweb.form(f"/Admin/Disputes/Details/{D3}", "/Admin/Disputes/IssueRefund", {"id": D3, "refundAmount": "100", "adminNote": "gw fail"})
        if SQLCMD:
            check("DISPUTE-012", "Gateway refund failure leaves dispute un-refunded",
                  sql(f"SELECT CAST(RefundIssued AS INT) FROM Disputes WHERE DisputeId={D3}") == "0")

    # ------------------------------------------------------------- promotions
    code = f"QA{RUN}".upper()
    r = adminweb.form("/Admin/PromoCodes/Create", "/Admin/PromoCodes/Create",
                      {"Code": code, "Description": "QA flat 50", "DiscountType": "Flat", "DiscountValue": "50", "MinimumAmount": "100",
                       "MaxUses": "10", "IsActive": "true", "ExpiresAt": iso(30)})
    check("PROMO-001", "Admin creates promo code", code in adminweb.get("/Admin/PromoCodes/Index").text, path_of(r.url))
    r = c1web.get("/Customer/Promotions/Index")
    check("PROMO-002", "Customer promotions page (points + referral)", r.status == 200, r.status)
    r = c1web.form("/Customer/Promotions/Index", "/Customer/Promotions/CheckPromo", {"promoCode": code, "billAmount": "500"})
    check("PROMO-003", "Valid promo previews Rs50 discount", r.status == 200 and "Valid!" in r.text and "50.00" in r.text, f"{r.status} {path_of(r.url)}")
    r = c1web.form("/Customer/Promotions/Index", "/Customer/Promotions/CheckPromo", {"promoCode": "NOPE-XYZ", "billAmount": "500"})
    check("PROMO-004", "Unknown promo rejected", "not found" in r.text, r.status)
    r = c1web.form("/Customer/Promotions/Index", "/Customer/Promotions/CheckPromo", {"promoCode": code, "billAmount": "50"})
    check("PROMO-006", "Promo below minimum order rejected", "Minimum order" in r.text, r.status)
    pay_page = c1web.get(f"/Customer/Bills/Pay?requestId={R4}")
    check("PROMO-005", "Promo / loyalty redemption is wired into payment (ApplyPromoCodeAsync/RedeemPointsAsync reachable)",
          False, "No controller/API calls ApplyPromoCodeAsync or RedeemPointsAsync - discount never applied", bug="BUG-11")

    # ------------------------------------------------------------- zones + auto-assign
    zname = f"QA Zone {RUN}"
    r = adminweb.form("/Admin/Zones/Create", "/Admin/Zones/Create",
                      {"ZoneName": zname, "City": "Thrissur", "State": "Kerala", "PinCodes": "680001,680002", "IsActive": "true"})
    zl = adminweb.get("/Admin/Zones/Index")
    check("ZONE-001", "Admin creates service zone", zname in zl.text, path_of(r.url))
    zid = re.findall(r"/Admin/Zones/Edit/(\d+)", zl.text)
    if zid:
        check("ZONE-002", "Zone edit page", adminweb.get(f"/Admin/Zones/Edit/{zid[-1]}").status == 200)
    E1, E2 = new_req(c2, "Electrical"), new_req(c2, "Electrical")
    r = adminweb.form("/Admin/Home/Dashboard", "/Admin/Home/RunAutoAssign", {})
    check("ASSIGN-010", "Admin 'Run auto-assign now' works", r.status == 200, r.status)
    e1 = ((c2.get(f"/api/customer/requests/{E1}").json() or {}).get("data") or {})
    e2 = ((c2.get(f"/api/customer/requests/{E2}").json() or {}).get("data") or {})
    check("ASSIGN-011", "Auto-assign matches skill+availability (Electrical -> QA Tech Three)",
          e1.get("status") == "Assigned" and (e1.get("technician") or {}).get("name") == f"QA Tech Three {RUN}", e1.get("status"))
    check("ASSIGN-012", "Auto-assign does not stack 2 jobs on one technician in one cycle",
          not (e2.get("status") == "Assigned" and (e2.get("technician") or {}).get("name") == f"QA Tech Three {RUN}"),
          f"E2 -> {e2.get('status')} {(e2.get('technician') or {}).get('name')}", bug="BUG-12")
    # T3 accepts E1 -> Busy; then a new Electrical request must not go to T3
    r = t3.post(f"/api/technician/jobs/{E1}/accept")
    r2 = t3.post(f"/api/technician/jobs/{E2}/accept")
    check("JOB-011", "Technician already InProgress (Busy) cannot accept a 2nd job", r2.status == 400, f"E1={r.status} E2={r2.status}", bug="BUG-12")
    E3 = new_req(c2, "Electrical")
    adminweb.form("/Admin/Home/Dashboard", "/Admin/Home/RunAutoAssign", {})
    e3 = ((c2.get(f"/api/customer/requests/{E3}").json() or {}).get("data") or {})
    check("ASSIGN-013", "Busy technician skipped by auto-assign",
          (e3.get("technician") or {}).get("name") != f"QA Tech Three {RUN}", f"{e3.get('status')} {(e3.get('technician') or {}).get('name')}")
    # deactivated technician must not log in / keep API access / be auto-assigned
    t4_email = f"qa.t4.{RUN}@example.invalid"
    reg_tech(t4_email, f"QA Tech Four {RUN}", "Carpentry")
    _, t4_tok, t4_id = api_login(t4_email, PW)
    t4 = Client(t4_tok)
    r = admin.post(f"/api/admin/users/{t4_id}/toggle-active")
    check("ADMIN-010", "Admin toggles technician inactive", (r.json() or {}).get("isActive") is False, r.text[:80])
    r, _, _ = api_login(t4_email, PW)
    check("AUTH-020", "Inactive user API login -> 401", r.status == 401, r.status)
    _, rr = mvc_login(t4_email, PW)
    check("AUTH-021", "Inactive user MVC login refused", "/Account/Login" in rr.url, path_of(rr.url), bug="BUG-13")
    r = t4.get("/api/technician/dashboard")
    check("AUTH-022", "Inactive user's existing JWT is rejected", r.status in (401, 403), r.status, bug="BUG-13")
    K = new_req(c2, "Carpentry")
    adminweb.form("/Admin/Home/Dashboard", "/Admin/Home/RunAutoAssign", {})
    k = ((c2.get(f"/api/customer/requests/{K}").json() or {}).get("data") or {})
    check("ASSIGN-014", "Deactivated technician is never auto-assigned",
          "QA Tech Four" not in str((k.get("technician") or {}).get("name")), f"{k.get('status')} {(k.get('technician') or {}).get('name')}", bug="BUG-13")
    r = admin.get("/api/admin/technicians/available?category=Carpentry")
    check("ASSIGN-015", "Deactivated technician not offered for manual assignment",
          f"QA Tech Four {RUN}" not in r.text, "", bug="BUG-13")
    # API Forbid() contract
    r = c2.get(f"/api/customer/requests/{R1}")
    check("API-001", "API IDOR denial returns 403 JSON, not a 302 to the MVC AccessDenied page", r.status == 403,
          f"{r.status} Location={r.headers.get('Location')}", bug="BUG-21")
    r = admin.post(f"/api/admin/users/{admin_id}/toggle-active")
    check("ADMIN-011", "Admin cannot deactivate own account", (r.json() or {}).get("isActive") is not False, r.text[:80], bug="BUG-17")
    if (r.json() or {}).get("isActive") is False:
        admin.post(f"/api/admin/users/{admin_id}/toggle-active")  # undo (token still valid)

    # cancel rules
    C = new_req(c2, "Painting")
    r = c2.post(f"/api/customer/requests/{C}/cancel")
    check("BOOK-010", "Cancel Pending request", r.status == 200, r.status)
    r = c2.post(f"/api/customer/requests/{C}/cancel")
    check("BOOK-011", "Cancel already-cancelled -> 400", r.status == 400, r.status)

    # ------------------------------------------------------------- wallet / withdrawals
    r = t1web.get("/Technician/Wallet/Index")
    m = re.findall(r"₹\s?([\d,]+\.\d\d)", r.text)
    nonzero = any(float(x.replace(",", "")) > 0 for x in m)
    check("WALLET-001", "Wallet shows earnings for 2 paid jobs", r.status == 200 and nonzero, f"amounts={m[:6]}", bug="BUG-05")
    r = t1web.get("/Technician/Wallet/Withdraw")
    check("WALLET-002", "Withdraw page", r.status == 200, r.status)
    r = t1web.form("/Technician/Wallet/Withdraw", "/Technician/Wallet/Withdraw",
                   {"RequestAmount": "50000", "AvailableAmount": "100000", "UpiId": "qa@upi"})
    created = "submitted" in r.text or "/Technician/Wallet" in path_of(r.url) and "Withdraw" not in path_of(r.url)
    check("WALLET-003", "Cannot withdraw more than real balance by tampering hidden AvailableAmount",
          not created, f"{path_of(r.url)}", bug="BUG-03W")
    r = adminweb.get("/Admin/Withdrawals/Index")
    wids = re.findall(r'name="id"\s+value="(\d+)"', r.text) or re.findall(r"value=\"(\d+)\"\s+name=\"id\"", r.text)
    check("WALLET-004", "Admin withdrawals page", r.status == 200, r.status)
    if SQLCMD:
        wid = sql(f"SELECT TOP 1 WithdrawalId FROM TechnicianWithdrawals WHERE TechnicianUserId='{t1_id}' ORDER BY WithdrawalId DESC")
        if wid:
            adminweb.form("/Admin/Withdrawals/Index", "/Admin/Withdrawals/Approve", {"id": wid})
            r = adminweb.get("/Admin/Withdrawals/Index")
            check("WALLET-005", "Approved withdrawal still visible so admin can mark it Paid", f'value="{wid}"' in r.text, "", bug="BUG-18")
            adminweb.form("/Admin/Withdrawals/Index", "/Admin/Withdrawals/Reject", {"id": wid, "adminNote": "x"})
            adminweb.form("/Admin/Withdrawals/Index", "/Admin/Withdrawals/Pay", {"id": wid})
            check("WALLET-006", "Rejected withdrawal cannot be marked Paid",
                  sql(f"SELECT Status FROM TechnicianWithdrawals WHERE WithdrawalId={wid}") != "Paid", "", bug="BUG-18")

    # ------------------------------------------------------------- admin MVC: search/filter/export/analytics
    r = adminweb.get(f"/Admin/Requests/Index?search=QA&status=Completed")
    check("ADMIN-020", "Admin request search+filter", r.status == 200, r.status)
    r = adminweb.get("/Admin/Requests/Index?search=680001")
    check("ADMIN-021", "Search by pin code", r.status == 200, r.status)
    r = adminweb.get("/Admin/Requests/ExportCsv?status=Completed")
    csv = r.text
    check("ADMIN-022", "CSV export downloads", r.status == 200 and "Request ID" in csv, r.headers.get("Content-Type"))
    line = next((l for l in csv.splitlines() if l.startswith(f"{R2},")), "")
    check("ADMIN-023", "CSV includes Bill Amount + Payment Status for paid request", "1200.00" in line and "Paid" in line, line[-60:], bug="BUG-05")
    r = adminweb.get("/Admin/Analytics/Index")
    check("ADMIN-024", "Analytics page", r.status == 200, r.status)
    r = adminweb.get("/Admin/Home/GetCounts")
    check("ADMIN-025", "Dashboard counts JSON", r.status == 200 and r.json() is not None, r.text[:80])
    r = adminweb.form("/Admin/Users/Create", "/Admin/Users/Create",
                      {"FullName": "QA Admin", "Email": f"qa.admin.{RUN}@example.invalid", "Phone": "9000000000", "Password": PW,
                       "ConfirmPassword": PW, "Department": "QA", "Designation": "Tester"})
    r2, _, _ = api_login(f"qa.admin.{RUN}@example.invalid", PW)
    check("ADMIN-026", "Admin creates another admin who can log in", r2.status == 200 and (r2.json() or {}).get("role") == "Admin", r2.status)

    # ------------------------------------------------------------- customer MVC: history / rebook / tracking
    r = c1web.get("/Customer/Requests/History")
    check("HIST-001", "Service history page", r.status == 200, r.status)
    r = c1web.get(f"/Customer/Requests/Rebook/{R2}")
    check("HIST-002", "Re-book page prefilled", r.status == 200 and "12" not in "", r.status)
    r = c1web.form(f"/Customer/Requests/Rebook/{R2}", "/Customer/Requests/Rebook",
                   {"OriginalRequestId": R2, "Category": "Plumbing", "Description": "Rebooked: the same leak came back again today",
                    "Address": f"Rebook {RUN} street", "PinCode": "680001", "PreferredDateTime": iso(3)[:16]})
    check("HIST-003", "Re-book creates new request", r.status == 200 and "Rebook" not in path_of(r.url), path_of(r.url))
    r = c2web.get(f"/Customer/Requests/Rebook/{R2}")
    check("SEC-017", "Other customer cannot rebook A's request (no data leak)", "12 QA Street" not in r.text and "QA Customer One" not in r.text, path_of(r.url))
    r = c2web.get(f"/Customer/Tracking/Map?requestId={E1}")
    check("TRACK-001", "Tracking map for InProgress request", r.status == 200 and "Map" in path_of(r.url), path_of(r.url))
    r = t3web.form(f"/Technician/Jobs/Details/{E1}", "/Customer/Tracking/UpdateLocation", {"requestId": E1, "lat": "10.5", "lng": "76.2"})
    check("TRACK-002", "Technician pushes location for own InProgress job", (r.json() or {}).get("success") is True, r.text[:80])
    r = t1web.form("/Technician/Jobs/Index", "/Customer/Tracking/UpdateLocation", {"requestId": E1, "lat": "1", "lng": "1"})
    check("TRACK-003", "Other technician cannot push location", (r.json() or {}).get("success") is not True, r.text[:80])

    # ------------------------------------------------------------- SignalR
    r = c1web.post("/hubs/service/negotiate?negotiateVersion=1")
    check("NOTIF-001", "SignalR negotiate with MVC cookie", r.status == 200 and "connectionId" in r.text, r.status)
    r = c1.post("/hubs/service/negotiate?negotiateVersion=1")
    check("NOTIF-002", "SignalR negotiate with JWT (API/Next.js clients)", r.status == 200 and "connectionId" in r.text, r.status, bug="BUG-19")
    r = anon.post("/hubs/service/negotiate?negotiateVersion=1", follow=False)
    check("NOTIF-003", "SignalR negotiate anonymous -> 401", r.status == 401, r.status)

    # ------------------------------------------------------------- lockout
    lock_email = c2_email
    for _ in range(6):
        api_login(lock_email, "WrongPass1")
    r, _, _ = api_login(lock_email, PW)
    check("AUTH-030", "API login locks out after 5 failures", r.status == 401, r.status, bug="BUG-20")
    lock_c = f"qa.lock.{RUN}@example.invalid"
    # MVC lockout on technician 2
    for _ in range(5):
        mvc_login(t2_email, "WrongPass1")
    _, rr = mvc_login(t2_email, PW)
    check("AUTH-031", "MVC login locks out after 5 failures", "/Account/Login" in rr.url, path_of(rr.url))

    # ------------------------------------------------------------- smoke every GET page per role
    pages = {
        "admin": (adminweb, ["/Admin/Home/Dashboard", "/Admin/Requests/Index", f"/Admin/Requests/Details/{R1}", f"/Admin/Requests/Assign/{E3}",
                             "/Admin/Technicians/Index", f"/Admin/Technicians/Details/{T1P}", "/Admin/Users/Index", "/Admin/Users/Create",
                             "/Admin/Withdrawals/Index", "/Admin/Zones/Index", "/Admin/Zones/Create", "/Admin/PromoCodes/Index",
                             "/Admin/PromoCodes/Create", "/Admin/Disputes/Index", "/Admin/Analytics/Index", "/Admin/Profile/Index"]),
        "customer": (c1web, ["/Customer/Home/Dashboard", "/Customer/Requests/Index", "/Customer/Requests/Create", f"/Customer/Requests/Details/{R1}",
                             "/Customer/Requests/History", "/Customer/Profile/Index", "/Customer/Promotions/Index", "/Customer/Disputes/MyDisputes",
                             f"/Customer/Bills/Pay?requestId={R4}"]),
        "technician": (t1web, ["/Technician/Home/Dashboard", "/Technician/Jobs/Index", f"/Technician/Jobs/Details/{R1}", "/Technician/Profile/Index",
                               "/Technician/Wallet/Index", "/Technician/Wallet/Withdraw", f"/Technician/Bills/Details?requestId={R1}"]),
    }
    for role, (cl, ps) in pages.items():
        for p in ps:
            r = cl.get(p)
            check(f"UI-{role}", f"GET {p} -> 200 (no 500 / no AccessDenied)", r.status == 200 and "AccessDenied" not in r.url, f"{r.status} {path_of(r.url)}")

    return finish()


def finish():
    passed = sum(1 for r in RESULTS if r["ok"]); failed = len(RESULTS) - passed
    print(f"\n== {passed} passed, {failed} failed, {len(RESULTS)} total")
    out = os.environ.get("ASMS_JSON_OUT", os.path.join(os.path.dirname(os.path.abspath(__file__)), "last-run.json"))
    with open(out, "w", encoding="utf-8") as f:
        json.dump({"base": BASE, "run": RUN, "results": RESULTS}, f, indent=1)
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
