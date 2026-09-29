// Fixed demo credentials seeded by Database/DbSeeder.cs and docker-compose.test.yml's
// SeedAdmin:Email/SeedAdmin:Password — see that file if these ever change.
export const accounts = {
  admin: { email: 'admin@ecomeal.local', password: 'Admin123!' },
  customer: { email: 'demo.customer@ecomeal.local', password: 'Demo123!' },
  customer2: { email: 'demo.customer2@ecomeal.local', password: 'Demo123!' },
  customer3: { email: 'demo.customer3@ecomeal.local', password: 'Demo123!' },
  // Staffs two businesses (Stadionul de Gusturi + VAR Bistro) — use this one for the
  // business-switcher scenario.
  manager: { email: 'demo.manager@ecomeal.local', password: 'Demo123!' },
  // Staffs one business only (Stadionul de Gusturi, alongside `manager` above).
  manager2: { email: 'demo.manager2@ecomeal.local', password: 'Demo123!' },
} as const;

export const seededBusinesses = {
  stadionulDeGusturi: '44444444-0000-0000-0000-000000000001',
  varBistro: '44444444-0000-0000-0000-000000000002',
  poartaDeAurBakery: '44444444-0000-0000-0000-000000000004',
} as const;

export const mailpitUrl = process.env.MAILPIT_URL ?? 'http://localhost:8025';
