import { getAuthHeader } from './auth'

const API_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000'

interface ApiResponse<T> {
  data?: T
  error?: string
}

async function apiFetch<T>(
  endpoint: string,
  options: RequestInit = {}
): Promise<ApiResponse<T>> {
  try {
    const response = await fetch(`${API_URL}${endpoint}`, {
      ...options,
      headers: {
        'Content-Type': 'application/json',
        'Authorization': getAuthHeader(),
        ...options.headers,
      },
    })

    if (!response.ok) {
      const errorData = await response.json().catch(() => ({}))
      return { error: errorData.error || `Error ${response.status}` }
    }

    // Handle blob responses for exports
    if (response.headers.get('Content-Type')?.includes('application/')) {
      const contentType = response.headers.get('Content-Type') || ''
      if (contentType.includes('json') && !contentType.includes('octet')) {
        const data = await response.json()
        return { data }
      }
    }

    const data = await response.json()
    return { data }
  } catch (err) {
    return { error: 'Error de conexión' }
  }
}

// Auth
export async function testLogin(username: string, password: string): Promise<ApiResponse<{ role: string }>> {
  const credentials = btoa(`${username}:${password}`)
  const response = await fetch(`${API_URL}/api/presets`, {
    headers: {
      'Authorization': `Basic ${credentials}`,
    },
  })

  if (response.ok) {
    // Determine role based on username pattern
    let role = 'READER'
    if (username.toLowerCase().includes('admin')) role = 'ADMIN'
    else if (username.toLowerCase().includes('operator')) role = 'OPERATOR'
    
    return { data: { role } }
  }

  return { error: 'Credenciales inválidas' }
}

// Presets
export interface Preset {
  id: number
  presetKey: string
  name: string
  description?: string
  dataset: string
  isHardcoded: boolean
  isEnabled: boolean
  currentVersion: number
  inputs?: PresetInput[]
}

export interface PresetInput {
  name: string
  type: string
  required: boolean
  default?: any
}

export async function getPresets(): Promise<ApiResponse<Preset[]>> {
  return apiFetch<Preset[]>('/api/presets')
}

// Jobs
export interface Job {
  id: string
  presetKey: string
  presetName: string
  status: 'PENDING' | 'PROCESSING' | 'COMPLETED' | 'FAILED' | 'CANCELLED' | 'PAUSED_BY_SCHEDULE'
  totalItems: number
  processedItems: number
  failedItems: number
  progressPercent: number
  params?: Record<string, any>
  errorMessage?: string
  createdBy: string
  createdAt: string
  startedAt?: string
  completedAt?: string
}

export interface JobResults {
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
  results: Record<string, any>[]
}

export interface CreateJobRequest {
  presetKey: string
  cedulas: string[]
  params?: Record<string, any>
}

export async function createJob(request: CreateJobRequest): Promise<ApiResponse<{ id: string }>> {
  return apiFetch<{ id: string }>('/api/jobs', {
    method: 'POST',
    body: JSON.stringify(request),
  })
}

export async function getJobs(page = 1, pageSize = 20): Promise<ApiResponse<Job[]>> {
  return apiFetch<Job[]>(`/api/jobs?page=${page}&pageSize=${pageSize}`)
}

export async function getJob(jobId: string): Promise<ApiResponse<Job>> {
  return apiFetch<Job>(`/api/jobs/${jobId}`)
}

export async function getJobResults(jobId: string, page = 1, pageSize = 50): Promise<ApiResponse<JobResults>> {
  return apiFetch<JobResults>(`/api/jobs/${jobId}/results?page=${page}&pageSize=${pageSize}`)
}

export async function exportJob(jobId: string, format: 'CSV' | 'XLSX' | 'JSON'): Promise<Blob | null> {
  try {
    const response = await fetch(`${API_URL}/api/jobs/${jobId}/export`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': getAuthHeader(),
      },
      body: JSON.stringify({ format }),
    })

    if (response.ok) {
      return await response.blob()
    }
    return null
  } catch {
    return null
  }
}

export async function cancelJob(jobId: string): Promise<ApiResponse<{ message: string }>> {
  return apiFetch<{ message: string }>(`/api/jobs/${jobId}/cancel`, {
    method: 'POST',
  })
}


export interface ParseIdsResponse {
  headers: string[]
  suggestedColumn: string
  selectedColumn: string
  sampleRows: Record<string, string>[]
  ids: string[]
  totalRows: number
}

