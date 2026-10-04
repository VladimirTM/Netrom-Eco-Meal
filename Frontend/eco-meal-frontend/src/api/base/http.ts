import axios from "axios";

export const TOKEN_KEY = "em_auth_token";

// Carries the AuthErrorDto `code` (e.g. "email_not_confirmed") through the generic error
// pipeline below, so a caller can branch on it without string-matching the display message.
export class ApiError extends Error {
  code?: string;
  status?: number;

  constructor(message: string, code?: string, status?: number) {
    super(message);
    this.name = "ApiError";
    this.code = code;
    this.status = status;
  }
}

let onUnauthorized: (() => void) | null = null;

export function setUnauthorizedHandler(handler: () => void) {
  onUnauthorized = handler;
}

const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL,
  headers: { "Content-Type": "application/json" },
});

api.interceptors.request.use((config) => {
  const token = localStorage.getItem(TOKEN_KEY);
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

api.interceptors.response.use(
  (response) => response,
  (error) => {
    // Any 401 while a token exists means the session is invalid/expired — log out globally.
    if (error.response?.status === 401 && localStorage.getItem(TOKEN_KEY)) {
      onUnauthorized?.();
    }

    const data = error.response?.data;
    let message: string;
    let code: string | undefined;

    if (typeof data === "string" && data !== "") {
      message = data;
    } else if (data && typeof data === "object") {
      const errors = data.errors as Record<string, string[]> | undefined;
      code = data.code;
      message =
        data.error ??
        data.detail ??
        data.title ??
        (errors ? Object.values(errors).flat()[0] : null) ??
        error.message ??
        "Request failed";
    } else {
      message = error.message || "Request failed";
    }

    return Promise.reject(new ApiError(message, code, error.response?.status));
  },
);

export const http = {
  get: async <T>(path: string): Promise<T> => {
    const response = await api.get<T>(path);
    return response.data;
  },
  post: async <T>(path: string, body?: unknown): Promise<T> => {
    const response = await api.post<T>(path, body);
    return response.data;
  },
  put: async <T>(path: string, body?: unknown): Promise<T> => {
    const response = await api.put<T>(path, body);
    return response.data;
  },
  remove: async <T>(path: string): Promise<T> => {
    const response = await api.delete<T>(path);
    return response.data;
  },
  // Multipart upload (UploadsController). The instance's static `Content-Type: application/json`
  // default would otherwise stick and break the multipart boundary — setting it to `undefined`
  // here lets the browser's XHR/fetch layer compute the correct `multipart/form-data; boundary=...`
  // header itself from the FormData body.
  postForm: async <T>(path: string, formData: FormData): Promise<T> => {
    const response = await api.post<T>(path, formData, { headers: { "Content-Type": undefined } });
    return response.data;
  },
  // CSV export download (ExportsController). A JWT bearer token can't ride along on a plain
  // `<a href>` navigation the way Blazor's auth cookie did — Axios carries the Authorization header
  // instead, and the caller turns the returned Blob into an object URL to trigger the save.
  getBlob: async (path: string): Promise<Blob> => {
    const response = await api.get<Blob>(path, { responseType: "blob" });
    return response.data;
  },
};
