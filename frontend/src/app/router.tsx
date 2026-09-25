import { lazy } from 'react';
import { createBrowserRouter, type RouteObject } from 'react-router-dom';
import { AppLayout } from '@/layouts/app-layout';
import { AuthLayout } from '@/layouts/auth-layout';
import { ForgotPasswordPage, LoginPage, RegisterPage, ResetPasswordPage } from '@/features/auth';
import { DOCUMENT_TYPE_LIST, DocumentDetailPage, DocumentEditPage, DocumentFormPage, DocumentListPage } from '@/features/documents';
import { StockPage } from '@/features/stock';
import { PERMISSIONS } from '@/lib/permissions';
import { HomeRedirect, NotFound, PermissionRoute, ProtectedRoute, PublicOnlyRoute } from './route-guards';

// Heavier pages (recharts on the dashboard) and admin-only pages are code-split.
const CatalogPage = lazy(() => import('@/features/catalog').then((m) => ({ default: m.CatalogPage })));
const SettingsPage = lazy(() => import('@/features/settings').then((m) => ({ default: m.SettingsPage })));
const DashboardPage = lazy(() => import('@/features/reports').then((m) => ({ default: m.DashboardPage })));
const KardexPage = lazy(() => import('@/features/reports').then((m) => ({ default: m.KardexPage })));
const StockAlertsPage = lazy(() => import('@/features/stock-alerts').then((m) => ({ default: m.StockAlertsPage })));
const ApiLogsPage = lazy(() => import('@/features/api-logs').then((m) => ({ default: m.ApiLogsPage })));

/** /goods-receipts, /goods-receipts/new, /goods-receipts/:id … for each document type, gated by its activity. */
const documentRoutes: RouteObject[] = DOCUMENT_TYPE_LIST.flatMap((cfg) => [
  {
    element: <PermissionRoute anyOf={[[cfg.activity, 'R']]} />,
    children: [
      { path: `/${cfg.path}`, element: <DocumentListPage key={cfg.type} type={cfg.type} /> },
      { path: `/${cfg.path}/:id`, element: <DocumentDetailPage key={cfg.type} type={cfg.type} /> },
    ],
  },
  {
    element: <PermissionRoute anyOf={[[cfg.activity, 'C']]} />,
    // declared after /:id is fine: react-router ranks the static "new" segment higher
    children: [
      { path: `/${cfg.path}/new`, element: <DocumentFormPage key={cfg.type} type={cfg.type} /> },
      { path: `/${cfg.path}/:id/edit`, element: <DocumentEditPage key={cfg.type} type={cfg.type} /> },
    ],
  },
]);

export const routes: RouteObject[] = [
  {
    element: <AuthLayout />,
    children: [
      {
        element: <PublicOnlyRoute />,
        children: [
          { path: '/login', element: <LoginPage /> },
          { path: '/register', element: <RegisterPage /> },
          { path: '/forgot-password', element: <ForgotPasswordPage /> },
        ],
      },
      { path: '/reset-password', element: <ResetPasswordPage /> },
    ],
  },
  {
    element: <ProtectedRoute />,
    children: [
      {
        element: <AppLayout />,
        children: [
          { index: true, element: <HomeRedirect /> },
          {
            element: <PermissionRoute anyOf={PERMISSIONS.stock} />,
            children: [
              { path: '/dashboard', element: <DashboardPage /> },
              { path: '/stock', element: <StockPage /> },
              { path: '/kardex', element: <KardexPage /> },
              { path: '/alerts', element: <StockAlertsPage /> },
            ],
          },
          ...documentRoutes,
          {
            element: <PermissionRoute anyOf={PERMISSIONS.catalog} />,
            children: [{ path: '/catalog', element: <CatalogPage /> }],
          },
          {
            element: <PermissionRoute anyOf={PERMISSIONS.settings} />,
            children: [{ path: '/settings', element: <SettingsPage /> }],
          },
          {
            element: <PermissionRoute anyOf={PERMISSIONS.apiLogs} />,
            children: [{ path: '/api-logs', element: <ApiLogsPage /> }],
          },
          { path: '*', element: <NotFound /> },
        ],
      },
    ],
  },
];

export const router = createBrowserRouter(routes, {
  future: {
    v7_relativeSplatPath: true,
    v7_fetcherPersist: true,
    v7_normalizeFormMethod: true,
    v7_partialHydration: true,
    v7_skipActionErrorRevalidation: true,
  },
});
