'use client'

import { useState, useEffect, useCallback, useRef } from 'react'
import { toast } from 'sonner'
import {
  DndContext,
  closestCenter,
  KeyboardSensor,
  PointerSensor,
  useSensor,
  useSensors,
  DragEndEvent,
  DragStartEvent,
  DragOverlay,
} from '@dnd-kit/core'
import {
  arrayMove,
  SortableContext,
  sortableKeyboardCoordinates,
  verticalListSortingStrategy,
  useSortable,
} from '@dnd-kit/sortable'
import { CSS } from '@dnd-kit/utilities'
import {
  Database, Table2, Columns3, Filter, ArrowUpDown, Play, Save,
  Plus, Trash2, GripVertical, ChevronRight, ChevronDown, Code,
  FolderTree, Edit2, Check, X, Copy, History, ToggleLeft, ToggleRight,
  Link2, Hash, Calendar, Type, CircleDot
} from 'lucide-react'
import {
  getSchema,
  getDatasets,
  SchemaResponse,
  TableSchema,
  ColumnSchema,
  createPreset,
  createPresetVersion,
  getPresetVersions,
  activatePresetVersion,
  compileAstToSql,
  testPreset,
} from '@/lib/api'
import Card from '../ui/Card'
import Button from '../ui/Button'

// ============================================
// TYPES - Normalized AST Format
// ============================================

interface NormalizedAst {
  dataset: string
  select: SelectColumn[]
  joins: JoinClause[]
  where: FilterGroup | null
  orderBy: OrderByClause[]
  limit: number
}

interface SelectColumn {
  id: string
  expr: string // "table.column"
  alias: string
  sourceTable: string
  sourceColumn: string
  dataType: DataType
}

interface JoinClause {
  id: string
  joinType: 'INNER' | 'LEFT' | 'RIGHT'
  table: string
  onLeft: string
  onRight: string
}

interface FilterGroup {
  id: string
  op: 'and' | 'or'
  rules: (FilterRule | FilterGroup)[]
}

interface FilterRule {
  id: string
  field: string // "table.column"
  operator: string
  value: any
  dataType: DataType
}

interface OrderByClause {
  id: string
  field: string
  dir: 'asc' | 'desc'
}

type DataType = 'string' | 'number' | 'date' | 'boolean' | 'unknown'

interface PresetVersion {
  id: number
  version: number
  astJson: string
  isActive: boolean
  createdBy: string
  createdAt: string
}

// Operators by data type
const OPERATORS_BY_TYPE: Record<DataType, { key: string; label: string; sql: string; needsValue: boolean }[]> = {
  number: [
    { key: 'eq', label: '=', sql: '=', needsValue: true },
    { key: 'neq', label: '≠', sql: '!=', needsValue: true },
    { key: 'gt', label: '>', sql: '>', needsValue: true },
    { key: 'gte', label: '≥', sql: '>=', needsValue: true },
    { key: 'lt', label: '<', sql: '<', needsValue: true },
    { key: 'lte', label: '≤', sql: '<=', needsValue: true },
    { key: 'between', label: 'BETWEEN', sql: 'BETWEEN', needsValue: true },
    { key: 'in', label: 'IN', sql: 'IN', needsValue: true },
    { key: 'in_ids', label: 'IN @ids', sql: 'IN', needsValue: false },
  ],
  date: [
    { key: 'eq', label: '=', sql: '=', needsValue: true },
    { key: 'neq', label: '≠', sql: '!=', needsValue: true },
    { key: 'gt', label: '>', sql: '>', needsValue: true },
    { key: 'gte', label: '≥', sql: '>=', needsValue: true },
    { key: 'lt', label: '<', sql: '<', needsValue: true },
    { key: 'lte', label: '≤', sql: '<=', needsValue: true },
    { key: 'between', label: 'BETWEEN', sql: 'BETWEEN', needsValue: true },
    { key: 'in', label: 'IN', sql: 'IN', needsValue: true },
  ],
  string: [
    { key: 'eq', label: '=', sql: '=', needsValue: true },
    { key: 'neq', label: '≠', sql: '!=', needsValue: true },
    { key: 'like', label: 'LIKE', sql: 'LIKE', needsValue: true },
    { key: 'in', label: 'IN', sql: 'IN', needsValue: true },
    { key: 'in_ids', label: 'IN @ids', sql: 'IN', needsValue: false },
    { key: 'is_null', label: 'IS NULL', sql: 'IS NULL', needsValue: false },
    { key: 'is_not_null', label: 'IS NOT NULL', sql: 'IS NOT NULL', needsValue: false },
  ],
  boolean: [
    { key: 'eq', label: '=', sql: '=', needsValue: true },
    { key: 'is_null', label: 'IS NULL', sql: 'IS NULL', needsValue: false },
    { key: 'is_not_null', label: 'IS NOT NULL', sql: 'IS NOT NULL', needsValue: false },
  ],
  unknown: [
    { key: 'eq', label: '=', sql: '=', needsValue: true },
    { key: 'neq', label: '≠', sql: '!=', needsValue: true },
    { key: 'is_null', label: 'IS NULL', sql: 'IS NULL', needsValue: false },
    { key: 'is_not_null', label: 'IS NOT NULL', sql: 'IS NOT NULL', needsValue: false },
  ],
}

