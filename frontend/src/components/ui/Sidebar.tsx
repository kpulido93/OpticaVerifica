'use client'

import { cn } from '@/lib/utils'
import { useAuth } from '@/lib/auth'
import { 
  LayoutDashboard, 
  History, 
  Settings, 
  LogOut, 
  Shield,
  Database
} from 'lucide-react'

interface SidebarProps {
  currentView: string
  onNavigate: (view: any) => void
  isAdmin: boolean
}

export default function Sidebar({ currentView, onNavigate, isAdmin }: SidebarProps) {
  const { user, logout } = useAuth()

  const navItems = [
    { id: 'dashboard', label: 'Dashboard', icon: LayoutDashboard },
    { id: 'jobs', label: 'Historial de Jobs', icon: History },
  ]

  const adminItems = [
    { id: 'preset-designer', label: 'Diseñador de Presets', icon: Database },
  ]

  return (
    <aside className="fixed left-0 top-0 h-full w-64 bg-surface border-r border-border flex flex-col z-40">
      {/* Logo */}
      <div className="p-6 border-b border-border">
        <h1 className="text-xl font-bold font-heading">
          Optima <span className="text-primary">Verifica</span>
        </h1>
      </div>

      {/* User Info */}
      <div className="p-4 border-b border-border">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-full bg-primary/20 flex items-center justify-center">
            <span className="text-primary font-semibold">
              {user?.username.charAt(0).toUpperCase()}
            </span>
          </div>
          <div>
            <p className="font-medium text-white">{user?.username}</p>
            <div className="flex items-center gap-1.5 text-xs text-zinc-400">
              <Shield className="w-3 h-3" />
              {user?.role}
            </div>
          </div>
        </div>
      </div>

      {/* Navigation */}
      <nav className="flex-1 p-4 space-y-1 overflow-y-auto">
        <p className="text-xs font-medium text-zinc-500 uppercase tracking-wider mb-3">
          Principal
        </p>
        {navItems.map((item) => (
          <button
            key={item.id}
            onClick={() => onNavigate(item.id)}
            data-testid={`nav-${item.id}`}
            className={cn(
              'w-full flex items-center gap-3 px-3 py-2.5 rounded-lg transition-all duration-200',
              currentView === item.id
                ? 'bg-primary text-white'
                : 'text-zinc-400 hover:text-white hover:bg-surface-highlight'
            )}
          >
            <item.icon className="w-5 h-5" />
            {item.label}
          </button>
        ))}

        {isAdmin && (
          <>
            <p className="text-xs font-medium text-zinc-500 uppercase tracking-wider mt-6 mb-3">
              Administración
            </p>
            {adminItems.map((item) => (
              <button
                key={item.id}
                onClick={() => onNavigate(item.id)}
                data-testid={`nav-${item.id}`}
                className={cn(
                  'w-full flex items-center gap-3 px-3 py-2.5 rounded-lg transition-all duration-200',
                  currentView === item.id
                    ? 'bg-primary text-white'
                    : 'text-zinc-400 hover:text-white hover:bg-surface-highlight'
                )}
              >
                <item.icon className="w-5 h-5" />
                {item.label}
              </button>
            ))}
          </>
        )}
      </nav>

      {/* Logout */}
      <div className="p-4 border-t border-border">
        <button
          onClick={logout}
          data-testid="logout-btn"
          className="w-full flex items-center gap-3 px-3 py-2.5 rounded-lg text-zinc-400 hover:text-destructive hover:bg-destructive/10 transition-all duration-200"
        >
          <LogOut className="w-5 h-5" />
          Cerrar Sesión
        </button>
      </div>
    </aside>
  )
}
