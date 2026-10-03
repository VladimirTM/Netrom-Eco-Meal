import { Route, Routes } from "react-router-dom";
import AuthProvider from "./context/AuthContext/AuthProvider";
import ThemeProvider from "./context/ThemeContext/ThemeProvider";
import ToastProvider from "./context/ToastContext/ToastProvider";
import TimeZoneProvider from "./context/TimeZoneContext/TimeZoneProvider";
import ScrollAndFocusManager from "./routes/ScrollAndFocusManager";
import RequireRole from "./routes/RequireRole";
import EmptyLayout from "./layouts/EmptyLayout";
import PublicLayout from "./layouts/PublicLayout";
import DashboardLayout from "./layouts/DashboardLayout";
import Login from "./components/Account/Login";
import Register from "./components/Account/Register";
import ConfirmEmail from "./components/Account/ConfirmEmail";
import ResendConfirmation from "./components/Account/ResendConfirmation";
import ForgotPassword from "./components/Account/ForgotPassword";
import ResetPassword from "./components/Account/ResetPassword";
import AccountSettings from "./components/Account/AccountSettings";
import AccessDenied from "./components/Account/AccessDenied";
import Home from "./components/Home";
import BusinessDetail from "./components/BusinessDetail";
import Brands from "./components/Brands";
import BrandDetail from "./components/BrandDetail";
import Impact from "./components/Impact";
import BasketPlanner from "./components/BasketPlanner";
import Dashboard from "./components/Dashboard";
import NotFound from "./components/NotFound";

function AppRoutes() {
  return (
    <Routes>
      {/* Chrome-free account pages — keeps the URL paths already sent in emails. */}
      <Route element={<EmptyLayout />}>
        <Route path="/account/login" element={<Login />} />
        <Route path="/account/register" element={<Register />} />
        <Route path="/account/confirm-email" element={<ConfirmEmail />} />
        <Route path="/account/resend-confirmation" element={<ResendConfirmation />} />
        <Route path="/account/forgot-password" element={<ForgotPassword />} />
        <Route path="/account/reset-password" element={<ResetPassword />} />
      </Route>

      {/* Public-facing shell — anonymous visitors and customers. */}
      <Route element={<PublicLayout />}>
        <Route path="/" element={<Home />} />
        <Route path="/businesses/:id" element={<BusinessDetail />} />
        <Route path="/brands" element={<Brands />} />
        <Route path="/brands/:id" element={<BrandDetail />} />
        <Route path="/impact" element={<Impact />} />
        <Route
          path="/plan-basket"
          element={
            <RequireRole roles={["Customer"]}>
              <BasketPlanner />
            </RequireRole>
          }
        />
        <Route path="/account/access-denied" element={<AccessDenied />} />
        <Route
          path="/account/settings"
          element={
            <RequireRole>
              <AccountSettings />
            </RequireRole>
          }
        />
        <Route path="/not-found" element={<NotFound />} />
        <Route path="*" element={<NotFound />} />
      </Route>

      {/* Admin/BusinessManager shell. */}
      <Route
        element={
          <RequireRole roles={["Admin", "BusinessManager"]}>
            <DashboardLayout />
          </RequireRole>
        }
      >
        <Route path="/dashboard" element={<Dashboard />} />
      </Route>
    </Routes>
  );
}

function App() {
  return (
    <AuthProvider>
      <ThemeProvider>
        <TimeZoneProvider>
          <ToastProvider>
            <ScrollAndFocusManager />
            <AppRoutes />
          </ToastProvider>
        </TimeZoneProvider>
      </ThemeProvider>
    </AuthProvider>
  );
}

export default App;
