// Mirrors ServiceApp.Api/Contracts/CustomerContracts.cs + the MVC "1 hour from now" rule.
import { z } from "zod";
import { SERVICE_CATEGORIES } from "@/types/enums";

export const createRequestSchema = z.object({
  category: z.enum(SERVICE_CATEGORIES, "Choose a service"),
  description: z.string().trim().min(5, "Describe the problem (at least 5 characters)").max(1000),
  scheduledDate: z
    .string()
    .min(1, "Pick a date and time")
    .refine((v) => {
      const t = Date.parse(v);
      return !Number.isNaN(t) && t > Date.now() + 60 * 60 * 1000;
    }, "Please select a time at least 1 hour from now."),
  address: z.string().trim().min(5, "Enter the full address").max(300),
  pinCode: z
    .string()
    .trim()
    .regex(/^\d{6}$/, "Enter a 6-digit pin code")
    .or(z.literal("")),
});
export type CreateRequestForm = z.infer<typeof createRequestSchema>;

export const rateSchema = z.object({
  stars: z.number().int().min(1, "Pick a rating").max(5),
  comment: z.string().max(1000).optional(),
});

export const disputeFormSchema = z.object({
  reason: z.string().min(1, "Choose a reason"),
  description: z
    .string()
    .trim()
    .min(20, "Please provide at least 20 characters describing the issue.")
    .max(2000),
});
export type DisputeForm = z.infer<typeof disputeFormSchema>;
