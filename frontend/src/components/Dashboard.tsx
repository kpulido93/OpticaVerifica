'use client'

import { useState, useEffect, useCallback, useRef } from 'react'
import { toast } from 'sonner'
import { Upload, FileText, Play, RefreshCw, Zap } from 'lucide-react'
import { getPresets, createJob, parseIdsFile, Preset } from '@/lib/api'
import { parseCedulas } from '@/lib/utils'
import Button from './ui/Button'
import Card from './ui/Card'

interface DashboardProps {
  onJobCreated: (jobId: string) => void
}

export default function Dashboard({ onJobCreated }: DashboardProps) {
  const [presets, setPresets] = useState<Preset[]>([])
  const [selectedPreset, setSelectedPreset] = useState<string>('')
  const [cedulasText, setCedulasText] = useState('')
  const [isLoading, setIsLoading] = useState(false)
  const [isLoadingPresets, setIsLoadingPresets] = useState(true)
  const [isParsingFile, setIsParsingFile] = useState(false)
  const [params, setParams] = useState<Record<string, any>>({})

  const [uploadedFile, setUploadedFile] = useState<File | null>(null)
  const [fileHeaders, setFileHeaders] = useState<string[]>([])
  const [selectedFileColumn, setSelectedFileColumn] = useState('')
  const [fileIds, setFileIds] = useState<string[]>([])
  const [fileSampleRows, setFileSampleRows] = useState<Record<string, string>[]>([])
  const fileInputRef = useRef<HTMLInputElement>(null)

  const loadPresets = useCallback(async () => {
    setIsLoadingPresets(true)
    const { data, error } = await getPresets()
    if (error) {
      toast.error('Error cargando presets')
    } else if (data) {
      setPresets(data)
      if (data.length > 0 && !selectedPreset) {
        setSelectedPreset(data[0].presetKey)
      }
    }
    setIsLoadingPresets(false)
  }, [selectedPreset])

  useEffect(() => {
    loadPresets()
  }, [loadPresets])

  const selectedPresetData = presets.find(p => p.presetKey === selectedPreset)
  const manualIds = parseCedulas(cedulasText)
  const finalCedulas = Array.from(new Set([...fileIds, ...manualIds]))

  const parseFile = async (file: File, column?: string) => {
    setIsParsingFile(true)
    const { data, error } = await parseIdsFile(file, column)

    if (error || !data) {
      toast.error(error || 'No se pudo procesar el archivo')
      setIsParsingFile(false)
      return
    }

    setFileHeaders(data.headers)
    setSelectedFileColumn(data.selectedColumn)
    setFileIds(data.ids)
    setFileSampleRows(data.sampleRows)

    toast.success(`${data.ids.length} cédulas cargadas desde archivo`)
    setIsParsingFile(false)
  }

  const handleFileUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (!file) return

    setUploadedFile(file)
    await parseFile(file)
    e.target.value = ''
  }

  const handleColumnChange = async (column: string) => {
    setSelectedFileColumn(column)
    if (!uploadedFile) return
    await parseFile(uploadedFile, column)
  }

  const clearFileData = () => {
    setUploadedFile(null)
    setFileHeaders([])
    setSelectedFileColumn('')
    setFileIds([])
    setFileSampleRows([])
  }

  const handleSubmit = async () => {
    if (finalCedulas.length === 0) {
      toast.error('Ingresa al menos una cédula (archivo o manual)')
      return
    }

    if (!selectedPreset) {
      toast.error('Selecciona un preset')
      return
    }

    setIsLoading(true)

    const { data, error } = await createJob({
      presetKey: selectedPreset,
      cedulas: finalCedulas,
      params: Object.keys(params).length > 0 ? params : undefined,
    })

    if (error) {
      toast.error(error)
    } else if (data) {
      toast.success(`Job creado con ${finalCedulas.length} cédulas`)
      setCedulasText('')
      setParams({})
      clearFileData()
      onJobCreated(data.id)
    }

    setIsLoading(false)
  }

  const handleParamChange = (name: string, value: any) => {
    setParams(prev => ({ ...prev, [name]: value }))
  }

  return (
    <div className="animate-fade-in">
      <div className="mb-8">
        <h1 className="text-3xl font-bold font-heading mb-2">Dashboard</h1>
        <p className="text-zinc-400">Ejecuta consultas masivas por cédula</p>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
        {/* Main Input Area */}
        <Card className="lg:col-span-8" data-testid="cedulas-input-card">
          <div className="flex items-center justify-between mb-4">
            <h2 className="text-lg font-semibold flex items-center gap-2">
              <FileText className="w-5 h-5 text-primary" />
              Cédulas a Consultar
            </h2>
            <input
              ref={fileInputRef}
              type="file"
              accept=".csv,.xlsx"
              onChange={handleFileUpload}
              className="sr-only"
              data-testid="file-upload-input"
            />
            <Button
              variant="secondary"
              size="sm"
              type="button"
              onClick={() => fileInputRef.current?.click()}
              data-testid="upload-file-btn"
              isLoading={isParsingFile}
            >
              <Upload className="w-4 h-4 mr-2" />
              Cargar CSV/XLSX
            </Button>
          </div>

          {uploadedFile && (
            <div className="mb-4 p-3 rounded-lg border border-zinc-700 bg-zinc-900/50 space-y-3" data-testid="file-parse-card">
              <div className="flex items-center justify-between">
                <span className="text-sm text-zinc-300">Archivo: <strong>{uploadedFile.name}</strong></span>
                <button onClick={clearFileData} className="text-xs text-zinc-400 hover:text-white">Quitar archivo</button>
              </div>

              {fileHeaders.length > 0 && (
                <div>
                  <label className="block text-xs text-zinc-400 mb-1">Columna de Cédula</label>
                  <select
                    value={selectedFileColumn}
                    onChange={(e) => handleColumnChange(e.target.value)}
                    className="w-full h-9 px-3 bg-zinc-950 border border-zinc-700 rounded-md text-white text-sm"
                    data-testid="cedula-column-select"
                  >
                    {fileHeaders.map((header) => (
                      <option key={header} value={header}>{header}</option>
                    ))}
                  </select>
                </div>
              )}

              {fileSampleRows.length > 0 && (
                <div className="text-xs text-zinc-400">
                  <p className="mb-1">Muestra:</p>
                  <pre className="p-2 bg-zinc-950 rounded border border-zinc-800 overflow-x-auto max-h-28">{JSON.stringify(fileSampleRows.slice(0, 3), null, 2)}</pre>
                </div>
              )}
            </div>
          )}

          <textarea
            value={cedulasText}
            onChange={(e) => setCedulasText(e.target.value)}
            placeholder="Alternativa manual: pega cédulas aquí (una por línea, coma o punto y coma)..."
            className="w-full h-64 p-4 bg-zinc-900 border border-zinc-700 rounded-lg text-white placeholder:text-zinc-500 font-mono text-sm resize-none focus:outline-none focus:ring-2 focus:ring-primary focus:border-transparent"
            data-testid="cedulas-textarea"
          />

          <div className="mt-3 flex items-center justify-between text-sm text-zinc-400">
            <span>
              Archivo: {fileIds.length} | Manual: {manualIds.length} | Total final: {finalCedulas.length}
            </span>
            <button
              onClick={() => setCedulasText('')}
              className="hover:text-white transition-colors"
              data-testid="clear-cedulas-btn"
            >
              Limpiar manual
            </button>
          </div>
        </Card>

        {/* Preset Selection */}
        <Card className="lg:col-span-4" data-testid="preset-selection-card">
          <div className="flex items-center justify-between mb-4">
            <h2 className="text-lg font-semibold flex items-center gap-2">
              <Zap className="w-5 h-5 text-accent" />
              Preset
            </h2>
            <button
              onClick={loadPresets}
              className="text-zinc-400 hover:text-white transition-colors"
              data-testid="refresh-presets-btn"
            >
              <RefreshCw className={`w-4 h-4 ${isLoadingPresets ? 'animate-spin' : ''}`} />
            </button>
          </div>

          <div className="space-y-3">
            {isLoadingPresets ? (
              <div className="text-center py-8 text-zinc-500">Cargando presets...</div>
            ) : (
              presets.map((preset) => (
                <button
                  key={preset.presetKey}
                  onClick={() => setSelectedPreset(preset.presetKey)}
                  data-testid={`preset-${preset.presetKey}`}
                  className={`w-full p-3 rounded-lg border text-left transition-all duration-200 ${
                    selectedPreset === preset.presetKey
                      ? 'border-primary bg-primary/10'
                      : 'border-border bg-surface hover:border-zinc-600'
                  }`}
                >
                  <p className="font-medium text-white">{preset.name}</p>
                  {preset.description && (
                    <p className="text-sm text-zinc-400 mt-1 line-clamp-2">
                      {preset.description}
                    </p>
                  )}
                </button>
              ))
            )}
          </div>

          {/* Preset Parameters */}
          {selectedPresetData?.inputs && selectedPresetData.inputs.length > 0 && (
            <div className="mt-4 pt-4 border-t border-border">
              <p className="text-sm font-medium text-zinc-300 mb-3">Parámetros</p>
              {selectedPresetData.inputs
                .filter(input => input.name !== 'cedula')
                .map((input) => (
                  <div key={input.name} className="mb-3">
                    <label className="block text-xs text-zinc-400 mb-1">
                      {input.name}
                      {input.required && <span className="text-destructive ml-1">*</span>}
                    </label>
                    <input
                      type={input.type === 'decimal' ? 'number' : 'text'}
                      step={input.type === 'decimal' ? '0.01' : undefined}
                      placeholder={input.default?.toString() || ''}
                      value={params[input.name] || ''}
                      onChange={(e) => handleParamChange(input.name, e.target.value)}
                      className="w-full h-9 px-3 bg-zinc-900 border border-zinc-700 rounded-md text-white text-sm focus:outline-none focus:ring-2 focus:ring-primary"
                      data-testid={`param-${input.name}`}
                    />
                  </div>
                ))}
            </div>
          )}

          <Button
            onClick={handleSubmit}
            className="w-full mt-4"
            isLoading={isLoading}
            disabled={finalCedulas.length === 0 || !selectedPreset}
            data-testid="execute-job-btn"
          >
            <Play className="w-4 h-4 mr-2" />
            Ejecutar Consulta
          </Button>
        </Card>
      </div>
    </div>
  )
}
