'use client'

import { useState, useEffect, useCallback } from 'react'
import { toast } from 'sonner'
import { 
  Database, Table2, Columns3, Filter, ArrowUpDown, Play, Save,
  Plus, Trash2, GripVertical, ChevronRight, Code
} from 'lucide-react'
import { getSchema, getDatasets, testPreset, SchemaResponse, TableSchema, ColumnSchema, AllowedOperator } from '@/lib/api'
import Card from '../ui/Card'
import Button from '../ui/Button'

interface PresetAst {
  type: string
  select: AstColumn[]
  fromTable: string
  where?: AstFilterGroup
  orderBy?: AstOrderBy[]
  limit?: number
}

interface AstColumn {
  table: string
  column: string
  alias?: string
}

interface AstFilterGroup {
  logic: 'AND' | 'OR'
  filters: AstFilter[]
}

interface AstFilter {
  table: string
  column: string
  operator: string
  value?: any
  parameterName?: string
}

interface AstOrderBy {
  table: string
  column: string
  direction: 'ASC' | 'DESC'
}

export default function PresetDesigner() {
  const [datasets, setDatasets] = useState<{ key: string; name: string }[]>([])
  const [selectedDataset, setSelectedDataset] = useState<string>('')
  const [schema, setSchema] = useState<SchemaResponse | null>(null)
  const [isLoading, setIsLoading] = useState(true)

  // AST State
  const [presetName, setPresetName] = useState('')
  const [presetDescription, setPresetDescription] = useState('')
  const [selectedTable, setSelectedTable] = useState('')
  const [selectedColumns, setSelectedColumns] = useState<AstColumn[]>([])
  const [filters, setFilters] = useState<AstFilter[]>([])
  const [orderBy, setOrderBy] = useState<AstOrderBy[]>([])
  const [limit, setLimit] = useState<number>(100)

  // Test state
  const [testCedula, setTestCedula] = useState('')
  const [testResults, setTestResults] = useState<any>(null)
  const [isTesting, setIsTesting] = useState(false)
  const [generatedSql, setGeneratedSql] = useState('')

  const loadDatasets = useCallback(async () => {
    const { data } = await getDatasets()
    if (data) {
      setDatasets(data)
      if (data.length > 0) {
        setSelectedDataset(data[0].key)
      }
    }
  }, [])

  const loadSchema = useCallback(async () => {
    if (!selectedDataset) return
    setIsLoading(true)
    const { data, error } = await getSchema(selectedDataset)
    if (error) {
      toast.error('Error cargando esquema')
    } else if (data) {
      setSchema(data)
      if (data.tables.length > 0 && !selectedTable) {
        setSelectedTable(data.tables[0].tableName)
      }
    }
    setIsLoading(false)
  }, [selectedDataset, selectedTable])

  useEffect(() => {
    loadDatasets()
  }, [loadDatasets])

  useEffect(() => {
    loadSchema()
  }, [loadSchema])

  const currentTable = schema?.tables.find(t => t.tableName === selectedTable)

  const addColumn = (col: ColumnSchema) => {
    if (selectedColumns.some(c => c.column === col.columnName && c.table === selectedTable)) {
      return
    }
    setSelectedColumns([...selectedColumns, {
      table: selectedTable,
      column: col.columnName,
    }])
  }

  const removeColumn = (index: number) => {
    setSelectedColumns(selectedColumns.filter((_, i) => i !== index))
  }

  const addFilter = () => {
    if (!currentTable?.columns[0]) return
    setFilters([...filters, {
      table: selectedTable,
      column: currentTable.columns[0].columnName,
      operator: 'eq',
      parameterName: 'cedula',
    }])
  }

  const updateFilter = (index: number, updates: Partial<AstFilter>) => {
    setFilters(filters.map((f, i) => i === index ? { ...f, ...updates } : f))
  }

  const removeFilter = (index: number) => {
    setFilters(filters.filter((_, i) => i !== index))
  }

  const addOrderBy = () => {
    if (!currentTable?.columns[0]) return
    setOrderBy([...orderBy, {
      table: selectedTable,
      column: currentTable.columns[0].columnName,
      direction: 'ASC',
    }])
  }

  const updateOrderBy = (index: number, updates: Partial<AstOrderBy>) => {
    setOrderBy(orderBy.map((o, i) => i === index ? { ...o, ...updates } : o))
  }

  const removeOrderBy = (index: number) => {
    setOrderBy(orderBy.filter((_, i) => i !== index))
  }

  const buildAst = (): PresetAst => {
    return {
      type: 'CUSTOM',
      select: selectedColumns,
      fromTable: selectedTable,
      where: filters.length > 0 ? { logic: 'AND', filters } : undefined,
      orderBy: orderBy.length > 0 ? orderBy : undefined,
      limit,
    }
  }

  const generateSqlPreview = () => {
    const ast = buildAst()
    let sql = 'SELECT '
    
    if (ast.select.length === 0) {
      sql += '*'
    } else {
      sql += ast.select.map(c => `${c.table}.${c.column}${c.alias ? ` AS ${c.alias}` : ''}`).join(', ')
    }
    
    sql += `\nFROM ${ast.fromTable}`
    
    if (ast.where && ast.where.filters.length > 0) {
      const conditions = ast.where.filters.map(f => {
        const op = schema?.operators.find(o => o.operatorKey === f.operator)?.operatorSql || '='
        const val = f.parameterName ? `@${f.parameterName}` : `'${f.value}'`
        return `${f.table}.${f.column} ${op} ${val}`
      })
      sql += `\nWHERE ${conditions.join(`\n  ${ast.where.logic} `)}`
    }
    
    if (ast.orderBy && ast.orderBy.length > 0) {
      sql += `\nORDER BY ${ast.orderBy.map(o => `${o.table}.${o.column} ${o.direction}`).join(', ')}`
    }
    
    if (ast.limit) {
      sql += `\nLIMIT ${ast.limit}`
    }
    
    return sql
  }

  useEffect(() => {
    setGeneratedSql(generateSqlPreview())
  }, [selectedColumns, filters, orderBy, limit, selectedTable])

  const handleTest = async () => {
    if (!testCedula) {
      toast.error('Ingresa una cédula de prueba')
      return
    }

    setIsTesting(true)
    // Note: This would require creating a temporary preset first
    // For now, we'll just show the generated SQL
    toast.info('Vista previa del SQL generado')
    setTestResults({ sql: generatedSql })
    setIsTesting(false)
  }

  const handleSave = () => {
    if (!presetName) {
      toast.error('Ingresa un nombre para el preset')
      return
    }

    const ast = buildAst()
    console.log('Saving preset:', { name: presetName, description: presetDescription, ast })
    toast.success('Preset guardado (demo)')
  }

  if (isLoading) {
    return (
      <div className="animate-fade-in">
        <div className="mb-8">
          <h1 className="text-3xl font-bold font-heading mb-2">Diseñador de Presets</h1>
          <p className="text-zinc-400">Cargando esquema...</p>
        </div>
      </div>
    )
  }

  return (
    <div className="animate-fade-in">
      <div className="mb-8">
        <h1 className="text-3xl font-bold font-heading mb-2">Diseñador de Presets</h1>
        <p className="text-zinc-400">Crea consultas visuales sin escribir SQL</p>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
        {/* Left Panel - Schema Explorer */}
        <div className="lg:col-span-4 space-y-4">
          {/* Dataset Selector */}
          <Card data-testid="dataset-selector-card">
            <h3 className="font-semibold mb-3 flex items-center gap-2">
              <Database className="w-5 h-5 text-primary" />
              Dataset
            </h3>
            <select
              value={selectedDataset}
              onChange={(e) => setSelectedDataset(e.target.value)}
              className="w-full h-10 px-3 bg-zinc-900 border border-zinc-700 rounded-lg text-white"
              data-testid="dataset-select"
            >
              {datasets.map((ds) => (
                <option key={ds.key} value={ds.key}>{ds.name}</option>
              ))}
            </select>
          </Card>

          {/* Table Selector */}
          <Card data-testid="table-selector-card">
            <h3 className="font-semibold mb-3 flex items-center gap-2">
              <Table2 className="w-5 h-5 text-accent" />
              Tabla Principal
            </h3>
            <div className="space-y-2">
              {schema?.tables.map((table) => (
                <button
                  key={table.tableName}
                  onClick={() => setSelectedTable(table.tableName)}
                  data-testid={`table-${table.tableName}`}
                  className={`w-full p-2 rounded-lg text-left text-sm transition-all ${
                    selectedTable === table.tableName
                      ? 'bg-primary/20 border border-primary text-white'
                      : 'bg-surface-highlight border border-transparent text-zinc-400 hover:text-white'
                  }`}
                >
                  {table.tableName}
                </button>
              ))}
            </div>
          </Card>

          {/* Available Columns */}
          <Card data-testid="columns-card">
            <h3 className="font-semibold mb-3 flex items-center gap-2">
              <Columns3 className="w-5 h-5 text-success" />
              Columnas Disponibles
            </h3>
            <div className="space-y-1 max-h-64 overflow-y-auto">
              {currentTable?.columns.map((col) => (
                <button
                  key={col.columnName}
                  onClick={() => addColumn(col)}
                  data-testid={`col-${col.columnName}`}
                  className="w-full p-2 rounded-lg text-left text-sm bg-surface-highlight hover:bg-zinc-700 flex items-center justify-between group transition-colors"
                >
                  <span>
                    <span className="text-white">{col.displayName || col.columnName}</span>
                    <span className="text-zinc-500 text-xs ml-2">{col.columnType}</span>
                  </span>
                  <Plus className="w-4 h-4 text-zinc-500 group-hover:text-primary" />
                </button>
              ))}
            </div>
          </Card>
        </div>

        {/* Right Panel - Builder */}
        <div className="lg:col-span-8 space-y-4">
          {/* Preset Info */}
          <Card data-testid="preset-info-card">
            <h3 className="font-semibold mb-3">Información del Preset</h3>
            <div className="grid grid-cols-2 gap-4">
              <div>
                <label className="block text-sm text-zinc-400 mb-1">Nombre</label>
                <input
                  type="text"
                  value={presetName}
                  onChange={(e) => setPresetName(e.target.value)}
                  placeholder="mi_preset_custom"
                  className="w-full h-10 px-3 bg-zinc-900 border border-zinc-700 rounded-lg text-white"
                  data-testid="preset-name-input"
                />
              </div>
              <div>
                <label className="block text-sm text-zinc-400 mb-1">Límite</label>
                <input
                  type="number"
                  value={limit}
                  onChange={(e) => setLimit(parseInt(e.target.value) || 100)}
                  className="w-full h-10 px-3 bg-zinc-900 border border-zinc-700 rounded-lg text-white"
                  data-testid="limit-input"
                />
              </div>
            </div>
            <div className="mt-4">
              <label className="block text-sm text-zinc-400 mb-1">Descripción</label>
              <textarea
                value={presetDescription}
                onChange={(e) => setPresetDescription(e.target.value)}
                placeholder="Descripción del preset..."
                className="w-full h-20 p-3 bg-zinc-900 border border-zinc-700 rounded-lg text-white resize-none"
                data-testid="preset-description-input"
              />
            </div>
          </Card>

          {/* Selected Columns */}
          <Card data-testid="selected-columns-card">
            <h3 className="font-semibold mb-3 flex items-center gap-2">
              <ChevronRight className="w-5 h-5 text-primary" />
              Columnas Seleccionadas (SELECT)
            </h3>
            {selectedColumns.length === 0 ? (
              <p className="text-zinc-500 text-sm py-4 text-center">
                Haz clic en las columnas de la izquierda para agregarlas
              </p>
            ) : (
              <div className="space-y-2">
                {selectedColumns.map((col, idx) => (
                  <div
                    key={idx}
                    className="flex items-center justify-between p-2 bg-surface-highlight rounded-lg"
                    data-testid={`selected-col-${idx}`}
                  >
                    <div className="flex items-center gap-2">
                      <GripVertical className="w-4 h-4 text-zinc-500" />
                      <span className="text-white">{col.table}.{col.column}</span>
                    </div>
                    <button
                      onClick={() => removeColumn(idx)}
                      className="text-zinc-500 hover:text-destructive transition-colors"
                      data-testid={`remove-col-${idx}`}
                    >
                      <Trash2 className="w-4 h-4" />
                    </button>
                  </div>
                ))}
              </div>
            )}
          </Card>

          {/* Filters */}
          <Card data-testid="filters-card">
            <div className="flex items-center justify-between mb-3">
              <h3 className="font-semibold flex items-center gap-2">
                <Filter className="w-5 h-5 text-warning" />
                Filtros (WHERE)
              </h3>
              <Button variant="ghost" size="sm" onClick={addFilter} data-testid="add-filter-btn">
                <Plus className="w-4 h-4 mr-1" />
                Agregar
              </Button>
            </div>
            {filters.length === 0 ? (
              <p className="text-zinc-500 text-sm py-4 text-center">
                Sin filtros. El parámetro @cedula se agregará automáticamente.
              </p>
            ) : (
              <div className="space-y-2">
                {filters.map((filter, idx) => (
                  <div
                    key={idx}
                    className="flex items-center gap-2 p-2 bg-surface-highlight rounded-lg"
                    data-testid={`filter-${idx}`}
                  >
                    <select
                      value={filter.column}
                      onChange={(e) => updateFilter(idx, { column: e.target.value })}
                      className="h-8 px-2 bg-zinc-900 border border-zinc-700 rounded text-white text-sm"
                    >
                      {currentTable?.columns.map((c) => (
                        <option key={c.columnName} value={c.columnName}>
                          {c.columnName}
                        </option>
                      ))}
                    </select>
                    <select
                      value={filter.operator}
                      onChange={(e) => updateFilter(idx, { operator: e.target.value })}
                      className="h-8 px-2 bg-zinc-900 border border-zinc-700 rounded text-white text-sm"
                    >
                      {schema?.operators.map((op) => (
                        <option key={op.operatorKey} value={op.operatorKey}>
                          {op.description || op.operatorKey}
                        </option>
                      ))}
                    </select>
                    <input
                      type="text"
                      value={filter.parameterName || filter.value || ''}
                      onChange={(e) => updateFilter(idx, { 
                        parameterName: e.target.value.startsWith('@') ? e.target.value.slice(1) : undefined,
                        value: e.target.value.startsWith('@') ? undefined : e.target.value
                      })}
                      placeholder="@cedula o valor"
                      className="flex-1 h-8 px-2 bg-zinc-900 border border-zinc-700 rounded text-white text-sm"
                    />
                    <button
                      onClick={() => removeFilter(idx)}
                      className="text-zinc-500 hover:text-destructive"
                      data-testid={`remove-filter-${idx}`}
                    >
                      <Trash2 className="w-4 h-4" />
                    </button>
                  </div>
                ))}
              </div>
            )}
          </Card>

          {/* Order By */}
          <Card data-testid="orderby-card">
            <div className="flex items-center justify-between mb-3">
              <h3 className="font-semibold flex items-center gap-2">
                <ArrowUpDown className="w-5 h-5 text-accent" />
                Ordenamiento (ORDER BY)
              </h3>
              <Button variant="ghost" size="sm" onClick={addOrderBy} data-testid="add-orderby-btn">
                <Plus className="w-4 h-4 mr-1" />
                Agregar
              </Button>
            </div>
            {orderBy.length === 0 ? (
              <p className="text-zinc-500 text-sm py-4 text-center">Sin ordenamiento</p>
            ) : (
              <div className="space-y-2">
                {orderBy.map((order, idx) => (
                  <div
                    key={idx}
                    className="flex items-center gap-2 p-2 bg-surface-highlight rounded-lg"
                    data-testid={`orderby-${idx}`}
                  >
                    <select
                      value={order.column}
                      onChange={(e) => updateOrderBy(idx, { column: e.target.value })}
                      className="h-8 px-2 bg-zinc-900 border border-zinc-700 rounded text-white text-sm"
                    >
                      {currentTable?.columns.map((c) => (
                        <option key={c.columnName} value={c.columnName}>
                          {c.columnName}
                        </option>
                      ))}
                    </select>
                    <select
                      value={order.direction}
                      onChange={(e) => updateOrderBy(idx, { direction: e.target.value as 'ASC' | 'DESC' })}
                      className="h-8 px-2 bg-zinc-900 border border-zinc-700 rounded text-white text-sm"
                    >
                      <option value="ASC">Ascendente</option>
                      <option value="DESC">Descendente</option>
                    </select>
                    <button
                      onClick={() => removeOrderBy(idx)}
                      className="text-zinc-500 hover:text-destructive"
                      data-testid={`remove-orderby-${idx}`}
                    >
                      <Trash2 className="w-4 h-4" />
                    </button>
                  </div>
                ))}
              </div>
            )}
          </Card>

          {/* SQL Preview */}
          <Card data-testid="sql-preview-card">
            <h3 className="font-semibold mb-3 flex items-center gap-2">
              <Code className="w-5 h-5 text-primary" />
              Vista Previa SQL
            </h3>
            <pre className="p-4 bg-zinc-900 rounded-lg overflow-x-auto text-sm font-mono text-zinc-300">
              {generatedSql || '-- Selecciona columnas y filtros para generar SQL'}
            </pre>
          </Card>

          {/* Actions */}
          <div className="flex items-center gap-4">
            <div className="flex-1 flex items-center gap-2">
              <input
                type="text"
                value={testCedula}
                onChange={(e) => setTestCedula(e.target.value)}
                placeholder="Cédula de prueba"
                className="w-48 h-10 px-3 bg-zinc-900 border border-zinc-700 rounded-lg text-white"
                data-testid="test-cedula-input"
              />
              <Button variant="secondary" onClick={handleTest} isLoading={isTesting} data-testid="test-preset-btn">
                <Play className="w-4 h-4 mr-2" />
                Probar
              </Button>
            </div>
            <Button onClick={handleSave} data-testid="save-preset-btn">
              <Save className="w-4 h-4 mr-2" />
              Guardar Preset
            </Button>
          </div>

          {/* Test Results */}
          {testResults && (
            <Card data-testid="test-results-card">
              <h3 className="font-semibold mb-3">Resultado de Prueba</h3>
              <pre className="p-4 bg-zinc-900 rounded-lg overflow-x-auto text-sm font-mono text-zinc-300 max-h-64">
                {JSON.stringify(testResults, null, 2)}
              </pre>
            </Card>
          )}
        </div>
      </div>
    </div>
  )
}
