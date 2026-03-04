import { createContext, useContext } from 'react'

export interface User {
  username: string
  role: 'ADMIN' | 'OPERATOR' | 'READER'
  credentials: string // Base64 encoded
}

interface AuthContextType {
  user: User | null
  logout: () => void
}

export const AuthContext = createContext<AuthContextType>({
  user: null,
  logout: () => {},
})

export const useAuth = () => useContext(AuthContext)

export const getAuthHeader = (): string => {
  const stored = localStorage.getItem('optima_auth')
  if (!stored) return ''
  
  try {
    const user: User = JSON.parse(stored)
    return `Basic ${user.credentials}`
  } catch {
    return ''
  }
}
