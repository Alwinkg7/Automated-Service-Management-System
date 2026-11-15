// Form schemas mirroring ServiceApp.Web/ViewModels/Auth/*.cs validation rules.
import { z } from "zod";
import { SERVICE_CATEGORIES } from "@/types/enums";

export const loginSchema = z.object({
  email: z.email("Enter a valid email"),
  password: z.string().min(1, "Password is required"),
});
export type LoginForm = z.infer<typeof loginSchema>;

const phone = z
  .string()
  .trim()
  .min(10, "Enter a valid phone number")
  .max(15, "Enter a valid phone number")
  .regex(/^[+\d][\d\s-]+$/, "Enter a valid phone number");

const base = {
  fullName: z.string().trim().min(2, "Name must be 2–100 characters").max(100, "Name must be 2–100 characters"),
  email: z.email("Enter a valid email address"),
  phone,
  password: z.string().min(8, "Password must be at least 8 characters").max(100),
  confirmPassword: z.string().min(1, "Please confirm your password"),
};

export const registerSchema = z
  .discriminatedUnion("role", [
    z.object({
      role: z.literal("Customer"),
      ...base,
      referralCode: z.string().trim().max(20).optional(),
    }),
    z.object({
      role: z.literal("Technician"),
      ...base,
      skill: z.enum(SERVICE_CATEGORIES, "Please select your skill"),
    }),
  ])
  .refine((v) => v.password === v.confirmPassword, {
    message: "Passwords do not match",
    path: ["confirmPassword"],
  });
export type RegisterForm = z.infer<typeof registerSchema>;
