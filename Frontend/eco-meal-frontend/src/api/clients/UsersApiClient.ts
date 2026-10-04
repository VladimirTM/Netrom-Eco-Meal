import { http } from "../base/http";
import type { PaginatedList } from "../models/Pagination";

// Matches Services.Interfaces.UserWithRole exactly. Admin-only (UsersController enforces it
// server-side) — the Dashboard page is this phase's only caller, for the admin "Users" stat tile.
export interface UserWithRoleDto {
  id: string;
  name: string;
  email: string;
  role: string;
}

export const usersApi = {
  getPaged: (pageIndex = 1, pageSize = 20, search?: string, role?: string): Promise<PaginatedList<UserWithRoleDto>> => {
    const query = new URLSearchParams({ pageIndex: String(pageIndex), pageSize: String(pageSize) });
    if (search) query.set("search", search);
    if (role) query.set("role", role);
    return http.get<PaginatedList<UserWithRoleDto>>(`/users?${query.toString()}`);
  },

  getByRole: (role: string): Promise<UserWithRoleDto[]> => http.get<UserWithRoleDto[]>(`/users/by-role/${role}`),

  // The "can't remove the last admin" guard rail arrives as a 409 with body {"error": "Cannot
  // remove the last remaining admin."} — UsersController takes the role as a query string, not a body.
  updateRole: (userId: string, role: string): Promise<void> => http.put<void>(`/users/${userId}/role?role=${encodeURIComponent(role)}`),
};
