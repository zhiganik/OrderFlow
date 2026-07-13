import { createBrowserRouter, Navigate } from 'react-router-dom'

import { AppShell } from '@/components/layout/app-shell'
import { NotFoundPage } from '@/components/layout/not-found-page'
import { RootErrorBoundary } from '@/components/layout/root-error-boundary'
import { PublicOnly, RequireAdmin, RequireAuth } from '@/components/layout/route-guards'
import { LoginPage } from '@/features/auth/pages/login-page'
import { RegisterPage } from '@/features/auth/pages/register-page'
import { AdminOrdersPage } from '@/features/admin/orders/pages/admin-orders-page'
import { AdminStockPage } from '@/features/admin/stock/pages/admin-stock-page'
import { OrderCreatePage } from '@/features/orders/pages/order-create-page'
import { OrderDetailPage } from '@/features/orders/pages/order-detail-page'
import { OrderListPage } from '@/features/orders/pages/order-list-page'

export const router = createBrowserRouter([
  {
    element: <AppShell />,
    errorElement: <RootErrorBoundary />,
    children: [
      {
        path: '/login',
        element: (
          <PublicOnly>
            <LoginPage />
          </PublicOnly>
        ),
      },
      {
        path: '/register',
        element: (
          <PublicOnly>
            <RegisterPage />
          </PublicOnly>
        ),
      },
      {
        element: <RequireAuth />,
        children: [
          { index: true, element: <Navigate to="/orders" replace /> },
          { path: 'orders', element: <OrderListPage /> },
          { path: 'orders/new', element: <OrderCreatePage /> },
          { path: 'orders/:orderId', element: <OrderDetailPage /> },
          {
            element: <RequireAdmin />,
            children: [
              { path: 'admin/orders', element: <AdminOrdersPage /> },
              { path: 'admin/stock', element: <AdminStockPage /> },
            ],
          },
        ],
      },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
])
