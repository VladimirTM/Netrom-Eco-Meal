export type AppRole = "Admin" | "Customer" | "BusinessManager";

export interface UserDto {
  id: string;
  name: string;
  email: string;
  role: AppRole;
}

export interface AuthResponseDto {
  token: string;
  user: UserDto;
}
