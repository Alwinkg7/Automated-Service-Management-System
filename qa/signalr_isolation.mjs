// SignalR hub group-isolation checks (BUG-23 / BUG-19) against the API host.
// Driven by qa/asms_e2e_v2.py; prints ONE JSON line: { checkId: { ok, title, detail } }.
// Env: SIGNALR_MODULE (path to @microsoft/signalr), SR_API, SR_A_TOK, SR_A_ID, SR_B_TOK,
//      SR_TECH_TOK (assigned tech, job InProgress), SR_OTHER_TECH_TOK, SR_ADMIN_TOK, SR_REQUEST_ID
import { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const signalR = require(process.env.SIGNALR_MODULE);

const API = process.env.SR_API;
const reqId = parseInt(process.env.SR_REQUEST_ID, 10);
const out = {};
const rec = (id, ok, title, detail = "") => (out[id] = { ok: !!ok, title, detail: String(detail) });
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function connect(token, label) {
  const events = [];
  const c = new signalR.HubConnectionBuilder()
    .withUrl(`${API}/hubs/service`, { accessTokenFactory: () => token, transport: signalR.HttpTransportType.WebSockets })
    .configureLogging(signalR.LogLevel.None)
    .build();
  for (const ev of ["LocationUpdate", "LocationStopped", "NewRequest", "StatusChanged", "JobAssigned", "NewJobAvailable", "JobAccepted", "BillCreated", "JobCompleted"])
    c.on(ev, (p) => events.push({ ev, p }));
  await c.start();
  return { c, events, label };
}

try {
  const A = await connect(process.env.SR_A_TOK, "customerA");
  const B = await connect(process.env.SR_B_TOK, "customerB");
  const T = await connect(process.env.SR_TECH_TOK, "tech");
  const T2 = await connect(process.env.SR_OTHER_TECH_TOK, "otherTech");
  const ADM = await connect(process.env.SR_ADMIN_TOK, "admin");
  rec("NOTIF-011", true, "WebSocket connect with JWT (accessTokenFactory) for all roles");

  // Spoof attempts: B asks to join A's personal group and the admin group.
  let spoofErr = "";
  try { await B.c.invoke("JoinGroup", "Customer", process.env.SR_A_ID); await B.c.invoke("JoinGroup", "Admin", process.env.SR_A_ID); }
  catch (e) { spoofErr = e.message; }

  // 1. Assigned tech shares location -> only A receives it.
  await T.c.invoke("ShareLocation", reqId, 10.111, 76.222, "");
  await sleep(1500);
  const aGot = A.events.some((e) => e.ev === "LocationUpdate" && e.p.lat === 10.111);
  const bGot = B.events.some((e) => e.ev === "LocationUpdate");
  rec("NOTIF-012", aGot, "Owner customer receives LocationUpdate from assigned technician", `a=${aGot}`);
  rec("NOTIF-013", !bGot, "JoinGroup spoof does not let customer B receive A's LocationUpdate", `b=${bGot} spoofErr=${spoofErr}`);

  // 2. Non-assigned technician cannot push to A (customerId arg ignored).
  await T2.c.invoke("ShareLocation", reqId, 3.333, 4.444, process.env.SR_A_ID).catch(() => {});
  await sleep(1200);
  const leak = A.events.some((e) => e.ev === "LocationUpdate" && e.p.lat === 3.333);
  rec("NOTIF-014", !leak, "Non-assigned technician cannot push location to the customer", `received=${leak}`);

  // 3. Customer cannot call technician-only hub methods.
  let custErr = null;
  try { await A.c.invoke("ShareLocation", reqId, 1, 1, ""); } catch (e) { custErr = e.message; }
  rec("NOTIF-015", custErr !== null, "Customer invoking ShareLocation is rejected", custErr ?? "no error");

  // 4. Admin-group broadcast: new request by A -> admin gets NewRequest, B (spoofed admin join) does not.
  const res = await fetch(`${API}/api/customer/requests`, {
    method: "POST",
    headers: { "Content-Type": "application/json", Authorization: `Bearer ${process.env.SR_A_TOK}` },
    body: JSON.stringify({ category: "Painting", description: "SignalR admin broadcast probe", scheduledDate: new Date(Date.now() + 2 * 864e5).toISOString().slice(0, 19), address: `SR ${Date.now()} Road`, pinCode: "680001" }),
  });
  const newId = (await res.json()).requestId;
  await sleep(1500);
  const admGot = ADM.events.some((e) => e.ev === "NewRequest" && e.p.requestId === newId);
  const bAdm = B.events.some((e) => e.ev === "NewRequest");
  rec("NOTIF-016", admGot, "Admin connection receives NewRequest", `admin=${admGot} id=${newId}`);
  rec("NOTIF-017", !bAdm, "Customer B (spoofed Admin JoinGroup) does not receive admin broadcasts", `b=${bAdm}`);

  // 5. StopSharing from non-assigned tech is ignored.
  await T2.c.invoke("StopSharing", reqId, process.env.SR_A_ID).catch(() => {});
  await sleep(800);
  rec("NOTIF-018", !A.events.some((e) => e.ev === "LocationStopped"), "Non-assigned technician cannot send LocationStopped");

  for (const x of [A, B, T, T2, ADM]) await x.c.stop();
} catch (e) {
  rec("NOTIF-010", false, "SignalR isolation suite ran", e.stack || e.message);
}
console.log(JSON.stringify(out));
process.exit(0);
