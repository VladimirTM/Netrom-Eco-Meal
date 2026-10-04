import { Route, Routes } from "react-router-dom";
import AuthProvider from "./context/AuthContext/AuthProvider";
import ThemeProvider from "./context/ThemeContext/ThemeProvider";
import ToastProvider from "./context/ToastContext/ToastProvider";
import TimeZoneProvider from "./context/TimeZoneContext/TimeZoneProvider";
import CartProvider from "./context/CartContext/CartProvider";
import NotificationProvider from "./context/NotificationContext/NotificationProvider";
import ManagedBusinessProvider from "./context/ManagedBusinessContext/ManagedBusinessProvider";
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
import PaymentReturn from "./components/PaymentReturn";
import PaymentCancel from "./components/PaymentCancel";
import Orders from "./components/Orders";
import OrderPickupPass from "./components/OrderPickupPass";
import TripPlanner from "./components/TripPlanner";
import RescueCircleList from "./components/RescueCircleList";
import RescueCircleInvite from "./components/RescueCircleInvite";
import RescueCircleReturn from "./components/RescueCircleReturn";
import StandingOrders from "./components/StandingOrders";
import Referrals from "./components/Referrals";
import BusinessApply from "./components/BusinessApply";
import OrderManagement from "./components/OrderManagement";
import OrderScan from "./components/OrderScan";
import OrderValidate from "./components/OrderValidate";
import OrderValidateLegacy from "./components/OrderValidateLegacy";
import Payments from "./components/Payments";
import Businesses from "./components/Businesses";
import BusinessForm from "./components/BusinessForm";
import Packages from "./components/Packages";
import PackageForm from "./components/PackageForm";
import PackageTemplates from "./components/PackageTemplates";
import Users from "./components/Users";
import Reports from "./components/Reports";
import AuditLog from "./components/AuditLog";
import Types from "./components/Types";

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

        {/* Stripe redirect targets — CheckoutService.cs/RescueCircleService.cs build these exact
            paths into their success_url/cancel_url, so they must not change. */}
        <Route
          path="/checkout/return"
          element={
            <RequireRole roles={["Customer"]}>
              <PaymentReturn />
            </RequireRole>
          }
        />
        <Route
          path="/checkout/cancel"
          element={
            <RequireRole roles={["Customer"]}>
              <PaymentCancel />
            </RequireRole>
          }
        />
        <Route
          path="/circles/return"
          element={
            <RequireRole roles={["Customer"]}>
              <RescueCircleReturn />
            </RequireRole>
          }
        />
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

        <Route
          path="/orders"
          element={
            <RequireRole roles={["Customer"]}>
              <Orders />
            </RequireRole>
          }
        />
        <Route
          path="/orders/pickup/:id"
          element={
            <RequireRole roles={["Customer"]}>
              <OrderPickupPass />
            </RequireRole>
          }
        />
        <Route
          path="/trip-planner"
          element={
            <RequireRole roles={["Customer"]}>
              <TripPlanner />
            </RequireRole>
          }
        />
        <Route
          path="/circles"
          element={
            <RequireRole roles={["Customer"]}>
              <RescueCircleList />
            </RequireRole>
          }
        />
        <Route
          path="/circles/:circleId"
          element={
            <RequireRole roles={["Customer"]}>
              <RescueCircleInvite />
            </RequireRole>
          }
        />
        <Route
          path="/standing-orders"
          element={
            <RequireRole roles={["Customer"]}>
              <StandingOrders />
            </RequireRole>
          }
        />
        <Route
          path="/referrals"
          element={
            <RequireRole roles={["Customer"]}>
              <Referrals />
            </RequireRole>
          }
        />
        <Route
          path="/businesses/apply"
          element={
            <RequireRole roles={["Customer", "BusinessManager"]}>
              <BusinessApply />
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
        <Route path="/businesses" element={<Businesses />} />
        <Route path="/businesses/create" element={<BusinessForm />} />
        <Route path="/businesses/edit/:id" element={<BusinessForm />} />
        <Route path="/packages" element={<Packages />} />
        <Route path="/packages/create" element={<PackageForm />} />
        <Route path="/packages/edit/:id" element={<PackageForm />} />
        <Route path="/packages/templates" element={<PackageTemplates />} />
        <Route path="/orders/manage" element={<OrderManagement />} />
        <Route path="/orders/scan" element={<OrderScan />} />
        <Route path="/orders/validate/:id/:passId" element={<OrderValidate />} />
        <Route path="/orders/validate/:id" element={<OrderValidateLegacy />} />
        <Route path="/payments" element={<Payments />} />

        {/* Admin-only pages: a nested RequireRole, stricter than the shell's Admin-or-BusinessManager
            gate, so a BusinessManager hitting one of these URLs sees ForbiddenPanel inline. */}
        <Route
          path="/users"
          element={
            <RequireRole roles={["Admin"]}>
              <Users />
            </RequireRole>
          }
        />
        <Route
          path="/reports"
          element={
            <RequireRole roles={["Admin"]}>
              <Reports />
            </RequireRole>
          }
        />
        <Route
          path="/audit-log"
          element={
            <RequireRole roles={["Admin"]}>
              <AuditLog />
            </RequireRole>
          }
        />
        <Route
          path="/types"
          element={
            <RequireRole roles={["Admin"]}>
              <Types />
            </RequireRole>
          }
        />
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
            <CartProvider>
              <NotificationProvider>
                <ManagedBusinessProvider>
                  <ScrollAndFocusManager />
                  <AppRoutes />
                </ManagedBusinessProvider>
              </NotificationProvider>
            </CartProvider>
          </ToastProvider>
        </TimeZoneProvider>
      </ThemeProvider>
    </AuthProvider>
  );
}

export default App;
