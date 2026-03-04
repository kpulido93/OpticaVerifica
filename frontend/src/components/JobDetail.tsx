'use client'

import { useState, useEffect, useCallback } from 'react'
import { toast } from 'sonner'
import { 
  ArrowLeft, RefreshCw, Download, XCircle, 
  Clock, CheckCircle, Loader, FileJson, FileSpreadsheet, FileText 
} from 'lucide-react'
import { getJob, getJobResults, exportJob, cancelJob, Job, JobResults } from '@/lib/api'
import { formatDate, getStatusColor, getStatusLabel, formatNumber, downloadBlob } from '@/lib/utils'
import Card from './ui/Card'
import Button from './ui/Button'

interface JobDetailProps {
  jobId: string
  onBack: () => void
}

export default function JobDetail({ jobId, onBack }: JobDetailProps) {
  const [job, setJob] = useState<Job | null>(null)
  const [results, setResults] = useState<JobResults | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [isExporting, setIsExporting] = useState(false)
  const [page, setPage] = useState(1)

  const loadJob = useCallback(async () => {
    const { data, error } = await getJob(jobId)
    if (error) {
      toast.error('Error cargando job')
    } else if (data) {
      setJob(data)
    }
  }, [jobId])

  const loadResults = useCallback(async () => {
    const { data, error } = await getJobResults(jobId, page, 50)
    if (error) {
      toast.error('Error cargando resultados')
    } else if (data) {
      setResults(data)
    }
  }, [jobId, page])

  useEffect(() => {
    const loadAll = async () => {
      setIsLoading(true)
      await loadJob()
      await loadResults()
      setIsLoading(false)
    }
    loadAll()
  }, [loadJob, loadResults])

  // Auto-refresh while processing
  useEffect(() => {
    if (!job || (job.status !== 'PROCESSING' && job.status !== 'PENDING')) return

    const interval = setInterval(() => {
      loadJob()
      loadResults()
    }, 3000)

    return () => clearInterval(interval)
  }, [job, loadJob, loadResults])

  const handleExport = async (format: 'CSV' | 'XLSX' | 'JSON') => {
    setIsExporting(true)
    const blob = await exportJob(jobId, format)
    if (blob) {
      const ext = format.toLowerCase()
      downloadBlob(blob, `job_${jobId.slice(0, 8)}.${ext}`)
      toast.success(`Exportado como ${format}`)
    } else {
      toast.error('Error exportando')
    }
    setIsExporting(false)
  }

  const handleCancel = async () => {
    const { error } = await cancelJob(jobId)
    if (error) {
      toast.error(error)
    } else {
      toast.success('Job cancelado')
      loadJob()
    }
  }

  if (isLoading || !job) {
    return (
      <div className="animate-fade-in">
        <div className="flex items-center gap-4 mb-8">
          <Button variant="ghost" onClick={onBack} data-testid="back-btn">
            <ArrowLeft className="w-5 h-5" />
          </Button>
          <div className="animate-pulse">
            <div className="h-8 w-48 bg-surface rounded mb-2" />
            <div className="h-4 w-32 bg-surface rounded" />
          </div>
        </div>
      </div>
    )
  }

  // Get all unique columns from results
  const columns = results?.results.length 
    ? Object.keys(results.results[0]).filter(k => k !== 'cedula')
    : []

  return (
    <div className="animate-fade-in">
      {/* Header */}
      <div className="flex items-center justify-between mb-8">
        <div className="flex items-center gap-4">
          <Button variant="ghost" onClick={onBack} data-testid="back-btn">
            <ArrowLeft className="w-5 h-5" />
          </Button>
          <div>
            <h1 className="text-2xl font-bold font-heading">{job.presetName}</h1>
            <p className="text-sm text-zinc-500 font-mono">{job.id}</p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          <Button variant="secondary" onClick={loadJob} data-testid="refresh-job-btn">
            <RefreshCw className="w-4 h-4" />
          </Button>
          {(job.status === 'PENDING' || job.status === 'PROCESSING') && (
            <Button variant="destructive" onClick={handleCancel} data-testid="cancel-job-btn">
              <XCircle className="w-4 h-4 mr-2" />
              Cancelar
            </Button>
          )}
        </div>
      </div>

      {/* Status Card */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4 mb-6">
        <Card className="text-center" data-testid="status-card">
          <span className={`inline-flex items-center gap-2 px-3 py-1.5 rounded-full text-sm font-medium border ${getStatusColor(job.status)}`}>
            {job.status === 'PROCESSING' ? <Loader className="w-4 h-4 animate-spin" /> : 
             job.status === 'COMPLETED' ? <CheckCircle className="w-4 h-4" /> :
             <Clock className="w-4 h-4" />}
            {getStatusLabel(job.status)}
          </span>
          <p className="text-xs text-zinc-500 mt-2">Estado</p>
        </Card>
        <Card className="text-center" data-testid="progress-card">
          <p className="text-2xl font-bold text-primary">{job.progressPercent}%</p>
          <p className="text-xs text-zinc-500 mt-1">Progreso</p>
          <div className="h-1.5 bg-zinc-800 rounded-full mt-2 overflow-hidden">
            <div 
              className="h-full bg-primary rounded-full transition-all duration-500"
              style={{ width: `${job.progressPercent}%` }}
            />
          </div>
        </Card>
        <Card className="text-center" data-testid="items-card">
          <p className="text-2xl font-bold text-white">
            {formatNumber(job.processedItems)} 
            <span className="text-zinc-500 text-lg"> / {formatNumber(job.totalItems)}</span>
          </p>
          <p className="text-xs text-zinc-500 mt-1">Items Procesados</p>
        </Card>
        <Card className="text-center" data-testid="errors-card">
          <p className={`text-2xl font-bold ${job.failedItems > 0 ? 'text-destructive' : 'text-success'}`}>
            {formatNumber(job.failedItems)}
          </p>
          <p className="text-xs text-zinc-500 mt-1">Errores</p>
        </Card>
      </div>

      {/* Timestamps */}
      <Card className="mb-6" data-testid="timestamps-card">
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4 text-sm">
          <div>
            <span className="text-zinc-500">Creado:</span>
            <span className="ml-2 text-white">{formatDate(job.createdAt)}</span>
          </div>
          {job.startedAt && (
            <div>
              <span className="text-zinc-500">Iniciado:</span>
              <span className="ml-2 text-white">{formatDate(job.startedAt)}</span>
            </div>
          )}
          {job.completedAt && (
            <div>
              <span className="text-zinc-500">Completado:</span>
              <span className="ml-2 text-white">{formatDate(job.completedAt)}</span>
            </div>
          )}
        </div>
        {job.errorMessage && (
          <div className="mt-4 p-3 bg-destructive/10 border border-destructive/20 rounded-lg">
            <p className="text-destructive text-sm">{job.errorMessage}</p>
          </div>
        )}
      </Card>

      {/* Export Buttons */}
      {job.status === 'COMPLETED' && results && results.totalCount > 0 && (
        <Card className="mb-6" data-testid="export-card">
          <h3 className="font-semibold mb-4 flex items-center gap-2">
            <Download className="w-5 h-5 text-accent" />
            Exportar Resultados
          </h3>
          <div className="flex gap-3">
            <Button 
              variant="secondary" 
              onClick={() => handleExport('CSV')}
              isLoading={isExporting}
              data-testid="export-csv-btn"
            >
              <FileText className="w-4 h-4 mr-2" />
              CSV
            </Button>
            <Button 
              variant="secondary" 
              onClick={() => handleExport('XLSX')}
              isLoading={isExporting}
              data-testid="export-xlsx-btn"
            >
              <FileSpreadsheet className="w-4 h-4 mr-2" />
              Excel
            </Button>
            <Button 
              variant="secondary" 
              onClick={() => handleExport('JSON')}
              isLoading={isExporting}
              data-testid="export-json-btn"
            >
              <FileJson className="w-4 h-4 mr-2" />
              JSON
            </Button>
          </div>
        </Card>
      )}

      {/* Results Table */}
      <Card data-testid="results-card">
        <div className="flex items-center justify-between mb-4">
          <h3 className="font-semibold">
            Resultados 
            <span className="text-zinc-500 font-normal ml-2">
              ({formatNumber(results?.totalCount || 0)} registros)
            </span>
          </h3>
        </div>

        {!results || results.results.length === 0 ? (
          <div className="text-center py-12 text-zinc-500">
            {job.status === 'PROCESSING' || job.status === 'PENDING' 
              ? 'Procesando...' 
              : 'Sin resultados'}
          </div>
        ) : (
          <>
            <div className="overflow-x-auto">
              <table className="w-full text-left text-sm" data-testid="results-table">
                <thead>
                  <tr className="border-b border-border">
                    <th className="pb-3 pr-4 text-xs font-medium text-zinc-500 uppercase tracking-wider sticky left-0 bg-surface">
                      Cédula
                    </th>
                    {columns.slice(0, 8).map((col) => (
                      <th key={col} className="pb-3 pr-4 text-xs font-medium text-zinc-500 uppercase tracking-wider whitespace-nowrap">
                        {col}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody className="divide-y divide-border font-mono">
                  {results.results.map((row, idx) => (
                    <tr key={idx} className="hover:bg-surface-highlight/50">
                      <td className="py-3 pr-4 text-primary sticky left-0 bg-surface">
                        {row.cedula}
                      </td>
                      {columns.slice(0, 8).map((col) => (
                        <td key={col} className="py-3 pr-4 text-zinc-300 whitespace-nowrap">
                          {typeof row[col] === 'object' 
                            ? JSON.stringify(row[col]).slice(0, 50) + '...'
                            : String(row[col] ?? '-')}
                        </td>
                      ))}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {/* Pagination */}
            {results.totalPages > 1 && (
              <div className="flex items-center justify-between mt-4 pt-4 border-t border-border">
                <Button
                  variant="secondary"
                  size="sm"
                  onClick={() => setPage(p => Math.max(1, p - 1))}
                  disabled={page === 1}
                  data-testid="results-prev-btn"
                >
                  Anterior
                </Button>
                <span className="text-sm text-zinc-400">
                  Página {page} de {results.totalPages}
                </span>
                <Button
                  variant="secondary"
                  size="sm"
                  onClick={() => setPage(p => p + 1)}
                  disabled={page >= results.totalPages}
                  data-testid="results-next-btn"
                >
                  Siguiente
                </Button>
              </div>
            )}
          </>
        )}
      </Card>
    </div>
  )
}