export async function parseIdsFile(file: File, selectedColumn?: string): Promise<ApiResponse<ParseIdsResponse>> {
  try {
    const formData = new FormData()
    formData.append('file', file)
    if (selectedColumn) formData.append('selectedColumn', selectedColumn)

    const response = await fetch(`${API_URL}/api/ids/parse`, {
      method: 'POST',
      headers: {
        'Authorization': getAuthHeader(),
      },
      body: formData,
    })

    if (!response.ok) {
      const errorData = await response.json().catch(() => ({}))
      return { error: errorData.error || `Error ${response.status}` }
    }

    const data = await response.json()
    return { data }
  } catch {
    return { error: 'Error de conexión' }
  }
}

// Admin
export interface SchemaResponse {
  dataset: string
  tables: TableSchema[]
  operators: AllowedOperator[]
}

export interface TableSchema {
  tableName: string
  columns: ColumnSchema[]
}

export interface ColumnSchema {
  columnName: string
  columnType?: string
  isFilterable: boolean
  isSortable: boolean
  isSelectable: boolean
  displayName?: string
}

export interface AllowedOperator {
  operatorKey: string
  operatorSql: string
  description?: string
  requiresValue: boolean
}

export async function getSchema(dataset: string): Promise<ApiResponse<SchemaResponse>> {
  return apiFetch<SchemaResponse>(`/api/admin/schema/${dataset}`)
}

export async function refreshSchema(
  dataset: string,
  body?: { includeTables?: string[]; excludeTables?: string[] }
): Promise<ApiResponse<{ message: string; tablesCount: number }>> {
  return apiFetch<{ message: string; tablesCount: number }>(`/api/admin/schema/${dataset}/refresh`, {
    method: 'POST',
    body: JSON.stringify(body ?? {}),
  })
}

export async function getDefaultSchema(): Promise<ApiResponse<SchemaResponse>> {
  return apiFetch<SchemaResponse>('/api/admin/schema')
}

export async function getDatasets(): Promise<ApiResponse<{ key: string; name: string }[]>> {
  return apiFetch<{ key: string; name: string }[]>('/api/admin/datasets')
}

export interface TestPresetResponse {
  success: boolean
  generatedSql?: string
  results?: Record<string, any>[]
  errorMessage?: string
  executionTimeMs: number
}

export async function testPreset(
  presetKey: string,
  cedula: string,
  params?: Record<string, any>
): Promise<ApiResponse<TestPresetResponse>> {
  return apiFetch<TestPresetResponse>(`/api/admin/presets/${presetKey}/test`, {
    method: 'POST',
    body: JSON.stringify({ cedula, params }),
  })
}

// Preset Versions
export interface PresetVersionResponse {
  id: number
  presetId: number
  version: number
  astJson: string
  isActive: boolean
  createdBy: string
  createdAt: string
}

export async function getPresetVersions(presetKey: string): Promise<ApiResponse<PresetVersionResponse[]>> {
  return apiFetch<PresetVersionResponse[]>(`/api/admin/presets/${presetKey}/versions`)
}

export async function createPreset(request: {
  presetKey: string
  name: string
  description?: string
  dataset: string
  ast: any
}): Promise<ApiResponse<{ id: number; presetKey: string }>> {
  return apiFetch<{ id: number; presetKey: string }>('/api/admin/presets', {
    method: 'POST',
    body: JSON.stringify(request),
  })
}

export async function createPresetVersion(
  presetId: number,
  ast: any
): Promise<ApiResponse<PresetVersionResponse>> {
  return apiFetch<PresetVersionResponse>(`/api/admin/presets/${presetId}/versions`, {
    method: 'POST',
    body: JSON.stringify({ ast }),
  })
}

export async function activatePresetVersion(
  presetId: number,
  versionId: number
): Promise<ApiResponse<{ message: string }>> {
  return apiFetch<{ message: string }>(`/api/admin/presets/${presetId}/versions/${versionId}/activate`, {
    method: 'POST',
  })
}

export async function compileAstToSql(ast: any, dataset?: string): Promise<ApiResponse<{ sql: string; params?: Record<string, any> }>> {
  return apiFetch<{ sql: string; params?: Record<string, any> }>('/api/admin/presets/compile', {
    method: 'POST',
    body: JSON.stringify({ ast, dataset }),
  })
}

export async function testAst(
  ast: any,
  cedula: string
): Promise<ApiResponse<TestPresetResponse>> {
  return apiFetch<TestPresetResponse>('/api/admin/test-ast', {
    method: 'POST',
    body: JSON.stringify({ ast, cedula }),
  })
}
