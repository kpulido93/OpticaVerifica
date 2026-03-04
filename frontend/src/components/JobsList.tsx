'use client'

import { useState, useEffect, useCallback } from 'react'
import { toast } from 'sonner'
import { RefreshCw, Eye, Clock, CheckCircle, XCircle, Pause, Loader } from 'lucide-react'
import { getJobs, Job } from '@/lib/api'
import { formatDate, getStatusColor, getStatusLabel, formatNumber } from '@/lib/utils'
import Card from './ui/Card'
import Button from './ui/Button'

interface JobsListProps {
  onViewJob: (jobId: string) => void
}

export default function JobsList({ onViewJob }: JobsListProps) {
  const [jobs, setJobs] = useState<Job[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [page, setPage] = useState(1)

  const loadJobs = useCallback(async () => {
    setIsLoading(true)
    const { data, error } = await getJobs(page, 20)
    if (error) {
      toast.error('Error cargando jobs')
    } else if (data) {
      setJobs(data)
    }
    setIsLoading(false)
  }, [page])

  useEffect(() => {
    loadJobs()
  }, [loadJobs])

  // Auto-refresh for processing jobs
  useEffect(() => {
    const hasProcessing = jobs.some(j => j.status === 'PROCESSING' || j.status === 'PENDING')
    if (!hasProcessing) return

    const interval = setInterval(loadJobs, 5000)
    return () => clearInterval(interval)
  }, [jobs, loadJobs])

  const StatusIcon = ({ status }: { status: string }) => {
    switch (status) {
      case 'COMPLETED': return <CheckCircle className="w-4 h-4" />
      case 'PROCESSING': return <Loader className="w-4 h-4 animate-spin" />
      case 'PENDING': return <Clock className="w-4 h-4" />
      case 'FAILED':
      case 'CANCELLED': return <XCircle className="w-4 h-4" />
      case 'PAUSED_BY_SCHEDULE': return <Pause className="w-4 h-4" />
      default: return null
    }
  }

  return (
    <div className="animate-fade-in">
      <div className="flex items-center justify-between mb-8">
        <div>
          <h1 className="text-3xl font-bold font-heading mb-2">Historial de Jobs</h1>
          <p className="text-zinc-400">Consultas ejecutadas y sus resultados</p>
        </div>
        <Button 
          variant="secondary" 
          onClick={loadJobs}
          isLoading={isLoading}
          data-testid="refresh-jobs-btn"
        >
          <RefreshCw className="w-4 h-4 mr-2" />
          Actualizar
        </Button>
      </div>

      <Card data-testid="jobs-list-card">
        {isLoading && jobs.length === 0 ? (
          <div className="text-center py-12 text-zinc-500">
            <Loader className="w-8 h-8 mx-auto mb-4 animate-spin" />
            Cargando jobs...
          </div>
        ) : jobs.length === 0 ? (
          <div className="text-center py-12">
            <div 
              className="w-32 h-32 mx-auto mb-4 rounded-lg bg-cover bg-center opacity-50"
              style={{
                backgroundImage: 'url(https://images.unsplash.com/photo-1762281429401-3bd99b5e4e7d?auto=format&fit=crop&w=300&q=80)',
              }}
            />
            <p className="text-zinc-500">No hay jobs registrados</p>
            <p className="text-sm text-zinc-600 mt-1">Ejecuta tu primera consulta desde el Dashboard</p>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left" data-testid="jobs-table">
              <thead>
                <tr className="border-b border-border">
                  <th className="pb-3 text-xs font-medium text-zinc-500 uppercase tracking-wider">Preset</th>
                  <th className="pb-3 text-xs font-medium text-zinc-500 uppercase tracking-wider">Estado</th>
                  <th className="pb-3 text-xs font-medium text-zinc-500 uppercase tracking-wider">Progreso</th>
                  <th className="pb-3 text-xs font-medium text-zinc-500 uppercase tracking-wider">Items</th>
                  <th className="pb-3 text-xs font-medium text-zinc-500 uppercase tracking-wider">Creado</th>
                  <th className="pb-3 text-xs font-medium text-zinc-500 uppercase tracking-wider">Acción</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {jobs.map((job) => (
                  <tr key={job.id} className="group" data-testid={`job-row-${job.id}`}>
                    <td className="py-4">
                      <p className="font-medium text-white">{job.presetName}</p>
                      <p className="text-xs text-zinc-500 font-mono">{job.id.slice(0, 8)}...</p>
                    </td>
                    <td className="py-4">
                      <span className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium border ${getStatusColor(job.status)}`}>
                        <StatusIcon status={job.status} />
                        {getStatusLabel(job.status)}
                      </span>
                    </td>
                    <td className="py-4">
                      <div className="w-32">
                        <div className="flex items-center justify-between text-xs text-zinc-400 mb-1">
                          <span>{job.progressPercent}%</span>
                          {job.failedItems > 0 && (
                            <span className="text-destructive">{job.failedItems} errores</span>
                          )}
                        </div>
                        <div className="h-1.5 bg-zinc-800 rounded-full overflow-hidden">
                          <div 
                            className="h-full bg-primary rounded-full transition-all duration-300"
                            style={{ width: `${job.progressPercent}%` }}
                          />
                        </div>
                      </div>
                    </td>
                    <td className="py-4 text-zinc-300 font-mono text-sm">
                      {formatNumber(job.processedItems)} / {formatNumber(job.totalItems)}
                    </td>
                    <td className="py-4 text-zinc-400 text-sm">
                      {formatDate(job.createdAt)}
                    </td>
                    <td className="py-4">
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => onViewJob(job.id)}
                        data-testid={`view-job-${job.id}`}
                      >
                        <Eye className="w-4 h-4 mr-1" />
                        Ver
                      </Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {jobs.length > 0 && (
          <div className="flex items-center justify-between mt-4 pt-4 border-t border-border">
            <Button
              variant="secondary"
              size="sm"
              onClick={() => setPage(p => Math.max(1, p - 1))}
              disabled={page === 1}
              data-testid="prev-page-btn"
            >
              Anterior
            </Button>
            <span className="text-sm text-zinc-400">Página {page}</span>
            <Button
              variant="secondary"
              size="sm"
              onClick={() => setPage(p => p + 1)}
              disabled={jobs.length < 20}
              data-testid="next-page-btn"
            >
              Siguiente
            </Button>
          </div>
        )}
      </Card>
    </div>
  )
}
