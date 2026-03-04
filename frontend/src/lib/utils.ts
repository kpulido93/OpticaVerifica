import { clsx, type ClassValue } from 'clsx'
import { twMerge } from 'tailwind-merge'

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}

export function formatDate(dateString: string): string {
  const date = new Date(dateString)
  return new Intl.DateTimeFormat('es-DO', {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(date)
}

export function formatNumber(num: number): string {
  return new Intl.NumberFormat('es-DO').format(num)
}

export function getStatusColor(status: string): string {
  switch (status) {
    case 'COMPLETED':
      return 'text-success bg-success/10 border-success/20'
    case 'PROCESSING':
      return 'text-primary bg-primary/10 border-primary/20'
    case 'PENDING':
      return 'text-warning bg-warning/10 border-warning/20'
    case 'FAILED':
    case 'CANCELLED':
      return 'text-destructive bg-destructive/10 border-destructive/20'
    case 'PAUSED_BY_SCHEDULE':
      return 'text-accent bg-accent/10 border-accent/20'
    default:
      return 'text-zinc-400 bg-zinc-400/10 border-zinc-400/20'
  }
}

export function getStatusLabel(status: string): string {
  switch (status) {
    case 'COMPLETED': return 'Completado'
    case 'PROCESSING': return 'Procesando'
    case 'PENDING': return 'Pendiente'
    case 'FAILED': return 'Fallido'
    case 'CANCELLED': return 'Cancelado'
    case 'PAUSED_BY_SCHEDULE': return 'Pausado'
    default: return status
  }
}

export function parseCedulas(text: string): string[] {
  return text
    .split(/[\n,;]+/)
    .map(c => c.trim())
    .filter(c => c.length > 0)
    .filter((c, i, arr) => arr.indexOf(c) === i) // Dedupe
}

export function downloadBlob(blob: Blob, filename: string) {
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = filename
  document.body.appendChild(a)
  a.click()
  document.body.removeChild(a)
  URL.revokeObjectURL(url)
}
