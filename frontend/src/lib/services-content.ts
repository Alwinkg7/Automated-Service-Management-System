// Static, truthful marketing copy for the public SEO pages (one per category).
import { CATEGORY_INFO, SERVICE_CATEGORIES, type ServiceCategory } from "@/types/enums";

export interface ServiceContent {
  category: ServiceCategory;
  slug: string;
  title: string;
  summary: string;
  covers: string[];
  faqs: { q: string; a: string }[];
}

const COVERS: Record<ServiceCategory, string[]> = {
  Plumbing: [
    "Leaking taps and pipes",
    "Blocked drains",
    "Toilet and flush repairs",
    "Water heater connections",
  ],
  Electrical: [
    "Switch and socket repairs",
    "Wiring faults",
    "Fan and light installation",
    "MCB / fuse issues",
  ],
  Carpentry: [
    "Door and hinge repairs",
    "Furniture assembly",
    "Cabinet and drawer fixes",
    "Custom woodwork",
  ],
  Cleaning: [
    "Deep home cleaning",
    "Kitchen and bathroom cleaning",
    "Sofa and carpet cleaning",
    "Pest control",
  ],
  AC_Repair: ["AC servicing", "Gas top-up", "Installation and uninstallation", "Cooling problems"],
  Appliance_Repair: ["Washing machines", "Refrigerators", "Microwaves", "Water purifiers"],
  Painting: ["Interior walls", "Exterior painting", "Touch-ups and texture", "Waterproofing"],
  General: ["Small home repairs", "Mounting and fixing", "Odd jobs", "Inspection visits"],
};

const COMMON_FAQS = [
  {
    q: "How is the technician assigned?",
    a: "After you book, the request is matched to an available technician with the right skill who serves your pin code. You are notified the moment a technician accepts.",
  },
  {
    q: "Can I track the technician?",
    a: "Yes. Once the job is in progress you can follow the technician's live location on a map from your request page.",
  },
  {
    q: "How do I pay?",
    a: "The technician creates an itemised bill when the work is done. You pay securely online through Razorpay and can download a PDF invoice.",
  },
  {
    q: "What if I'm not happy with the service?",
    a: "You can raise a dispute from the request page. Our team reviews disputes within 48 hours and can issue refunds.",
  },
];

export const SERVICES: ServiceContent[] = SERVICE_CATEGORIES.map((category) => {
  const info = CATEGORY_INFO[category];
  return {
    category,
    slug: info.slug,
    title: `${info.label} services`,
    summary: `${info.description}. Book a verified ${info.label.toLowerCase()} technician, track them live and pay online.`,
    covers: COVERS[category],
    faqs: COMMON_FAQS,
  };
});

export function serviceBySlug(slug: string) {
  return SERVICES.find((s) => s.slug === slug);
}