// Map MySQL types to our DataType
function getDataType(mysqlType: string | undefined): DataType {
  if (!mysqlType) return 'unknown'
  const t = mysqlType.toUpperCase()
  if (t.includes('INT') || t.includes('DECIMAL') || t.includes('FLOAT') || t.includes('DOUBLE') || t.includes('NUMERIC')) {
    return 'number'
  }
  if (t.includes('DATE') || t.includes('TIME') || t.includes('YEAR')) {
    return 'date'
  }
  if (t.includes('BIT') || t.includes('BOOL')) {
    return 'boolean'
  }
  if (t.includes('CHAR') || t.includes('TEXT') || t.includes('BLOB') || t.includes('ENUM')) {
    return 'string'
  }
  return 'unknown'
}

function generateId(): string {
  return Math.random().toString(36).substring(2, 11)
}

// Data type icon component
function DataTypeIcon({ type, className = "w-3.5 h-3.5" }: { type: DataType; className?: string }) {
  switch (type) {
    case 'number': return <Hash className={`${className} text-blue-400`} />
    case 'date': return <Calendar className={`${className} text-purple-400`} />
    case 'string': return <Type className={`${className} text-green-400`} />
    case 'boolean': return <CircleDot className={`${className} text-yellow-400`} />
    default: return <CircleDot className={`${className} text-zinc-400`} />
  }
}

// ============================================
// SORTABLE COLUMN COMPONENT
// ============================================

interface SortableColumnProps {
  column: SelectColumn
  onRemove: () => void
  onUpdateAlias: (alias: string) => void
}

function SortableColumn({ column, onRemove, onUpdateAlias }: SortableColumnProps) {
  const [isEditing, setIsEditing] = useState(false)
  const [tempAlias, setTempAlias] = useState(column.alias)
  const inputRef = useRef<HTMLInputElement>(null)

  const {
    attributes,
    listeners,
    setNodeRef,
    transform,
    transition,
    isDragging,
  } = useSortable({ id: column.id })

  const style = {
    transform: CSS.Transform.toString(transform),
    transition,
    opacity: isDragging ? 0.5 : 1,
  }

  useEffect(() => {
    if (isEditing && inputRef.current) {
      inputRef.current.focus()
      inputRef.current.select()
    }
  }, [isEditing])

  const handleSave = () => {
    onUpdateAlias(tempAlias)
    setIsEditing(false)
  }

  const handleCancel = () => {
    setTempAlias(column.alias)
    setIsEditing(false)
  }

  return (
    <div
      ref={setNodeRef}
      style={style}
      className="flex items-center gap-2 p-2.5 bg-surface-highlight rounded-lg border border-border hover:border-zinc-600 transition-colors group"
      data-testid={`selected-col-${column.id}`}
    >
      <button
        {...attributes}
        {...listeners}
        className="cursor-grab active:cursor-grabbing text-zinc-500 hover:text-zinc-300"
      >
        <GripVertical className="w-4 h-4" />
      </button>
      
      <DataTypeIcon type={column.dataType} />
      
      <div className="flex-1 min-w-0">
        <div className="text-xs text-zinc-500 truncate">{column.expr}</div>
        {isEditing ? (
          <div className="flex items-center gap-1 mt-0.5">
            <input
              ref={inputRef}
              type="text"
              value={tempAlias}
              onChange={(e) => setTempAlias(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Enter') handleSave()
                if (e.key === 'Escape') handleCancel()
              }}
              className="flex-1 h-6 px-1.5 bg-zinc-900 border border-primary rounded text-white text-sm"
            />
            <button onClick={handleSave} className="text-success hover:text-green-400">
              <Check className="w-4 h-4" />
            </button>
            <button onClick={handleCancel} className="text-zinc-500 hover:text-zinc-300">
              <X className="w-4 h-4" />
            </button>
          </div>
        ) : (
          <div className="flex items-center gap-1 mt-0.5">
            <span className="text-white text-sm font-medium truncate">
              {column.alias || column.sourceColumn}
            </span>
            <button 
              onClick={() => setIsEditing(true)}
              className="opacity-0 group-hover:opacity-100 text-zinc-500 hover:text-primary transition-opacity"
            >
              <Edit2 className="w-3 h-3" />
            </button>
          </div>
        )}
      </div>

      <button
        onClick={onRemove}
        className="opacity-0 group-hover:opacity-100 text-zinc-500 hover:text-destructive transition-opacity"
      >
        <Trash2 className="w-4 h-4" />
      </button>
    </div>
  )
}

// ============================================
// FILTER BUILDER COMPONENT
// ============================================

interface FilterBuilderProps {
  group: FilterGroup
  schema: SchemaResponse
  allColumns: Map<string, { table: string; column: ColumnSchema }>
  onChange: (group: FilterGroup) => void
  onRemove?: () => void
  depth?: number
}

