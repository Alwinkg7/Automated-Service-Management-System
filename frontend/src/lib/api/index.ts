"use client";
// Ready-to-use endpoint groups for Client Components (bound to the BFF proxy client).
import { browserApi } from "./browser";
import { adminEndpoints } from "./endpoints/admin";
import { customerEndpoints } from "./endpoints/customer";
import { technicianEndpoints } from "./endpoints/technician";

export const customerApi = customerEndpoints(browserApi);
export const technicianApi = technicianEndpoints(browserApi);
export const adminApi = adminEndpoints(browserApi);
export { browserApi };
