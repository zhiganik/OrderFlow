import { NavLink, Outlet, useNavigate } from 'react-router-dom'

import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'
import { useAuth } from '@/features/auth/auth-context'

function NavItem({ to, children }: { to: string; children: React.ReactNode }) {
  return (
    <NavLink
      to={to}
      className={({ isActive }) =>
        cn(
          'rounded-md px-3 py-1.5 text-sm font-medium transition-colors hover:bg-muted hover:text-foreground',
          isActive ? 'bg-muted text-foreground' : 'text-muted-foreground'
        )
      }
    >
      {children}
    </NavLink>
  )
}

export function AppShell() {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const isAdmin = user?.roles.includes('Admin') ?? false

  async function handleLogout() {
    await logout()
    navigate('/login', { replace: true })
  }

  return (
    <div className="flex min-h-svh flex-col">
      <header className="border-b">
        <div className="mx-auto flex max-w-5xl items-center gap-4 px-4 py-3">
          <span className="font-semibold">OrderFlow</span>
          {user && (
            <nav className="flex flex-1 items-center gap-1">
              <NavItem to="/orders">Orders</NavItem>
              <NavItem to="/orders/new">New order</NavItem>
              {isAdmin && (
                <>
                  <NavItem to="/admin/orders">All orders</NavItem>
                  <NavItem to="/admin/stock">Stock</NavItem>
                </>
              )}
            </nav>
          )}
          {user && (
            <div className="flex items-center gap-3">
              <span className="text-sm text-muted-foreground">{user.email}</span>
              <Button variant="outline" size="sm" onClick={handleLogout}>
                Log out
              </Button>
            </div>
          )}
        </div>
      </header>
      <main className="mx-auto flex w-full max-w-5xl flex-1 flex-col p-4">
        <Outlet />
      </main>
    </div>
  )
}
