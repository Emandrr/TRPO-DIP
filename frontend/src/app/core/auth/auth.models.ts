export type UserRole = 'Patient' | 'Doctor' | 'Admin';

export interface TokenResponse {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;
  mustChangePassword: boolean;
  role: UserRole;
  userId: string;
}

export interface AuthSession {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;
  mustChangePassword: boolean;
  role: UserRole;
  userId: string;
}
