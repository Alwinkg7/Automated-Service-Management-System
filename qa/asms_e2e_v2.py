#!/usr/bin/env python3
"""
ASMS end-to-end regression probe for the split architecture (stdlib only).

  ServiceApp.Api  (JWT, /api/*, SignalR, Hangfire server)  -> ASMS_API_URL (default http://localhost:5090)
  ServiceApp.Web  (MVC, cookie auth, anti-forgery)          -> ASMS_WEB_URL (default http://localhost:5091)

Every check is PASS/FAIL against the documented contract (docs/API.md). Check ids keep the
baseline bug tags (BUG-01..23) so before/after can be compared with qa/baseline/last-run.json.

Env:
  ASMS_API_URL, ASMS_WEB_URL
  ASMS_ADMIN_EMAIL / ASMS_ADMIN_PASSWORD   seeded admin (set Seed__AdminPassword on the hosts)
  ASMS_RZP_SECRET            FAKE Razorpay KeySecret the hosts run with (to forge checkout signatures)
  ASMS_RZP_WEBHOOK_SECRET    FAKE Razorpay WebhookSecret the hosts run with
  ASMS_SQLCMD / ASMS_SQLCMD_DB   optional DB assertions + test seams (e.g. "(localdb)\\AsmsQa", ServiceAppDB_QA_v2)
  ASMS_API_LOG / ASMS_WEB_LOG    host stdout logs (Hangfire-only-in-API + error scan)
  ASMS_SIGNALR_MODULE        path to @microsoft/signalr (default ../frontend/node_modules/@microsoft/signalr)
  ASMS_JSON_OUT              results file (default qa/last-run-v2.json)

Test seam (documented): with fake Razorpay keys the API cannot create a real order, so when
ASMS_SQLCMD is set the probe writes a synthetic order id into Bills.RazorpayOrderId to stand in
for a successful POST /razorpay/order, then exercises verify/webhook with locally forged HMACs.
No real Razorpay/SendGrid call can succeed with the fake credentials.
"""
import collections
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

API = os.environ.get("ASMS_API_URL", "http://localhost:5090").rstrip("/")
WEB = os.environ.get("ASMS_WEB_URL", "http://localhost:5091").rstrip("/")
ADMIN_EMAIL = os.environ.get("ASMS_ADMIN_EMAIL", "admin@serviceapp.com")
ADMIN_PW = os.environ.get("ASMS_ADMIN_PASSWORD", "QaAdmin#2026x")
RZP_SECRET = os.environ.get("ASMS_RZP_SECRET")
RZP_WH_SECRET = os.environ.get("ASMS_RZP_WEBHOOK_SECRET")
SQLCMD = os.environ.get("ASMS_SQLCMD")
SQLDB = os.environ.get("ASMS_SQLCMD_DB", "ServiceAppDB_QA_v2")
HERE = os.path.dirname(os.path.abspath(__file__))
SIGNALR_MODULE = os.environ.get("ASMS_SIGNALR_MODULE",
                                os.path.join(HERE, "..", "frontend", "node_modules", "@microsoft", "signalr"))
RUN = uuid.uuid4().hex[:6]
PW = "Passw0rdQA"
RESULTS = []


def check(cid, title, ok, detail="", bug=None):
    RESULTS.append({"id": cid, "title": title, "ok": bool(ok), "detail": str(detail)[:400], "bug": bug})
    tag = "PASS" if ok else "FAIL"
    print(f"[{tag}] {cid} {title}" + (f"  ({bug})" if bug else "") + (f" -- {str(detail)[:220]}" if detail and not ok else ""), flush=True)
    return ok


def sql(q):
    if not SQLCMD:
        return None
    out = subprocess.run(["sqlcmd", "-S", SQLCMD, "-E", "-I", "-d", SQLDB, "-h", "-1", "-W", "-Q",
                          "SET NOCOUNT ON;" + " ".join(q.splitlines())], capture_output=True, text=True)
    return out.stdout.strip()


# ------------------------------------------------------------------ http plumbing
class AuthPacer:
    """/api/auth/* is limited to 10/min/IP (fixed window). Keep <= 9 in any rolling 60 s."""
    def __init__(self, limit=9, window=61.0):
        self.limit, self.window, self.ts = limit, window, collections.deque()

    def wait(self):
        now = time.monotonic()
        while self.ts and now - self.ts[0] > self.window:
            self.ts.popleft()
        if len(self.ts) >= self.limit:
            sleep = self.window - (now - self.ts[0]) + 0.5
            print(f"   .. pacing auth calls {sleep:.0f}s (rate limit 10/min)", flush=True)
            time.sleep(sleep)
            return self.wait()
        self.ts.append(time.monotonic())


PACER = AuthPacer()


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *a, **k):
        return None


class Resp:
    def __init__(self, status, headers, body, url):
        self.status, self.headers, self.body, self.url = status, {k.lower(): v for k, v in headers.items()}, body, url

    @property
    def text(self):
        return self.body.decode("utf-8", "replace")

    def json(self):
        try:
            return json.loads(self.body or b"null")
        except Exception:
            return None

    @property
    def ctype(self):
        return self.headers.get("content-type", "")


class Client:
    def __init__(self, base, token=None):
        self.base, self.token = base, token
        self.jar = http.cookiejar.CookieJar()
        self.op = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(self.jar))
        self.op_nr = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(self.jar), NoRedirect)

    def req(self, method, path, data=None, json_body=None, headers=None, follow=None, raw=None, retry429=True, pace=True):
        h = dict(headers or {})
        body = None
        if raw is not None:
            body = raw
        elif json_body is not None:
            body = json.dumps(json_body).encode(); h.setdefault("Content-Type", "application/json")
        elif data is not None:
            body = urllib.parse.urlencode(data, doseq=True).encode(); h["Content-Type"] = "application/x-www-form-urlencoded"
        if self.token:
            h["Authorization"] = "Bearer " + self.token
        if follow is None:
            follow = self.base == WEB   # API calls: never follow, assert raw status
        if pace and path.startswith("/api/auth/") and not path.startswith("/api/auth/me"):
            PACER.wait()
        r = urllib.request.Request(self.base + path, data=body, method=method, headers=h)
        try:
            resp = (self.op if follow else self.op_nr).open(r, timeout=90)
            res = Resp(resp.status, dict(resp.headers), resp.read(), resp.url)
        except urllib.error.HTTPError as e:
            res = Resp(e.code, dict(e.headers), e.read(), getattr(e, "url", self.base + path))
        except (urllib.error.URLError, ConnectionError, TimeoutError) as e:
            res = Resp(0, {}, str(e).encode(), self.base + path)
        if res.status == 429 and retry429:
            wait = int(res.headers.get("retry-after", "10") or 10) + 1
            print(f"   .. 429 on {path}, sleeping {wait}s", flush=True)
            time.sleep(wait)
            return self.req(method, path, data, json_body, headers, follow, raw, retry429=False, pace=pace)
        return res

    def get(self, path, **k): return self.req("GET", path, **k)
    def post(self, path, **k): return self.req("POST", path, **k)
    def put(self, path, **k): return self.req("PUT", path, **k)
    def delete(self, path, **k): return self.req("DELETE", path, **k)

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


def J(r, *keys, default=None):
    v = r.json() if isinstance(r, Resp) else r
    for k in keys:
        if isinstance(v, dict):
            v = v.get(k)
        elif isinstance(v, list) and isinstance(k, int) and len(v) > k:
            v = v[k]
        else:
            return default
    return default if v is None else v


CTYPES = collections.defaultdict(set)   # status -> content types seen on error bodies (contract: application/problem+json)


def is_problem(r, status):
    """Status + RFC 7807 body shape (+ legacy success/error). Content-Type tracked separately (API-015)."""
    j = r.json() or {}
    if r.status >= 400:
        CTYPES[r.status].add(r.ctype.split(";")[0])
    return r.status == status and j.get("status") == status and "title" in j and j.get("success") is False and bool(j.get("error"))


def api_login(email, pw):
    r = Client(API).post("/api/auth/login", json_body={"email": email, "password": pw})
    return r, J(r, "token"), J(r, "userId"), J(r, "refreshToken")


def mvc_login(email, pw):
    c = Client(WEB)
    r = c.form("/Account/Login", "/Account/Login", {"Email": email, "Password": pw, "RememberMe": "false"})
    return c, r


def iso(days=1, hour=10):
    d = (dt.datetime.now() + dt.timedelta(days=days)).replace(hour=hour, minute=0, second=0, microsecond=0)
    return d.strftime("%Y-%m-%dT%H:%M:%S")


def hm(secret, msg):
    return hmac.new(secret.encode(), msg.encode() if isinstance(msg, str) else msg, hashlib.sha256).hexdigest()


def status_of(client, rid):
    return J(client.get(f"/api/customer/requests/{rid}"), "data", "status")


