export interface Job {
  id: string
  presetKey: string
  presetName: string
  status: JobStatus
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

export type JobStatus = 
  | 'PENDING' 
  | 'PROCESSING' 
  | 'COMPLETED' 
  | 'FAILED' 
  | 'CANCELLED' 
  | 'PAUSED_BY_SCHEDULE'

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

// Legacy AST format (for hardcoded presets)
export interface PresetAst {
  type: 'HARDCODED' | 'CUSTOM'
  handler?: string
  select: AstColumn[]
  fromTable: string
  joins?: AstJoin[]
  where?: AstFilterGroup
  orderBy?: AstOrderBy[]
  limit?: number
  inputs?: AstInput[]
}

export interface AstColumn {
  table: string
  column: string
  alias?: string
  aggregate?: string
}

export interface AstJoin {
  joinType: 'INNER' | 'LEFT' | 'RIGHT'
  table: string
  onLeft: string
  onRight: string
}

export interface AstFilterGroup {
  logic: 'AND' | 'OR'
  filters?: AstFilter[]
  groups?: AstFilterGroup[]
}

export interface AstFilter {
  table: string
  column: string
  operator: string
  value?: any
  parameterName?: string
}

export interface AstOrderBy {
  table: string
  column: string
  direction: 'ASC' | 'DESC'
}

export interface AstInput {
  name: string
  type: string
  required: boolean
  default?: any
}

// ==============================================
// Normalized AST Format (for Preset Designer)
// ==============================================

export type DataType = 'string' | 'number' | 'date' | 'boolean' | 'unknown'

export interface NormalizedAst {
  dataset: string
  select: SelectColumn[]
  joins: JoinClause[]
  where: FilterGroup | null
  orderBy: OrderByClause[]
  limit: number
}

export interface SelectColumn {
  id: string
  expr: string // "table.column"
  alias: string
  sourceTable: string
  sourceColumn: string
  dataType: DataType
}

export interface JoinClause {
  id: string
  joinType: 'INNER' | 'LEFT' | 'RIGHT'
  table: string
  onLeft: string
  onRight: string
}

export interface FilterGroup {
  id: string
  op: 'and' | 'or'
  rules: (FilterRule | FilterGroup)[]
}

export interface FilterRule {
  id: string
  field: string // "table.column"
  operator: string
  value: any
  dataType: DataType
}

export interface OrderByClause {
  id: string
  field: string
  dir: 'asc' | 'desc'
}

export interface PresetVersion {
  id: number
  presetId: number
  version: number
  astJson: string
  isActive: boolean
  createdBy: string
  createdAt: string
}

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

export interface User {
  username: string
  role: 'ADMIN' | 'OPERATOR' | 'READER'
  credentials: string
}
