'use client'

import { useState, useEffect } from 'react'
import { toast } from 'sonner'
import Login from '@/components/Login'
import Dashboard from '@/components/Dashboard'
import JobsList from '@/components/JobsList'
import JobDetail from '@/components/JobDetail'
import PresetDesigner from '@/components/admin/PresetDesigner'
import Sidebar from '@/components/ui/Sidebar'
import { AuthContext, User } from '@/lib/auth'

type View = 'dashboard' | 'jobs' | 'job-detail' | 'preset-designer'

export default function Home() {
  const [user, setUser] = useState<User | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [currentView, setCurrentView] = useState<View>('dashboard')
  const [selectedJobId, setSelectedJobId] = useState<string | null>(null)

  useEffect(() => {
    // Check for stored credentials
    const stored = localStorage.getItem('optima_auth')
    if (stored) {
      try {
        const parsed = JSON.parse(stored)
        setUser(parsed)
      } catch {
        localStorage.removeItem('optima_auth')
      }
    }
    setIsLoading(false)
  }, [])

  const handleLogin = (userData: User) => {
    setUser(userData)
    localStorage.setItem('optima_auth', JSON.stringify(userData))
    toast.success(`Bienvenido, ${userData.username}`)
  }

  const handleLogout = () => {
    setUser(null)
    localStorage.removeItem('optima_auth')
    setCurrentView('dashboard')
    toast.info('Sesión cerrada')
  }

  const handleViewJob = (jobId: string) => {
    setSelectedJobId(jobId)
    setCurrentView('job-detail')
  }

  const handleBackToJobs = () => {
    setSelectedJobId(null)
    setCurrentView('jobs')
  }

  if (isLoading) {
    return (
      <div className="min-h-screen bg-background flex items-center justify-center">
        <div className="animate-pulse text-primary text-xl">Cargando...</div>
      </div>
    )
  }

  if (!user) {
    return <Login onLogin={handleLogin} />
  }

  return (
    <AuthContext.Provider value={{ user, logout: handleLogout }}>
      <div className="min-h-screen bg-background flex">
        <Sidebar 
          currentView={currentView} 
          onNavigate={setCurrentView}
          isAdmin={user.role === 'ADMIN'}
        />
        
        <main className="flex-1 p-6 lg:p-8 ml-64">
          {currentView === 'dashboard' && (
            <Dashboard 
              onJobCreated={(jobId) => {
                setSelectedJobId(jobId)
                setCurrentView('job-detail')
              }}
            />
          )}
          
          {currentView === 'jobs' && (
            <JobsList onViewJob={handleViewJob} />
          )}
          
          {currentView === 'job-detail' && selectedJobId && (
            <JobDetail jobId={selectedJobId} onBack={handleBackToJobs} />
          )}
          
          {currentView === 'preset-designer' && user.role === 'ADMIN' && (
            <PresetDesigner />
          )}
        </main>
      </div>
    </AuthContext.Provider>
  )
}
