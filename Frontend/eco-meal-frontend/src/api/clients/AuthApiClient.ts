import { http } from "../base/http";
import type { AuthResponseDto, UserDto } from "../models/User";

interface LoginRequest {
  email: string;
  password: string;
}

interface RegisterRequest {
  name: string;
  email: string;
  password: string;
  referralCode?: string | null;
}

interface RegisterResponse {
  token: string | null;
  user: UserDto | null;
  info: string | null;
}

export const authApi = {
  login: (data: LoginRequest): Promise<AuthResponseDto> => http.post<AuthResponseDto>("/auth/login", data),

  register: (data: RegisterRequest): Promise<RegisterResponse> =>
    http.post<RegisterResponse>("/auth/register", {
      name: data.name,
      email: data.email,
      password: data.password,
      referralCode: data.referralCode || null,
    }),

  me: (): Promise<UserDto> => http.get<UserDto>("/auth/me"),

  updateName: (name: string): Promise<void> => http.put<void>("/auth/me/name", { name }),

  changePassword: (currentPassword: string, newPassword: string): Promise<void> =>
    http.put<void>("/auth/me/password", { currentPassword, newPassword }),

  confirmEmail: (userId: string, token: string): Promise<void> =>
    http.post<void>("/auth/confirm-email", { userId, token }),

  resendConfirmation: (email: string): Promise<void> => http.post<void>("/auth/resend-confirmation", { email }),

  forgotPassword: (email: string): Promise<void> => http.post<void>("/auth/forgot-password", { email }),

  resetPassword: (email: string, token: string, newPassword: string): Promise<void> =>
    http.post<void>("/auth/reset-password", { email, token, newPassword }),
};
