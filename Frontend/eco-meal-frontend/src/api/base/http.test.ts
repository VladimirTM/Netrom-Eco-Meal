import { afterAll, afterEach, beforeAll, beforeEach, describe, expect, it, vi } from "vitest";
import { http as mswHttp, HttpResponse } from "msw";
import { setupServer } from "msw/node";
import { http, setUnauthorizedHandler, TOKEN_KEY, ApiError } from "./http";

const API_URL = import.meta.env.VITE_API_URL;

const server = setupServer(
  mswHttp.get(`${API_URL}/unauthorized`, () => HttpResponse.json({ error: "nope", code: "bad" }, { status: 401 })),
  mswHttp.get(`${API_URL}/structured-error`, () =>
    HttpResponse.json({ error: "Invalid email or password.", code: "invalid_credentials" }, { status: 401 }),
  ),
);

beforeAll(() => server.listen());
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

beforeEach(() => {
  localStorage.clear();
  setUnauthorizedHandler(() => {});
});

describe("http interceptor", () => {
  it("calls the unauthorized handler on a 401 when a token exists", async () => {
    localStorage.setItem(TOKEN_KEY, "some-token");
    const onUnauthorized = vi.fn();
    setUnauthorizedHandler(onUnauthorized);

    await expect(http.get("/unauthorized")).rejects.toThrow();
    expect(onUnauthorized).toHaveBeenCalledTimes(1);
  });

  it("does not call the unauthorized handler on a 401 when there is no token", async () => {
    const onUnauthorized = vi.fn();
    setUnauthorizedHandler(onUnauthorized);

    await expect(http.get("/unauthorized")).rejects.toThrow();
    expect(onUnauthorized).not.toHaveBeenCalled();
  });

  it("surfaces the backend's error/code shape as an ApiError", async () => {
    localStorage.setItem(TOKEN_KEY, "some-token");

    await expect(http.get("/structured-error")).rejects.toMatchObject({
      message: "Invalid email or password.",
      code: "invalid_credentials",
    });
  });

  it("is an instance of ApiError", async () => {
    try {
      await http.get("/structured-error");
      expect.fail("expected http.get to throw");
    } catch (err) {
      expect(err).toBeInstanceOf(ApiError);
    }
  });
});