# ------------------------------------------------------------------ the run
def main():
    print(f"== ASMS v2 probe  API={API}  WEB={WEB}  run={RUN}", flush=True)
    anon_api, anon_web = Client(API), Client(WEB)

    # ---------------------------------------------------------- smoke / ops
    check("SMOKE-001", "API /health Healthy", anon_api.get("/health").text.strip() == "Healthy")
    check("SMOKE-002", "Web /health Healthy", anon_web.get("/health").text.strip() == "Healthy")
    check("SMOKE-003", "MVC login page loads", anon_web.get("/Account/Login").status == 200)
    check("SMOKE-004", "Swagger served by API in Development", anon_api.get("/swagger/index.html").status == 200)
    check("SMOKE-005", "Categories are anonymous", J(anon_api.get("/api/customer/categories"), "success") is True)
    r = anon_api.get("/hangfire")
    check("HF-001", "API /hangfire refuses anonymous", r.status in (401, 403), r.status)
    r = anon_web.get("/hangfire", follow=False)
    check("HF-002", "MVC host does not serve /hangfire", r.status == 404, r.status)
    api_log, web_log = os.environ.get("ASMS_API_LOG"), os.environ.get("ASMS_WEB_LOG")
    if api_log and web_log and os.path.exists(api_log) and os.path.exists(web_log):
        a, w = open(api_log, encoding="utf-8", errors="replace").read(), open(web_log, encoding="utf-8", errors="replace").read()
        check("HF-003", "Hangfire server starts in the API only", "Starting Hangfire Server" in a and "Starting Hangfire Server" not in w,
              f"api={'Starting Hangfire Server' in a} web={'Starting Hangfire Server' in w}")
    if SQLCMD:
        reg = sql("SELECT COUNT(*) FROM HangFire.[Set] WHERE [Key]='recurring-jobs' AND Value IN ('auto-assign','refresh-token-cleanup')")
        check("HF-004", "Recurring jobs auto-assign + refresh-token-cleanup registered by the API", reg == "2", reg)
        # Seam: remove the 2-minute recurring auto-assign so manual-assignment checks are deterministic.
        # Auto-assign itself is exercised explicitly via POST /api/admin/auto-assign. The API re-registers it on restart.
        sql("DELETE FROM HangFire.Hash WHERE [Key]='recurring-job:auto-assign'; DELETE FROM HangFire.[Set] WHERE [Key]='recurring-jobs' AND Value='auto-assign'")
    # CORS (static finding: allowed any origin)
    r = anon_api.req("OPTIONS", "/api/auth/login", headers={"Origin": "https://evil.example", "Access-Control-Request-Method": "POST"})
    check("SEC-020", "CORS rejects foreign origin", "access-control-allow-origin" not in r.headers, r.headers.get("access-control-allow-origin"))
    r = anon_api.req("OPTIONS", "/api/auth/login", headers={"Origin": "http://localhost:3000", "Access-Control-Request-Method": "POST"})
    check("SEC-021", "CORS allows configured frontend origin", r.headers.get("access-control-allow-origin") == "http://localhost:3000", r.headers.get("access-control-allow-origin"))

    # ---------------------------------------------------------- 401 / 403 ProblemDetails
    r = anon_api.get("/api/customer/dashboard")
    check("API-010", "Anonymous -> 401 ProblemDetails (no redirect)", is_problem(r, 401), f"{r.status} {r.ctype}", bug="BUG-21")
    r = Client(API, "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJ4In0.bad").get("/api/customer/dashboard")
    check("API-011", "Tampered JWT -> 401", r.status == 401, r.status)

    # ---------------------------------------------------------- registration (BUG-01)
    e = lambda tag: f"qa.{tag}.{RUN}@example.invalid"
    c1_email, c2_email = e("c1"), e("c2")
    cweb = Client(WEB)
    r = cweb.form("/Account/CustomerRegister", "/Account/CustomerRegister",
                  {"FullName": "QA Customer One", "Email": c1_email, "Phone": "9876543210", "Password": PW, "ConfirmPassword": PW})
    lr, c1_tok, c1_id, c1_rt = api_login(c1_email, PW)
    check("AUTH-001", "Customer self-registration via MVC creates a usable account", lr.status == 200 and J(lr, "role") == "Customer",
          f"final={path_of(r.url)} login={lr.status}", bug="BUG-01")
    check("AUTH-001b", "MVC customer registration lands on Customer dashboard", "/Customer" in path_of(r.url), path_of(r.url))
    r = anon_api.post("/api/auth/register/customer", json_body={"fullName": "QA Customer Two", "email": c2_email, "phone": "9876543211", "password": PW})
    c2_tok, c2_id = J(r, "token"), J(r, "userId")
    check("AUTH-040", "API register/customer -> 201 AuthResponse", r.status == 201 and c2_tok and J(r, "refreshToken"), f"{r.status} {r.text[:120]}")
    r = anon_api.post("/api/auth/register/customer", json_body={"fullName": "Dup", "email": c2_email, "phone": "9876543211", "password": PW})
    check("AUTH-041", "Duplicate email -> 409 ProblemDetails", is_problem(r, 409), r.status)
    r = anon_api.post("/api/auth/register/customer", json_body={"fullName": "W", "email": "not-an-email", "phone": "1", "password": "short"})
    check("AUTH-042", "Invalid registration -> 400 with errors map", is_problem(r, 400) and isinstance(J(r, "errors"), dict), r.status)

    def reg_tech_api(tag, name, skill):
        rr = anon_api.post("/api/auth/register/technician",
                           json_body={"fullName": name, "email": e(tag), "phone": "9876500000", "password": PW, "skill": skill})
        return rr, J(rr, "token"), J(rr, "userId")

    tw = Client(WEB)
    r = tw.form("/Account/TechnicianRegister", "/Account/TechnicianRegister",
                {"FullName": f"QA Tech One {RUN}", "Email": e("t1"), "Phone": "9876500001", "Skill": "Plumbing", "Password": PW, "ConfirmPassword": PW})
    check("AUTH-002", "Technician MVC registration lands on Technician dashboard", "/Technician" in path_of(r.url), path_of(r.url), bug="BUG-03")
    _, t1_tok, t1_id, _ = api_login(e("t1"), PW)
    r, t2_tok, t2_id = reg_tech_api("t2", f"QA Tech Two {RUN}", "Plumbing")
    check("AUTH-043", "API register/technician -> 201", r.status == 201 and J(r, "role") == "Technician", r.status)
    _, t3_tok, t3_id = reg_tech_api("t3", f"QA Tech Three {RUN}", "Electrical")
    _, t4_tok, t4_id = reg_tech_api("t4", f"QA Tech Four {RUN}", "Carpentry")

    r, admin_tok, admin_id, admin_rt = api_login(ADMIN_EMAIL, ADMIN_PW)
    check("AUTH-003", "Seeded admin API login (Seed:AdminPassword)", r.status == 200 and admin_tok, r.status)
    if not (admin_tok and c1_tok and c2_tok and t1_tok and t2_tok and t3_tok and t4_tok):
        print("!! missing role tokens - aborting"); return finish()
    admin, c1, c2 = Client(API, admin_tok), Client(API, c1_tok), Client(API, c2_tok)
    t1, t2, t3, t4 = Client(API, t1_tok), Client(API, t2_tok), Client(API, t3_tok), Client(API, t4_tok)
    r = c1.get("/api/auth/me")
    check("AUTH-044", "/api/auth/me returns caller", J(r, "data", "role") == "Customer" and J(r, "data", "email") == c1_email, r.text[:120])

    adminweb, r = mvc_login(ADMIN_EMAIL, ADMIN_PW)
    check("AUTH-008", "Admin MVC login lands on Admin dashboard", "/Admin" in path_of(r.url), path_of(r.url))
    c1web, r = mvc_login(c1_email, PW)
    check("AUTH-016", "Customer MVC login lands on Customer dashboard", "/Customer" in path_of(r.url), path_of(r.url))
    c2web, _ = mvc_login(c2_email, PW)
    t1web, _ = mvc_login(e("t1"), PW)
    t2web, _ = mvc_login(e("t2"), PW)

    # role matrix + ProblemDetails 403
    r = c1.get("/api/admin/dashboard")
    check("API-012", "Customer on admin API -> 403 ProblemDetails", is_problem(r, 403), f"{r.status} {r.ctype}", bug="BUG-21")
    check("AUTH-014", "Technician on customer API -> 403", t1.get("/api/customer/dashboard").status == 403)
    check("AUTH-015", "Customer cookie on Admin MVC -> AccessDenied", "AccessDenied" in c1web.get("/Admin/Home/Dashboard").url)
    r = c1.get("/api/customer/requests/abc")
    check("API-013", "Non-numeric route id -> 404", r.status == 404, r.status)
    r = c1.post("/api/customer/requests", raw=b"category=Plumbing", headers={"Content-Type": "application/x-www-form-urlencoded"})
    check("API-014", "Wrong Content-Type -> 415", r.status == 415, r.status)

    # ---------------------------------------------------------- booking + validation (BUG-10) + idempotency
    body = {"category": "Plumbing", "description": "Kitchen sink leaking badly under the cabinet", "scheduledDate": iso(1),
            "address": "12 QA Street, Thrissur", "pinCode": "680001"}
    r = c1.post("/api/customer/requests", json_body=body)
    R1 = J(r, "requestId")
    check("BOOK-002", "Create request -> 200 + requestId", r.status == 200 and R1, r.text[:150])
    r = c1.post("/api/customer/requests", json_body=body)
    check("BOOK-003", "Duplicate submit within 30s returns same request", J(r, "requestId") == R1, r.text[:100])
    check("BOOK-004", "Invalid category -> 400", is_problem(c1.post("/api/customer/requests", json_body={**body, "category": "Rocketry"}), 400))
    r = c1.post("/api/customer/requests", json_body={**body, "scheduledDate": "2020-01-01T10:00:00", "address": "past date road 1"})
    check("BOOK-005", "Past preferred date -> 400", is_problem(r, 400), f"{r.status} {r.text[:80]}", bug="BUG-10")
    r = c1.post("/api/customer/requests", json_body={**body, "description": "", "address": "empty desc road"})
    check("BOOK-006", "Empty description -> 400", is_problem(r, 400), f"{r.status} {r.text[:80]}", bug="BUG-10")
    r = c1.post("/api/customer/requests", json_body={"category": "Plumbing", "description": "no address given here", "scheduledDate": iso(1)})
    check("BOOK-007", "Missing address -> 400", is_problem(r, 400), r.status, bug="BUG-10")
    key = str(uuid.uuid4())
    ib = {**body, "address": f"Idem {RUN} Road", "description": "Idempotency probe request body"}
    r1 = c1.post("/api/customer/requests", json_body=ib, headers={"Idempotency-Key": key})
    time.sleep(31)  # outlive the 30 s duplicate window so only the key can dedupe
    r2 = c1.post("/api/customer/requests", json_body=ib, headers={"Idempotency-Key": key})
    check("IDEM-001", "Idempotency-Key replay returns same requestId + Idempotent-Replayed header",
          J(r1, "requestId") and J(r1, "requestId") == J(r2, "requestId") and r2.headers.get("idempotent-replayed") == "true",
          f"{J(r1,'requestId')} vs {J(r2,'requestId')} hdr={r2.headers.get('idempotent-replayed')}")
    r3 = c2.post("/api/customer/requests", json_body={**ib, "address": f"Idem other {RUN}"}, headers={"Idempotency-Key": key})
    check("IDEM-002", "Same key from another user is not replayed", J(r3, "requestId") not in (None, J(r1, "requestId")), r3.text[:80])
    r4 = c1.post("/api/customer/requests", json_body={**ib, "category": "Bogus"}, headers={"Idempotency-Key": (k2 := str(uuid.uuid4()))})
    r5 = c1.post("/api/customer/requests", json_body={**ib, "address": f"Idem fixed {RUN}"}, headers={"Idempotency-Key": k2})
    check("IDEM-003", "Error responses are not stored (corrected retry with same key succeeds)", r4.status == 400 and r5.status == 200, f"{r4.status}/{r5.status}")

    r6 = c1.post("/api/customer/requests", json_body={**ib, "address": f"Idem other body {RUN}", "category": "Painting"}, headers={"Idempotency-Key": key})
    check("IDEM-004", "Same key with a DIFFERENT body is rejected (not silently replayed)", r6.status in (409, 422),
          f"{r6.status} replayed={r6.headers.get('idempotent-replayed')} id={J(r6,'requestId')}", bug="NEW-03")
    r7 = c1.post("/api/customer/requests", json_body=ib, headers={"Idempotency-Key": "k" * 101})
    check("IDEM-005", "Over-long Idempotency-Key -> 400 ProblemDetails", is_problem(r7, 400), f"{r7.status} {r7.text[:100]}", bug="NEW-02")

    def new_req(client, cat="Plumbing", pin="680001"):
        rr = client.post("/api/customer/requests", json_body={"category": cat, "description": f"QA {cat} job needs attention {uuid.uuid4().hex[:4]}",
                                                              "scheduledDate": iso(2), "address": f"{uuid.uuid4().hex[:6]} QA Road", "pinCode": pin})
        return J(rr, "requestId")

    R2, R3, R4 = new_req(c1), new_req(c1), new_req(c1)

    # IDOR (API + MVC)
    r = c2.get(f"/api/customer/requests/{R1}")
    check("SEC-010", "Customer B GET A's request -> 403 ProblemDetails", is_problem(r, 403), r.status, bug="BUG-21")
    check("SEC-011", "Customer B cancel A's request -> 403", c2.post(f"/api/customer/requests/{R1}/cancel").status == 403)
    r = c2web.get(f"/Customer/Requests/Details/{R1}")
    check("SEC-012", "Customer B cannot open A's request in MVC", "Kitchen sink" not in r.text, path_of(r.url))

    # cross-host: MVC create -> API, API create -> MVC
    r = c1web.form("/Customer/Requests/Create", "/Customer/Requests/Create",
                   {"Category": "Cleaning", "Description": f"MVC created cleaning job {RUN} for cross-host check",
                    "Address": f"MVC {RUN} Lane", "PinCode": "680002", "PreferredDateTime": iso(3)[:16]})
    lst = J(c1.get("/api/customer/requests"), "data", default=[])
    check("XHOST-001", "Request created in MVC is visible in the API", any(f"MVC created cleaning job {RUN}" in x.get("description", "") for x in lst), path_of(r.url))
    r = c1web.get("/Customer/Requests/Index")
    check("XHOST-002", "Request created in the API is visible in MVC", f"#{R1}" in r.text or f"/Customer/Requests/Details/{R1}" in r.text, r.status)

    # ---------------------------------------------------------- assignment (BUG-12, BUG-13 partly)
    techs = J(admin.get("/api/admin/technicians/available?category=Plumbing"), "data", default=[])
    tid = {t["name"]: t["id"] for t in techs}
    T1P, T2P = tid.get(f"QA Tech One {RUN}"), tid.get(f"QA Tech Two {RUN}")
    check("ADMIN-002", "Available Plumbing technicians listed", T1P and T2P, techs)
    if not (T1P and T2P):
        allt = {t["name"]: t["id"] for t in J(admin.get(f"/api/admin/technicians?search={RUN}"), "data", default=[])}
        T1P, T2P = T1P or allt.get(f"QA Tech One {RUN}"), T2P or allt.get(f"QA Tech Two {RUN}")
    r = admin.post(f"/api/admin/requests/{R1}/assign", json_body={"technicianProfileId": T1P})
    check("ASSIGN-001", "Admin assigns Pending request", r.status == 200, r.text[:120])
    r = admin.post(f"/api/admin/requests/{R1}/assign", json_body={"technicianProfileId": T2P})
    check("ASSIGN-002", "Re-assign non-Pending -> 409", is_problem(r, 409), r.status)
    r = admin.post(f"/api/admin/requests/{R2}/assign", json_body={"technicianProfileId": T1P})
    check("ASSIGN-003", "Technician with an active assignment cannot get a 2nd job", r.status == 409, f"{r.status}", bug="BUG-12")
    admin.post(f"/api/admin/requests/{R3}/assign", json_body={"technicianProfileId": T2P})
    r = admin.post(f"/api/admin/requests/{R4}/assign", json_body={"technicianProfileId": 999999})
    check("ASSIGN-004", "Assign unknown technician -> 404", r.status == 404, r.status)
    d = J(admin.get(f"/api/admin/requests/{R1}"), "data", default={})
    check("ASSIGN-005", "Admin detail shows Assigned + technician", d.get("status") == "Assigned" and d.get("technician"), d.get("status"))

    # ---------------------------------------------------------- technician lifecycle (BUG-07)
    r = t2.post(f"/api/technician/jobs/{R1}/accept")
    check("JOB-001", "Other technician accept -> 403", r.status == 403, r.status)
    check("SEC-013", "Other technician job detail -> 403", t2.get(f"/api/technician/jobs/{R1}").status == 403)
    r = t1.post(f"/api/technician/jobs/{R1}/accept")
    check("JOB-002", "Assigned technician accepts -> InProgress", r.status == 200 and status_of(c1, R1) == "InProgress", r.text[:100])
    check("JOB-003", "Accept twice -> 409", t1.post(f"/api/technician/jobs/{R1}/accept").status == 409)
    r = t1.post("/api/technician/toggle-status")
    check("JOB-004", "Toggle availability blocked while Busy", r.status in (400, 409), r.status)
    check("JOB-004b", "Toggle-while-Busy status matches docs/API.md (400)", r.status == 400, f"{r.status} (docs say 400)", bug="NEW-04")
    r = t2.post(f"/api/technician/jobs/{R1}/complete")
    check("JOB-005", "/complete by non-assigned tech -> 403", r.status == 403, f"{r.status} {r.text[:80]}", bug="BUG-07")
    r = t1.post(f"/api/technician/jobs/{R1}/complete")
    check("JOB-006", "/complete before Billed -> 409", r.status == 409, r.status, bug="BUG-07")
    check("BOOK-008", "Cancel InProgress -> 400/409", c1.post(f"/api/customer/requests/{R1}/cancel").status in (400, 409))
    r = t1.post(f"/api/technician/jobs/{R1}/location", json_body={"lat": 10.5, "lng": 76.2})
    check("TRACK-010", "API location push for own InProgress job", r.status == 200, r.status)
    check("TRACK-011", "API location push by other tech -> 403", t2.post(f"/api/technician/jobs/{R1}/location", json_body={"lat": 1, "lng": 1}).status == 403)

    # bill
    check("BILL-001", "Bill an Assigned job -> 409", t2.post(f"/api/technician/jobs/{R3}/bill", json_body={"laborCost": 100, "materialCost": 0}).status == 409)
    check("BILL-002", "Other tech bills job -> 403", t2.post(f"/api/technician/jobs/{R1}/bill", json_body={"laborCost": 100, "materialCost": 0}).status == 403)
    check("BILL-003", "Zero bill -> 400", t1.post(f"/api/technician/jobs/{R1}/bill", json_body={"laborCost": 0, "materialCost": 0}).status == 400)
    r = t1.post(f"/api/technician/jobs/{R1}/bill", json_body={"laborCost": 500, "materialCost": 250.555, "description": "PVC pipe"})
    B1 = J(r, "billId")
    check("BILL-004", "Assigned tech creates bill -> Billed", r.status == 200 and B1, r.text[:120])
    check("BILL-005", "Second bill -> 409", t1.post(f"/api/technician/jobs/{R1}/bill", json_body={"laborCost": 1, "materialCost": 0}).status == 409)
    d = J(c1.get(f"/api/customer/requests/{R1}"), "data", default={})
    bill = d.get("bill") or {}
    check("BILL-006", "Bill total = labor + material", bill.get("amount") in (750.56, 750.55), bill.get("amount"))
    check("BILL-007", "laborCost/materialCost exposed", bill.get("laborCost") == 500 and (bill.get("materialCost") or 0) > 250,
          f"labor={bill.get('laborCost')} material={bill.get('materialCost')}", bug="BUG-06")
    check("BILL-008", "History rows per transition", [h["status"] for h in d.get("statusHistory", [])] == ["Pending", "Assigned", "InProgress", "Billed"],
          [h["status"] for h in d.get("statusHistory", [])])
    row = next((x for x in J(c1.get("/api/customer/requests"), "data", default=[]) if x["id"] == R1), {})
    check("BILL-009", "Customer list shows hasBill/billAmount", row.get("hasBill") is True and row.get("billAmount"), row, bug="BUG-05")
    jr = next((x for x in J(t1.get("/api/technician/jobs"), "data", default=[]) if x["id"] == R1), {})
    check("BILL-010", "Technician job list shows hasBill", jr.get("hasBill") is True, jr, bug="BUG-05")
    r = c1.get(f"/api/customer/bills/{B1}/pdf")
    check("BILL-011", "Customer bill PDF (API)", r.status == 200 and r.body[:4] == b"%PDF", f"{r.status} {r.ctype}")
    check("SEC-014", "Other customer bill PDF (API) -> 403", c2.get(f"/api/customer/bills/{B1}/pdf").status == 403)
    r = t1.get(f"/api/technician/bills/{B1}/pdf")
    check("BILL-012", "Technician own bill PDF (API)", r.body[:4] == b"%PDF", r.status)
    check("SEC-018", "Other technician bill PDF (API) -> 403", t2.get(f"/api/technician/bills/{B1}/pdf").status == 403)
    r = c1web.get(f"/Customer/Bills/Download?billId={B1}")
    check("BILL-013", "Customer bill PDF (MVC)", r.body[:4] == b"%PDF", r.status)
    r = t1web.get(f"/Technician/Bills/Details?requestId={R1}")
    check("BILL-014", "Owner technician opens bill details (MVC) -> 200", r.status == 200 and "Bills/Details" in path_of(r.url), f"{r.status} {path_of(r.url)}", bug="NEW-06")
    r = t2web.get(f"/Technician/Bills/Details?requestId={R1}")
    check("SEC-015", "Other technician cannot view bill details (MVC)", "Bills/Details" not in path_of(r.url) and "PVC pipe" not in r.text, path_of(r.url), bug="BUG-15")

    # ---------------------------------------------------------- BUG-02: /pay removed
    r = c1.post(f"/api/customer/bills/{B1}/pay")
    check("PAY-002", "POST /bills/{id}/pay -> 410 Gone, request stays Billed", r.status == 410 and status_of(c1, R1) == "Billed", f"{r.status} {status_of(c1, R1)}", bug="BUG-02")

    # ---------------------------------------------------------- promo + points + quote (discounted amount)
    code = f"QA{RUN}".upper()
    r = admin.post("/api/admin/promo-codes", json_body={"code": code, "description": "QA flat 50", "discountType": "Flat", "discountValue": 50,
                                                         "minimumAmount": 100, "maxUses": 10, "expiresAt": (dt.datetime.utcnow() + dt.timedelta(days=30)).isoformat() + "Z"})
    PROMO_ID = J(r, "data", "id")
    check("PROMO-001", "Admin creates promo code (API) -> 201", r.status == 201 and PROMO_ID, r.text[:120])
    check("PROMO-011", "Duplicate promo code -> 409", admin.post("/api/admin/promo-codes", json_body={"code": code, "discountType": "Flat", "discountValue": 5, "minimumAmount": 0}).status == 409)
    r = admin.post("/api/admin/promo-codes", json_body={"code": f"PCT{RUN}".upper(), "discountType": "Percentage", "discountValue": 150, "minimumAmount": 0})
    check("PROMO-012", "Percentage > 100 -> 400", r.status == 400, r.status)
    r = admin.post("/api/admin/promo-codes", json_body={"code": f"OLD{RUN}".upper(), "discountType": "Flat", "discountValue": 5, "minimumAmount": 0, "expiresAt": "2020-01-01T00:00:00Z"})
    check("PROMO-013", "Past expiry -> 400", r.status == 400, r.status)
    r = c1.post("/api/customer/promotions/validate", json_body={"code": code, "billAmount": 500})
    check("PROMO-003", "Validate promo -> discount 50", J(r, "data", "discount") == 50, r.text[:120])
    check("PROMO-004", "Unknown promo -> 400", c1.post("/api/customer/promotions/validate", json_body={"code": "NOPEXYZ", "billAmount": 500}).status == 400)
    check("PROMO-006", "Below minimum -> 400", c1.post("/api/customer/promotions/validate", json_body={"code": code, "billAmount": 50}).status == 400)
    r = c1.get(f"/api/customer/bills/{B1}/quote")
    check("PROMO-020", "Quote before discount: payable = subtotal", J(r, "data", "payable") == J(r, "data", "subtotal"), r.text[:150])
    r = c1.post(f"/api/customer/bills/{B1}/promo/preview", json_body={"code": code})
    check("PROMO-021", "Promo preview shows discount, records nothing", J(r, "data", "discount") == 50 and J(c1.get(f"/api/customer/bills/{B1}/quote"), "data", "discount") == 0, r.text[:150])
    check("PROMO-022", "Other customer cannot apply promo to A's bill -> 403", c2.post(f"/api/customer/bills/{B1}/promo", json_body={"code": code}).status == 403)
    r = c1.post(f"/api/customer/bills/{B1}/promo", json_body={"code": code})
    payable = J(r, "data", "payable")
    check("PROMO-005", "Apply promo to bill -> payable = subtotal - 50", r.status == 200 and payable is not None and abs(payable - (J(r, 'data', 'subtotal') - 50)) < 0.01,
          r.text[:160], bug="BUG-11")
    check("PROMO-023", "Second promo on same bill -> 409", c1.post(f"/api/customer/bills/{B1}/promo", json_body={"code": code}).status == 409)
    r = c1.post(f"/api/customer/bills/{B1}/points", json_body={"points": 100})
    check("PROMO-024", "Redeem points with zero balance -> 422", r.status == 422, r.status)
    check("PROMO-025", "Points not a multiple of 100 -> 400/422", c1.post(f"/api/customer/bills/{B1}/points", json_body={"points": 50}).status in (400, 422))

    # ---------------------------------------------------------- Razorpay (BUG-08) with order seam + webhook
    r = c1.post(f"/api/customer/bills/{B1}/razorpay/order", json_body={})
    check("PAY-020", "razorpay/order with fake gateway keys -> 502 ProblemDetails (no crash)", is_problem(r, 502), f"{r.status} {r.text[:100]}")
    check("PAY-021", "Other customer razorpay/order -> 403", c2.post(f"/api/customer/bills/{B1}/razorpay/order", json_body={}).status == 403)
    P1 = None
    if RZP_SECRET and SQLCMD:
        oid = f"order_QA{RUN}"
        sql(f"UPDATE Bills SET RazorpayOrderId='{oid}' WHERE Id={B1}")   # seam: stands in for a successful order
        pid = f"pay_QA{RUN}"
        r = c1.post(f"/api/customer/bills/{B1}/razorpay/verify", json_body={"razorpayOrderId": oid, "razorpayPaymentId": pid, "razorpaySignature": "deadbeef"})
        check("PAY-022", "Bad signature -> 400, stays Billed", r.status == 400 and status_of(c1, R1) == "Billed", r.status)
        other = f"order_UNRELATED{RUN}"
        r = c1.post(f"/api/customer/bills/{B1}/razorpay/verify",
                    json_body={"razorpayOrderId": other, "razorpayPaymentId": pid, "razorpaySignature": hm(RZP_SECRET, f"{other}|{pid}")})
        check("PAY-023", "Valid signature for an order NOT bound to this bill -> 400", r.status == 400 and status_of(c1, R1) == "Billed",
              f"{r.status} {status_of(c1, R1)}", bug="BUG-08")
        r = c2.post(f"/api/customer/bills/{B1}/razorpay/verify",
                    json_body={"razorpayOrderId": oid, "razorpayPaymentId": pid, "razorpaySignature": hm(RZP_SECRET, f"{oid}|{pid}")})
        check("PAY-024", "Other customer verify -> 403", r.status == 403, r.status)
        r = c1.post(f"/api/customer/bills/{B1}/razorpay/verify",
                    json_body={"razorpayOrderId": oid, "razorpayPaymentId": pid, "razorpaySignature": hm(RZP_SECRET, f"{oid}|{pid}")})
        P1 = J(r, "data", "paymentId")
        check("PAY-025", "Verify bound order -> 200, Completed, amount = discounted payable", r.status == 200 and status_of(c1, R1) == "Completed"
              and abs((J(r, "data", "amount") or 0) - payable) < 0.01, f"{r.status} {r.text[:150]}", bug="BUG-09")
        r2 = c1.post(f"/api/customer/bills/{B1}/razorpay/verify",
                     json_body={"razorpayOrderId": oid, "razorpayPaymentId": pid, "razorpaySignature": hm(RZP_SECRET, f"{oid}|{pid}")})
        check("PAY-026", "Verify replay is idempotent (same paymentId)", J(r2, "data", "paymentId") == P1, f"{r2.status} {r2.text[:100]}")
        check("PAY-027", "Exactly one Payment row", sql(f"SELECT COUNT(*) FROM Payments WHERE BillId={B1}") == "1")
        r = c1.post(f"/api/customer/bills/{B1}/razorpay/order", json_body={})
        check("PAY-028", "Order on a paid bill -> 409", r.status == 409, r.status)
        time.sleep(2)
        pts = sql(f"SELECT ISNULL(SUM(Points),0) FROM LoyaltyLedger WHERE CustomerId='{c1_id}' AND TransactionType='Earned'")
        check("PROMO-010", "Loyalty points awarded (floor(payable/10))", pts == str(int(payable // 10)), f"points={pts} payable={payable}", bug="BUG-09")
    d = J(c1.get(f"/api/customer/requests/{R1}"), "data", default={})
    check("PAY-004", "Paid bill reports isPaid=true", J(d, "bill", "isPaid") is True, d.get("bill"), bug="BUG-04")
    r = admin.get("/api/admin/dashboard")
    check("PAY-005", "Admin dashboard revenue > 0", (J(r, "stats", "revenue") or 0) > 0, J(r, "stats"), bug="BUG-04")

    # rating
    check("RATE-001", "Rating out of range -> 400", c1.post(f"/api/customer/requests/{R1}/rate", json_body={"stars": 6}).status == 400)
    check("RATE-002", "Rate completed request", c1.post(f"/api/customer/requests/{R1}/rate", json_body={"stars": 4, "comment": "good"}).status == 200)
    check("RATE-003", "Rate twice -> 409", c1.post(f"/api/customer/requests/{R1}/rate", json_body={"stars": 5}).status == 409)

    # webhook: R3 (T2) with promo-less bill; wrong amount ignored, right amount completes
    t2.post(f"/api/technician/jobs/{R3}/accept")
    B3 = J(t2.post(f"/api/technician/jobs/{R3}/bill", json_body={"laborCost": 2000, "materialCost": 0}), "billId")
    if RZP_WH_SECRET and SQLCMD and B3:
        oid3 = f"order_WH{RUN}"
        sql(f"UPDATE Bills SET RazorpayOrderId='{oid3}' WHERE Id={B3}")
        def wh(amount_paise, pid):
            payload = json.dumps({"event": "payment.captured", "payload": {"payment": {"entity": {"id": pid, "order_id": oid3, "amount": amount_paise, "method": "upi"}}}})
            return anon_api.post("/api/payments/webhook", raw=payload.encode(),
                                 headers={"Content-Type": "application/json", "X-Razorpay-Signature": hm(RZP_WH_SECRET, payload)})
        r = anon_api.post("/api/payments/webhook", raw=b'{"event":"payment.captured"}', headers={"Content-Type": "application/json", "X-Razorpay-Signature": "bad"})
        check("WH-001", "Webhook bad signature -> 401", r.status == 401, r.status)
        r = wh(100, f"pay_WHlow{RUN}")
        check("WH-002", "Webhook with wrong amount not applied", J(r, "handled") is False and status_of(c1, R3) == "Billed", r.text[:120])
        r = wh(200000, f"pay_WH{RUN}")
        check("WH-003", "Webhook with matching amount completes request", J(r, "handled") is True and status_of(c1, R3) == "Completed", r.text[:120])
        r = wh(200000, f"pay_WH{RUN}")
        check("WH-004", "Webhook replay is a no-op", r.status == 200 and sql(f"SELECT COUNT(*) FROM Payments WHERE BillId={B3}") == "1", r.text[:100])

    # ---------------------------------------------------------- cash flow: customer chooses, technician confirms (R2 by T1)
    r = admin.post(f"/api/admin/requests/{R2}/assign", json_body={"technicianProfileId": T1P})
    check("ASSIGN-006", "Technician freed after payment can be assigned again", r.status == 200, r.text[:100])
    t1.post(f"/api/technician/jobs/{R2}/accept")
    B2 = J(t1.post(f"/api/technician/jobs/{R2}/bill", json_body={"laborCost": 1200, "materialCost": 0}), "billId")
    r = c1.post(f"/api/customer/bills/{B2}/cash")
    check("CASH-001", "Customer chooses cash -> records choice only (still Billed)", r.status == 200 and status_of(c1, R2) == "Billed"
          and J(r, "data", "preferredPaymentMethod") == "Cash", r.text[:120])
    r = c1web.form(f"/Customer/Bills/Pay?requestId={R2}", "/Customer/Bills/PayCash", {"billId": B2})
    check("CASH-002", "MVC 'Pay with cash' does not complete the job", status_of(c1, R2) == "Billed", path_of(r.url))
    r = t1web.form(f"/Technician/Jobs/Details/{R2}", "/Technician/Jobs/ConfirmCash", {"id": R2})
    check("CASH-003", "MVC technician 'Confirm cash' completes the job", status_of(c1, R2) == "Completed", path_of(r.url), bug="BUG-09")
    r = t1.post(f"/api/technician/jobs/{R2}/complete")
    check("CASH-004", "API /complete on already-paid -> 409", r.status == 409, r.status)
    if SQLCMD:
        check("CASH-005", "One Cash payment row", sql(f"SELECT COUNT(*) FROM Payments WHERE BillId={B2} AND PaymentMethod='Cash'") == "1")
        tj = sql(f"SELECT TotalJobsCompleted FROM TechnicianProfiles WHERE TechnicianProfileId={T1P}")
        check("JOB-010", "TotalJobsCompleted counts both paid jobs", tj == "2", f"TotalJobsCompleted={tj}", bug="BUG-02")
    pid2 = sql(f"SELECT PaymentId FROM Payments WHERE BillId={B2}") if SQLCMD else None
    if pid2:
        r = c2web.get(f"/Customer/Bills/Confirmation?paymentId={pid2}")
        check("SEC-016", "Other customer cannot open payment confirmation (MVC)", "Confirmation" not in path_of(r.url), path_of(r.url), bug="BUG-14")
    # API cash complete path on a fresh job (T1)
    R5 = new_req(c1)
    admin.post(f"/api/admin/requests/{R5}/assign", json_body={"technicianProfileId": T1P})
    t1.post(f"/api/technician/jobs/{R5}/accept")
    B5 = J(t1.post(f"/api/technician/jobs/{R5}/bill", json_body={"laborCost": 300, "materialCost": 0}), "billId")
    r = t1.post(f"/api/technician/jobs/{R5}/complete")
    check("JOB-007", "API /complete (cash) by assigned tech on Billed -> paymentId + Completed", J(r, "data", "paymentId") and status_of(c1, R5) == "Completed",
          r.text[:120], bug="BUG-07")

    # ---------------------------------------------------------- history / rebook
    r = c1.get("/api/customer/history")
    check("HIST-010", "API history totals", (J(r, "data", "completedJobs") or 0) >= 2, r.text[:150])
    r = c1.post(f"/api/customer/requests/{R2}/rebook", json_body={"scheduledDate": iso(4)})
    check("HIST-011", "API rebook completed request -> 201", r.status == 201 and J(r, "requestId"), r.text[:100])
    check("HIST-012", "Other customer rebook -> 403", c2.post(f"/api/customer/requests/{R2}/rebook", json_body={"scheduledDate": iso(4)}).status == 403)
    check("HIST-013", "Rebook with past date -> 400", c1.post(f"/api/customer/requests/{R2}/rebook", json_body={"scheduledDate": "2020-01-01T10:00:00"}).status == 400)
    check("HIST-014", "Rebook non-final request -> 409", c1.post(f"/api/customer/requests/{R4}/rebook", json_body={"scheduledDate": iso(4)}).status == 409)

    # ---------------------------------------------------------- disputes (API + MVC) (BUG-16)
    desc = "The technician left the site without fixing the leak properly."
    r = c1.post("/api/customer/disputes", json_body={"requestId": R2, "reason": "ServiceQuality", "description": desc})
    D2 = J(r, "data", "id")
    check("DISPUTE-002", "Customer raises dispute (API) -> 201", r.status == 201 and D2, r.text[:120])
    check("DISPUTE-003", "Second open dispute -> 409", c1.post("/api/customer/disputes", json_body={"requestId": R2, "reason": "Other", "description": desc}).status == 409)
    check("DISPUTE-004", "Other customer dispute on A's request -> 403", c2.post("/api/customer/disputes", json_body={"requestId": R2, "reason": "Other", "description": desc}).status == 403)
    check("DISPUTE-005", "Dispute on non-completed request -> 422", c1.post("/api/customer/disputes", json_body={"requestId": R4, "reason": "Other", "description": desc}).status == 422)
    r = c1.post("/api/customer/disputes", json_body={"requestId": R5, "reason": "X" * 80, "description": desc})
    check("DISPUTE-006", "Unknown/over-long reason -> 400 (no 500)", r.status == 400, r.status, bug="BUG-16")
    check("DISPUTE-013", "Short description -> 400", c1.post("/api/customer/disputes", json_body={"requestId": R5, "reason": "Other", "description": "short"}).status == 400)
    r = c1web.form(f"/Customer/Disputes/Raise?requestId={R5}", "/Customer/Disputes/Raise", {"requestId": R5, "reason": "X" * 80, "description": desc})
    check("DISPUTE-014", "MVC over-long reason handled (no 500)", r.status != 500, r.status, bug="BUG-16")
    check("DISPUTE-015", "Customer lists own disputes", any(x.get("id") == D2 for x in J(c1.get("/api/customer/disputes"), "data", default=[])))
    check("DISPUTE-016", "Other customer GET dispute -> 403", c2.get(f"/api/customer/disputes/{D2}").status == 403)
    r = admin.get("/api/admin/disputes")
    check("DISPUTE-007", "Admin dispute list + counts", any(x.get("id") == D2 for x in J(r, "data", default=[])) and isinstance(J(r, "counts"), dict), r.status)
    check("DISPUTE-008", "Admin review -> UnderReview", J(admin.post(f"/api/admin/disputes/{D2}/review", json_body={"adminNote": "looking"}), "data", "status") == "UnderReview")
    check("DISPUTE-017", "Review twice -> 409", admin.post(f"/api/admin/disputes/{D2}/review", json_body={}).status == 409)
    r = admin.post(f"/api/admin/disputes/{D2}/refund", json_body={"amount": 100, "adminNote": "partial"})
    check("DISPUTE-009", "Refund on cash-paid request -> 422", r.status == 422, r.status)
    check("DISPUTE-018", "Resolve with invalid resolution -> 400", admin.post(f"/api/admin/disputes/{D2}/resolve", json_body={"resolution": "Refunded"}).status == 400)
    check("DISPUTE-010", "Admin resolves (ClosedNoAction)", J(admin.post(f"/api/admin/disputes/{D2}/resolve", json_body={"resolution": "ClosedNoAction", "adminNote": "closed"}), "data", "status") == "Resolved")
    check("DISPUTE-019", "Resolve twice -> 409", admin.post(f"/api/admin/disputes/{D2}/resolve", json_body={"resolution": "Rejected"}).status == 409)
    r = c1.post("/api/customer/disputes", json_body={"requestId": R1, "reason": "WrongCharge", "description": desc})
    D1 = J(r, "data", "id")
    if D1:
        check("DISPUTE-011", "Refund > paid amount -> 400", admin.post(f"/api/admin/disputes/{D1}/refund", json_body={"amount": 999999}).status == 400)
        r = admin.post(f"/api/admin/disputes/{D1}/refund", json_body={"amount": 100, "adminNote": "gw"})
        check("DISPUTE-012", "Refund via fake gateway -> 502, dispute not marked refunded", r.status == 502 and J(admin.get(f"/api/admin/disputes/{D1}"), "data", "refundIssued") is False, r.status)

    # ---------------------------------------------------------- wallet + withdrawals (BUG-05, BUG-22, BUG-18)
    r = t1.get("/api/technician/wallet")
    w = J(r, "data", default={})
    check("WALLET-001", "Wallet gross earnings = sum of paid subtotals (750.56+1200+300)", abs((w.get("grossEarningsAllTime") or 0) - 2250.56) < 0.02,
          {k: w.get(k) for k in ("grossEarningsAllTime", "netEarningsAllTime", "availableToWithdraw")}, bug="BUG-05")
    avail = w.get("availableToWithdraw") or 0
    r = t1web.get("/Technician/Wallet/Index")
    check("WALLET-007", "MVC wallet shows non-zero earnings", re.search(r"₹\s?(?:[1-9][\d,]*\.\d\d)", r.text) is not None, r.status, bug="BUG-05")
    r = t1.post("/api/technician/wallet/withdrawals", json_body={"amount": 50000, "upiId": "qa@upi"})
    check("WALLET-003", "Withdraw more than available -> 422", r.status == 422, r.status, bug="BUG-22")
    r = t1web.form("/Technician/Wallet/Withdraw", "/Technician/Wallet/Withdraw", {"RequestAmount": "50000", "AvailableAmount": "100000", "UpiId": "qa@upi"})
    n_big = sql(f"SELECT COUNT(*) FROM TechnicianWithdrawals WHERE TechnicianUserId='{t1_id}' AND Amount>=50000") if SQLCMD else "0"
    check("WALLET-008", "MVC tampered AvailableAmount does not create withdrawal", n_big == "0", f"rows={n_big}", bug="BUG-22")
    check("WALLET-009", "Withdraw amount <= 0 -> 400", t1.post("/api/technician/wallet/withdrawals", json_body={"amount": 0, "upiId": "qa@upi"}).status == 400)
    r = t1.post("/api/technician/wallet/withdrawals", json_body={"amount": 100, "upiId": "qa@upi"})
    W1 = J(r, "data", "id")
    check("WALLET-010", "Valid withdrawal -> 201", r.status == 201 and W1, r.text[:120])
    r = t1.post("/api/technician/wallet/withdrawals", json_body={"amount": 100, "upiId": "qa@upi"})
    W2 = J(r, "data", "id")
    check("WALLET-011", "Pending withdrawals reduce available balance", (J(t1.get("/api/technician/wallet"), "data", "availableToWithdraw") or 0) <= avail - 199.99)
    check("WALLET-012", "Pay a Pending withdrawal -> 409", admin.post(f"/api/admin/withdrawals/{W1}/pay").status == 409, bug="BUG-18")
    check("WALLET-013", "Approve Pending -> Approved", J(admin.post(f"/api/admin/withdrawals/{W1}/approve"), "data", "status") == "Approved")
    check("WALLET-005", "Approved withdrawal visible in admin 'open' list", any(x.get("id") == W1 for x in J(admin.get("/api/admin/withdrawals?status=open"), "data", default=[])), bug="BUG-18")
    r = adminweb.get("/Admin/Withdrawals/Index")
    check("WALLET-014", "Approved withdrawal visible in MVC admin list", f'value="{W1}"' in r.text or f"/Pay/{W1}" in r.text or f"#{W1}" in r.text, r.status, bug="BUG-18")
    check("WALLET-015", "Approve twice -> 409", admin.post(f"/api/admin/withdrawals/{W1}/approve").status == 409)
    check("WALLET-016", "Pay Approved -> Paid", J(admin.post(f"/api/admin/withdrawals/{W1}/pay"), "data", "status") == "Paid")
    check("WALLET-017", "Reject Paid -> 409", admin.post(f"/api/admin/withdrawals/{W1}/reject", json_body={"adminNote": "x"}).status == 409)
    check("WALLET-018", "Reject Pending -> Rejected", J(admin.post(f"/api/admin/withdrawals/{W2}/reject", json_body={"adminNote": "no"}), "data", "status") == "Rejected")
    check("WALLET-006", "Pay Rejected -> 409", admin.post(f"/api/admin/withdrawals/{W2}/pay").status == 409, bug="BUG-18")
    check("WALLET-019", "Technician sees own withdrawals", len(J(t1.get("/api/technician/wallet/withdrawals"), "data", default=[])) >= 2)
    check("WALLET-020", "Customer cannot call admin withdrawals -> 403", c1.post(f"/api/admin/withdrawals/{W2}/approve").status == 403)

    # ---------------------------------------------------------- zones
    zname = f"QA Zone {RUN}"
    pin_a, pin_b = f"6{RUN[:5].translate(str.maketrans('abcdef', '123456'))}", f"7{RUN[:5].translate(str.maketrans('abcdef', '654321'))}"
    r = admin.post("/api/admin/zones", json_body={"zoneName": zname, "city": "Thrissur", "state": "Kerala", "pinCodes": f"{pin_a},{pin_b}"})
    Z = J(r, "data", "id")
    check("ZONE-001", "Create zone (API) -> 201, pinCodes array", r.status == 201 and J(r, "data", "pinCodes") == [pin_a, pin_b], r.text[:150])
    check("ZONE-003", "Duplicate zone name -> 409", admin.post("/api/admin/zones", json_body={"zoneName": zname, "city": "X", "state": "Y", "pinCodes": "999991"}).status == 409)
    check("ZONE-004", "Pin already in another active zone -> 409", admin.post("/api/admin/zones", json_body={"zoneName": zname + "b", "city": "X", "state": "Y", "pinCodes": pin_a}).status == 409)
    check("ZONE-005", "Non-6-digit pin -> 400", admin.post("/api/admin/zones", json_body={"zoneName": zname + "c", "city": "X", "state": "Y", "pinCodes": "12ab"}).status == 400)
    r = admin.put(f"/api/admin/zones/{Z}", json_body={"zoneName": zname, "city": "Thrissur", "state": "Kerala", "pinCodes": pin_a})
    check("ZONE-006", "Update zone", J(r, "data", "pinCodes") == [pin_a], r.text[:100])
    check("ZONE-007", "Toggle zone inactive", J(admin.post(f"/api/admin/zones/{Z}/toggle"), "data", "isActive") is False)
    admin.post(f"/api/admin/zones/{Z}/toggle")
    check("ZONE-002", "MVC zones page lists API-created zone", zname in adminweb.get("/Admin/Zones/Index").text)

    # promo admin CRUD extras
    check("PROMO-030", "Toggle promo", J(admin.post(f"/api/admin/promo-codes/{PROMO_ID}/toggle"), "data", "isActive") is False)
    admin.post(f"/api/admin/promo-codes/{PROMO_ID}/toggle")
    check("PROMO-031", "Delete used promo -> 409", admin.delete(f"/api/admin/promo-codes/{PROMO_ID}").status == 409)
    tmp = J(admin.post("/api/admin/promo-codes", json_body={"code": f"DEL{RUN}".upper(), "discountType": "Flat", "discountValue": 5, "minimumAmount": 0}), "data", "id")
    check("PROMO-032", "Delete unused promo -> 204", admin.delete(f"/api/admin/promo-codes/{tmp}").status == 204)
    check("PROMO-033", "MVC promo list shows API code", code in adminweb.get("/Admin/PromoCodes/Index").text)

    # ---------------------------------------------------------- auto-assign, busy, deactivation (BUG-12, BUG-13)
    E1, E2 = new_req(c2, "Electrical"), new_req(c2, "Electrical")
    r = admin.post("/api/admin/auto-assign")
    check("ASSIGN-010", "API auto-assign cycle", r.status == 200 and J(r, "data", "assigned") is not None, r.text[:100])
    e1, e2 = J(c2.get(f"/api/customer/requests/{E1}"), "data", default={}), J(c2.get(f"/api/customer/requests/{E2}"), "data", default={})
    names = [J(x, "technician", "name") for x in (e1, e2)]
    check("ASSIGN-011", "Auto-assign Electrical -> QA Tech Three", f"QA Tech Three {RUN}" in names, names)
    check("ASSIGN-012", "Auto-assign does not stack 2 jobs on one tech", names.count(f"QA Tech Three {RUN}") <= 1, names, bug="BUG-12")
    mine = E1 if names[0] == f"QA Tech Three {RUN}" else E2
    t3.post(f"/api/technician/jobs/{mine}/accept")
    E3 = new_req(c2, "Electrical")
    admin.post("/api/admin/auto-assign")
    check("ASSIGN-013", "Busy technician skipped", J(c2.get(f"/api/customer/requests/{E3}"), "data", "technician", "name") != f"QA Tech Three {RUN}")
    r = adminweb.form("/Admin/Home/Dashboard", "/Admin/Home/RunAutoAssign", {})
    check("ASSIGN-016", "MVC 'Run auto-assign' (enqueue/execute) works", r.status == 200, r.status)

    # technician management (admin API)
    T4P = next((t["id"] for t in J(admin.get(f"/api/admin/technicians?search={urllib.parse.quote('QA Tech Four ' + RUN)}"), "data", default=[])), None)
    T3P = next((t["id"] for t in J(admin.get(f"/api/admin/technicians?search={urllib.parse.quote('QA Tech Three ' + RUN)}"), "data", default=[])), None)
    check("TECH-001", "Admin technician search", T4P and T3P, f"{T4P} {T3P}")
    r = admin.get("/api/admin/technicians?page=1&pageSize=2")
    check("TECH-002", "Paged technician list shape", J(r, "data", "pageSize") == 2 and isinstance(J(r, "data", "items"), list) and isinstance(J(r, "counts"), dict), r.text[:120])
    check("TECH-003", "Technician detail", J(admin.get(f"/api/admin/technicians/{T3P}"), "data", "technician", "id") == T3P)
    check("TECH-004", "Skill change blocked while tech has active job -> 409", admin.post(f"/api/admin/technicians/{T3P}/skill", json_body={"skill": "Plumbing"}).status == 409)
    check("TECH-005", "Skill change allowed when idle", J(admin.post(f"/api/admin/technicians/{T4P}/skill", json_body={"skill": "Carpentry"}), "skill") == "Carpentry")
    check("TECH-006", "Admin status override", J(admin.post(f"/api/admin/technicians/{T4P}/status", json_body={"status": "Offline"}), "status") == "Offline")
    admin.post(f"/api/admin/technicians/{T4P}/status", json_body={"status": "Available"})
    r = admin.post(f"/api/admin/technicians/{T4P}/deactivate")
    check("ADMIN-010", "Admin deactivates technician", J(r, "isActive") is False, r.text[:80])
    r, _, _, _ = api_login(e("t4"), PW)
    check("AUTH-020", "Inactive user API login -> 401", r.status == 401, r.status)
    _, rr = mvc_login(e("t4"), PW)
    check("AUTH-021", "Inactive user MVC login refused", "/Account/Login" in rr.url, path_of(rr.url), bug="BUG-13")
    K = new_req(c2, "Carpentry")
    admin.post("/api/admin/auto-assign")
    k = J(c2.get(f"/api/customer/requests/{K}"), "data", default={})
    check("ASSIGN-014", "Deactivated technician never auto-assigned", "QA Tech Four" not in str(J(k, "technician", "name")), f"{k.get('status')} {J(k, 'technician', 'name')}", bug="BUG-13")
    check("ASSIGN-015", "Deactivated technician not offered for assignment", f"QA Tech Four {RUN}" not in admin.get("/api/admin/technicians/available?category=Carpentry").text, bug="BUG-13")
    print("   .. waiting 35s for deactivated-token revocation window", flush=True)
    time.sleep(35)
    r = t4.get("/api/technician/dashboard")
    check("AUTH-022", "Deactivated user's existing JWT rejected (401) within ~30s", r.status == 401, r.status, bug="BUG-13")
    admin.post(f"/api/admin/technicians/{T4P}/activate")
    r = admin.post(f"/api/admin/users/{admin_id}/toggle-active")
    check("ADMIN-011", "Admin cannot deactivate self -> 409", r.status == 409, r.text[:80], bug="BUG-17")

    # cancel rules
    C = new_req(c2, "Painting")
    check("BOOK-010", "Cancel Pending", c2.post(f"/api/customer/requests/{C}/cancel").status == 200)
    check("BOOK-011", "Cancel cancelled -> 409", c2.post(f"/api/customer/requests/{C}/cancel").status == 409)
    C2 = new_req(c2, "Painting")
    check("ADMIN-012", "Admin cancels Pending request (API)", admin.post(f"/api/admin/requests/{C2}/cancel").status == 200)

    # ---------------------------------------------------------- admin lists, analytics, CSV export + escaping
    f = c2.post("/api/customer/requests", json_body={"category": "General", "description": f'=HYPERLINK("http://evil.example","x") {RUN}',
                                                     "scheduledDate": iso(2), "address": "+SUM(1,2) QA Road", "pinCode": "680001"})
    FX = J(f, "requestId")
    r = admin.get("/api/admin/requests?search=QA&status=Completed")
    check("ADMIN-020", "Admin request search+status filter (legacy array)", isinstance(J(r, "data"), list) and all(x["status"] == "Completed" for x in J(r, "data", default=[])), r.status)
    r = admin.get("/api/admin/requests?page=1&pageSize=5")
    check("ADMIN-027", "Admin requests paged shape", J(r, "data", "pageSize") == 5 and J(r, "data", "totalCount", default=0) >= 5, r.text[:100])
    row = next((x for x in J(admin.get("/api/admin/requests?status=Completed"), "data", default=[]) if x["id"] == R2), {})
    check("ADMIN-028", "Admin list shows bill amount + paid for completed request", row.get("billPaid") is True and row.get("billAmount") == 1200, row, bug="BUG-05")
    r = admin.get("/api/admin/requests/export")
    csv = r.body.decode("utf-8-sig", "replace")
    line = next((l for l in csv.splitlines() if l.startswith(f"{R2},")), "")
    check("ADMIN-022", "API CSV export (text/csv, BOM)", r.status == 200 and "text/csv" in r.ctype and r.body[:3] == b"\xef\xbb\xbf", r.ctype)
    check("ADMIN-023", "CSV includes bill amount + payment status", "1200.00" in line and "Paid" in line, line[-80:], bug="BUG-05")
    fl = next((l for l in csv.splitlines() if l.startswith(f"{FX},")), "")
    check("SEC-022", "CSV formula injection neutralised (no cell starts with = + - @)",
          fl and not re.search(r'(^|,)"?[=+\-@]', fl.split(",", 1)[1]), fl[:160])
    r = adminweb.get("/Admin/Requests/ExportCsv")
    mfl = next((l for l in r.body.decode("utf-8-sig", "replace").splitlines() if l.startswith(f"{FX},")), "")
    check("SEC-023", "MVC CSV formula injection neutralised", mfl and not re.search(r'(^|,)"?[=+\-@]', mfl.split(",", 1)[1]), mfl[:160])
    r = admin.get("/api/admin/analytics")
    check("ADMIN-024", "Analytics totals + revenueByDay", (J(r, "data", "totals", "totalRevenue") or 0) > 0 and isinstance(J(r, "data", "revenueByDay"), list), r.text[:160])
    check("ADMIN-025", "MVC analytics page", adminweb.get("/Admin/Analytics/Index").status == 200)
    r = admin.get("/api/admin/users?role=Technician")
    check("ADMIN-029", "Admin users filter", all(u["role"] == "Technician" for u in J(r, "data", default=[])) and J(r, "data"), r.status)
    r = adminweb.form("/Admin/Users/Create", "/Admin/Users/Create", {"FullName": "QA Admin", "Email": e("adm"), "Phone": "9000000000", "Password": PW,
                                                                     "ConfirmPassword": PW, "Department": "QA", "Designation": "Tester"})
    r2, _, _, _ = api_login(e("adm"), PW)
    check("ADMIN-026", "MVC create admin -> can log in to API", J(r2, "role") == "Admin", r2.status)

    # ---------------------------------------------------------- refresh tokens
    rr, tok_a, _, rt_a = api_login(c2_email, PW)
    r = anon_api.post("/api/auth/refresh", json_body={"refreshToken": rt_a})
    rt_b = J(r, "refreshToken")
    check("AUTH-050", "Refresh returns a new pair (rotation)", r.status == 200 and rt_b and rt_b != rt_a and J(r, "token"), r.status)
    r = anon_api.post("/api/auth/refresh", json_body={"refreshToken": rt_a})
    check("AUTH-051", "Reusing a consumed refresh token -> 401 ProblemDetails", is_problem(r, 401), r.status)
    r = anon_api.post("/api/auth/refresh", json_body={"refreshToken": rt_b})
    rt_c = J(r, "refreshToken")
    check("AUTH-052", "Refresh-token reuse revokes the rotated descendant (theft detection)", r.status == 401,
          f"descendant still usable after reuse of its parent: {r.status}", bug="NEW-HARDEN")
    tok = rt_c or rt_b
    r = anon_api.post("/api/auth/logout", json_body={"refreshToken": tok})
    check("AUTH-053", "Logout -> 204", r.status == 204, r.status)
    check("AUTH-054", "Refresh after logout -> 401", anon_api.post("/api/auth/refresh", json_body={"refreshToken": tok}).status == 401)
    check("AUTH-055", "Garbage refresh token -> 401", anon_api.post("/api/auth/refresh", json_body={"refreshToken": "x.y.z"}).status == 401)

    # ---------------------------------------------------------- MVC pages smoke (cookie host)
    pages = {
        "admin": (adminweb, ["/Admin/Home/Dashboard", "/Admin/Requests/Index", f"/Admin/Requests/Details/{R1}", f"/Admin/Requests/Assign/{E3}",
                             "/Admin/Technicians/Index", f"/Admin/Technicians/Details/{T1P}", "/Admin/Users/Index", "/Admin/Users/Create",
                             "/Admin/Withdrawals/Index", "/Admin/Zones/Index", "/Admin/Zones/Create", f"/Admin/Zones/Edit/{Z}", "/Admin/PromoCodes/Index",
                             "/Admin/PromoCodes/Create", "/Admin/Disputes/Index", f"/Admin/Disputes/Details/{D2}", "/Admin/Analytics/Index", "/Admin/Profile/Index",
                             "/Admin/Home/GetCounts"]),
        "customer": (c1web, ["/Customer/Home/Dashboard", "/Customer/Requests/Index", "/Customer/Requests/Create", f"/Customer/Requests/Details/{R1}",
                             "/Customer/Requests/History", f"/Customer/Requests/Rebook/{R2}", "/Customer/Profile/Index", "/Customer/Promotions/Index",
                             "/Customer/Disputes/MyDisputes", f"/Customer/Disputes/Details/{D2}", f"/Customer/Bills/Pay?requestId={R1}"]),
        "technician": (t1web, ["/Technician/Home/Dashboard", "/Technician/Jobs/Index", f"/Technician/Jobs/Details/{R1}", "/Technician/Profile/Index",
                               "/Technician/Wallet/Index", "/Technician/Wallet/Withdraw", f"/Technician/Bills/Details?requestId={R1}"]),
    }
    for role, (cl, ps) in pages.items():
        for p in ps:
            r = cl.get(p)
            check(f"UI-{role}", f"GET {p} -> 200", r.status == 200 and "AccessDenied" not in r.url and "/Account/Login" not in r.url, f"{r.status} {path_of(r.url)}")

    # ---------------------------------------------------------- SignalR (BUG-19, BUG-23)
    r = Client(API).post(f"/hubs/service/negotiate?negotiateVersion=1", headers={"Authorization": f"Bearer {c1_tok}"}, follow=False)
    check("NOTIF-002", "API hub negotiate with JWT", r.status == 200 and "connectionId" in r.text, r.status, bug="BUG-19")
    r = anon_api.post("/hubs/service/negotiate?negotiateVersion=1")
    check("NOTIF-003", "API hub negotiate anonymous -> 401 (no redirect)", r.status == 401, r.status, bug="BUG-21")
    r = c1web.post("/hubs/service/negotiate?negotiateVersion=1", follow=False)
    check("NOTIF-001", "MVC hub negotiate with cookie", r.status == 200 and "connectionId" in r.text, r.status)
    # dedicated technician + category so earlier auto-assign cycles cannot interfere
    _, t5_tok, _ = reg_tech_api("t5", f"QA Tech Five {RUN}", "AC_Repair")
    T5P = next((t["id"] for t in J(admin.get(f"/api/admin/technicians?search={urllib.parse.quote('QA Tech Five ' + RUN)}"), "data", default=[])), None)
    RS = new_req(c1, "AC_Repair")
    ra = admin.post(f"/api/admin/requests/{RS}/assign", json_body={"technicianProfileId": T5P})
    rb = Client(API, t5_tok).post(f"/api/technician/jobs/{RS}/accept")
    check("NOTIF-019", "Hub test job prepared (assigned + InProgress)", ra.status == 200 and rb.status == 200 and status_of(c1, RS) == "InProgress", f"{ra.status}/{rb.status}")
    sig = run_signalr(api=API, a_tok=c1_tok, a_id=c1_id, b_tok=c2_tok, tech_tok=t5_tok, other_tech_tok=t1_tok, admin_tok=admin_tok, request_id=RS)
    if sig is None:
        check("NOTIF-010", "SignalR WebSocket isolation suite ran", False, "node/@microsoft/signalr unavailable")
    else:
        for cid, (ok, title, detail) in sig.items():
            check(cid, title, ok, detail, bug="BUG-23")

    # ---------------------------------------------------------- lockout + rate limiting (last: they burn the auth window)
    lock_email = e("t3")
    for _ in range(5):
        api_login(lock_email, "WrongPass1")
    r, _, _, _ = api_login(lock_email, PW)
    check("AUTH-030", "API login locked after 5 failures (correct pw -> 401 'locked')", r.status == 401 and "lock" in (J(r, "error") or "").lower(), f"{r.status} {J(r,'error')}", bug="BUG-20")
    _, rr = mvc_login(lock_email, PW)
    check("AUTH-031", "Lockout shared with MVC login", "/Account/Login" in rr.url, path_of(rr.url))
    statuses, retry_after = [], None
    for _ in range(14):
        rr = Client(API).req("POST", "/api/auth/login", json_body={"email": "nobody@example.invalid", "password": "x"}, retry429=False, pace=False)
        statuses.append(rr.status)
        if rr.status == 429:
            retry_after = rr.headers.get("retry-after")
            break
    check("RATE-010", "Auth endpoint rate-limited -> 429 ProblemDetails + Retry-After", statuses[-1] == 429 and retry_after and is_problem(rr, 429),
          f"statuses={statuses} retry-after={retry_after}")
    check("RATE-011", "Health/hub not rate limited while auth is limited", anon_api.get("/health").status == 200)

    bad = {k: sorted(v) for k, v in CTYPES.items() if any(c != "application/problem+json" for c in v)}
    check("API-015", "Every error body is served as application/problem+json", not bad, bad, bug="NEW-01")

    # ---------------------------------------------------------- DB-level assertions
    if SQLCMD:
        check("DB-001", "History row for every request status change (no request without history)",
              sql("SELECT COUNT(*) FROM ServiceRequests r WHERE NOT EXISTS (SELECT 1 FROM ServiceHistories h WHERE h.RequestId=r.RequestId)") == "0")
        check("DB-002", "Every Completed request with a bill has exactly one Payment",
              sql("SELECT COUNT(*) FROM ServiceRequests r JOIN Bills b ON b.ServiceRequestId=r.RequestId LEFT JOIN Payments p ON p.BillId=b.Id WHERE r.Status='Completed' AND p.PaymentId IS NULL") == "0")
    for name, path in (("API", os.environ.get("ASMS_API_LOG")), ("WEB", os.environ.get("ASMS_WEB_LOG"))):
        if path and os.path.exists(path):
            txt = open(path, encoding="utf-8", errors="replace").read()
            if name == "API":
                n_empty = len(re.findall(r"SendGrid returned \w+ for :", txt))
                check("LOG-EMAIL", "No email is sent to an empty recipient", n_empty == 0, f"{n_empty} sends with empty 'to'", bug="NEW-05")
            check(f"LOG-{name}", f"{name} log has no DbContext concurrency errors / 500s",
                  "second operation was started" not in txt and "responded 500" not in txt,
                  "; ".join(sorted(set(re.findall(r"HTTP \w+ \S+ responded 500", txt)))[:5]) or "second-operation error", bug="BUG-09")
    return finish()


def run_signalr(**kw):
    script = os.path.join(HERE, "signalr_isolation.mjs")
    if not os.path.exists(os.path.join(SIGNALR_MODULE, "package.json")):
        return None
    env = dict(os.environ, SIGNALR_MODULE=os.path.abspath(SIGNALR_MODULE), **{f"SR_{k.upper()}": str(v) for k, v in kw.items()})
    p = subprocess.run(["node", script], capture_output=True, text=True, env=env, timeout=120)
    try:
        out = json.loads(p.stdout.strip().splitlines()[-1])
    except Exception:
        return {"NOTIF-010": (False, "SignalR isolation suite ran", (p.stdout + p.stderr)[-300:])}
    return {k: (v["ok"], v["title"], v.get("detail", "")) for k, v in out.items()}


def finish():
    passed = sum(1 for r in RESULTS if r["ok"]); failed = len(RESULTS) - passed
    print(f"\n== {passed} passed, {failed} failed, {len(RESULTS)} total", flush=True)
    out = os.environ.get("ASMS_JSON_OUT", os.path.join(HERE, "last-run-v2.json"))
    with open(out, "w", encoding="utf-8") as fh:
        json.dump({"api": API, "web": WEB, "run": RUN, "passed": passed, "failed": failed, "results": RESULTS}, fh, indent=1)
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