function FilterBuilder({ group, schema, allColumns, onChange, onRemove, depth = 0 }: FilterBuilderProps) {
  const toggleLogic = () => {
    onChange({ ...group, op: group.op === 'and' ? 'or' : 'and' })
  }

  const addRule = () => {
    const firstCol = Array.from(allColumns.entries())[0]
    if (!firstCol) return

    const [key, col] = firstCol
    const dataType = getDataType(col.column.columnType)
    const operators = OPERATORS_BY_TYPE[dataType]

    const newRule: FilterRule = {
      id: generateId(),
      field: key,
      operator: operators[0]?.key || 'eq',
      value: '',
      dataType,
    }
    onChange({ ...group, rules: [...group.rules, newRule] })
  }

  const addGroup = () => {
    const newGroup: FilterGroup = {
      id: generateId(),
      op: 'and',
      rules: [],
    }
    onChange({ ...group, rules: [...group.rules, newGroup] })
  }

  const updateRule = (index: number, updates: Partial<FilterRule | FilterGroup>) => {
    const newRules = [...group.rules]
    newRules[index] = { ...newRules[index], ...updates } as FilterRule | FilterGroup
    onChange({ ...group, rules: newRules })
  }

  const removeRule = (index: number) => {
    onChange({ ...group, rules: group.rules.filter((_, i) => i !== index) })
  }

  const isFilterGroup = (rule: FilterRule | FilterGroup): rule is FilterGroup => {
    return 'op' in rule && 'rules' in rule
  }

  return (
    <div
      className={`rounded-lg border ${depth === 0 ? 'border-border bg-surface' : 'border-zinc-700 bg-zinc-900/50'} p-3`}
      data-testid={`filter-group-${group.id}`}
    >
      <div className="flex items-center gap-2 mb-3">
        <button
          onClick={toggleLogic}
          className={`px-3 py-1 rounded-full text-xs font-bold uppercase tracking-wider transition-colors ${
            group.op === 'and'
              ? 'bg-primary/20 text-primary border border-primary/30'
              : 'bg-warning/20 text-warning border border-warning/30'
          }`}
          data-testid={`toggle-logic-${group.id}`}
        >
          {group.op}
        </button>
        <span className="text-xs text-zinc-500">
          {group.op === 'and' ? 'Todas las condiciones deben cumplirse' : 'Al menos una condición debe cumplirse'}
        </span>
        <div className="flex-1" />
        {onRemove && (
          <button
            onClick={onRemove}
            className="text-zinc-500 hover:text-destructive transition-colors"
          >
            <Trash2 className="w-4 h-4" />
          </button>
        )}
      </div>

      <div className="space-y-2">
        {group.rules.map((rule, idx) => (
          <div key={'id' in rule ? rule.id : idx}>
            {isFilterGroup(rule) ? (
              <FilterBuilder
                group={rule}
                schema={schema}
                allColumns={allColumns}
                onChange={(updated) => updateRule(idx, updated)}
                onRemove={() => removeRule(idx)}
                depth={depth + 1}
              />
            ) : (
              <FilterRuleRow
                rule={rule}
                allColumns={allColumns}
                onChange={(updates) => updateRule(idx, updates)}
                onRemove={() => removeRule(idx)}
              />
            )}
          </div>
        ))}
      </div>

      <div className="flex gap-2 mt-3 pt-3 border-t border-zinc-800">
        <Button variant="ghost" size="sm" onClick={addRule} data-testid={`add-rule-${group.id}`}>
          <Plus className="w-3.5 h-3.5 mr-1" />
          Condición
        </Button>
        <Button variant="ghost" size="sm" onClick={addGroup} data-testid={`add-group-${group.id}`}>
          <FolderTree className="w-3.5 h-3.5 mr-1" />
          Grupo
        </Button>
      </div>
    </div>
  )
}

interface FilterRuleRowProps {
  rule: FilterRule
  allColumns: Map<string, { table: string; column: ColumnSchema }>
  onChange: (updates: Partial<FilterRule>) => void
  onRemove: () => void
}

function FilterRuleRow({ rule, allColumns, onChange, onRemove }: FilterRuleRowProps) {
  const selectedCol = allColumns.get(rule.field)
  const dataType = selectedCol ? getDataType(selectedCol.column.columnType) : 'unknown'
  const operators = OPERATORS_BY_TYPE[dataType]
  const currentOp = operators.find(o => o.key === rule.operator) || operators[0]

  const handleFieldChange = (field: string) => {
    const col = allColumns.get(field)
    const newDataType = col ? getDataType(col.column.columnType) : 'unknown'
    const newOperators = OPERATORS_BY_TYPE[newDataType]
    onChange({
      field,
      dataType: newDataType,
      operator: newOperators[0]?.key || 'eq',
      value: '',
    })
  }

  return (
    <div
      className="flex items-center gap-2 p-2 bg-zinc-800/50 rounded-lg"
      data-testid={`filter-rule-${rule.id}`}
    >
      <DataTypeIcon type={dataType} />
      
      <select
        value={rule.field}
        onChange={(e) => handleFieldChange(e.target.value)}
        className="h-8 px-2 bg-zinc-900 border border-zinc-700 rounded text-white text-sm min-w-[150px]"
      >
        {Array.from(allColumns.entries()).map(([key, col]) => (
          <option key={key} value={key}>
            {key}
          </option>
        ))}
      </select>

      <select
        value={rule.operator}
        onChange={(e) => onChange({ operator: e.target.value })}
        className="h-8 px-2 bg-zinc-900 border border-zinc-700 rounded text-white text-sm"
      >
        {operators.map((op) => (
          <option key={op.key} value={op.key}>
            {op.label}
          </option>
        ))}
      </select>

      {currentOp?.needsValue && (
        <>
          {rule.operator === 'between' ? (
            <div className="flex items-center gap-1">
              <input
                type={dataType === 'date' ? 'date' : dataType === 'number' ? 'number' : 'text'}
                value={Array.isArray(rule.value) ? rule.value[0] || '' : ''}
                onChange={(e) => onChange({ value: [e.target.value, Array.isArray(rule.value) ? rule.value[1] : ''] })}
                placeholder="Desde"
                className="h-8 px-2 bg-zinc-900 border border-zinc-700 rounded text-white text-sm w-28"
              />
              <span className="text-zinc-500 text-xs">y</span>
              <input
                type={dataType === 'date' ? 'date' : dataType === 'number' ? 'number' : 'text'}
                value={Array.isArray(rule.value) ? rule.value[1] || '' : ''}
                onChange={(e) => onChange({ value: [Array.isArray(rule.value) ? rule.value[0] : '', e.target.value] })}
                placeholder="Hasta"
                className="h-8 px-2 bg-zinc-900 border border-zinc-700 rounded text-white text-sm w-28"
              />
            </div>
          ) : rule.operator === 'in' ? (
            <input
              type="text"
              value={Array.isArray(rule.value) ? rule.value.join(', ') : rule.value}
              onChange={(e) => onChange({ value: e.target.value.split(',').map(v => v.trim()) })}
              placeholder="valor1, valor2, ..."
              className="flex-1 h-8 px-2 bg-zinc-900 border border-zinc-700 rounded text-white text-sm"
            />
          ) : (
            <input
              type={dataType === 'date' ? 'date' : dataType === 'number' ? 'number' : 'text'}
              value={rule.value || ''}
              onChange={(e) => onChange({ value: e.target.value })}
              placeholder="Valor"
              className="flex-1 h-8 px-2 bg-zinc-900 border border-zinc-700 rounded text-white text-sm"
            />
          )}
        </>
      )}

      {rule.operator === 'in_ids' && (
        <span className="text-xs text-accent bg-accent/10 px-2 py-1 rounded">
          Lista de cédulas del job
        </span>
      )}

      <button
        onClick={onRemove}
        className="text-zinc-500 hover:text-destructive transition-colors"
      >
        <Trash2 className="w-4 h-4" />
      </button>
    </div>
  )
}

