'use client'

import { useState } from 'react'
import { toast } from 'sonner'
import { Lock, User as UserIcon, Eye, EyeOff, ChevronRight } from 'lucide-react'
import { testLogin } from '@/lib/api'
import { User } from '@/lib/auth'
import Button from './ui/Button'
import Input from './ui/Input'

interface LoginProps {
  onLogin: (user: User) => void
}

export default function Login({ onLogin }: LoginProps) {
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [showPassword, setShowPassword] = useState(false)
  const [isLoading, setIsLoading] = useState(false)

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    
    if (!username || !password) {
      toast.error('Por favor completa todos los campos')
      return
    }

    setIsLoading(true)

    // For demo purposes, accept predefined credentials
    const validUsers: Record<string, { password: string; role: 'ADMIN' | 'OPERATOR' | 'READER' }> = {
      'admin': { password: 'admin123', role: 'ADMIN' },
      'operator': { password: 'operator123', role: 'OPERATOR' },
      'reader': { password: 'reader123', role: 'READER' },
    }

    const userConfig = validUsers[username.toLowerCase()]
    
    if (userConfig && userConfig.password === password) {
      const credentials = btoa(`${username}:${password}`)
      onLogin({
        username,
        role: userConfig.role,
        credentials,
      })
    } else {
      // Try API authentication
      const { data, error } = await testLogin(username, password)
      if (error) {
        toast.error(error)
      } else if (data) {
        const credentials = btoa(`${username}:${password}`)
        onLogin({
          username,
          role: data.role as 'ADMIN' | 'OPERATOR' | 'READER',
          credentials,
        })
      }
    }

    setIsLoading(false)
  }

  return (
    <div className="min-h-screen flex">
      {/* Left Panel - Image */}
      <div 
        className="hidden lg:flex lg:w-1/2 relative bg-cover bg-center"
        style={{
          backgroundImage: 'url(https://images.unsplash.com/photo-1760112783543-737c7721a824?auto=format&fit=crop&w=1920&q=80)',
        }}
      >
        <div className="absolute inset-0 bg-gradient-to-r from-background via-background/80 to-transparent" />
        <div className="relative z-10 flex flex-col justify-center p-12">
          <h1 className="text-5xl font-bold font-heading text-white mb-4">
            Optima <span className="text-primary">Verifica</span>
          </h1>
          <p className="text-xl text-zinc-300 max-w-md">
            Plataforma de verificación de datos masiva con consultas preconfiguradas y exportación flexible.
          </p>
        </div>
      </div>

      {/* Right Panel - Form */}
      <div className="w-full lg:w-1/2 flex items-center justify-center p-8">
        <div className="w-full max-w-md">
          <div className="lg:hidden mb-8 text-center">
            <h1 className="text-4xl font-bold font-heading text-white mb-2">
              Optima <span className="text-primary">Verifica</span>
            </h1>
          </div>

          <div className="bg-surface border border-border rounded-xl p-8 shadow-xl">
            <h2 className="text-2xl font-semibold font-heading mb-2">Iniciar Sesión</h2>
            <p className="text-zinc-400 mb-6">Ingresa tus credenciales para continuar</p>

            <form onSubmit={handleSubmit} className="space-y-5">
              <Input
                label="Usuario"
                type="text"
                value={username}
                onChange={(e) => setUsername(e.target.value)}
                placeholder="Ingresa tu usuario"
                icon={<UserIcon className="w-5 h-5" />}
                data-testid="login-username-input"
              />

              <Input
                label="Contraseña"
                type={showPassword ? 'text' : 'password'}
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="Ingresa tu contraseña"
                icon={<Lock className="w-5 h-5" />}
                rightIcon={
                  <button
                    type="button"
                    onClick={() => setShowPassword(!showPassword)}
                    className="text-zinc-400 hover:text-white transition-colors"
                    data-testid="toggle-password-btn"
                  >
                    {showPassword ? <EyeOff className="w-5 h-5" /> : <Eye className="w-5 h-5" />}
                  </button>
                }
                data-testid="login-password-input"
              />

              <Button
                type="submit"
                className="w-full"
                isLoading={isLoading}
                data-testid="login-submit-btn"
              >
                Ingresar
                <ChevronRight className="w-5 h-5 ml-2" />
              </Button>
            </form>

            <div className="mt-6 pt-6 border-t border-border">
              <p className="text-sm text-zinc-500 text-center">
                Demo: admin/admin123, operator/operator123, reader/reader123
              </p>
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}