// ============================================
// MAIN PRESET DESIGNER COMPONENT
// ============================================

export default function PresetDesigner() {
  const [datasets, setDatasets] = useState<{ key: string; name: string }[]>([])
  const [selectedDataset, setSelectedDataset] = useState<string>('')
  const [schema, setSchema] = useState<SchemaResponse | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [expandedTables, setExpandedTables] = useState<Set<string>>(new Set())

  // Preset info
  const [presetKey, setPresetKey] = useState('')
  const [presetName, setPresetName] = useState('')
  const [presetDescription, setPresetDescription] = useState('')
  const [presetId, setPresetId] = useState<number | null>(null)

  // AST State
  const [selectedColumns, setSelectedColumns] = useState<SelectColumn[]>([])
  const [joins, setJoins] = useState<JoinClause[]>([])
  const [filters, setFilters] = useState<FilterGroup>({ id: 'root', op: 'and', rules: [] })
  const [orderBy, setOrderBy] = useState<OrderByClause[]>([])
  const [limit, setLimit] = useState<number>(100)

  // Versions
  const [versions, setVersions] = useState<PresetVersion[]>([])
  const [showVersions, setShowVersions] = useState(false)

  // Preview & Test
  const [sqlPreview, setSqlPreview] = useState<string>('')
  const [testCedula, setTestCedula] = useState('')
  const [testResults, setTestResults] = useState<any>(null)
  const [isTesting, setIsTesting] = useState(false)
  const [isSaving, setIsSaving] = useState(false)
  const [isCompiling, setIsCompiling] = useState(false)
  const [compileError, setCompileError] = useState<string | null>(null)

  // Drag state
  const [activeId, setActiveId] = useState<string | null>(null)

  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 5 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates })
  )

  // Build column lookup map
  const allColumns = new Map<string, { table: string; column: ColumnSchema }>()
  schema?.tables.forEach(table => {
    table.columns.forEach(col => {
      allColumns.set(`${table.tableName}.${col.columnName}`, { table: table.tableName, column: col })
    })
  })

  // Load datasets
  const loadDatasets = useCallback(async () => {
    const { data } = await getDatasets()
    if (data) {
      setDatasets(data)
      if (data.length > 0 && !selectedDataset) {
        setSelectedDataset(data[0].key)
      }
    }
  }, [selectedDataset])

  // Load schema
  const loadSchema = useCallback(async () => {
    if (!selectedDataset) return
    setIsLoading(true)
    const { data, error } = await getSchema(selectedDataset)
    if (error) {
      toast.error('Error cargando esquema')
    } else if (data) {
      setSchema(data)
      // Auto-expand first table
      if (data.tables.length > 0) {
        setExpandedTables(new Set([data.tables[0].tableName]))
      }
    }
    setIsLoading(false)
  }, [selectedDataset])

  useEffect(() => {
    loadDatasets()
  }, [loadDatasets])

  useEffect(() => {
    loadSchema()
  }, [loadSchema])

  // Toggle table expansion
  const toggleTable = (tableName: string) => {
    const newExpanded = new Set(expandedTables)
    if (newExpanded.has(tableName)) {
      newExpanded.delete(tableName)
    } else {
      newExpanded.add(tableName)
    }
    setExpandedTables(newExpanded)
  }

  // Add column from schema explorer
  const addColumn = (table: string, column: ColumnSchema) => {
    const expr = `${table}.${column.columnName}`
    if (selectedColumns.some(c => c.expr === expr)) {
      toast.info('Columna ya agregada')
      return
    }

    const newCol: SelectColumn = {
      id: generateId(),
      expr,
      alias: column.columnName,
      sourceTable: table,
      sourceColumn: column.columnName,
      dataType: getDataType(column.columnType),
    }
    setSelectedColumns([...selectedColumns, newCol])
  }

  // Remove column
  const removeColumn = (id: string) => {
    setSelectedColumns(selectedColumns.filter(c => c.id !== id))
  }

  // Update column alias
  const updateColumnAlias = (id: string, alias: string) => {
    setSelectedColumns(selectedColumns.map(c => c.id === id ? { ...c, alias } : c))
  }

  // Drag handlers
  const handleDragStart = (event: DragStartEvent) => {
    setActiveId(event.active.id as string)
  }

  const handleDragEnd = (event: DragEndEvent) => {
    const { active, over } = event
    setActiveId(null)

    if (over && active.id !== over.id) {
      setSelectedColumns((items) => {
        const oldIndex = items.findIndex(i => i.id === active.id)
        const newIndex = items.findIndex(i => i.id === over.id)
        return arrayMove(items, oldIndex, newIndex)
      })
    }
  }

  // Add order by
  const addOrderBy = () => {
    if (selectedColumns.length === 0) {
      toast.info('Primero selecciona columnas')
      return
    }
    const firstCol = selectedColumns[0]
    setOrderBy([...orderBy, {
      id: generateId(),
      field: firstCol.expr,
      dir: 'asc',
    }])
  }

  // Build normalized AST
  const buildAst = (): NormalizedAst => {
    return {
      dataset: selectedDataset,
      select: selectedColumns,
      joins,
      where: filters.rules.length > 0 ? filters : null,
      orderBy,
      limit,
    }
  }

  const toBackendAst = useCallback(() => {
    const ast = buildAst()

    const mapFilterGroup = (group: FilterGroup) => {
      const filtersOnly = group.rules.filter((r) => !('op' in r && 'rules' in r)) as FilterRule[]
      const groupsOnly = group.rules.filter((r) => 'op' in r && 'rules' in r) as FilterGroup[]

      return {
        logic: group.op.toUpperCase(),
        filters: filtersOnly.map((rule) => {
          const [table, column] = rule.field.split('.')
          return { table, column, operator: rule.operator, value: rule.value }
        }),
        groups: groupsOnly.map(mapFilterGroup),
      }
    }

    return {
      type: 'CUSTOM',
      select: ast.select.map((c) => ({
        table: c.sourceTable,
        column: c.sourceColumn,
        alias: c.alias,
      })),
      fromTable: ast.select[0]?.sourceTable ?? schema?.tables[0]?.tableName,
      joins: ast.joins.map((j) => ({
        joinType: j.joinType,
        table: j.table,
        onLeft: j.onLeft,
        onRight: j.onRight,
      })),
      where: ast.where ? mapFilterGroup(ast.where) : null,
      orderBy: ast.orderBy.map((o) => {
        const [table, column] = o.field.split('.')
        return { table, column, direction: o.dir.toUpperCase() }
      }),
      limit: ast.limit,
      inputs: [{ name: 'cedula', type: 'string', required: true }],
    }
  }, [buildAst, schema])

  useEffect(() => {
    const compile = async () => {
      if (!selectedDataset) return
      setIsCompiling(true)
      const { data, error } = await compileAstToSql(toBackendAst(), selectedDataset)
      if (error) {
        setSqlPreview('')
        setCompileError(error)
      } else {
        setSqlPreview(data?.sql || '')
        setCompileError(null)
      }
      setIsCompiling(false)
    }

    compile()
  }, [selectedColumns, joins, filters, orderBy, limit, selectedDataset, toBackendAst])

  // Save preset (creates new version)
  const handleSave = async () => {
    if (!presetKey) {
      toast.error('Ingresa una clave única para el preset')
      return
    }
    if (!presetName) {
      toast.error('Ingresa un nombre para el preset')
      return
    }

    setIsSaving(true)
    try {
      const backendAst = toBackendAst()
      let currentPresetId = presetId

      if (!currentPresetId) {
        const createResp = await createPreset({
          presetKey,
          name: presetName,
          description: presetDescription,
          dataset: selectedDataset,
          ast: backendAst,
        })

        if (createResp.error || !createResp.data) {
          toast.error(createResp.error || 'Error creando preset')
          return
        }

        currentPresetId = createResp.data.id
        setPresetId(createResp.data.id)
      } else {
        const versionResp = await createPresetVersion(currentPresetId, backendAst)
        if (versionResp.error) {
          toast.error(versionResp.error)
          return
        }
      }

      const versionsResp = await getPresetVersions(presetKey)
      if (versionsResp.error || !versionsResp.data) {
        toast.error(versionsResp.error || 'No se pudieron cargar versiones')
        return
      }

      setVersions(versionsResp.data.map(v => ({
        id: v.id,
        version: v.version,
        astJson: v.astJson,
        isActive: v.isActive,
        createdBy: v.createdBy,
        createdAt: v.createdAt,
      })))

      const activeVersion = versionsResp.data.find(v => v.isActive)
      toast.success(`Preset guardado${activeVersion ? ` - Versión ${activeVersion.version}` : ''}`)
    } catch (error) {
      toast.error('Error guardando preset')
    } finally {
      setIsSaving(false)
    }
  }

  // Activate version
  const activateVersion = async (versionId: number) => {
    if (!presetId) {
      toast.error('Guarda el preset antes de activar versiones')
      return
    }

    const resp = await activatePresetVersion(presetId, versionId)
    if (resp.error) {
      toast.error(resp.error)
      return
    }

    setVersions(versions.map(v => ({ ...v, isActive: v.id === versionId })))
    const version = versions.find(v => v.id === versionId)
    if (version) {
      // Load AST from version
      try {
        const ast = JSON.parse(version.astJson) as NormalizedAst
        setSelectedColumns(ast.select)
        setJoins(ast.joins)
        setFilters(ast.where || { id: 'root', op: 'and', rules: [] })
        setOrderBy(ast.orderBy)
        setLimit(ast.limit)
        toast.success(`Versión ${version.version} activada`)
      } catch {
        toast.error('Error cargando versión')
      }
    }
  }

  // Copy AST to clipboard
  const copyAst = () => {
    const ast = buildAst()
    navigator.clipboard.writeText(JSON.stringify(ast, null, 2))
    toast.success('AST copiado al portapapeles')
  }

  // Test preset
  const handleTest = async () => {
    if (!testCedula) {
      toast.error('Ingresa una cédula de prueba')
      return
    }

    setIsTesting(true)
    try {
      let response
      if (presetKey && presetId) {
        response = await testPreset(presetKey, testCedula)
      } else {
        const { data, error } = await compileAstToSql(toBackendAst(), selectedDataset)
        if (error) {
          toast.error(error)
          return
        }
        response = {
          data: {
            success: true,
            generatedSql: data?.sql,
            results: [],
            executionTimeMs: 0,
          }
        }
      }

      if (response.error || !response.data) {
        toast.error(response.error || 'Error ejecutando prueba')
        return
      }

      setTestResults(response.data)
      if (response.data.success) {
        toast.success('Prueba ejecutada correctamente')
      } else {
        toast.error(response.data.errorMessage || 'Prueba fallida')
      }
    } finally {
      setIsTesting(false)
    }
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
    <DndContext
      sensors={sensors}
      collisionDetection={closestCenter}
      onDragStart={handleDragStart}
      onDragEnd={handleDragEnd}
    >
      <div className="animate-fade-in">
        <div className="flex items-center justify-between mb-8">
          <div>
            <h1 className="text-3xl font-bold font-heading mb-2">Diseñador de Presets</h1>
            <p className="text-zinc-400">Crea consultas visuales sin escribir SQL</p>
          </div>
          <div className="flex items-center gap-2">
            <Button variant="ghost" onClick={copyAst} data-testid="copy-ast-btn">
              <Copy className="w-4 h-4 mr-2" />
              Copiar AST
            </Button>
            <Button 
              variant="secondary" 
              onClick={() => setShowVersions(!showVersions)}
              data-testid="toggle-versions-btn"
            >
              <History className="w-4 h-4 mr-2" />
              Versiones {versions.length > 0 && `(${versions.length})`}
            </Button>
          </div>
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

            {/* Schema Explorer Tree */}
            <Card data-testid="schema-explorer-card" className="max-h-[500px] overflow-y-auto">
              <h3 className="font-semibold mb-3 flex items-center gap-2 sticky top-0 bg-surface pb-2">
                <FolderTree className="w-5 h-5 text-accent" />
                Schema Explorer
              </h3>
              <div className="space-y-1">
                {schema?.tables.map((table) => (
                  <div key={table.tableName}>
                    <button
                      onClick={() => toggleTable(table.tableName)}
                      className="w-full flex items-center gap-2 p-2 rounded-lg hover:bg-surface-highlight transition-colors text-left"
                      data-testid={`table-toggle-${table.tableName}`}
                    >
                      {expandedTables.has(table.tableName) ? (
                        <ChevronDown className="w-4 h-4 text-zinc-500" />
                      ) : (
                        <ChevronRight className="w-4 h-4 text-zinc-500" />
                      )}
                      <Table2 className="w-4 h-4 text-primary" />
                      <span className="text-white font-medium">{table.tableName}</span>
                      <span className="text-xs text-zinc-500 ml-auto">{table.columns.length}</span>
                    </button>

                    {expandedTables.has(table.tableName) && (
                      <div className="ml-6 space-y-0.5 py-1">
                        {table.columns.map((col) => {
                          const dataType = getDataType(col.columnType)
                          const isSelected = selectedColumns.some(c => c.expr === `${table.tableName}.${col.columnName}`)
                          
                          return (
                            <button
                              key={col.columnName}
                              onClick={() => addColumn(table.tableName, col)}
                              disabled={isSelected}
                              className={`w-full flex items-center gap-2 p-1.5 pl-2 rounded text-left text-sm transition-colors ${
                                isSelected
                                  ? 'bg-primary/10 text-primary cursor-not-allowed'
                                  : 'hover:bg-surface-highlight text-zinc-400 hover:text-white'
                              }`}
                              data-testid={`col-${table.tableName}-${col.columnName}`}
                            >
                              <DataTypeIcon type={dataType} className="w-3.5 h-3.5" />
                              <span className="truncate">{col.displayName || col.columnName}</span>
                              <span className="text-xs text-zinc-600 ml-auto">{col.columnType}</span>
                              {isSelected && <Check className="w-3 h-3 text-primary" />}
                            </button>
                          )
                        })}
                      </div>
                    )}
                  </div>
                ))}
              </div>
            </Card>

            {/* Data Type Legend */}
            <Card className="text-xs">
              <h4 className="font-medium text-zinc-400 mb-2">Tipos de Datos</h4>
              <div className="grid grid-cols-2 gap-2">
                <div className="flex items-center gap-1.5"><DataTypeIcon type="number" /> Número</div>
                <div className="flex items-center gap-1.5"><DataTypeIcon type="date" /> Fecha</div>
                <div className="flex items-center gap-1.5"><DataTypeIcon type="string" /> Texto</div>
                <div className="flex items-center gap-1.5"><DataTypeIcon type="boolean" /> Booleano</div>
              </div>
            </Card>
          </div>

          {/* Right Panel - Builder */}
          <div className="lg:col-span-8 space-y-4">
            {/* Preset Info */}
            <Card data-testid="preset-info-card">
              <h3 className="font-semibold mb-3">Información del Preset</h3>
              <div className="grid grid-cols-3 gap-4">
                <div>
                  <label className="block text-xs text-zinc-400 mb-1">Clave (única)</label>
                  <input
                    type="text"
                    value={presetKey}
                    onChange={(e) => setPresetKey(e.target.value.toLowerCase().replace(/[^a-z0-9_]/g, '_'))}
                    placeholder="mi_preset"
                    className="w-full h-9 px-3 bg-zinc-900 border border-zinc-700 rounded-lg text-white text-sm font-mono"
                    data-testid="preset-key-input"
                  />
                </div>
                <div>
                  <label className="block text-xs text-zinc-400 mb-1">Nombre</label>
                  <input
                    type="text"
                    value={presetName}
                    onChange={(e) => setPresetName(e.target.value)}
                    placeholder="Mi Preset"
                    className="w-full h-9 px-3 bg-zinc-900 border border-zinc-700 rounded-lg text-white text-sm"
                    data-testid="preset-name-input"
                  />
                </div>
                <div>
                  <label className="block text-xs text-zinc-400 mb-1">Límite</label>
                  <input
                    type="number"
                    value={limit}
                    onChange={(e) => setLimit(Math.max(1, parseInt(e.target.value) || 100))}
                    min={1}
                    max={10000}
                    className="w-full h-9 px-3 bg-zinc-900 border border-zinc-700 rounded-lg text-white text-sm"
                    data-testid="limit-input"
                  />
                </div>
              </div>
              <div className="mt-3">
                <label className="block text-xs text-zinc-400 mb-1">Descripción</label>
                <textarea
                  value={presetDescription}
                  onChange={(e) => setPresetDescription(e.target.value)}
                  placeholder="Descripción del preset..."
                  className="w-full h-16 p-2 bg-zinc-900 border border-zinc-700 rounded-lg text-white text-sm resize-none"
                  data-testid="preset-description-input"
                />
              </div>
            </Card>

            {/* Selected Columns with Drag & Drop */}
            <Card data-testid="selected-columns-card">
              <h3 className="font-semibold mb-3 flex items-center gap-2">
                <Columns3 className="w-5 h-5 text-success" />
                Columnas Seleccionadas (SELECT)
                <span className="text-xs text-zinc-500 font-normal ml-auto">
                  Arrastra para reordenar
                </span>
              </h3>
              
              {selectedColumns.length === 0 ? (
                <div className="text-center py-8 border-2 border-dashed border-zinc-700 rounded-lg">
                  <Columns3 className="w-8 h-8 mx-auto mb-2 text-zinc-600" />
                  <p className="text-zinc-500 text-sm">
                    Haz clic en las columnas del Schema Explorer para agregarlas
                  </p>
                </div>
              ) : (
                <SortableContext items={selectedColumns.map(c => c.id)} strategy={verticalListSortingStrategy}>
                  <div className="space-y-2">
                    {selectedColumns.map((col) => (
                      <SortableColumn
                        key={col.id}
                        column={col}
                        onRemove={() => removeColumn(col.id)}
                        onUpdateAlias={(alias) => updateColumnAlias(col.id, alias)}
                      />
                    ))}
                  </div>
                </SortableContext>
              )}

              <DragOverlay>
                {activeId ? (
                  <div className="p-2.5 bg-primary/20 border border-primary rounded-lg">
                    {selectedColumns.find(c => c.id === activeId)?.expr}
                  </div>
                ) : null}
              </DragOverlay>
            </Card>

            {/* Filter Builder */}
            <Card data-testid="filters-card">
              <div className="flex items-center justify-between mb-3">
                <h3 className="font-semibold flex items-center gap-2">
                  <Filter className="w-5 h-5 text-warning" />
                  Filtros (WHERE)
                </h3>
              </div>
              
              {allColumns.size === 0 ? (
                <p className="text-zinc-500 text-sm py-4 text-center">
                  Carga un esquema para agregar filtros
                </p>
              ) : (
                <FilterBuilder
                  group={filters}
                  schema={schema!}
                  allColumns={allColumns}
                  onChange={setFilters}
                />
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
                      key={order.id}
                      className="flex items-center gap-2 p-2 bg-surface-highlight rounded-lg"
                      data-testid={`orderby-${idx}`}
                    >
                      <select
                        value={order.field}
                        onChange={(e) => {
                          const newOrderBy = [...orderBy]
                          newOrderBy[idx] = { ...order, field: e.target.value }
                          setOrderBy(newOrderBy)
                        }}
                        className="flex-1 h-8 px-2 bg-zinc-900 border border-zinc-700 rounded text-white text-sm"
                      >
                        {selectedColumns.map((col) => (
                          <option key={col.id} value={col.expr}>
                            {col.alias || col.expr}
                          </option>
                        ))}
                      </select>
                      <select
                        value={order.dir}
                        onChange={(e) => {
                          const newOrderBy = [...orderBy]
                          newOrderBy[idx] = { ...order, dir: e.target.value as 'asc' | 'desc' }
                          setOrderBy(newOrderBy)
                        }}
                        className="h-8 px-2 bg-zinc-900 border border-zinc-700 rounded text-white text-sm"
                      >
                        <option value="asc">↑ Ascendente</option>
                        <option value="desc">↓ Descendente</option>
                      </select>
                      <button
                        onClick={() => setOrderBy(orderBy.filter((_, i) => i !== idx))}
                        className="text-zinc-500 hover:text-destructive"
                      >
                        <Trash2 className="w-4 h-4" />
                      </button>
                    </div>
                  ))}
                </div>
              )}
            </Card>

            {/* SQL Preview (Read-only) */}
            <Card data-testid="sql-preview-card">
              <div className="flex items-center justify-between mb-3">
                <h3 className="font-semibold flex items-center gap-2">
                  <Code className="w-5 h-5 text-primary" />
                  Vista Previa SQL
                  <span className="text-xs text-zinc-500 font-normal">(generado por backend)</span>
                </h3>
              </div>
              <pre className="p-4 bg-zinc-950 rounded-lg overflow-x-auto text-sm font-mono text-zinc-300 border border-zinc-800">
                {isCompiling
                  ? '-- Compilando SQL desde backend...'
                  : compileError
                  ? `-- Error al compilar: ${compileError}`
                  : sqlPreview || '-- Selecciona columnas para generar preview'}
              </pre>
            </Card>

            {/* AST Preview */}
            <Card data-testid="ast-preview-card">
              <div className="flex items-center justify-between mb-3">
                <h3 className="font-semibold flex items-center gap-2">
                  <Code className="w-5 h-5 text-accent" />
                  AST JSON Normalizado
                </h3>
                <Button variant="ghost" size="sm" onClick={copyAst}>
                  <Copy className="w-3.5 h-3.5 mr-1" />
                  Copiar
                </Button>
              </div>
              <pre className="p-4 bg-zinc-950 rounded-lg overflow-x-auto text-xs font-mono text-zinc-400 border border-zinc-800 max-h-48">
                {JSON.stringify(buildAst(), null, 2)}
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
                  className="w-40 h-10 px-3 bg-zinc-900 border border-zinc-700 rounded-lg text-white font-mono"
                  data-testid="test-cedula-input"
                />
                <Button variant="secondary" onClick={handleTest} isLoading={isTesting} data-testid="test-preset-btn">
                  <Play className="w-4 h-4 mr-2" />
                  Probar
                </Button>
              </div>
              <Button onClick={handleSave} isLoading={isSaving} data-testid="save-preset-btn">
                <Save className="w-4 h-4 mr-2" />
                Guardar Nueva Versión
              </Button>
            </div>

            {/* Test Results */}
            {testResults && (
              <Card data-testid="test-results-card">
                <h3 className="font-semibold mb-3">Resultado de Prueba</h3>
                <pre className="p-4 bg-zinc-950 rounded-lg overflow-x-auto text-xs font-mono text-zinc-400 max-h-64 border border-zinc-800">
                  {JSON.stringify(testResults, null, 2)}
                </pre>
              </Card>
            )}
          </div>
        </div>

        {/* Versions Panel (Slide-in) */}
        {showVersions && (
          <div className="fixed inset-y-0 right-0 w-96 bg-surface border-l border-border shadow-2xl z-50 animate-slide-up overflow-y-auto">
            <div className="p-4 border-b border-border sticky top-0 bg-surface">
              <div className="flex items-center justify-between">
                <h3 className="font-semibold flex items-center gap-2">
                  <History className="w-5 h-5" />
                  Historial de Versiones
                </h3>
                <button
                  onClick={() => setShowVersions(false)}
                  className="text-zinc-500 hover:text-white"
                >
                  <X className="w-5 h-5" />
                </button>
              </div>
            </div>
            
            <div className="p-4 space-y-3">
              {versions.length === 0 ? (
                <p className="text-zinc-500 text-sm text-center py-8">
                  No hay versiones guardadas
                </p>
              ) : (
                versions.slice().reverse().map((version) => (
                  <div
                    key={version.id}
                    className={`p-3 rounded-lg border transition-colors ${
                      version.isActive
                        ? 'bg-primary/10 border-primary'
                        : 'bg-surface-highlight border-border hover:border-zinc-600'
                    }`}
                  >
                    <div className="flex items-center justify-between mb-2">
                      <span className="font-medium">
                        Versión {version.version}
                        {version.isActive && (
                          <span className="ml-2 text-xs bg-primary text-white px-1.5 py-0.5 rounded">
                            Activa
                          </span>
                        )}
                      </span>
                      {!version.isActive && (
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => activateVersion(version.id)}
                        >
                          <ToggleRight className="w-4 h-4 mr-1" />
                          Activar
                        </Button>
                      )}
                    </div>
                    <div className="text-xs text-zinc-500">
                      <div>Por: {version.createdBy}</div>
                      <div>{new Date(version.createdAt).toLocaleString()}</div>
                    </div>
                  </div>
                ))
              )}
            </div>
          </div>
        )}
      </div>
    </DndContext>
  )
}
