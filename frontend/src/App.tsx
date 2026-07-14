import {
  AlertTriangle,
  Blocks,
  Braces,
  CheckCircle2,
  ChevronDown,
  ChevronRight,
  Circle,
  Code2,
  Copy,
  Database,
  Download,
  ExternalLink,
  FileArchive,
  FileCode2,
  FileDown,
  FileSearch,
  FileText,
  Filter,
  FolderGit2,
  Info,
  BookOpen,
  CircleHelp,
  FolderSearch,
  GitBranch,
  LayoutDashboard,
  ListChecks,
  LogOut,
  Play,
  RefreshCw,
  Search,
  ServerCog,
  Settings,
  ShieldCheck,
  UploadCloud,
  XCircle,
  type LucideIcon,
} from 'lucide-react'
import Editor, { type OnMount } from '@monaco-editor/react'
import { type ChangeEvent, type FormEvent, type MouseEvent as ReactMouseEvent, useCallback, useEffect, useMemo, useState } from 'react'

type Severity = 'Info' | 'Warning' | 'Error' | 'Critical'
type IssueConfidence = 'High' | 'Medium' | 'Low'
type Category = 'Architecture' | 'DependencyInjection' | 'EfCore' | 'Security' | 'ApiReadiness'
type CategoryFilter = 'All' | Category
type SeverityFilter = 'All' | Severity
type FindingDisposition = 'Open' | 'Ignore' | 'False Positive' | 'Accepted Risk'
type ProjectFilter = 'All' | string
type SortMode = 'Severity' | 'Category' | 'Project' | 'File'
type AnalysisMode = 'path' | 'zip'
type AppPage = 'Overview' | 'Findings' | 'Code Explorer' | 'Architecture' | 'Rule Explorer' | 'Settings' | 'Docs' | 'Contact'
type StartPage = 'Home' | 'Docs' | 'Contact' | 'Changelog' | 'Dashboard' | 'Analyze' | 'Reports' | 'Settings'
type AnalyzeTab = 'GitHub Repository' | 'Upload ZIP' | 'Sample Project'
type ResizePanel = 'explorer' | 'guide'
type DensityMode = 'Compact' | 'Comfortable'
type CommandBarMode = 'Compact Microsoft style' | 'Expanded'
type EditorFontFamily = 'Cascadia Code' | 'Consolas'
type ExportFormat = 'JSON' | 'Markdown' | 'HTML'
type SettingsSectionId = 'Editor' | 'Analysis' | 'Export' | 'Advanced'

type CodeFile = {
  id: string
  name: string
  path: string
  projectName: string
  folder: string
  content: string
  language?: string
}

type AnalysisSourceFile = {
  projectName: string
  filePath: string
  relativePath: string
  content: string
  language: string
}

type CategoryScores = {
  architecture: number
  dependencyInjection: number
  efCore: number
  security: number
  apiReadiness: number
}

type DocumentationLink = {
  label: string
  href: string
}

type AnalysisEvidence = {
  label: string
  detail: string
  filePath?: string
  lineNumber?: number
}

type AnalysisIssue = {
  id: string
  ruleId?: string
  title: string
  description: string
  severity: Severity
  category: Category
  projectName?: string
  filePath?: string
  lineNumber?: number
  recommendation: string
  confidence?: IssueConfidence
  detectionMethod?: string
  problemSummary?: string
  whyDetected?: string
  whyItMatters?: string
  recommendedPattern?: string
  suggestedImplementation?: string
  documentationLinks?: DocumentationLink[]
  relatedFindingIds?: string[]
  evidence?: AnalysisEvidence[]
  rootCauseKey?: string
  suggestedSnippet?: string
  goodExample?: string
  badExample?: string
  suppression?: FindingSuppression
}

type RecommendedAction = AnalysisIssue & {
  groupedCount?: number
  groupedProjectCount?: number
}

type FindingSuppression = {
  id: string
  ruleId: string
  file?: string
  project?: string
  reason: string
  status: FindingDisposition
  createdDate: string
  expiration?: string
  isExpired: boolean
}

type RepositorySuppression = {
  id: string
  ruleId: string
  file?: string
  project?: string
  reason: string
  status: FindingDisposition
  createdDate: string
  expiration?: string
}

type ProjectNode = {
  name: string
  filePath: string
  logicalLayer?: string
  isTestProject?: boolean
  isAspNetCoreEntryPoint?: boolean
}

type ProjectDependency = {
  sourceProjectName: string
  targetProjectName: string
}

type ProjectGraph = {
  projects: ProjectNode[]
  dependencies: ProjectDependency[]
}

type ArchitectureMapProject = {
  name: string
  filePath: string
  layer: string
  namespaceRoot: string
  issueCount: number
  criticalOrErrorCount: number
}

type ArchitectureMapDependency = {
  sourceProjectName: string
  targetProjectName: string
  sourceLayer: string
  targetLayer: string
  direction: string
  isViolation: boolean
  ruleId?: string
  reason?: string
  relatedFindingId?: string
}

type ArchitectureMapViolation = {
  id: string
  title: string
  description: string
  ruleId: string
  sourceProjectName?: string
  targetProjectName?: string
  relatedFindingId?: string
}

type ArchitectureLayer = {
  name: string
  order: number
  projectNames: string[]
}

type ArchitectureMap = {
  layers: ArchitectureLayer[]
  projects: ArchitectureMapProject[]
  dependencies: ArchitectureMapDependency[]
  violations: ArchitectureMapViolation[]
}

type EngineeringAssessmentSummary = {
  overallProductionReadiness: string
  scoreExplanation: string
  strongAreas: string[]
  highestRisks: string[]
  architecturalObservations: string[]
  securityObservations: string[]
  apiReadinessObservations: string[]
  maintainabilityObservations: string[]
  recommendedPriorities: string[]
}

type AnalysisResult = {
  analysisRunId?: string
  solutionName: string
  analyzedAt: string
  overallScore: number
  categoryScores: CategoryScores
  issues: AnalysisIssue[]
  projectGraph: ProjectGraph
  sourceFiles?: AnalysisSourceFile[]
  isHistoricalSnapshot?: boolean
  sourcePreviewAvailable?: boolean
  sourcePreviewUnavailableReason?: string
  sourcePreviewCapped?: boolean
  sourcePreviewCappedReason?: string
  sourcePreviewIncludedFileCount?: number
  sourcePreviewOmittedFileCount?: number
  sourcePreviewIncludedBytes?: number
  sourcePreviewFileCountLimit?: number
  sourcePreviewByteLimit?: number
  analysisFidelity?: string
  semanticAnalysisSkipped?: boolean
  semanticAnalysisSkippedReason?: string
  architectureMap?: ArchitectureMap
  engineeringAssessment?: EngineeringAssessmentSummary
  solutionPath?: string
  repositoryRoot?: string
  suppressionFilePath?: string
  suppressionCount?: number
}

type RuleDocumentation = {
  ruleId: string
  title: string
  category: Category
  severity: Severity
  detectionMethod: string
  confidence: IssueConfidence
  confidenceExplanation: string
  problemSummary: string
  whyItMatters: string
  detectionLogic: string
  recommendedPattern: string
  suggestedImplementation: string
  goodExample?: string
  badExample?: string
  suggestedCodeSnippet?: string
  documentationLinks: DocumentationLink[]
  falsePositiveGuidance: string
  relatedRules: string[]
}

type StoredWorkbenchState = {
  activeCategory: CategoryFilter
  activePage: AppPage
  activeProject: ProjectFilter
  activeSeverity: SeverityFilter
  query: string
  result: AnalysisResult | null
  selectedFileId: string | null
  selectedIssueId: string | null
}

type AuthUser = {
  githubUserId: string
  githubUsername?: string
  gitHubUsername?: string
  displayName?: string
  email?: string
  avatarUrl?: string
  createdDate: string
  lastLoginDate: string
}

type AuthMeResponse = {
  isAuthenticated: boolean
  user: AuthUser | null
}

function normalizeAuthUser(user: AuthUser | null) {
  if (!user) {
    return null
  }

  return {
    ...user,
    githubUsername: user.githubUsername || user.gitHubUsername || '',
  }
}

function GitHubMark({ className = 'h-4 w-4' }: { className?: string }) {
  return (
    <svg className={className} viewBox="0 0 16 16" fill="currentColor" aria-hidden="true">
      <path d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82A7.65 7.65 0 0 1 8 3.87c.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.01 8.01 0 0 0 16 8c0-4.42-3.58-8-8-8Z" />
    </svg>
  )
}

type AnalysisSourceType = 'GitHubRepo' | 'ZipUpload' | 'SampleProject' | 'LocalDevPath'

type AnalysisHistorySummary = {
  id: string
  solutionName: string
  sourceType: AnalysisSourceType
  sourceLabel: string
  sourceUrl?: string
  gitHubOwner?: string
  gitHubRepo?: string
  gitRef?: string
  repositoryVisibility?: string
  score: number
  grade: string
  status: string
  openFindingCount: number
  totalFindingCount: number
  createdAt: string
  completedAt: string
  canRerun: boolean
}

type AnalysisHistoryDetail = {
  summary: AnalysisHistorySummary
  result: AnalysisResult
}

type GitHubRepositoryListingResponse = {
  isAvailable?: boolean
  reason?: string
  enabled: boolean
  message: string
  privateAccessEnabled?: boolean
  privateAccessMessage?: string
  repositories: Array<{
    owner: string
    name: string
    visibility?: string
    defaultBranch?: string
    description?: string
    htmlUrl?: string
    updatedAt?: string
  }>
}

const localApiHost =
  typeof window !== 'undefined' && ['localhost', '127.0.0.1', '::1'].includes(window.location.hostname)
    ? window.location.hostname
    : '127.0.0.1'
const formattedLocalApiHost = localApiHost === '::1' ? '[::1]' : localApiHost
const API_BASE_URL =
  import.meta.env.VITE_DOTDET_API_URL
  ?? import.meta.env.VITE_FORGE_API_URL
  ?? (import.meta.env.DEV ? `http://${formattedLocalApiHost}:5241` : window.location.origin)
const DOTDET_REPOSITORY_URL = 'https://github.com/cezarpedroso/dotdet'
const DOTDET_ISSUES_URL = 'https://github.com/cezarpedroso/DotDet/issues'
const DOTDET_README_URL = `${DOTDET_REPOSITORY_URL}/blob/main/README.md`
const DOTDET_DOCS_URL = `${DOTDET_REPOSITORY_URL}/blob/main/docs`
const SCHEMA_ARCHITECT_URL = 'https://schemarchitect.azurewebsites.net/'

const explorerWidthStorageKey = 'det.codeExplorer.explorerWidth'
const guideWidthStorageKey = 'det.codeExplorer.guideWidth'
const lastAnalysisResultStorageKey = 'det.analysis.lastResult.v1'
const lastAnalysisSolutionPathStorageKey = 'det.analysis.lastSolutionPath'
const browserStorageSourcePreviewUnavailableReason =
  'Source preview is not persisted in browser storage. Re-run the analysis to inspect source preview again.'
const exportSourcePreviewUnavailableReason =
  'Source preview is not included in this export.'
const activePageStorageKey = 'det.workbench.activePage'
const selectedIssueStorageKey = 'det.workbench.selectedIssueId'
const selectedFileStorageKey = 'det.workbench.selectedFileId'
const activeCategoryStorageKey = 'det.findings.activeCategory'
const activeSeverityStorageKey = 'det.findings.activeSeverity'
const activeProjectStorageKey = 'det.findings.activeProject'
const findingQueryStorageKey = 'det.findings.query'
const densityStorageKey = 'det.ui.density'
const commandBarStorageKey = 'det.ui.commandBar'
const gutterMarkersStorageKey = 'det.editor.gutterMarkers'
const minimapMarkersStorageKey = 'det.editor.minimapMarkers'
const editorFontStorageKey = 'det.editor.fontFamily'
const openFirstFindingStorageKey = 'det.analysis.openFirstFinding'
const defaultSortStorageKey = 'det.analysis.defaultSort'
const exportFormatStorageKey = 'det.export.format'
const includeSourcePreviewStorageKey = 'det.export.includeSourcePreview'
const findingDispositionsStorageKey = 'det.findingDispositions.v1'
const hideSuppressedStorageKey = 'det.findings.hideSuppressed'

const categoryDefinitions: Array<{
  key: Category
  label: string
  reportLabel: string
  icon: LucideIcon
  scoreKey: keyof CategoryScores
}> = [
  { key: 'Architecture', label: 'Architecture', reportLabel: 'Architecture Rules', icon: Blocks, scoreKey: 'architecture' },
  {
    key: 'Security',
    label: 'Security',
    reportLabel: 'Security & Configuration',
    icon: ShieldCheck,
    scoreKey: 'security',
  },
  { key: 'EfCore', label: 'EF Core', reportLabel: 'EF Core / Migrations', icon: Database, scoreKey: 'efCore' },
  {
    key: 'DependencyInjection',
    label: 'Dependency Injection',
    reportLabel: 'Dependency Injection',
    icon: ServerCog,
    scoreKey: 'dependencyInjection',
  },
  { key: 'ApiReadiness', label: 'API Readiness', reportLabel: 'API Readiness', icon: Code2, scoreKey: 'apiReadiness' },
]

const severityRank: Record<Severity, number> = {
  Critical: 4,
  Error: 3,
  Warning: 2,
  Info: 1,
}

const confidenceRank: Record<IssueConfidence, number> = {
  High: 3,
  Medium: 2,
  Low: 1,
}

const severityTextTone: Record<Severity, string> = {
  Critical: 'text-red-700',
  Error: 'text-red-700',
  Warning: 'text-amber-700',
  Info: 'text-sky-600',
}

const severityBorderTone: Record<Severity, string> = {
  Critical: 'border-l-red-800/60',
  Error: 'border-l-red-600/60',
  Warning: 'border-l-amber-500/60',
  Info: 'border-l-slate-500/40',
}

const selectedRowClass = 'bg-[#252a26] border-l-2 border-l-[#2ea043]'

function App() {
  const [storedWorkbenchState] = useState(() => loadStoredWorkbenchState())
  const [solutionPath] = useState('')
  const [zipFile, setZipFile] = useState<File | null>(null)
  const [result, setResult] = useState<AnalysisResult | null>(storedWorkbenchState.result)
  const [ruleCatalog, setRuleCatalog] = useState<RuleDocumentation[]>([])
  const [ruleCatalogError, setRuleCatalogError] = useState<string | null>(null)
  const [startPage, setStartPage] = useState<StartPage>(() => getStartPageFromPath(Boolean(storedWorkbenchState.result)))
  const [authUser, setAuthUser] = useState<AuthUser | null>(null)
  const [authLoading, setAuthLoading] = useState(true)
  const [analysisHistory, setAnalysisHistory] = useState<AnalysisHistorySummary[]>([])
  const [historyLoading, setHistoryLoading] = useState(false)
  const [historyError, setHistoryError] = useState<string | null>(null)
  const [analyzeTab, setAnalyzeTab] = useState<AnalyzeTab>('Upload ZIP')
  const [density, setDensity] = useState<DensityMode>(() => getStoredString(densityStorageKey, 'Compact', ['Compact', 'Comfortable'] as const))
  const [commandBarMode, setCommandBarMode] = useState<CommandBarMode>(() =>
    getStoredString(commandBarStorageKey, 'Compact Microsoft style', ['Compact Microsoft style', 'Expanded'] as const),
  )
  const [showGutterMarkers, setShowGutterMarkers] = useState(() => getStoredBoolean(gutterMarkersStorageKey, true))
  const [showMinimapMarkers, setShowMinimapMarkers] = useState(() => getStoredBoolean(minimapMarkersStorageKey, true))
  const [editorFontFamily, setEditorFontFamily] = useState<EditorFontFamily>(() =>
    getStoredString(editorFontStorageKey, 'Cascadia Code', ['Cascadia Code', 'Consolas'] as const),
  )
  const [openFirstFinding, setOpenFirstFinding] = useState(() => getStoredBoolean(openFirstFindingStorageKey, true))
  const [defaultSortMode, setDefaultSortMode] = useState<SortMode>(() =>
    getStoredString(defaultSortStorageKey, 'Severity', ['Severity', 'Category', 'Project', 'File'] as const),
  )
  const [exportFormat, setExportFormat] = useState<ExportFormat>(() => getStoredString(exportFormatStorageKey, 'HTML', ['JSON', 'Markdown', 'HTML'] as const))
  const [includeSourcePreview, setIncludeSourcePreview] = useState(() => getStoredBoolean(includeSourcePreviewStorageKey, false))
  const [selectedIssueId, setSelectedIssueId] = useState<string | null>(storedWorkbenchState.selectedIssueId)
  const [selectedRuleId, setSelectedRuleId] = useState<string | null>(null)
  const [selectedFileId, setSelectedFileId] = useState<string | null>(storedWorkbenchState.selectedFileId)
  const [findingDispositions, setFindingDispositions] = useState<Record<string, FindingDisposition>>(() => loadFindingDispositions())
  const [hideSuppressedFindings, setHideSuppressedFindings] = useState(() => getStoredBoolean(hideSuppressedStorageKey, false))
  const [activePage, setActivePage] = useState<AppPage>(storedWorkbenchState.activePage)
  const [activeCategory, setActiveCategory] = useState<CategoryFilter>(storedWorkbenchState.activeCategory)
  const [activeSeverity, setActiveSeverity] = useState<SeverityFilter>(storedWorkbenchState.activeSeverity)
  const [activeProject, setActiveProject] = useState<ProjectFilter>(storedWorkbenchState.activeProject)
  const [sortMode, setSortMode] = useState<SortMode>(defaultSortMode)
  const [query, setQuery] = useState(storedWorkbenchState.query)
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const openIssues = useMemo(() => (result ? getOpenFindings(result, findingDispositions) : []), [findingDispositions, result])
  const activeIssues = useMemo(() => openIssues.filter(isActiveProductionFinding), [openIssues])
  const summaryResult = useMemo(() => (result ? rebuildAnalysisResultForActiveFindings(result, activeIssues) : null), [activeIssues, result])
  const severityCounts = useMemo(() => getSeverityCounts(activeIssues), [activeIssues])
  const projectIssueCounts = useMemo(() => getProjectIssueCounts(activeIssues), [activeIssues])
  const codeFiles = useMemo(() => (result ? buildCodeFiles(result) : []), [result])

  const refreshHistory = useCallback(async () => {
    if (!authUser) {
      return
    }

    setHistoryLoading(true)
    setHistoryError(null)
    try {
      const response = await fetch(`${API_BASE_URL}/api/analysis/history`, { credentials: 'include' })
      if (!response.ok) {
        throw new Error(`History request failed with HTTP ${response.status}`)
      }

      setAnalysisHistory((await response.json()) as AnalysisHistorySummary[])
    } catch (caughtError) {
      setHistoryError(caughtError instanceof Error ? caughtError.message : 'Analysis history could not be loaded.')
    } finally {
      setHistoryLoading(false)
    }
  }, [authUser])

  const filteredIssues = useMemo(() => {
    if (!result) {
      return []
    }

    const normalizedQuery = query.trim().toLowerCase()

    return result.issues
      .filter((issue) => !hideSuppressedFindings || !issue.suppression)
      .filter((issue) => activeCategory === 'All' || issue.category === activeCategory)
      .filter((issue) => activeSeverity === 'All' || issue.severity === activeSeverity)
      .filter((issue) => activeProject === 'All' || (issue.projectName ?? 'Solution') === activeProject)
      .filter((issue) => {
        if (!normalizedQuery) {
          return true
        }

        return [
          issue.title,
          issue.description,
          issue.recommendation,
          issue.projectName,
          issue.filePath,
          issue.category,
          issue.severity,
        ]
          .filter(Boolean)
          .join(' ')
          .toLowerCase()
          .includes(normalizedQuery)
      })
      .sort((left, right) => {
        if (sortMode === 'Category') {
          return left.category.localeCompare(right.category) || severityRank[right.severity] - severityRank[left.severity]
        }

        if (sortMode === 'Project') {
          return (left.projectName ?? 'Solution').localeCompare(right.projectName ?? 'Solution') || severityRank[right.severity] - severityRank[left.severity]
        }

        if (sortMode === 'File') {
          return (left.filePath ?? '').localeCompare(right.filePath ?? '') || severityRank[right.severity] - severityRank[left.severity]
        }

        const severityDelta = severityRank[right.severity] - severityRank[left.severity]
        return severityDelta === 0 ? left.category.localeCompare(right.category) : severityDelta
      })
  }, [activeCategory, activeProject, activeSeverity, hideSuppressedFindings, query, result, sortMode])

  const selectedIssue = useMemo(() => {
    if (!result) {
      return null
    }

    return result.issues.find((issue) => issue.id === selectedIssueId) ?? null
  }, [result, selectedIssueId])

  const selectedFile = useMemo(() => {
    if (codeFiles.length === 0) {
      return null
    }

    return codeFiles.find((file) => file.id === selectedFileId) ?? codeFiles[0]
  }, [codeFiles, selectedFileId])

  useEffect(() => {
    let cancelled = false

    fetch(`${API_BASE_URL}/api/auth/me`, { credentials: 'include' })
      .then(async (response) => {
        if (!response.ok) {
          throw new Error(`Auth status request failed with ${response.status}`)
        }

        return response.json() as Promise<AuthMeResponse>
      })
      .then((auth) => {
        if (cancelled) {
          return
        }

        setAuthUser(auth.isAuthenticated ? normalizeAuthUser(auth.user) : null)
        if (auth.isAuthenticated && window.location.pathname === '/') {
          navigateToPath('/dashboard', true)
          setStartPage('Dashboard')
        } else if (
          !auth.isAuthenticated
          && ['/dashboard', '/analyze', '/reports', '/rules', '/settings'].some((path) => window.location.pathname.toLowerCase().startsWith(path))
        ) {
          navigateToPath('/', true)
          setStartPage('Home')
        }
      })
      .catch(() => {
        if (!cancelled) {
          setAuthUser(null)
        }
      })
      .finally(() => {
        if (!cancelled) {
          setAuthLoading(false)
        }
      })

    return () => {
      cancelled = true
    }
  }, [])

  useEffect(() => {
    if (!authUser) {
      setAnalysisHistory([])
      setHistoryError(null)
      setHistoryLoading(false)
      return
    }

    refreshHistory()
  }, [authUser, refreshHistory])

  useEffect(() => {
    if (!result) {
      setRuleCatalog([])
      setRuleCatalogError(null)
      return
    }

    let cancelled = false

    fetch(`${API_BASE_URL}/api/rules`)
      .then(async (response) => {
        if (!response.ok) {
          throw new Error(`Rule catalog request failed with ${response.status}`)
        }

        return response.json() as Promise<RuleDocumentation[]>
      })
      .then((rules) => {
        if (cancelled) {
          return
        }

        setRuleCatalog(rules)
        setRuleCatalogError(null)
      })
      .catch((fetchError) => {
        if (!cancelled) {
          setRuleCatalogError(fetchError instanceof Error ? fetchError.message : 'Rule catalog could not be loaded.')
        }
      })

    return () => {
      cancelled = true
    }
  }, [result])

  useEffect(() => {
    localStorage.setItem(densityStorageKey, density)
  }, [density])

  useEffect(() => {
    localStorage.setItem(commandBarStorageKey, commandBarMode)
  }, [commandBarMode])

  useEffect(() => {
    localStorage.setItem(gutterMarkersStorageKey, String(showGutterMarkers))
  }, [showGutterMarkers])

  useEffect(() => {
    localStorage.setItem(minimapMarkersStorageKey, String(showMinimapMarkers))
  }, [showMinimapMarkers])

  useEffect(() => {
    localStorage.setItem(editorFontStorageKey, editorFontFamily)
  }, [editorFontFamily])

  useEffect(() => {
    localStorage.setItem(openFirstFindingStorageKey, String(openFirstFinding))
  }, [openFirstFinding])

  useEffect(() => {
    localStorage.setItem(defaultSortStorageKey, defaultSortMode)
    setSortMode(defaultSortMode)
  }, [defaultSortMode])

  useEffect(() => {
    if (result) {
      safeSetLocalStorage(lastAnalysisResultStorageKey, JSON.stringify(sanitizeAnalysisResultForBrowserStorage(result)))
    } else {
      localStorage.removeItem(lastAnalysisResultStorageKey)
    }
  }, [result])

  useEffect(() => {
    localStorage.setItem(activePageStorageKey, activePage)
  }, [activePage])

  useEffect(() => {
    if (selectedIssueId) {
      localStorage.setItem(selectedIssueStorageKey, selectedIssueId)
    } else {
      localStorage.removeItem(selectedIssueStorageKey)
    }
  }, [selectedIssueId])

  useEffect(() => {
    if (selectedFileId) {
      localStorage.setItem(selectedFileStorageKey, selectedFileId)
    } else {
      localStorage.removeItem(selectedFileStorageKey)
    }
  }, [selectedFileId])

  useEffect(() => {
    localStorage.setItem(activeCategoryStorageKey, activeCategory)
  }, [activeCategory])

  useEffect(() => {
    localStorage.setItem(activeSeverityStorageKey, activeSeverity)
  }, [activeSeverity])

  useEffect(() => {
    localStorage.setItem(activeProjectStorageKey, activeProject)
  }, [activeProject])

  useEffect(() => {
    localStorage.setItem(findingQueryStorageKey, query)
  }, [query])

  useEffect(() => {
    localStorage.setItem(exportFormatStorageKey, exportFormat)
  }, [exportFormat])

  useEffect(() => {
    localStorage.setItem(includeSourcePreviewStorageKey, String(includeSourcePreview))
  }, [includeSourcePreview])

  useEffect(() => {
    localStorage.setItem(findingDispositionsStorageKey, JSON.stringify(findingDispositions))
  }, [findingDispositions])

  useEffect(() => {
    localStorage.setItem(hideSuppressedStorageKey, String(hideSuppressedFindings))
  }, [hideSuppressedFindings])

  useEffect(() => {
    if (!result) {
      setSelectedIssueId(null)
      setSelectedRuleId(null)
      return
    }

    setSelectedIssueId((current) => {
      if (current && result.issues.some((issue) => issue.id === current)) {
        return current
      }

      return openFirstFinding ? result.issues[0]?.id ?? null : null
    })
    setSelectedFileId((current) => {
      if (current && codeFiles.some((file) => file.id === current)) {
        return current
      }

      const firstIssueFileId = openFirstFinding ? getFileId(result.issues[0]?.filePath) : ''
      return firstIssueFileId || codeFiles[0]?.id || null
    })
  }, [codeFiles, openFirstFinding, result])

  useEffect(() => {
    if (ruleCatalog.length === 0) {
      return
    }

    setSelectedRuleId((current) => {
      if (current && (!result || result.issues.some((issue) => getRuleId(issue) === current))) {
        return current
      }

      return getHighestRiskActiveRuleId(ruleCatalog, result?.issues ?? []) ?? current ?? ruleCatalog[0]?.ruleId ?? null
    })
  }, [result, ruleCatalog])

  const canAnalyze = zipFile !== null

  async function analyze(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    await runAnalysis()
  }

  async function analyzeSample() {
    setZipFile(null)
    await runAnalysis('sample', '', null)
  }

  async function analyzeGitHubRepository(repositoryInput: string) {
    if (!repositoryInput.trim()) {
      setError('Enter a GitHub repository URL or owner/repo.')
      return
    }

    setIsLoading(true)
    setError(null)
    setResult(null)
    setSelectedIssueId(null)
    setSelectedRuleId(null)
    setSelectedFileId(null)
    setActiveCategory('All')
    setActiveSeverity('All')
    setActiveProject('All')
    setSortMode(defaultSortMode)
    setQuery('')
    setZipFile(null)

    try {
      const response = await fetch(`${API_BASE_URL}/api/github/analyze-repo`, {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ repository: repositoryInput, repositoryUrl: repositoryInput }),
      })

      if (!response.ok) {
        const message = await response.text()
        throw new Error(message || `GitHub repository analysis failed with HTTP ${response.status}`)
      }

      const analysis = (await response.json()) as AnalysisResult
      setResult(analysis)
      setStartPage('Analyze')
      setActivePage('Code Explorer')
      if (authUser) {
        await refreshHistory()
      }
    } catch (caughtError) {
      setError(caughtError instanceof Error ? caughtError.message : 'GitHub repository analysis failed.')
    } finally {
      setIsLoading(false)
    }
  }

  async function runAnalysis(requestMode: AnalysisMode | 'sample' = 'zip', requestPath = solutionPath, requestFile = zipFile) {
    const hasInput = requestMode === 'path' ? requestPath.trim().length > 0 : requestFile !== null
    if (requestMode !== 'sample' && !hasInput) {
      return
    }

    setIsLoading(true)
    setError(null)
    setResult(null)
    setSelectedIssueId(null)
    setSelectedRuleId(null)
    setSelectedFileId(null)
    setActiveCategory('All')
    setActiveSeverity('All')
    setActiveProject('All')
    setSortMode(defaultSortMode)
    setQuery('')

    try {
      const response =
        requestMode === 'sample'
          ? await fetch(`${API_BASE_URL}/api/analysis/analyze-sample`, {
              method: 'POST',
              credentials: 'include',
            })
          : requestMode === 'path'
            ? await fetch(`${API_BASE_URL}/api/analysis/analyze-path`, {
                method: 'POST',
                credentials: 'include',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ solutionPath: requestPath.trim() }),
              })
          : await analyzeZip(requestFile)

      if (!response.ok) {
        const message = await response.text()
        throw new Error(message || `Analysis failed with HTTP ${response.status}`)
      }

      const analysis = (await response.json()) as AnalysisResult
      setResult(analysis)
      setStartPage('Analyze')
      setActivePage('Code Explorer')
      if (authUser) {
        await refreshHistory()
      }
    } catch (caughtError) {
      setError(caughtError instanceof Error ? caughtError.message : 'Analysis failed.')
    } finally {
      setIsLoading(false)
    }
  }

  async function analyzeZip(file: File | null) {
    if (!file) {
      throw new Error('Choose a zip archive before starting analysis.')
    }

    const formData = new FormData()
    formData.append('file', file)

    return fetch(`${API_BASE_URL}/api/analysis/analyze-zip`, {
      method: 'POST',
      credentials: 'include',
      body: formData,
    })
  }

  function loginWithGitHub() {
    window.location.href = `${API_BASE_URL}/api/auth/github-login`
  }

  async function logout() {
    clearCachedAnalysisState()
    await fetch(`${API_BASE_URL}/api/auth/logout`, {
      credentials: 'include',
      method: 'POST',
    })
    setAuthUser(null)
    setResult(null)
    clearCachedAnalysisState()
    setSelectedIssueId(null)
    setSelectedFileId(null)
    setStartPage('Home')
    navigateToPath('/')
  }

  function setStartPageAndPath(page: StartPage) {
    setStartPage(page)
    navigateToPath(getPathForStartPage(page))
  }

  function openAnalyzeTab(tab: AnalyzeTab) {
    setAnalyzeTab(tab)
    setStartPageAndPath('Analyze')
  }

  async function openHistoryReport(id: string) {
    setHistoryError(null)
    try {
      const response = await fetch(`${API_BASE_URL}/api/analysis/history/${encodeURIComponent(id)}`, { credentials: 'include' })
      if (!response.ok) {
        throw new Error(`Report request failed with HTTP ${response.status}`)
      }

      const detail = (await response.json()) as AnalysisHistoryDetail
      setResult(detail.result)
      setSelectedIssueId(detail.result.issues[0]?.id ?? null)
      setSelectedRuleId(getHighestRiskActiveRuleId(ruleCatalog, detail.result.issues) ?? null)
      setSelectedFileId(getFileId(detail.result.issues[0]?.filePath) || buildCodeFiles(detail.result)[0]?.id || null)
      setActivePage('Overview')
      setStartPage('Reports')
      navigateToPath('/reports')
    } catch (caughtError) {
      setHistoryError(caughtError instanceof Error ? caughtError.message : 'Saved report could not be opened.')
    }
  }

  async function rerunHistoryReport(id: string) {
    setIsLoading(true)
    setHistoryError(null)
    setError(null)
    try {
      const response = await fetch(`${API_BASE_URL}/api/analysis/history/${encodeURIComponent(id)}/rerun`, {
        credentials: 'include',
        method: 'POST',
      })
      if (!response.ok) {
        const message = await response.text()
        throw new Error(message || `Re-run failed with HTTP ${response.status}`)
      }

      const analysis = (await response.json()) as AnalysisResult
      setResult(analysis)
      setActivePage('Code Explorer')
      setStartPage('Reports')
      await refreshHistory()
    } catch (caughtError) {
      setHistoryError(caughtError instanceof Error ? caughtError.message : 'Saved report could not be re-run.')
    } finally {
      setIsLoading(false)
    }
  }

  async function deleteHistoryReport(id: string) {
    setHistoryError(null)
    try {
      const response = await fetch(`${API_BASE_URL}/api/analysis/history/${encodeURIComponent(id)}`, {
        credentials: 'include',
        method: 'DELETE',
      })
      if (!response.ok && response.status !== 404) {
        throw new Error(`Delete failed with HTTP ${response.status}`)
      }

      setAnalysisHistory((current) => current.filter((item) => item.id !== id))
    } catch (caughtError) {
      setHistoryError(caughtError instanceof Error ? caughtError.message : 'Saved report could not be deleted.')
    }
  }

  async function exportHistoryReport(id: string, format: ExportFormat) {
    setHistoryError(null)
    try {
      const response = await fetch(`${API_BASE_URL}/api/analysis/history/${encodeURIComponent(id)}`, { credentials: 'include' })
      if (!response.ok) {
        throw new Error(`Report request failed with HTTP ${response.status}`)
      }

      const detail = (await response.json()) as AnalysisHistoryDetail
      exportReport(detail.result, {
        codeFiles: buildCodeFiles(detail.result),
        dispositions: findingDispositions,
        format,
        includeSourcePreview,
      })
    } catch (caughtError) {
      setHistoryError(caughtError instanceof Error ? caughtError.message : 'Saved report could not be exported.')
    }
  }

  function onFileChange(event: ChangeEvent<HTMLInputElement>) {
    setZipFile(event.target.files?.[0] ?? null)
  }

  function openIssueInCode(issueId: string) {
    const issue = result?.issues.find((candidate) => candidate.id === issueId)
    setSelectedIssueId(issueId)
    if (issue?.filePath) {
      setSelectedFileId(getFileId(issue.filePath))
    }
    setActivePage('Code Explorer')
  }

  function openRuleDocumentation(ruleId: string) {
    setSelectedRuleId(ruleId)
    setActivePage('Rule Explorer')
  }

  function selectFile(fileId: string) {
    setSelectedFileId(fileId)
    const firstIssueInFile = result?.issues.find((issue) => getFileId(issue.filePath) === fileId)
    setSelectedIssueId(firstIssueInFile?.id ?? null)
  }

  function selectRelativeFinding(direction: 'previous' | 'next') {
    if (!selectedFile || !result) {
      return
    }

    const fileIssues = result.issues
      .filter((issue) => getFileId(issue.filePath) === selectedFile.id)
      .sort((left, right) => (left.lineNumber ?? 0) - (right.lineNumber ?? 0))

    if (fileIssues.length === 0) {
      return
    }

    const currentIndex = Math.max(
      0,
      fileIssues.findIndex((issue) => issue.id === selectedIssueId),
    )
    const nextIndex =
      direction === 'next' ? Math.min(fileIssues.length - 1, currentIndex + 1) : Math.max(0, currentIndex - 1)
    setSelectedIssueId(fileIssues[nextIndex].id)
  }

  function exportCurrentReport(formatOverride?: ExportFormat) {
    exportReport(result, { codeFiles, dispositions: findingDispositions, format: formatOverride ?? exportFormat, includeSourcePreview })
  }

  async function updateFindingDisposition(issue: AnalysisIssue, disposition: FindingDisposition) {
    if (!result) {
      return
    }

    if (!result.analysisRunId) {
      setError('Suppressions are not available for this analysis source yet.')
      return
    }

    const key = getFindingDispositionKey(result.solutionName, issue)

    try {
      if (disposition === 'Open') {
        if (!issue.suppression) {
          setFindingDispositions((current) => {
            const next = { ...current }
            delete next[key]
            return next
          })
          return
        }

        const response = await fetch(
          `${API_BASE_URL}/api/suppressions/${encodeURIComponent(issue.suppression.id)}?analysisRunId=${encodeURIComponent(result.analysisRunId)}`,
          { credentials: 'include', method: 'DELETE' },
        )

        if (!response.ok && response.status !== 404) {
          throw new Error(`Suppression removal failed with HTTP ${response.status}`)
        }

        setResult((current) => current ? removeIssueSuppression(current, issue.id) : current)
        setFindingDispositions((current) => {
          const next = { ...current }
          delete next[key]
          return next
        })
        return
      }

      const response = await fetch(`${API_BASE_URL}/api/suppressions`, {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          analysisRunId: result.analysisRunId,
          findingId: issue.id,
          reason: `${disposition} set from the DotDet workbench.`,
          status: disposition,
        }),
      })

      if (!response.ok) {
        throw new Error(`Suppression creation failed with HTTP ${response.status}`)
      }

      const suppression = (await response.json()) as RepositorySuppression
      setResult((current) => current ? applyIssueSuppression(current, issue.id, suppression) : current)
      setFindingDispositions((current) => ({
        ...current,
        [key]: disposition,
      }))
    } catch (suppressionError) {
      setError(suppressionError instanceof Error ? suppressionError.message : 'Repository suppression update failed.')
    }
  }

  return (
    <AppShell
      activePage={activePage}
      authLoading={authLoading}
      authUser={authUser}
      commandBarMode={commandBarMode}
      density={density}
      isLoading={isLoading}
      onExportReport={exportCurrentReport}
      onLogin={loginWithGitHub}
      onLogout={logout}
      onPageChange={setActivePage}
      onRunAnalysisAgain={() => {
        setResult(null)
        setSelectedIssueId(null)
        setSelectedFileId(null)
        setError(null)
        setStartPageAndPath(authUser ? 'Dashboard' : 'Home')
      }}
      onStartPageChange={setStartPageAndPath}
      onOpenSettings={() => {
        if (result) {
          setActivePage('Settings')
        } else {
          setStartPageAndPath('Settings')
        }
      }}
      result={result}
      selectedFile={selectedFile}
      severityCounts={severityCounts}
      startPage={startPage}
    >
      {authLoading ? (
        <AuthLoadingPage />
      ) : !result ? (
        startPage === 'Settings' ? (
          <SettingsPage
            commandBarMode={commandBarMode}
            defaultSortMode={defaultSortMode}
            density={density}
            editorFontFamily={editorFontFamily}
            exportFormat={exportFormat}
            includeSourcePreview={includeSourcePreview}
            onBack={() => setStartPageAndPath(authUser ? 'Dashboard' : 'Home')}
            onCommandBarModeChange={setCommandBarMode}
            onDefaultSortModeChange={setDefaultSortMode}
            onDensityChange={setDensity}
            onEditorFontFamilyChange={setEditorFontFamily}
            onExportFormatChange={setExportFormat}
            onIncludeSourcePreviewChange={setIncludeSourcePreview}
            onOpenFirstFindingChange={setOpenFirstFinding}
            onShowGutterMarkersChange={setShowGutterMarkers}
            onShowMinimapMarkersChange={setShowMinimapMarkers}
            openFirstFinding={openFirstFinding}
            showGutterMarkers={showGutterMarkers}
            showMinimapMarkers={showMinimapMarkers}
          />
        ) : startPage === 'Docs' ? (
          <DocsPage />
        ) : startPage === 'Contact' ? (
          <ContactPage />
        ) : !authUser && startPage === 'Changelog' ? (
          <ChangelogPage />
        ) : !authUser ? (
          <HomePage
            onLogin={loginWithGitHub}
            onAnalyzeSample={analyzeSample}
          />
        ) : startPage === 'Dashboard' ? (
          <DashboardPage
            authUser={authUser}
            history={analysisHistory}
            historyError={historyError}
            historyLoading={historyLoading}
            isLoading={isLoading}
            onAnalyzeSample={analyzeSample}
            onExportHistory={exportHistoryReport}
            onOpenAnalyze={openAnalyzeTab}
            onOpenHistory={openHistoryReport}
            onRerunHistory={rerunHistoryReport}
          />
        ) : startPage === 'Analyze' ? (
          <AnalyzePage
            activeTab={analyzeTab}
            canAnalyze={canAnalyze}
            error={error}
            isLoading={isLoading}
            onActiveTabChange={setAnalyzeTab}
            onAnalyzeGitHubRepository={analyzeGitHubRepository}
            onAnalyzeSample={analyzeSample}
            onClearCachedAnalysisState={() => {
              clearCachedAnalysisState()
              setResult(null)
              setSelectedIssueId(null)
              setSelectedFileId(null)
            }}
            onFileChange={onFileChange}
            onSubmit={analyze}
            zipFile={zipFile}
          />
        ) : startPage === 'Reports' ? (
          <ReportsPage
            history={analysisHistory}
            historyError={historyError}
            historyLoading={historyLoading}
            isLoading={isLoading}
            onDelete={deleteHistoryReport}
            onExport={exportHistoryReport}
            onOpenAnalyze={openAnalyzeTab}
            onRefresh={refreshHistory}
            onRerun={rerunHistoryReport}
            onView={openHistoryReport}
          />
        ) : (
          <HomePage
            onLogin={loginWithGitHub}
            onAnalyzeSample={analyzeSample}
          />
        )
      ) : (
        <div className="flex min-h-0 flex-1 flex-col">
          {activePage === 'Overview' ? (
            <OverviewPage
              projectCount={result.projectGraph.projects.length}
              result={summaryResult ?? result}
              severityCounts={severityCounts}
              onOpenArchitecture={() => setActivePage('Architecture')}
              onOpenFindings={() => setActivePage('Findings')}
              onOpenIssue={openIssueInCode}
            />
          ) : null}

          {activePage === 'Findings' ? (
            <FindingsPage
              activeCategory={activeCategory}
              activeProject={activeProject}
              activeSeverity={activeSeverity}
              filteredIssues={filteredIssues}
              getDisposition={(issue) => getFindingDisposition(result.solutionName, issue, findingDispositions)}
              hideSuppressed={hideSuppressedFindings}
              onCategoryChange={setActiveCategory}
              onHideSuppressedChange={setHideSuppressedFindings}
              onProjectChange={setActiveProject}
              onQueryChange={setQuery}
              onSeverityChange={setActiveSeverity}
              onSortModeChange={setSortMode}
              onSelectIssue={openIssueInCode}
              projects={result.projectGraph.projects}
              query={query}
              selectedIssueId={selectedIssue?.id ?? null}
              sortMode={sortMode}
              totalCount={result.issues.length}
            />
          ) : null}

          {activePage === 'Code Explorer' ? (
            <CodeExplorerPage
              editorFontFamily={editorFontFamily}
              files={codeFiles}
              onNextFinding={() => selectRelativeFinding('next')}
              onPreviousFinding={() => selectRelativeFinding('previous')}
              onOpenRule={openRuleDocumentation}
              onSelectFile={selectFile}
              onSelectIssue={setSelectedIssueId}
              projectIssueCounts={projectIssueCounts}
              result={result}
              selectedFile={selectedFile}
              selectedIssue={selectedIssue}
              showGutterMarkers={showGutterMarkers}
              showMinimapMarkers={showMinimapMarkers}
              relatedIssues={getRelatedFindings(selectedIssue, result.issues)}
              getDisposition={(issue) => getFindingDisposition(result.solutionName, issue, findingDispositions)}
              onUpdateDisposition={updateFindingDisposition}
            />
          ) : null}

          {activePage === 'Architecture' ? (
            <ArchitecturePage
              result={result}
              onOpenIssue={openIssueInCode}
            />
          ) : null}

          {activePage === 'Rule Explorer' ? (
            <RuleExplorerPage
              currentIssues={result.issues}
              error={ruleCatalogError}
              onOpenIssue={openIssueInCode}
              onSelectRule={setSelectedRuleId}
              rules={ruleCatalog}
              selectedRuleId={selectedRuleId}
            />
          ) : null}

          {activePage === 'Settings' ? (
            <SettingsPage
              commandBarMode={commandBarMode}
              defaultSortMode={defaultSortMode}
              density={density}
              editorFontFamily={editorFontFamily}
              exportFormat={exportFormat}
              includeSourcePreview={includeSourcePreview}
              onCommandBarModeChange={setCommandBarMode}
              onDefaultSortModeChange={setDefaultSortMode}
              onDensityChange={setDensity}
              onEditorFontFamilyChange={setEditorFontFamily}
              onExportFormatChange={setExportFormat}
              onIncludeSourcePreviewChange={setIncludeSourcePreview}
              onOpenFirstFindingChange={setOpenFirstFinding}
              onShowGutterMarkersChange={setShowGutterMarkers}
              onShowMinimapMarkersChange={setShowMinimapMarkers}
              openFirstFinding={openFirstFinding}
              showGutterMarkers={showGutterMarkers}
              showMinimapMarkers={showMinimapMarkers}
            />
          ) : null}

          {activePage === 'Docs' ? (
            <DocsPage />
          ) : null}

          {activePage === 'Contact' ? (
            <ContactPage />
          ) : null}
        </div>
      )}
    </AppShell>
  )
}

function AppShell({
  activePage,
  authLoading,
  authUser,
  children,
  commandBarMode,
  density,
  isLoading,
  onExportReport,
  onLogin,
  onLogout,
  onOpenSettings,
  onPageChange,
  onRunAnalysisAgain,
  onStartPageChange,
  result,
  selectedFile,
  severityCounts,
  startPage,
}: {
  activePage: AppPage
  authLoading: boolean
  authUser: AuthUser | null
  children: React.ReactNode
  commandBarMode: CommandBarMode
  density: DensityMode
  isLoading: boolean
  onExportReport: (format?: ExportFormat) => void
  onLogin: () => void
  onLogout: () => void
  onOpenSettings: () => void
  onPageChange: (page: AppPage) => void
  onRunAnalysisAgain: () => void
  onStartPageChange: (page: StartPage) => void
  result: AnalysisResult | null
  selectedFile: CodeFile | null
  severityCounts: Record<Severity, number>
  startPage: StartPage
}) {
  const commandBarClass =
    commandBarMode === 'Expanded'
      ? 'flex flex-col gap-2 px-4 py-2 lg:flex-row lg:items-center lg:justify-between'
      : 'flex flex-col gap-1.5 px-3 py-1 lg:flex-row lg:items-center lg:justify-between'
  const commandButtonClass =
    commandBarMode === 'Expanded'
      ? 'inline-flex h-8 items-center gap-2 border px-3 text-sm font-medium transition'
      : 'inline-flex h-6 items-center gap-1.5 border px-2 text-xs font-medium transition'
  const commandIconClass = commandBarMode === 'Expanded' ? 'h-4 w-4' : 'h-3.5 w-3.5'
  const navItems: Array<{ page: AppPage; icon: LucideIcon; label: string }> = [
    { page: 'Overview', icon: LayoutDashboard, label: 'Overview' },
    { page: 'Findings', icon: ListChecks, label: 'Findings' },
    { page: 'Code Explorer', icon: Code2, label: 'Code Explorer' },
    { page: 'Architecture', icon: GitBranch, label: 'Architecture' },
    { page: 'Rule Explorer', icon: FileText, label: 'Rule Explorer' },
  ]
  const startItems: Array<{ page: StartPage; icon: LucideIcon; label: string }> = authUser
    ? [
        { page: 'Dashboard', icon: LayoutDashboard, label: 'Dashboard' },
        { page: 'Analyze', icon: FolderSearch, label: 'Analyze' },
        { page: 'Reports', icon: FileText, label: 'Reports' },
      ]
    : [
        { page: 'Home', icon: LayoutDashboard, label: 'Home' },
      ]
  const issueCount = result?.issues.length ?? 0
  const statusText = isLoading ? 'Analyzing' : result ? 'Analysis ready' : 'Ready'
  const displayName = authUser?.displayName || authUser?.githubUsername || 'GitHub user'
  const githubHandle = authUser?.githubUsername?.replace(/^@/, '').trim()
  const [isExportMenuOpen, setExportMenuOpen] = useState(false)
  const [isProductMenuOpen, setProductMenuOpen] = useState(false)

  const isPublicPage = startPage === 'Home' || startPage === 'Docs' || startPage === 'Contact' || startPage === 'Changelog'

  if (!result && !authUser && isPublicPage) {
    return (
      <main className={`night-mode det-public-shell min-h-screen overflow-auto text-slate-100 ${density === 'Comfortable' ? 'density-comfortable' : 'density-compact'}`}>
        <header className="det-public-topbar">
          <div className="det-public-topbar-inner">
            <a
              href="/"
              className="det-public-brand"
              aria-label="DotDet home"
              onClick={(event) => {
                event.preventDefault()
                onStartPageChange('Home')
              }}
            >
              <img src="/dotdet-logo.png" alt=".DET logo" className="h-8 w-8 object-contain" />
              <div className="min-w-0">
                <div className="text-sm font-semibold leading-5 text-slate-100">DotDet</div>
                <div className="truncate text-[11px] text-slate-500">.NET Development Engineering Toolkit</div>
              </div>
            </a>
            <nav className="det-public-nav" aria-label="Landing navigation">
              <div
                className={`det-product-menu ${isProductMenuOpen ? 'det-product-menu-open' : ''}`}
                onBlur={(event) => {
                  if (!event.currentTarget.contains(event.relatedTarget)) {
                    setProductMenuOpen(false)
                  }
                }}
                onKeyDown={(event) => {
                  if (event.key === 'Escape') {
                    setProductMenuOpen(false)
                  }
                }}
              >
                <button
                  type="button"
                  className="det-public-nav-link det-product-menu-trigger"
                  aria-expanded={isProductMenuOpen}
                  aria-haspopup="menu"
                  onClick={() => setProductMenuOpen((current) => !current)}
                >
                  Product
                  <ChevronDown className="h-3.5 w-3.5" aria-hidden="true" />
                </button>
                <div className="det-product-menu-panel" role="menu">
                  <a
                    href="/"
                    className="det-product-menu-item"
                    role="menuitem"
                    onClick={(event) => {
                      event.preventDefault()
                      setProductMenuOpen(false)
                      onStartPageChange('Home')
                    }}
                  >
                    <Code2 className="det-product-menu-icon" aria-hidden="true" />
                    <span>
                      <span className="det-product-menu-label">DotDet</span>
                      <span className="det-product-menu-description">Production-readiness analysis</span>
                    </span>
                  </a>
                  <a href={SCHEMA_ARCHITECT_URL} className="det-product-menu-item" role="menuitem" rel="noreferrer">
                    <Database className="det-product-menu-icon" aria-hidden="true" />
                    <span>
                      <span className="det-product-menu-label">Schema Architect</span>
                      <span className="det-product-menu-description">Schema design and .NET foundations</span>
                    </span>
                  </a>
                </div>
              </div>
              <a
                href="/docs"
                className={`det-public-nav-link ${startPage === 'Docs' ? 'det-public-nav-link-active' : ''}`}
                aria-current={startPage === 'Docs' ? 'page' : undefined}
                onClick={(event) => {
                  event.preventDefault()
                  onStartPageChange('Docs')
                }}
              >
                Docs
              </a>
              <a
                href="/changelog"
                className={`det-public-nav-link ${startPage === 'Changelog' ? 'det-public-nav-link-active' : ''}`}
                aria-current={startPage === 'Changelog' ? 'page' : undefined}
                onClick={(event) => {
                  event.preventDefault()
                  onStartPageChange('Changelog')
                }}
              >
                Changelog
              </a>
              <a href={DOTDET_REPOSITORY_URL} className="det-public-nav-link" rel="noreferrer">GitHub</a>
            </nav>
            {authLoading ? (
              <span className="text-xs text-slate-500">Checking session</span>
            ) : (
              <button type="button" onClick={onLogin} className="det-public-login-button">
                <GitHubMark className="h-3.5 w-3.5" />
                Login with GitHub
              </button>
            )}
          </div>
        </header>
        <div className="det-public-content">
          {children}
        </div>
        <PublicFooter onNavigate={onStartPageChange} />
      </main>
    )
  }

  return (
    <main className={`night-mode min-h-screen overflow-hidden text-slate-100 ${density === 'Comfortable' ? 'density-comfortable' : 'density-compact'}`}>
      <div className="det-ide-shell">
        <aside className="det-left-sidebar">
          <div className="det-sidebar-brand">
            <img
              src="/dotdet-logo.png"
              alt=".DET logo"
              className="h-8 w-8 border border-black/20 bg-transparent object-contain"
            />
            <div className="min-w-0">
              <div className="text-sm font-semibold leading-5 text-slate-100">DotDet</div>
              <div className="truncate text-[11px] text-slate-500">.NET Development Engineering</div>
            </div>
          </div>

          <nav className="det-sidebar-nav" aria-label="Primary navigation">
            <div className="det-sidebar-section-label">{result ? 'Analysis' : 'Workspace'}</div>
            {result ? (
              navItems.map((item) => (
                <button
                  key={item.page}
                  type="button"
                  aria-label={item.label}
                  title={item.label}
                  onClick={() => onPageChange(item.page)}
                  className={`det-sidebar-nav-item ${activePage === item.page ? 'det-sidebar-nav-item-active' : ''}`}
                >
                  <item.icon className="h-4 w-4" aria-hidden="true" />
                  <span>{item.label}</span>
                  {item.page === 'Findings' && issueCount > 0 ? <span className="det-sidebar-nav-count">{issueCount}</span> : null}
                </button>
              ))
            ) : (
              startItems.map((item) => (
                <button
                  key={item.page}
                  type="button"
                  aria-label={item.label}
                  title={item.label}
                  onClick={() => onStartPageChange(item.page)}
                  className={`det-sidebar-nav-item ${startPage === item.page ? 'det-sidebar-nav-item-active' : ''}`}
                >
                  <item.icon className="h-4 w-4" aria-hidden="true" />
                  <span>{item.label}</span>
                </button>
              ))
            )}
          </nav>

          <div className="det-sidebar-utility">
            {authUser ? (
              <div className="det-sidebar-account" title={githubHandle ? `@${githubHandle}` : displayName}>
                {authUser.avatarUrl ? (
                  <img src={authUser.avatarUrl} alt="" className="det-sidebar-account-avatar" />
                ) : (
                  <Circle className="det-sidebar-account-avatar det-sidebar-account-fallback" aria-hidden="true" />
                )}
                <div className="min-w-0">
                  <div className="truncate text-xs font-semibold text-slate-100">{displayName}</div>
                  <div className="truncate text-[11px] text-slate-400">{githubHandle ? `@${githubHandle}` : 'GitHub connected'}</div>
                </div>
              </div>
            ) : null}
            {result ? (
              <>
                <button type="button" aria-label="New Analysis" title="New Analysis" onClick={onRunAnalysisAgain} className="det-sidebar-nav-item">
                  <RefreshCw className="h-4 w-4" aria-hidden="true" />
                  <span>New Analysis</span>
                </button>
              </>
            ) : null}
            {result || authUser ? (
              <button
                type="button"
                aria-label="Settings"
                title="Settings"
                onClick={onOpenSettings}
                className={`det-sidebar-nav-item ${(result && activePage === 'Settings') || (!result && startPage === 'Settings') ? 'det-sidebar-nav-item-active' : ''}`}
              >
                <Settings className="h-4 w-4" aria-hidden="true" />
                <span>Settings</span>
              </button>
            ) : null}
            {authUser ? (
              <button
                type="button"
                aria-label="Logout"
                title="Logout"
                onClick={onLogout}
                className="det-sidebar-nav-item det-sidebar-logout"
              >
                <LogOut className="h-4 w-4" aria-hidden="true" />
                <span>Logout</span>
              </button>
            ) : null}
          </div>
        </aside>

        <section className="det-main-shell">
          <header className="det-command-bar">
            <div className={commandBarClass}>
              <div className="min-w-0">
                <p className="truncate text-xs text-slate-400">
                  {result ? `${result.solutionName} - analyzed ${new Date(result.analyzedAt).toLocaleString()}` : statusText}
                </p>
              </div>
            <div className="flex flex-wrap items-center gap-1.5">
              {authLoading ? (
                <span className="px-2 text-xs text-slate-500">Checking session</span>
              ) : (
                <>
                  {authUser ? null : (
                    <button type="button" onClick={onLogin} className={commandButtonClass}>
                      <GitHubMark className={commandIconClass} />
                      Login with GitHub
                    </button>
                  )}
                  <button
                    type="button"
                    onClick={() => {
                      if (result) {
                        onPageChange('Contact')
                      } else {
                        onStartPageChange('Contact')
                      }
                    }}
                    className={commandButtonClass}
                  >
                    <CircleHelp className={commandIconClass} aria-hidden="true" />
                    Help
                  </button>
                  <button
                    type="button"
                    onClick={() => {
                      if (result) {
                        onPageChange('Docs')
                      } else {
                        onStartPageChange('Docs')
                      }
                    }}
                    className={commandButtonClass}
                  >
                    <BookOpen className={commandIconClass} aria-hidden="true" />
                    About
                  </button>
                </>
              )}
              {result ? (
                <>
                <button
                  type="button"
                  onClick={onRunAnalysisAgain}
                  className={commandButtonClass}
                >
                  <RefreshCw className={commandIconClass} aria-hidden="true" />
                  Run Again
                </button>
                <div className="relative">
                  <button
                    type="button"
                    aria-expanded={isExportMenuOpen}
                    aria-haspopup="menu"
                    aria-label="Export Report"
                    onClick={() => setExportMenuOpen((current) => !current)}
                    className={commandButtonClass}
                  >
                    <Download className={commandIconClass} aria-hidden="true" />
                    Export
                    <ChevronDown className={commandIconClass} aria-hidden="true" />
                  </button>
                  {isExportMenuOpen ? (
                    <div
                      role="menu"
                      aria-label="Choose export format"
                      className="absolute right-0 top-full z-50 mt-1 min-w-44 border border-slate-700 bg-[#1b1f1d] py-1 shadow-lg"
                    >
                      {(['HTML', 'Markdown', 'JSON'] as const).map((format) => (
                    <button
                      key={format}
                      type="button"
                      role="menuitem"
                      onClick={() => {
                        setExportMenuOpen(false)
                        onExportReport(format)
                      }}
                      className="flex w-full items-center gap-2 px-3 py-1.5 text-left text-xs text-slate-200 transition hover:bg-[#252a26] hover:text-slate-50"
                    >
                      <Download className={commandIconClass} aria-hidden="true" />
                      {format === 'HTML' ? 'HTML report' : format === 'Markdown' ? 'Markdown report' : 'JSON data'}
                    </button>
                      ))}
                    </div>
                  ) : null}
                </div>
                </>
              ) : null}
            </div>
          </div>
          </header>

          <div className="det-work-area">
            {children}
          </div>

          <footer className="det-status-bar">
            <span>{statusText}</span>
            {result ? (
              <>
                <span>{severityCounts.Error + severityCounts.Critical} errors</span>
                <span>{severityCounts.Warning} warnings</span>
                <span>{issueCount} findings</span>
                {selectedFile ? <span className="truncate">{selectedFile.name}</span> : null}
              </>
            ) : (
              <span>.NET Development Engineering Toolkit</span>
            )}
          </footer>
        </section>
      </div>
    </main>
  )
}

const landingProofRows = [
  {
    number: '01',
    icon: FileSearch,
    title: 'Evidence-first findings',
    body: 'Project, file, line, confidence, detection method, and recommendation.',
  },
  {
    number: '02',
    icon: Braces,
    title: 'ASP.NET Core production focus',
    body: 'Architecture boundaries, DI reliability, EF Core migrations, security configuration, and API readiness.',
  },
  {
    number: '03',
    icon: FileDown,
    title: 'Review-ready output',
    body: 'Saved history plus HTML, Markdown, and JSON exports for engineering review.',
  },
] as const

const landingCapabilities = [
  {
    icon: Blocks,
    title: 'Architecture boundaries',
    body: 'Layering, project dependencies, and cross-boundary violations.',
  },
  {
    icon: ServerCog,
    title: 'Dependency Injection',
    body: 'Service registration, lifetimes, and unresolved dependencies.',
  },
  {
    icon: Database,
    title: 'EF Core / migrations',
    body: 'DbContext setup, migration posture, and data-access readiness.',
  },
  {
    icon: ShieldCheck,
    title: 'Security & configuration',
    body: 'Auth middleware, secrets, HTTPS, and configuration hardening.',
  },
  {
    icon: Code2,
    title: 'API readiness',
    body: 'Health checks, versioning, OpenAPI, and production endpoints.',
  },
  {
    icon: FolderGit2,
    title: 'GitHub repository analysis',
    body: 'Analyze GitHub repositories without cloning locally; private repositories require explicit access.',
  },
] as const

const landingWorkflow = [
  {
    number: '01',
    title: 'Connect a solution',
    body: 'Choose a GitHub repository, upload a ZIP, or run the bundled sample.',
  },
  {
    number: '02',
    title: 'Inspect the evidence',
    body: 'Review production risks against the exact project, file, symbol, and line.',
  },
  {
    number: '03',
    title: 'Apply the guidance',
    body: 'Move from finding to recommended pattern, implementation, and Microsoft documentation.',
  },
  {
    number: '04',
    title: 'Share the assessment',
    body: 'Export an engineering report for pull requests, release reviews, and remediation planning.',
  },
] as const

function HomePage({
  onAnalyzeSample,
  onLogin,
}: {
  onAnalyzeSample: () => void
  onLogin: () => void
}) {
  return (
    <section id="product" className="det-public-landing">
      <div className="det-landing-hero-band">
        <div className="det-landing-page det-landing-page-hero">
          <div className="det-landing-hero">
            <div className="det-landing-copy">
              <div className="det-landing-eyebrow"><span />Production assurance for ASP.NET Core</div>
              <h1 className="det-landing-title">Production readiness, grounded in your code.</h1>
              <p className="det-landing-subtitle">
                DotDet turns Roslyn symbols, MSBuild project structure, configuration, and framework usage into an engineering assessment your team can trust and act on.
              </p>
              <div className="det-landing-actions">
                <button type="button" onClick={onLogin} className="det-primary-cta det-landing-primary-cta">
                  <GitHubMark className="h-4 w-4" />
                  Login with GitHub
                  <ChevronRight className="h-4 w-4" aria-hidden="true" />
                </button>
                <button type="button" onClick={onAnalyzeSample} className="det-secondary-cta det-landing-secondary-cta">
                  <Play className="h-3.5 w-3.5" aria-hidden="true" />
                  Run sample analysis
                </button>
              </div>
              <div className="det-landing-assurances" aria-label="Product assurances">
                <span><CheckCircle2 aria-hidden="true" />Source-linked evidence</span>
                <span><CheckCircle2 aria-hidden="true" />Deterministic assessment</span>
                <span><CheckCircle2 aria-hidden="true" />No application execution</span>
              </div>
            </div>

            <div className="det-hero-video-shell">
              <video
                className="det-hero-video"
                autoPlay
                muted
                loop
                playsInline
                preload="metadata"
                aria-label="DotDet production-readiness workbench demonstration"
              >
                <source src="/dotdetv.mp4" type="video/mp4" />
              </video>
            </div>
          </div>
        </div>
      </div>

      <div className="det-landing-signal-strip">
        <div className="det-landing-signal-strip-inner">
          <div><span>ANALYSIS ENGINE</span><strong>Roslyn + MSBuild</strong></div>
          <div><span>PRODUCTION DOMAINS</span><strong>Architecture through API readiness</strong></div>
          <div><span>ENGINEERING OUTPUT</span><strong>Evidence, guidance, and review-ready reports</strong></div>
        </div>
      </div>

      <div className="det-landing-page det-landing-content-page">
        <section id="docs" className="det-proof-section" aria-labelledby="landing-proof-title">
          <div className="det-proof-intro">
            <div className="det-section-kicker">A different kind of analyzer</div>
            <h2 id="landing-proof-title">Why DotDet is different</h2>
            <p>
              Detection is only useful when an engineer can verify it. DotDet keeps evidence, confidence, production impact, and remediation in the same workflow.
            </p>
          </div>
          <div className="det-proof-list">
            {landingProofRows.map((row) => (
              <article key={row.title} className="det-proof-row">
                <span className="det-proof-row-number">{row.number}</span>
                <row.icon className="det-proof-row-icon" aria-hidden="true" />
                <div>
                  <h3>{row.title}</h3>
                  <p>{row.body}</p>
                </div>
              </article>
            ))}
          </div>
        </section>

        <section className="det-landing-workflow" aria-labelledby="landing-workflow-title">
          <div className="det-landing-workflow-heading">
            <div className="det-section-kicker">From repository to remediation</div>
            <h2 id="landing-workflow-title">A workflow built for engineering review.</h2>
          </div>
          <div className="det-landing-workflow-list">
            {landingWorkflow.map((step) => (
              <article key={step.number} className="det-landing-workflow-step">
                <span>{step.number}</span>
                <h3>{step.title}</h3>
                <p>{step.body}</p>
              </article>
            ))}
          </div>
        </section>

        <section className="det-capability-section" aria-labelledby="landing-capabilities-title">
          <div className="det-capability-section-heading">
            <div>
              <div className="det-section-kicker">Analysis scope</div>
              <h2 id="landing-capabilities-title" className="det-capability-section-title">The production concerns that matter.</h2>
            </div>
            <p>Focused checks for ASP.NET Core teams, with applicability and confidence built into every finding.</p>
          </div>
          <div className="det-capability-list">
            {landingCapabilities.map((capability) => (
              <article key={capability.title} className="det-capability-row">
                <capability.icon className="det-capability-icon" aria-hidden="true" />
                <div>
                  <h3 className="det-capability-card-title">{capability.title}</h3>
                  <p className="det-capability-card-body">{capability.body}</p>
                </div>
              </article>
            ))}
          </div>
        </section>

        <section className="det-landing-final-cta" aria-labelledby="landing-final-title">
          <div>
            <div className="det-section-kicker">Start with real evidence</div>
            <h2 id="landing-final-title">See what stands between your solution and production.</h2>
          </div>
          <div className="det-landing-final-actions">
            <button type="button" onClick={onLogin} className="det-primary-cta"><GitHubMark className="h-4 w-4" />Login with GitHub <ChevronRight className="h-4 w-4" aria-hidden="true" /></button>
            <button type="button" onClick={onAnalyzeSample} className="det-secondary-cta">Explore sample project</button>
          </div>
        </section>
      </div>
    </section>
  )
}

const docsQuickLinks: Array<{
  description: string
  href: string
  icon: LucideIcon
  label: string
  section: string
}> = [
  { label: 'Getting started', description: 'Run DotDet locally and analyze the bundled sample.', href: DOTDET_README_URL, icon: Play, section: 'Start' },
  { label: 'Security & privacy', description: 'Understand source, token, history, and archive boundaries.', href: `${DOTDET_DOCS_URL}/security.md`, icon: ShieldCheck, section: 'Trust' },
  { label: 'Private GitHub repositories', description: 'Review permissions, token handling, and disconnect behavior.', href: `${DOTDET_DOCS_URL}/private-github-security.md`, icon: FolderGit2, section: 'Trust' },
  { label: 'Engine maturity', description: 'See current coverage, fidelity, and calibration status.', href: `${DOTDET_DOCS_URL}/engine-maturity.md`, icon: FileSearch, section: 'Engine' },
  { label: 'Known limitations', description: 'Read the boundaries of the v0.1 Preview engine and analysis model.', href: `${DOTDET_DOCS_URL}/known-limitations.md`, icon: AlertTriangle, section: 'Preview' },
  { label: 'Roadmap', description: 'Review planned calibration, worker, branch, and PR work.', href: `${DOTDET_DOCS_URL}/roadmap.md`, icon: GitBranch, section: 'Project' },
  { label: 'Changelog', description: 'See the capabilities and hardening delivered in v0.1 Preview.', href: '/changelog', icon: FileText, section: 'Release' },
]

const docsAnalysisSources = [
  ['ZIP uploads', 'Authenticated. Extracted through the guarded archive path and analyzed in safe syntax mode.'],
  ['Public GitHub', 'Authenticated. DotDet downloads the default branch archive server-side.'],
  ['Private GitHub', 'Authenticated with an explicit repository-access upgrade; tokens remain server-side.'],
  ['Sample project', 'Public and rate-limited. Intended for evaluating the workbench and report flow.'],
  ['Local path', 'Development only. Blocked outside the Development environment.'],
] as const

const docsAnalyzerCoverage = [
  ['Architecture boundaries', 'Project graph, inferred layers, cycles, and dependency violations.'],
  ['Dependency injection reliability', 'Constructor requirements, registrations, framework exclusions, and lifetime risks.'],
  ['EF Core migration risk', 'DbContext/entity evidence, migration operations, raw SQL, and destructive-change indicators.'],
  ['Security and configuration', 'Secrets, connection strings, CORS, HTTPS, JWT, and authentication middleware.'],
  ['API readiness', 'API/Web UI intent, endpoints, OpenAPI, exception handling, health, logging, and validation.'],
] as const

const docsFullIndex = [
  ['Project README', DOTDET_README_URL],
  ['Security', `${DOTDET_DOCS_URL}/security.md`],
  ['Private GitHub security', `${DOTDET_DOCS_URL}/private-github-security.md`],
  ['Engine maturity', `${DOTDET_DOCS_URL}/engine-maturity.md`],
  ['Rule quality principles', `${DOTDET_DOCS_URL}/rule-quality.md`],
  ['Calibration', `${DOTDET_DOCS_URL}/calibration.md`],
  ['Known limitations', `${DOTDET_DOCS_URL}/known-limitations.md`],
  ['Roadmap', `${DOTDET_DOCS_URL}/roadmap.md`],
] as const

function DocsPage() {
  return (
    <section className="det-docs-page">
      <div className="det-docs-shell">
        <header className="det-docs-header">
          <div className="det-docs-eyebrow">v0.1 Preview documentation</div>
          <h2>DotDet Documentation</h2>
          <p>
            Learn how DotDet analyzes ASP.NET Core applications, handles private repositories, scores production-readiness findings, and protects source code during analysis.
          </p>
          <div className="det-docs-header-actions" aria-label="Documentation actions">
            <a href={DOTDET_README_URL} className="det-docs-primary-link">
              Start with the README
              <ExternalLink className="h-3.5 w-3.5" aria-hidden="true" />
            </a>
            <a href="/changelog" className="det-docs-secondary-link">
              View v0.1 changes
              <ChevronRight className="h-3.5 w-3.5" aria-hidden="true" />
            </a>
          </div>
        </header>

        <div className="det-docs-layout">
          <aside className="det-docs-toc" aria-label="Documentation sections">
            <span className="det-docs-toc-label">On this page</span>
            <a href="#quick-links">Quick links</a>
            <a href="#analysis-sources">Analysis sources</a>
            <a href="#analyzer-coverage">Analyzer coverage</a>
            <a href="#security-privacy">Security and privacy</a>
            <a href="#engine-maturity">Engine maturity</a>
            <a href="#full-documentation">Full documentation</a>
          </aside>

          <div className="det-docs-content">
            <section id="quick-links" className="det-docs-section">
              <div className="det-docs-section-heading">
                <div>
                  <span className="det-docs-section-index">01</span>
                  <h3>Quick links</h3>
                </div>
                <p>Use the repository documentation for the complete product and engine reference.</p>
              </div>
              <div className="det-docs-link-list">
                {docsQuickLinks.map((item) => (
                  <a key={item.label} href={item.href} className="det-docs-link-row">
                    <item.icon className="det-docs-link-icon" aria-hidden="true" />
                    <span className="det-docs-link-copy">
                      <span>{item.label}</span>
                      <small>{item.description}</small>
                    </span>
                    <span className="det-docs-link-section">{item.section}</span>
                    {item.href.startsWith('http')
                      ? <ExternalLink className="h-3.5 w-3.5" aria-hidden="true" />
                      : <ChevronRight className="h-3.5 w-3.5" aria-hidden="true" />}
                  </a>
                ))}
              </div>
            </section>

            <section id="analysis-sources" className="det-docs-section">
              <div className="det-docs-section-heading">
                <div>
                  <span className="det-docs-section-index">02</span>
                  <h3>What DotDet analyzes</h3>
                </div>
                <p>Repository analysis is authenticated; the public sample remains available under request limits.</p>
              </div>
              <div className="det-docs-definition-list">
                {docsAnalysisSources.map(([label, description]) => (
                  <div key={label} className="det-docs-definition-row">
                    <h4>{label}</h4>
                    <p>{description}</p>
                  </div>
                ))}
              </div>
            </section>

            <section id="analyzer-coverage" className="det-docs-section">
              <div className="det-docs-section-heading">
                <div>
                  <span className="det-docs-section-index">03</span>
                  <h3>Analyzer coverage</h3>
                </div>
                <p>Findings are evidence-first, source-linked, and designed to explain the next engineering action.</p>
              </div>
              <div className="det-docs-definition-list">
                {docsAnalyzerCoverage.map(([label, description]) => (
                  <div key={label} className="det-docs-definition-row">
                    <h4>{label}</h4>
                    <p>{description}</p>
                  </div>
                ))}
              </div>
              <p className="det-docs-note">
                Every supported finding can include severity, confidence, project, file, line, detection method, evidence, production impact, and remediation guidance.
              </p>
            </section>

            <section id="security-privacy" className="det-docs-section det-docs-emphasis-section">
              <div className="det-docs-section-heading">
                <div>
                  <span className="det-docs-section-index">04</span>
                  <h3>Security and privacy</h3>
                </div>
                <a href={`${DOTDET_DOCS_URL}/security.md`} className="det-docs-inline-link">
                  Read the security model
                  <ExternalLink className="h-3.5 w-3.5" aria-hidden="true" />
                </a>
              </div>
              <ul className="det-docs-bullets">
                <li>GitHub tokens stay server-side and are protected at rest.</li>
                <li>Saved history is sanitized and does not store raw source.</li>
                <li>Browser storage and default exports remove source content and server paths.</li>
                <li>Root-cause keys use repository-relative paths.</li>
                <li>Untrusted ZIP and GitHub inputs use safe syntax analysis in v0.1 Preview.</li>
              </ul>
              <p className="det-docs-note">
                Full semantic Roslyn/MSBuild analysis for untrusted repositories requires a future isolated worker or container boundary.
              </p>
            </section>

            <section id="engine-maturity" className="det-docs-section">
              <div className="det-docs-section-heading">
                <div>
                  <span className="det-docs-section-index">05</span>
                  <h3>Engine maturity</h3>
                </div>
                <span className="det-docs-status">v0.1 Preview / Calibration</span>
              </div>
              <p>
                DotDet is evidence-first and calibrated against real ASP.NET Core repositories. It is designed to reduce obvious false positives, but it is not a replacement for manual architecture review, penetration testing, full SAST/DAST, runtime observability, or maintainer code review.
              </p>
              <div className="det-docs-related-links" aria-label="Engine documentation">
                <a href={`${DOTDET_DOCS_URL}/engine-maturity.md`}>Engine maturity <ExternalLink className="h-3 w-3" aria-hidden="true" /></a>
                <a href={`${DOTDET_DOCS_URL}/calibration.md`}>Calibration <ExternalLink className="h-3 w-3" aria-hidden="true" /></a>
                <a href={`${DOTDET_DOCS_URL}/rule-quality.md`}>Rule quality <ExternalLink className="h-3 w-3" aria-hidden="true" /></a>
                <a href={`${DOTDET_DOCS_URL}/known-limitations.md`}>Known limitations <ExternalLink className="h-3 w-3" aria-hidden="true" /></a>
              </div>
            </section>

            <section id="full-documentation" className="det-docs-section">
              <div className="det-docs-section-heading">
                <div>
                  <span className="det-docs-section-index">06</span>
                  <h3>Full documentation</h3>
                </div>
                <p>The Markdown set is versioned with the source and reviewed with each Preview release.</p>
              </div>
              <div className="det-docs-index-list">
                {docsFullIndex.map(([label, href]) => (
                  <a key={label} href={href}>
                    <span>{label}</span>
                    <ExternalLink className="h-3.5 w-3.5" aria-hidden="true" />
                  </a>
                ))}
                <a href="/changelog">
                  <span>Changelog</span>
                  <ChevronRight className="h-3.5 w-3.5" aria-hidden="true" />
                </a>
              </div>
            </section>
          </div>
        </div>
      </div>
    </section>
  )
}

const changelogSections = [
  {
    title: 'Analysis engine',
    items: [
      'Added ASP.NET Core project discovery for .sln, .slnx, and .csproj projects.',
      'Added analyzers for architecture boundaries, dependency injection, EF Core migrations, security/configuration, and API readiness.',
      'Added source-linked findings with severity, confidence, detection method, evidence, and remediation guidance.',
      'Added root-cause grouping and score explanations.',
    ],
  },
  {
    title: 'GitHub and analysis sources',
    items: [
      'Added ZIP upload analysis.',
      'Added public and private GitHub repository analysis.',
      'Added sample project analysis.',
      'Restricted local path analysis to Development mode.',
    ],
  },
  {
    title: 'Security and privacy hardening',
    items: [
      'Kept GitHub tokens server-side and protected repository access at rest.',
      'Sanitized saved history, browser storage, and default exports without retaining raw source.',
      'Changed root-cause keys and report paths to repository-relative values.',
      'Blocked local path analysis outside Development and kept untrusted ZIP/GitHub input in safe syntax mode.',
      'Added rate limits, per-caller concurrency limits, and analysis timeouts.',
    ],
  },
  {
    title: 'Analyzer quality improvements',
    items: [
      'Added API/Web UI intent detection for MVC, Razor, Blazor, minimal API, and mixed hosts.',
      'Reduced DI false positives for ASP.NET Core framework-provided services and added MediatR registration detection.',
      'Limited destructive EF migration checks to Up() operations.',
      'Improved clean-report consistency, root-cause path sanitization, and fidelity reporting.',
      'Prevented Info and low-confidence findings from driving production readiness risk.',
    ],
  },
  {
    title: 'Reporting and workflow',
    items: [
      'Added saved analysis history and finding dispositions.',
      'Added Code Explorer, Engineering Guide, and architecture overview.',
      'Added JSON, Markdown, and standalone HTML exports.',
      'Added sanitized historical reports with an explicit source-preview-unavailable state.',
    ],
  },
  {
    title: 'Public UI and documentation',
    items: [
      'Added the public landing page, Docs, Changelog, Contact, and product navigation.',
      'Published security, private GitHub security, engine maturity, rule quality, calibration, known limitations, and roadmap documentation.',
    ],
  },
  {
    title: 'Known limitations',
    items: [
      'Untrusted ZIP and GitHub inputs use safe syntax analysis until isolated semantic worker support exists.',
      'Branch and pull-request analysis remain future work.',
      'DotDet is Preview software and complements rather than replaces human review and dedicated security testing.',
    ],
  },
]

function ContactPage() {
  return (
    <section className="det-docs-page">
      <div className="det-docs-shell det-simple-public-shell">
        <header className="det-docs-header">
          <div className="det-docs-eyebrow">Contact</div>
          <h2>Contact DotDet</h2>
          <p>Use the channel that matches the work. Product questions can go directly to the maintainer; reproducible defects and analyzer false positives belong in the issue tracker.</p>
        </header>

        <div className="det-contact-list">
          <article className="det-contact-card">
            <div>
              <div className="det-contact-card-label">Email</div>
              <h3>Product and collaboration</h3>
              <p>Use email for product questions, engineering collaboration, or responsible disclosure.</p>
            </div>
            <a href="mailto:cezarapedroso@gmail.com">
              cezarpedroso@gmail.com
              <ChevronRight className="h-3.5 w-3.5" aria-hidden="true" />
            </a>
          </article>

          <article className="det-contact-card">
            <div>
              <div className="det-contact-card-label">GitHub Issues</div>
              <h3>Defects and analyzer feedback</h3>
              <p>Include the rule ID, project type, expected behavior, and a minimal reproduction when possible.</p>
            </div>
            <a href={DOTDET_ISSUES_URL} rel="noreferrer">
              Open GitHub Issues
              <ExternalLink className="h-3.5 w-3.5" aria-hidden="true" />
            </a>
          </article>
        </div>
      </div>
    </section>
  )
}

function ChangelogPage() {
  return (
    <section className="det-docs-page">
      <div className="det-docs-shell det-changelog-shell">
        <header className="det-docs-header">
          <div className="det-docs-eyebrow">Changelog</div>
          <h2>Release notes</h2>
          <p>Release notes for DotDet preview builds.</p>
        </header>

        <article className="det-changelog-release">
          <header className="det-changelog-release-header">
            <div>
              <span className="det-changelog-version">v0.1 Preview</span>
              <h3>Initial public preview</h3>
            </div>
            <span className="det-changelog-state">Preview / Calibration</span>
          </header>
          <p className="det-changelog-intro">
            Initial public preview of DotDet, a production-readiness analysis platform for ASP.NET Core applications.
          </p>

          <div className="det-changelog-timeline">
            {changelogSections.map((section, index) => (
              <section key={section.title} className="det-changelog-item">
                <div className="det-changelog-item-heading">
                  <span>{String(index + 1).padStart(2, '0')}</span>
                  <h4>{section.title}</h4>
                </div>
                <ul className="det-docs-bullets">
                  {section.items.map((item) => (
                    <li key={item}>{item}</li>
                  ))}
                </ul>
              </section>
            ))}
          </div>

          <footer className="det-changelog-footer">
            <div>
              <span>Release documentation</span>
              <p>For security and engine maturity details, see the Docs page.</p>
            </div>
            <a href="/docs">
              Open Docs
              <ChevronRight className="h-3.5 w-3.5" aria-hidden="true" />
            </a>
          </footer>
        </article>
      </div>
    </section>
  )
}

function PublicFooter({ onNavigate }: { onNavigate: (page: StartPage) => void }) {
  const navigate = (event: ReactMouseEvent<HTMLAnchorElement>, page: StartPage) => {
    event.preventDefault()
    onNavigate(page)
  }

  return (
    <footer className="det-public-footer">
      <div className="det-public-footer-inner">
        <div className="det-public-footer-lockup">
          <img src="/dotdet-logo.png" alt="" className="det-public-footer-logo" />
          <div>
            <div className="det-public-footer-brand">DotDet</div>
            <p>.NET production-readiness analysis with source-linked evidence.</p>
            <p className="det-public-footer-credit">Built by Cezar Pedroso.</p>
          </div>
        </div>
        <nav aria-label="Footer navigation">
          <a href="/docs" onClick={(event) => navigate(event, 'Docs')}>Docs</a>
          <a href="/changelog" onClick={(event) => navigate(event, 'Changelog')}>Changelog</a>
          <a href="/contact" onClick={(event) => navigate(event, 'Contact')}>Contact</a>
          <a href={DOTDET_REPOSITORY_URL} rel="noreferrer">GitHub</a>
          <a href={DOTDET_ISSUES_URL} rel="noreferrer">Issues</a>
        </nav>
      </div>
    </footer>
  )
}

function AuthLoadingPage() {
  return (
    <section className="flex flex-1 items-center justify-center p-6">
      <div className="border border-slate-300 bg-white p-5 text-sm text-slate-600">
        Checking GitHub session...
      </div>
    </section>
  )
}

function DashboardPage({
  authUser,
  history,
  historyError,
  historyLoading,
  isLoading,
  onAnalyzeSample,
  onExportHistory,
  onOpenAnalyze,
  onOpenHistory,
  onRerunHistory,
}: {
  authUser: AuthUser | null
  history: AnalysisHistorySummary[]
  historyError: string | null
  historyLoading: boolean
  isLoading: boolean
  onAnalyzeSample: () => void
  onExportHistory: (id: string, format: ExportFormat) => void
  onOpenAnalyze: (tab: AnalyzeTab) => void
  onOpenHistory: (id: string) => void
  onRerunHistory: (id: string) => void
}) {
  const displayName = authUser?.displayName || authUser?.githubUsername || 'Developer'
  const githubHandle = authUser?.githubUsername?.replace(/^@/, '').trim()
  const latest = history[0]
  const totalOpenFindings = history.reduce((sum, item) => sum + item.openFindingCount, 0)
  const recentReports = history.slice(0, 5)

  return (
    <section className="det-dashboard-page flex-1 overflow-auto p-5">
      <div className="det-auth-workspace mx-auto">
        <header className="det-overview-title">
          <div className="text-xs font-semibold uppercase tracking-wide text-[#2ea043]">Dashboard</div>
          <h1 className="mt-2 text-2xl font-semibold text-slate-950">Welcome, {displayName}</h1>
          {githubHandle ? <div className="mt-1 font-mono text-xs text-slate-500">@{githubHandle}</div> : null}
          <p className="mt-2 max-w-2xl text-sm text-slate-500">
            Start a new analysis or reopen a previous production-readiness report.
          </p>
        </header>

        <div className="mt-5 grid gap-4 xl:grid-cols-[minmax(0,1fr)_320px]">
          <section className="det-auth-upload-panel mt-0">
            <div className="flex flex-wrap items-start justify-between gap-3 border-b border-slate-200 pb-3">
              <div>
                <h2 className="text-base font-semibold text-slate-950">Workspace Summary</h2>
                <p className="mt-1 text-xs text-slate-500">Saved analyses for your GitHub account.</p>
              </div>
              <button type="button" onClick={() => onOpenAnalyze('Upload ZIP')} className="det-run-analysis-button h-8">
                <FolderSearch className="h-4 w-4" aria-hidden="true" />
                New Analysis
              </button>
            </div>

            <dl className="mt-4 grid gap-4 sm:grid-cols-4">
              <CompactMetric label="Total Analyses" value={history.length} tone="neutral" />
              <CompactMetric label="Latest Score" value={latest ? latest.score : '-'} tone={latest && latest.score >= 85 ? 'ok' : latest ? 'warn' : 'neutral'} />
              <CompactMetric label="Open Findings" value={totalOpenFindings} tone={totalOpenFindings > 0 ? 'danger' : 'ok'} />
              <CompactMetric label="Last Analysis" value={latest ? formatHistoryDate(latest.completedAt) : '-'} tone="neutral" />
            </dl>

            <div className="mt-5 grid gap-3 md:grid-cols-3">
              <button type="button" onClick={() => onOpenAnalyze('GitHub Repository')} className={analysisActionClass(false)}>
                <FolderGit2 className="h-5 w-5 text-[#2ea043]" aria-hidden="true" />
                <span className="font-semibold text-slate-950">Analyze GitHub Repository</span>
                <span className="text-xs leading-5 text-slate-500">Analyze public repositories, or enable private repository access when needed.</span>
              </button>
              <button type="button" onClick={() => onOpenAnalyze('Upload ZIP')} className={analysisActionClass(false)}>
                <UploadCloud className="h-5 w-5 text-[#2ea043]" aria-hidden="true" />
                <span className="font-semibold text-slate-950">Upload ZIP</span>
                <span className="text-xs leading-5 text-slate-500">Analyze a zipped .NET solution.</span>
              </button>
              <button type="button" onClick={onAnalyzeSample} disabled={isLoading} className={analysisActionClass(false)}>
                <FileCode2 className="h-5 w-5 text-[#2ea043]" aria-hidden="true" />
                <span className="font-semibold text-slate-950">Analyze Sample Project</span>
                <span className="text-xs leading-5 text-slate-500">Open the included ASP.NET Core sample.</span>
              </button>
            </div>
          </section>

          <aside className="det-auth-side-panel">
            <h2 className="text-sm font-semibold text-slate-950">Connected GitHub Account</h2>
            <div className="mt-2 flex items-center gap-3 border border-slate-200 bg-white p-3">
              {authUser?.avatarUrl ? (
                <img src={authUser.avatarUrl} alt="" className="h-10 w-10" />
              ) : (
                <Circle className="h-10 w-10 text-slate-500" aria-hidden="true" />
              )}
              <div className="min-w-0">
                <div className="truncate text-sm font-semibold text-slate-950">{displayName}</div>
                {githubHandle ? <div className="truncate text-xs text-slate-500">@{githubHandle}</div> : null}
              </div>
            </div>
            <p className="mt-3 text-xs leading-5 text-slate-500">
              Report history is scoped to this authenticated GitHub user. Repository cloning is not enabled until secure token storage is added.
            </p>
          </aside>
        </div>

        <section className="det-auth-upload-panel">
          <div className="flex flex-wrap items-start justify-between gap-3 border-b border-slate-200 pb-3">
            <div>
              <h2 className="text-base font-semibold text-slate-950">Recent Analyses</h2>
              <p className="mt-1 text-xs text-slate-500">Open or export saved report snapshots.</p>
            </div>
            <button type="button" onClick={() => onOpenAnalyze('Upload ZIP')} className="det-card-link-button">
              Open Analyze
            </button>
          </div>

          {historyError ? <InlineError message={historyError} /> : null}
          {historyLoading ? <p className="mt-4 text-sm text-slate-500">Loading reports...</p> : null}

          {!historyLoading && recentReports.length === 0 ? (
            <EmptyState
              title="No reports yet"
              body="Start by analyzing a sample project or uploading a zipped .NET solution."
              actionLabel="Analyze a solution"
              onAction={() => onOpenAnalyze('Upload ZIP')}
            />
          ) : (
            <HistoryList
              compact
              history={recentReports}
              isLoading={isLoading}
              onExport={onExportHistory}
              onRerun={onRerunHistory}
              onView={onOpenHistory}
            />
          )}
        </section>
      </div>
    </section>
  )
}

function analysisActionClass(active: boolean) {
  return `det-dashboard-action flex min-h-24 flex-col items-start gap-1.5 border p-3 text-left transition hover:border-[#2ea043] hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-60 ${
    active ? 'border-teal-600 bg-slate-50' : 'border-slate-200 bg-white'
  }`
}

function AnalyzePage({
  activeTab,
  canAnalyze,
  error,
  isLoading,
  onActiveTabChange,
  onAnalyzeGitHubRepository,
  onAnalyzeSample,
  onClearCachedAnalysisState,
  onFileChange,
  onSubmit,
  zipFile,
}: {
  activeTab: AnalyzeTab
  canAnalyze: boolean
  error: string | null
  isLoading: boolean
  onActiveTabChange: (tab: AnalyzeTab) => void
  onAnalyzeGitHubRepository: (repositoryInput: string) => Promise<void>
  onAnalyzeSample: () => void
  onClearCachedAnalysisState: () => void
  onFileChange: (event: ChangeEvent<HTMLInputElement>) => void
  onSubmit: (event: FormEvent<HTMLFormElement>) => void
  zipFile: File | null
}) {
  const [repoState, setRepoState] = useState<GitHubRepositoryListingResponse | null>(null)
  const [repoError, setRepoError] = useState<string | null>(null)
  const [repoSearch, setRepoSearch] = useState('')
  const [manualRepository, setManualRepository] = useState('')
  const [manualRepositoryError, setManualRepositoryError] = useState<string | null>(null)
  const [repositoryAccessUpdating, setRepositoryAccessUpdating] = useState(false)
  const tabs: AnalyzeTab[] = ['GitHub Repository', 'Upload ZIP', 'Sample Project']
  const repositories = repoState?.repositories ?? []
  const privateAccessEnabled = repoState?.privateAccessEnabled ?? false
  const filteredRepositories = repositories.filter((repo) =>
    `${repo.owner}/${repo.name} ${repo.description ?? ''}`.toLowerCase().includes(repoSearch.trim().toLowerCase()),
  )

  useEffect(() => {
    if (activeTab !== 'GitHub Repository' || repoState || repoError) {
      return
    }

    let cancelled = false
    fetch(`${API_BASE_URL}/api/github/repos`, { credentials: 'include' })
      .then(async (response) => {
        if (!response.ok) {
          throw new Error(`Repository request failed with HTTP ${response.status}`)
        }

        return response.json() as Promise<GitHubRepositoryListingResponse>
      })
      .then((payload) => {
        if (!cancelled) {
          setRepoState(payload)
        }
      })
      .catch((caughtError) => {
        if (!cancelled) {
          setRepoError(caughtError instanceof Error ? caughtError.message : 'Repository access is not available yet.')
        }
      })

    return () => {
      cancelled = true
    }
  }, [activeTab, repoError, repoState])

  function enablePrivateRepositoryAccess() {
    window.location.href = `${API_BASE_URL}/api/auth/github-repo-access-login`
  }

  async function disconnectPrivateRepositoryAccess() {
    setRepositoryAccessUpdating(true)
    setRepoError(null)
    onClearCachedAnalysisState()
    try {
      const response = await fetch(`${API_BASE_URL}/api/auth/repository-access`, {
        credentials: 'include',
        method: 'DELETE',
      })
      if (!response.ok) {
        throw new Error(`Repository access disconnect failed with HTTP ${response.status}`)
      }

      setRepoState(null)
      onClearCachedAnalysisState()
    } catch (caughtError) {
      setRepoError(caughtError instanceof Error ? caughtError.message : 'Private repository access could not be disconnected.')
    } finally {
      setRepositoryAccessUpdating(false)
    }
  }

  return (
    <section className="det-dashboard-page flex-1 overflow-auto p-5">
      <div className="det-auth-workspace mx-auto">
        <header className="det-overview-title">
          <div className="text-xs font-semibold uppercase tracking-wide text-[#2ea043]">Analyze</div>
          <h1 className="mt-2 text-2xl font-semibold text-slate-950">Analyze a .NET solution</h1>
          <p className="mt-2 max-w-3xl text-sm text-slate-500">
            Run DotDet against a GitHub repository, uploaded ZIP, or sample ASP.NET Core project.
          </p>
        </header>

        <div className="det-analyze-tabs mt-5 flex flex-wrap gap-1 border-b border-slate-200">
          {tabs.map((tab) => (
            <button
              key={tab}
              type="button"
              onClick={() => onActiveTabChange(tab)}
              className={`det-analyze-tab px-3 py-2 text-xs font-semibold transition ${
                activeTab === tab ? 'det-analyze-tab-active border-b-2 border-[#2ea043] text-slate-950' : 'text-slate-500 hover:text-slate-200'
              }`}
            >
              {tab}
            </button>
          ))}
        </div>

        {activeTab === 'GitHub Repository' ? (
          <section className="det-auth-upload-panel">
            <div className="flex items-start gap-3">
              <FolderGit2 className="mt-0.5 h-5 w-5 text-[#2ea043]" aria-hidden="true" />
              <div>
                <h2 className="text-base font-semibold text-slate-950">Analyze GitHub Repository</h2>
                <p className="mt-2 max-w-2xl text-sm leading-6 text-slate-500">
                  Analyze the default branch of a GitHub repository. Private repositories require explicit repository access.
                </p>
              </div>
            </div>

            <div className="mt-5 grid gap-5 xl:grid-cols-[minmax(0,1fr)_360px]">
              <div>
                <div className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-200 pb-3">
                  <div>
                    <h3 className="text-sm font-semibold text-slate-950">Repositories</h3>
                    <p className="mt-1 text-xs text-slate-500">
                      {repoError ?? repoState?.message ?? 'Checking repository listing...'}
                    </p>
                  </div>
                  <label className="relative">
                    <Search className="pointer-events-none absolute left-2 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-slate-500" aria-hidden="true" />
                    <input
                      value={repoSearch}
                      onChange={(event) => setRepoSearch(event.target.value)}
                      placeholder="Search repositories"
                      className="h-8 w-64 border border-slate-300 bg-white pl-7 pr-2 text-xs text-slate-900 outline-none focus:border-[#2ea043]"
                    />
                  </label>
                </div>

                {repoState?.enabled && filteredRepositories.length > 0 ? (
                  <div className="mt-3 grid gap-2">
                    {filteredRepositories.map((repo) => (
                      <div key={`${repo.owner}/${repo.name}`} className="flex items-center justify-between gap-3 border border-slate-200 bg-white p-3">
                        <div className="min-w-0">
                          <div className="truncate text-sm font-semibold text-slate-950">{repo.owner}/{repo.name}</div>
                          <div className="mt-0.5 text-xs text-slate-500">
                            {repo.visibility ?? 'Public'} - default branch {repo.defaultBranch ?? 'unknown'}
                            {repo.updatedAt ? ` - updated ${formatHistoryDate(repo.updatedAt)}` : ''}
                          </div>
                          {repo.description ? <p className="mt-1 line-clamp-2 text-xs leading-5 text-slate-500">{repo.description}</p> : null}
                        </div>
                        <button
                          type="button"
                          disabled={isLoading}
                          onClick={() => onAnalyzeGitHubRepository(`${repo.owner}/${repo.name}`)}
                          className="det-run-analysis-button h-8 shrink-0 disabled:cursor-not-allowed disabled:opacity-60"
                        >
                          Analyze default branch
                        </button>
                      </div>
                    ))}
                  </div>
                ) : (
                  <div className="mt-3 border border-slate-200 bg-white p-4 text-sm text-slate-500">
                    {repoState?.enabled
                      ? 'No repositories found. Paste a GitHub repository URL to analyze it.'
                      : repoState?.reason ?? repoError ?? 'Repository listing is unavailable. Paste a GitHub repository URL to analyze it.'}
                  </div>
                )}
              </div>

              <aside className="grid gap-3">
                <form
                  className="border border-slate-200 bg-white p-3"
                  onSubmit={(event) => {
                    event.preventDefault()
                    if (!isValidGitHubRepositoryInput(manualRepository)) {
                      setManualRepositoryError('Enter a valid GitHub repository URL or owner/repo.')
                      return
                    }

                    setManualRepositoryError(null)
                    void onAnalyzeGitHubRepository(manualRepository)
                  }}
                >
                  <h3 className="text-sm font-semibold text-slate-950">Analyze by URL</h3>
                  <p className="mt-1 text-xs leading-5 text-slate-500">
                    Paste a GitHub repository URL or owner/repo.
                  </p>
                  <input
                    value={manualRepository}
                    onChange={(event) => {
                      setManualRepository(event.target.value)
                      setManualRepositoryError(null)
                    }}
                    placeholder="github.com/owner/repo"
                    className="mt-3 h-9 w-full border border-slate-300 bg-white px-2 text-sm text-slate-900 outline-none focus:border-[#2ea043]"
                  />
                  {manualRepositoryError ? <p className="mt-2 text-xs text-red-700">{manualRepositoryError}</p> : null}
                  {error ? <InlineError message={error} /> : null}
                  {isLoading ? <AnalysisProgress /> : null}
                  <button type="submit" disabled={isLoading || !manualRepository.trim()} className="det-run-analysis-button mt-3 h-8 w-full disabled:cursor-not-allowed disabled:opacity-60">
                    Analyze repository
                  </button>
                </form>

                <div className="border border-slate-200 bg-white p-3">
                  <h3 className="text-sm font-semibold text-slate-950">Private repositories</h3>
                  <p className="mt-1 text-xs leading-5 text-slate-500">
                    {privateAccessEnabled
                      ? repoState?.privateAccessMessage ?? 'Private repository access is enabled for this GitHub account.'
                      : repoState?.privateAccessMessage ?? 'DotDet requests repository read permission only when private analysis is enabled.'}
                  </p>
                  {privateAccessEnabled ? (
                    <button
                      type="button"
                      disabled={repositoryAccessUpdating}
                      onClick={disconnectPrivateRepositoryAccess}
                      className="det-secondary-cta mt-3 h-8 w-full disabled:cursor-not-allowed disabled:opacity-60"
                    >
                      Disconnect private access
                    </button>
                  ) : (
                    <button
                      type="button"
                      disabled={repositoryAccessUpdating}
                      onClick={enablePrivateRepositoryAccess}
                      className="det-run-analysis-button mt-3 h-8 w-full disabled:cursor-not-allowed disabled:opacity-60"
                    >
                      Enable private access
                    </button>
                  )}
                </div>
              </aside>
            </div>
          </section>
        ) : null}

        {activeTab === 'Upload ZIP' ? (
          <form onSubmit={onSubmit} className="det-auth-upload-panel">
            <div className="mb-3 flex items-center gap-2">
              <UploadCloud className="h-4 w-4 text-[#2ea043]" aria-hidden="true" />
              <h2 className="text-base font-semibold text-slate-950">Upload ZIP</h2>
            </div>
            <p className="mb-4 max-w-3xl text-sm leading-6 text-slate-500">
              Upload a zipped solution. DotDet analyzes project structure, architecture, DI, EF Core, security configuration, and API readiness without executing your application code.
            </p>
            <label className="det-upload-dropzone">
              <FileArchive className="mb-2 h-7 w-7 text-[#2ea043]" aria-hidden="true" />
              <span className="text-sm font-semibold text-slate-900">{zipFile?.name ?? 'Choose .zip solution archive'}</span>
              <span className="mt-1 text-xs text-slate-500">Accepted file type: .zip. Maximum upload size: 250 MB.</span>
              <input type="file" accept=".zip" onChange={onFileChange} className="sr-only" />
            </label>

            {isLoading ? <AnalysisProgress /> : null}
            {error ? <InlineError message={error} /> : null}

            <div className="mt-4 flex justify-end border-t border-slate-200 pt-4">
              <button type="submit" disabled={!canAnalyze || isLoading} className="det-run-analysis-button">
                <Play className="h-4 w-4" aria-hidden="true" />
                Run Analysis
              </button>
            </div>
          </form>
        ) : null}

        {activeTab === 'Sample Project' ? (
          <section className="det-auth-upload-panel">
            <div className="flex items-start gap-3">
              <FileCode2 className="mt-0.5 h-5 w-5 text-[#2ea043]" aria-hidden="true" />
              <div>
                <h2 className="text-base font-semibold text-slate-950">Sample Project</h2>
                <p className="mt-2 max-w-2xl text-sm leading-6 text-slate-500">
                  Analyze the bundled ASP.NET Core sample to preview DotDet findings, source navigation, and report exports.
                </p>
              </div>
            </div>

            {isLoading ? <AnalysisProgress /> : null}
            {error ? <InlineError message={error} /> : null}

            <div className="mt-5">
              <button type="button" onClick={onAnalyzeSample} disabled={isLoading} className="det-run-analysis-button">
                <Play className="h-4 w-4" aria-hidden="true" />
                Analyze Sample Project
              </button>
            </div>
          </section>
        ) : null}
      </div>
    </section>
  )
}

function ReportsPage({
  history,
  historyError,
  historyLoading,
  isLoading,
  onDelete,
  onExport,
  onOpenAnalyze,
  onRefresh,
  onRerun,
  onView,
}: {
  history: AnalysisHistorySummary[]
  historyError: string | null
  historyLoading: boolean
  isLoading: boolean
  onDelete: (id: string) => void
  onExport: (id: string, format: ExportFormat) => void
  onOpenAnalyze: (tab: AnalyzeTab) => void
  onRefresh: () => void
  onRerun: (id: string) => void
  onView: (id: string) => void
}) {
  const [query, setQuery] = useState('')
  const [sourceFilter, setSourceFilter] = useState<'All' | AnalysisSourceType>('All')
  const normalizedQuery = query.trim().toLowerCase()
  const filteredHistory = history.filter((item) => {
    const matchesSource = sourceFilter === 'All' || item.sourceType === sourceFilter
    const matchesQuery =
      !normalizedQuery
      || [item.solutionName, item.sourceLabel, item.status, formatSourceType(item.sourceType)]
        .join(' ')
        .toLowerCase()
        .includes(normalizedQuery)

    return matchesSource && matchesQuery
  })

  return (
    <section className="det-dashboard-page flex-1 overflow-auto p-5">
      <div className="det-auth-workspace mx-auto">
        <header className="det-overview-title">
          <div className="text-xs font-semibold uppercase tracking-wide text-[#2ea043]">Reports</div>
          <h1 className="mt-2 text-2xl font-semibold text-slate-950">Analysis history</h1>
          <p className="mt-2 max-w-3xl text-sm text-slate-500">
            View previous report snapshots, export them, or re-run sample analyses.
          </p>
        </header>

        <section className="det-auth-upload-panel">
          <div className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-200 pb-3">
            <div className="flex flex-wrap items-center gap-2">
              <label className="relative">
                <Search className="pointer-events-none absolute left-2 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-slate-500" aria-hidden="true" />
                <input
                  value={query}
                  onChange={(event) => setQuery(event.target.value)}
                  placeholder="Search reports"
                  className="h-8 min-w-64 border border-slate-300 bg-white pl-7 pr-2 text-xs text-slate-900 outline-none focus:border-[#2ea043]"
                />
              </label>
              <select
                value={sourceFilter}
                onChange={(event) => setSourceFilter(event.target.value as 'All' | AnalysisSourceType)}
                className="h-8 border border-slate-300 bg-white px-2 text-xs text-slate-900 outline-none focus:border-[#2ea043]"
              >
                <option value="All">All sources</option>
                <option value="GitHubRepo">GitHub</option>
                <option value="ZipUpload">Upload ZIP</option>
                <option value="SampleProject">Sample</option>
                <option value="LocalDevPath">LocalDev</option>
              </select>
            </div>
            <div className="flex items-center gap-2">
              <button type="button" onClick={onRefresh} className="det-card-link-button">
                Refresh
              </button>
              <button type="button" onClick={() => onOpenAnalyze('Upload ZIP')} className="det-run-analysis-button h-8">
                New Analysis
              </button>
            </div>
          </div>

          {historyError ? <InlineError message={historyError} /> : null}
          {historyLoading ? <p className="mt-4 text-sm text-slate-500">Loading reports...</p> : null}

          {!historyLoading && filteredHistory.length === 0 ? (
            <EmptyState
              title="No reports yet"
              body="No reports yet. Start by analyzing a sample project or uploading a zipped .NET solution."
              actionLabel="Analyze a solution"
              onAction={() => onOpenAnalyze('Upload ZIP')}
            />
          ) : (
            <HistoryList
              history={filteredHistory}
              isLoading={isLoading}
              onDelete={onDelete}
              onExport={onExport}
              onRerun={onRerun}
              onView={onView}
            />
          )}
        </section>
      </div>
    </section>
  )
}

function HistoryList({
  compact = false,
  history,
  isLoading,
  onDelete,
  onExport,
  onRerun,
  onView,
}: {
  compact?: boolean
  history: AnalysisHistorySummary[]
  isLoading: boolean
  onDelete?: (id: string) => void
  onExport: (id: string, format: ExportFormat) => void
  onRerun: (id: string) => void
  onView: (id: string) => void
}) {
  const [openExportId, setOpenExportId] = useState<string | null>(null)
  const exportFormats: Array<{ format: ExportFormat; label: string }> = [
    { format: 'HTML', label: 'HTML report' },
    { format: 'Markdown', label: 'Markdown report' },
    { format: 'JSON', label: 'JSON data' },
  ]

  return (
    <div className="mt-4 overflow-visible border border-slate-200 bg-white">
      <table className="min-w-full text-left text-xs">
        <thead className="border-b border-slate-200 text-[11px] uppercase tracking-wide text-slate-500">
          <tr>
            <th className="px-3 py-2 font-semibold">Project</th>
            <th className="px-3 py-2 font-semibold">Source</th>
            {!compact ? <th className="px-3 py-2 font-semibold">Score</th> : null}
            <th className="px-3 py-2 font-semibold">Findings</th>
            <th className="px-3 py-2 font-semibold">Analyzed</th>
            <th className="px-3 py-2 text-right font-semibold">Actions</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-slate-200">
          {history.map((item) => (
            <tr key={item.id}>
              <td className="px-3 py-2">
                <div className="font-semibold text-slate-950">{item.solutionName}</div>
                <div className="mt-0.5 truncate text-slate-500">{item.sourceLabel}</div>
              </td>
              <td className="px-3 py-2 text-slate-600">{formatSourceType(item.sourceType)}</td>
              {!compact ? (
                <td className="px-3 py-2">
                  <span className="font-semibold text-slate-950">{item.score}/100</span>
                  <span className="ml-2 text-slate-500">{item.grade}</span>
                  <div className="text-slate-500">{item.status}</div>
                </td>
              ) : null}
              <td className="px-3 py-2 text-slate-600">{item.openFindingCount} open / {item.totalFindingCount} total</td>
              <td className="px-3 py-2 text-slate-600">{formatHistoryDate(item.completedAt)}</td>
              <td className="px-3 py-2">
                <div className="flex flex-wrap justify-end gap-1">
                  <button type="button" onClick={() => onView(item.id)} className="det-card-link-button">View</button>
                  <div
                    className="relative"
                    onBlur={(event) => {
                      if (!event.currentTarget.contains(event.relatedTarget)) {
                        setOpenExportId(null)
                      }
                    }}
                    onKeyDown={(event) => {
                      if (event.key === 'Escape') {
                        setOpenExportId(null)
                      }
                    }}
                  >
                    <button
                      type="button"
                      aria-expanded={openExportId === item.id}
                      aria-haspopup="menu"
                      onClick={() => setOpenExportId((current) => current === item.id ? null : item.id)}
                      className="det-card-link-button"
                    >
                      Export
                      <ChevronDown className="h-3 w-3" aria-hidden="true" />
                    </button>
                    {openExportId === item.id ? (
                      <div role="menu" className="det-history-export-menu">
                        {exportFormats.map(({ format, label }) => (
                          <button
                            key={format}
                            type="button"
                            role="menuitem"
                            onClick={() => {
                              setOpenExportId(null)
                              onExport(item.id, format)
                            }}
                          >
                            <Download className="h-3.5 w-3.5" aria-hidden="true" />
                            {compact && format === 'Markdown' ? 'Markdown' : label}
                          </button>
                        ))}
                      </div>
                    ) : null}
                  </div>
                  {item.canRerun ? (
                    <button type="button" onClick={() => onRerun(item.id)} disabled={isLoading} className="det-card-link-button disabled:cursor-not-allowed disabled:opacity-60">
                      Re-run
                    </button>
                  ) : null}
                  {onDelete ? (
                    <button type="button" onClick={() => onDelete(item.id)} className="det-card-link-button">Delete</button>
                  ) : null}
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

function EmptyState({
  actionLabel,
  body,
  onAction,
  title,
}: {
  actionLabel: string
  body: string
  onAction: () => void
  title: string
}) {
  return (
    <div className="mt-4 border border-slate-200 bg-white p-5">
      <h3 className="text-sm font-semibold text-slate-950">{title}</h3>
      <p className="mt-2 max-w-2xl text-sm text-slate-500">{body}</p>
      <button type="button" onClick={onAction} className="det-run-analysis-button mt-4 h-8">
        {actionLabel}
      </button>
    </div>
  )
}

function InlineError({ message }: { message: string }) {
  return (
    <div className="mt-3 flex items-start gap-2 border border-rose-200 bg-rose-50 p-2.5 text-sm text-rose-800">
      <XCircle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
      <p className="break-words">{message}</p>
    </div>
  )
}

function AnalysisProgress() {
  const steps = ['Loading solution', 'Parsing projects', 'Analyzing architecture', 'Inspecting EF Core', 'Checking security config', 'Calculating score']

  return (
    <div className="det-analysis-progress mt-4 border p-4">
      <div className="flex items-start gap-3">
        <div className="det-analysis-spinner" aria-hidden="true" />
        <div className="min-w-0 flex-1">
          <p className="text-sm font-semibold text-slate-100">Analyzing projects, references, migrations, and configuration...</p>
          <p className="mt-1 text-xs leading-5 text-slate-400">Resolving solution structure and applying production-readiness checks.</p>
        </div>
      </div>
      <div className="det-analysis-progress-bar mt-4" aria-hidden="true">
        <span />
      </div>
      <div className="mt-3 grid gap-2 sm:grid-cols-2">
        {steps.map((step, index) => {
          const status = index < 3 ? 'completed' : index === 3 ? 'current' : 'pending'

          return (
            <div key={step} className={`det-progress-step det-progress-step-${status} flex items-center gap-2 text-sm text-slate-300`}>
              {status === 'completed' ? (
                <CheckCircle2 className="h-3.5 w-3.5 shrink-0 text-emerald-500" aria-hidden="true" />
              ) : status === 'current' ? (
                <Circle className="det-progress-current h-3.5 w-3.5 shrink-0 fill-emerald-500 text-emerald-500" aria-hidden="true" />
              ) : (
                <Circle className="h-3.5 w-3.5 shrink-0 text-slate-500" aria-hidden="true" />
              )}
              {step}
            </div>
          )
        })}
      </div>
    </div>
  )
}

function OverviewPage({
  onOpenArchitecture,
  onOpenFindings,
  onOpenIssue,
  projectCount,
  result,
  severityCounts,
}: {
  onOpenArchitecture: () => void
  onOpenFindings: () => void
  onOpenIssue: (issueId: string) => void
  projectCount: number
  result: AnalysisResult
  severityCounts: Record<Severity, number>
}) {
  const quickRecommendations = getGroupedRecommendedActions(result.issues)

  return (
    <div className="det-overview-page flex-1 overflow-auto">
      <div className="det-overview-canvas">
        <header className="det-overview-masthead">
          <div>
            <div className="det-overview-kicker">Engineering readiness report</div>
            <h1>{result.solutionName}</h1>
            <p>Analyzed {new Date(result.analyzedAt).toLocaleString()} - {projectCount} projects in scope</p>
          </div>
          <button type="button" onClick={onOpenFindings} className="det-overview-primary-action">
            <ListChecks className="h-4 w-4" aria-hidden="true" />
            Review findings
            <ChevronRight className="h-4 w-4" aria-hidden="true" />
          </button>
        </header>

        <OverviewSummary projectCount={projectCount} result={result} severityCounts={severityCounts} />

        <div className="det-overview-priority-layout">
          <BiggestRisksSection onOpenFindings={onOpenFindings} result={result} />
          <RecommendedActionsSection issues={quickRecommendations} onOpenIssue={onOpenIssue} />
        </div>

        <div className="det-overview-support-layout">
          <CategoryScoresSection result={result} />
          <ArchitectureOverviewSection result={result} onOpenArchitecture={onOpenArchitecture} />
        </div>

        <EngineeringAssessmentPanel assessment={result.engineeringAssessment} />
      </div>
    </div>
  )
}

function OverviewSummary({
  projectCount,
  result,
  severityCounts,
}: {
  projectCount: number
  result: AnalysisResult
  severityCounts: Record<Severity, number>
}) {
  const grade = getGrade(result.overallScore)
  const status = getReadinessDecision(result.overallScore, severityCounts)
  const blockerCount = severityCounts.Critical + severityCounts.Error
  const statusTone = status === 'Not Ready' ? 'text-red-700' : status === 'Needs Review' ? 'text-amber-700' : 'text-teal-700'
  const concerns = getPrimaryConcerns(result)

  return (
    <section className="det-readiness-hero" aria-labelledby="readiness-title">
      <div className="det-readiness-score-pane">
        <h2 className="det-readiness-label">Production Readiness</h2>
        <div className="det-readiness-score-line">
          <strong>{result.overallScore}</strong>
          <span>/100</span>
          <em>{grade}</em>
        </div>
        <div className="det-readiness-progress">
          <div className="det-readiness-progress-track">
            <div className={`det-readiness-progress-fill ${getScoreBarClass(result.overallScore)}`} style={{ width: `${result.overallScore}%` }} />
          </div>
        </div>
        <p>Weighted across architecture, dependency injection, EF Core, security, and API readiness.</p>
      </div>

      <div className="det-readiness-decision-pane">
        <div className="det-readiness-label">Release decision</div>
        <h2 id="readiness-title" className={statusTone}>{status}</h2>
        <p>{getOverviewLead(severityCounts)}</p>
        {result.semanticAnalysisSkipped ? (
          <p className="det-analysis-fidelity-note">
            {result.semanticAnalysisSkippedReason ?? 'Semantic project loading was skipped. DotDet used safe syntax-based analysis.'}
          </p>
        ) : null}
        <div className="det-readiness-concerns">
          <span>Primary concerns</span>
          <ul>
            {(concerns.length > 0 ? concerns : ['No major concerns detected']).slice(0, 3).map((concern) => (
              <li key={concern}>{concern}</li>
            ))}
          </ul>
        </div>
      </div>

      <dl className="det-readiness-metrics">
        <div>
          <dt>Critical / error</dt>
          <dd className={blockerCount > 0 ? 'text-red-700' : 'text-teal-700'}>{blockerCount}</dd>
          <span>release-impact findings</span>
        </div>
        <div>
          <dt>Warnings</dt>
          <dd className={severityCounts.Warning > 0 ? 'text-amber-700' : 'text-teal-700'}>{severityCounts.Warning}</dd>
          <span>review recommended</span>
        </div>
        <div>
          <dt>Projects</dt>
          <dd>{projectCount}</dd>
          <span>analyzed in solution</span>
        </div>
      </dl>
    </section>
  )
}

function CategoryScoresSection({ result }: { result: AnalysisResult }) {
  return (
    <section className="det-overview-panel det-category-health-panel">
      <div className="det-overview-panel-heading">
        <div>
          <span>Category health</span>
          <h2>Readiness by engineering domain</h2>
        </div>
      </div>
      <div className="det-category-health-list">
        {categoryDefinitions.map((category) => {
          const score = result.categoryScores[category.scoreKey]

          return (
            <div key={category.key} className="det-category-health-row">
              <div className="det-category-health-name">
                <category.icon aria-hidden="true" />
                <span>{category.reportLabel}</span>
              </div>
              <div className="det-category-health-track"><span style={{ width: `${score}%` }} /></div>
              <strong>{score}</strong>
            </div>
          )
        })}
      </div>
    </section>
  )
}

function BiggestRisksSection({
  onOpenFindings,
  result,
}: {
  onOpenFindings: () => void
  result: AnalysisResult
}) {
  const riskAreas = getRiskAreas(result).slice(0, 3)

  return (
    <section className="det-overview-panel det-overview-risk-panel">
      <div className="det-overview-panel-heading">
        <div>
          <span>Priority review</span>
          <h2>Biggest risks</h2>
          <p>Ranked by release impact, confidence, and category health.</p>
        </div>
        <button type="button" onClick={onOpenFindings} className="det-card-link-button">
          All findings <ChevronRight className="h-3.5 w-3.5" aria-hidden="true" />
        </button>
      </div>
      <div className="det-overview-risk-list">
        {riskAreas.length > 0 ? (
          riskAreas.map((area, index) => (
            <div key={area.category.key} className="det-overview-risk-row">
              <span className="det-overview-risk-rank">0{index + 1}</span>
              <area.category.icon className={categoryTextClass(area.category.key)} aria-hidden="true" />
              <div className="det-overview-risk-copy">
                <strong>{area.category.reportLabel}</strong>
                <span>{area.criticalCount} critical/error - {area.issueCount} findings</span>
              </div>
              <div className="det-overview-risk-score">
                <strong>{area.score}</strong>
                <div><span style={{ width: `${area.score}%` }} /></div>
              </div>
            </div>
          ))
        ) : (
          <div className="det-overview-clean-state">
            <CheckCircle2 aria-hidden="true" />
            <div>
              <strong>No significant production risks detected.</strong>
              <p>No active production findings are currently affecting category risk.</p>
            </div>
          </div>
        )}
      </div>
    </section>
  )
}

function EngineeringAssessmentPanel({ assessment }: { assessment?: EngineeringAssessmentSummary }) {
  if (!assessment) {
    return (
      <section className="det-overview-panel det-engineering-assessment-panel">
        <h2 className="text-lg font-semibold text-slate-950">Engineering Assessment</h2>
        <p className="mt-1 text-xs text-slate-500">Run analysis again to generate the deterministic architecture assessment.</p>
      </section>
    )
  }

  const sections = [
    { title: 'Strong Areas', items: assessment.strongAreas },
    { title: 'Architectural Observations', items: assessment.architecturalObservations },
    { title: 'Security Observations', items: assessment.securityObservations },
    { title: 'API Readiness Observations', items: assessment.apiReadinessObservations },
    { title: 'Maintainability Observations', items: assessment.maintainabilityObservations },
  ]

  return (
    <section className="det-overview-panel det-engineering-assessment-panel">
      <div className="det-overview-panel-heading">
        <div>
          <span>Supporting analysis</span>
          <h2>Engineering assessment</h2>
          <p>Deterministic observations derived from findings, scores, and dependency structure.</p>
        </div>
      </div>
      <div className="det-assessment-score-explanation">
        <span>Score explanation</span>
        <p>{assessment.scoreExplanation}</p>
      </div>
      <div className="det-assessment-grid">
        {sections.map((section) => (
          <div key={section.title} className="det-assessment-block">
            <h3>{section.title}</h3>
            <ul>
              {section.items.length > 0 ? (
                section.items.map((item, index) => (
                  <li key={`${section.title}-${index}`}>{item}</li>
                ))
              ) : (
                <li className="text-slate-500">{getEngineeringAssessmentEmptyText(section.title)}</li>
              )}
            </ul>
          </div>
        ))}
      </div>
    </section>
  )
}

function RecommendedActionsSection({
  issues,
  onOpenIssue,
}: {
  issues: RecommendedAction[]
  onOpenIssue: (issueId: string) => void
}) {
  const subtitle = issues.length > 0
    ? 'Start with the highest-confidence, highest-impact work.'
    : 'No immediate remediation actions were identified.'

  return (
    <section className="det-overview-panel det-overview-actions-panel">
      <div className="det-overview-panel-heading">
        <div>
          <span>Remediation roadmap</span>
          <h2>Recommended next actions</h2>
          <p>{subtitle}</p>
        </div>
      </div>
      {issues.length > 0 ? (
        <div className="det-recommendation-list">
          {issues.map((issue, index) => (
            <button
              key={issue.id}
              type="button"
              onClick={() => onOpenIssue(issue.id)}
              className="det-recommendation-item text-left"
            >
              <span className="det-recommendation-rank">0{index + 1}</span>
              <div className="min-w-0">
                <h3>{getRecommendedActionTitle(issue)}</h3>
                <div className="det-recommendation-meta">
                  <SeverityLabel severity={issue.severity} />
                  <CategoryText category={issue.category} />
                  <span className="truncate text-slate-500">{issue.filePath ? formatPath(issue.filePath) : issue.projectName ?? 'Solution'}</span>
                </div>
                <p>{getConciseRecommendation(issue.recommendation)}</p>
              </div>
              <ChevronRight className="det-recommendation-arrow" aria-hidden="true" />
            </button>
          ))}
        </div>
      ) : (
        <div className="det-overview-clean-state"><CheckCircle2 aria-hidden="true" /><p>No action list is shown because there are no active production findings.</p></div>
      )}
    </section>
  )
}

function getEngineeringAssessmentEmptyText(sectionTitle: string) {
  if (sectionTitle === 'Highest Risks') {
    return 'No significant production risks detected.'
  }

  if (sectionTitle === 'Recommended Priorities') {
    return 'No immediate remediation actions were identified.'
  }

  return 'No additional observations detected.'
}

function ArchitectureOverviewSection({
  onOpenArchitecture,
  result,
}: {
  onOpenArchitecture: () => void
  result: AnalysisResult
}) {
  const map = result.architectureMap ?? buildFallbackArchitectureMap(result.projectGraph, result.issues)
  const boundaryRisks = map.dependencies.filter((dependency) => dependency.isViolation).length

  return (
    <section className="det-overview-panel det-architecture-summary-panel">
      <div className="det-overview-panel-heading">
        <div>
          <span>Solution structure</span>
          <h2>Architecture map</h2>
          <p>Project references, dependency direction, and boundary health.</p>
        </div>
        <button type="button" onClick={onOpenArchitecture} className="det-card-link-button">
          Explore map <ChevronRight className="h-3.5 w-3.5" aria-hidden="true" />
        </button>
      </div>
      <dl className="det-architecture-summary-metrics">
        <CompactMetric label="Projects" value={map.projects.length} tone="neutral" />
        <CompactMetric label="Dependencies" value={map.dependencies.length} tone="neutral" />
        <CompactMetric label="Boundary Risks" value={boundaryRisks} tone={boundaryRisks > 0 ? 'danger' : 'ok'} />
      </dl>
      <div className="det-architecture-summary-flow" aria-hidden="true">
        {map.layers.slice(0, 4).map((layer, index) => (
          <div key={layer.name}>
            <span>{layer.name}</span>
            {index < Math.min(map.layers.length, 4) - 1 ? <ChevronRight /> : null}
          </div>
        ))}
      </div>
    </section>
  )
}

function FindingsPage({
  activeCategory,
  activeProject,
  activeSeverity,
  filteredIssues,
  getDisposition,
  hideSuppressed,
  onCategoryChange,
  onHideSuppressedChange,
  onProjectChange,
  onQueryChange,
  onSelectIssue,
  onSeverityChange,
  onSortModeChange,
  projects,
  query,
  selectedIssueId,
  sortMode,
  totalCount,
}: {
  activeCategory: CategoryFilter
  activeProject: ProjectFilter
  activeSeverity: SeverityFilter
  filteredIssues: AnalysisIssue[]
  getDisposition: (issue: AnalysisIssue) => FindingDisposition
  hideSuppressed: boolean
  onCategoryChange: (category: CategoryFilter) => void
  onHideSuppressedChange: (hide: boolean) => void
  onProjectChange: (project: ProjectFilter) => void
  onQueryChange: (query: string) => void
  onSelectIssue: (issueId: string) => void
  onSeverityChange: (severity: SeverityFilter) => void
  onSortModeChange: (sortMode: SortMode) => void
  projects: ProjectNode[]
  query: string
  selectedIssueId: string | null
  sortMode: SortMode
  totalCount: number
}) {
  return (
    <div className="flex min-h-0 flex-1 flex-col p-4">
      <section className="flex min-h-0 flex-1 flex-col overflow-hidden border border-slate-300 bg-white">
        <div className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-200 px-3 py-2">
          <div>
            <h2 className="text-sm font-semibold text-slate-950">Findings</h2>
            <p className="text-xs text-slate-500">Review analyzer findings.</p>
          </div>
        </div>
        <FindingsToolbar
          activeCategory={activeCategory}
          activeProject={activeProject}
          activeSeverity={activeSeverity}
          filteredCount={filteredIssues.length}
          onCategoryChange={onCategoryChange}
          hideSuppressed={hideSuppressed}
          onHideSuppressedChange={onHideSuppressedChange}
          onProjectChange={onProjectChange}
          onQueryChange={onQueryChange}
          onSeverityChange={onSeverityChange}
          onSortModeChange={onSortModeChange}
          projects={projects}
          query={query}
          sortMode={sortMode}
          totalCount={totalCount}
        />
        <FindingsTable
          getDisposition={getDisposition}
          issues={filteredIssues}
          onSelectIssue={onSelectIssue}
          selectedIssueId={selectedIssueId}
        />
      </section>
    </div>
  )
}

function SettingsPage({
  commandBarMode,
  defaultSortMode,
  density,
  editorFontFamily,
  exportFormat,
  includeSourcePreview,
  onBack,
  onCommandBarModeChange,
  onDefaultSortModeChange,
  onDensityChange,
  onEditorFontFamilyChange,
  onExportFormatChange,
  onIncludeSourcePreviewChange,
  onOpenFirstFindingChange,
  onShowGutterMarkersChange,
  onShowMinimapMarkersChange,
  openFirstFinding,
  showGutterMarkers,
  showMinimapMarkers,
}: {
  commandBarMode: CommandBarMode
  defaultSortMode: SortMode
  density: DensityMode
  editorFontFamily: EditorFontFamily
  exportFormat: ExportFormat
  includeSourcePreview: boolean
  onBack?: () => void
  onCommandBarModeChange: (mode: CommandBarMode) => void
  onDefaultSortModeChange: (mode: SortMode) => void
  onDensityChange: (mode: DensityMode) => void
  onEditorFontFamilyChange: (fontFamily: EditorFontFamily) => void
  onExportFormatChange: (format: ExportFormat) => void
  onIncludeSourcePreviewChange: (include: boolean) => void
  onOpenFirstFindingChange: (open: boolean) => void
  onShowGutterMarkersChange: (show: boolean) => void
  onShowMinimapMarkersChange: (show: boolean) => void
  openFirstFinding: boolean
  showGutterMarkers: boolean
  showMinimapMarkers: boolean
}) {
  const [activeSection, setActiveSection] = useState<SettingsSectionId>('Editor')
  const sections: SettingsSectionId[] = ['Editor', 'Analysis', 'Export', 'Advanced']

  return (
    <div className="flex-1 overflow-auto p-3">
      <section className="mx-auto max-w-5xl">
        <div className="flex items-center justify-between gap-3 px-3 py-3">
          <div>
            {onBack ? (
              <button type="button" onClick={onBack} className="mb-1 text-xs font-medium text-slate-600 hover:text-[#0d660d]">
                Back to home
              </button>
            ) : null}
            <h2 className="text-sm font-semibold text-slate-950">Settings</h2>
            <p className="mt-0.5 text-xs text-slate-500">Configure the .DET workbench experience. Changes are saved automatically.</p>
          </div>
          <span className="text-[11px] font-medium text-slate-500">Local preferences</span>
        </div>

        <div className="grid lg:grid-cols-[220px_minmax(0,1fr)]">
          <nav className="bg-slate-50 p-2 text-xs">
            {sections.map((item) => (
              <button
                key={item}
                type="button"
                onClick={() => setActiveSection(item)}
                className={`block w-full px-2 py-1.5 text-left ${
                  item === activeSection ? 'bg-white font-semibold text-slate-950' : 'text-slate-600 hover:bg-white hover:text-slate-900'
                }`}
              >
                {item}
              </button>
            ))}
          </nav>

          <div>
            {activeSection === 'Editor' ? (
              <SettingsSection title="Editor">
                <SettingsToggle
                  checked={showGutterMarkers}
                  description="Show clickable severity markers in the editor gutter."
                  label="Gutter markers"
                  onChange={() => onShowGutterMarkersChange(!showGutterMarkers)}
                />
                <SettingsToggle
                  checked={showMinimapMarkers}
                  description="Show finding positions in the Monaco minimap and overview ruler."
                  label="Minimap markers"
                  onChange={() => onShowMinimapMarkersChange(!showMinimapMarkers)}
                />
              </SettingsSection>
            ) : null}

            {activeSection === 'Analysis' ? (
              <SettingsSection title="Analysis">
                <SettingsToggle
                  checked={openFirstFinding}
                  description="Open and scroll to the first finding after analysis completes."
                  label="Open first finding"
                  onChange={() => onOpenFirstFindingChange(!openFirstFinding)}
                />
                <SettingsSelect
                  description="Default ordering for the findings table when analysis starts."
                  label="Default sort"
                  onChange={(value) => onDefaultSortModeChange(value as SortMode)}
                  options={['Severity', 'Category', 'Project', 'File']}
                  value={defaultSortMode}
                />
              </SettingsSection>
            ) : null}

            {activeSection === 'Export' ? (
              <SettingsSection title="Export">
                <SettingsSelect
                  description="Default file format for exported reports."
                  label="Report format"
                  onChange={(value) => onExportFormatChange(value as ExportFormat)}
                  options={['HTML', 'Markdown', 'JSON']}
                  value={exportFormat}
                />
                <SettingsToggle
                  checked={includeSourcePreview}
                  description="Include generated source preview content in exported reports."
                  label="Include source preview"
                  onChange={() => onIncludeSourcePreviewChange(!includeSourcePreview)}
                />
              </SettingsSection>
            ) : null}

            {activeSection === 'Advanced' ? (
              <SettingsSection title="Advanced">
                <SettingsSelect
                  description="Controls panel and table spacing throughout the workbench."
                  label="Density"
                  onChange={(value) => onDensityChange(value as DensityMode)}
                  options={['Compact', 'Comfortable']}
                  value={density}
                />
                <SettingsSelect
                  description="Controls how the top command bar is presented."
                  label="Command bar"
                  onChange={(value) => onCommandBarModeChange(value as CommandBarMode)}
                  options={['Compact Microsoft style', 'Expanded']}
                  value={commandBarMode}
                />
                <SettingsSelect
                  description="Read-only code editor font."
                  label="Font family"
                  onChange={(value) => onEditorFontFamilyChange(value as EditorFontFamily)}
                  options={['Cascadia Code', 'Consolas']}
                  value={editorFontFamily}
                />
              </SettingsSection>
            ) : null}
          </div>
        </div>
      </section>
    </div>
  )
}

function SettingsSection({ children, title }: { children: React.ReactNode; title: string }) {
  return (
    <section className="p-3">
      <h3 className="mb-3 text-xs font-semibold uppercase tracking-wide text-slate-500">{title}</h3>
      <div className="space-y-0">{children}</div>
    </section>
  )
}

function SettingsToggle({
  checked,
  description,
  label,
  onChange,
}: {
  checked: boolean
  description: string
  label: string
  onChange: () => void
}) {
  return (
    <label className="grid cursor-pointer grid-cols-[minmax(0,1fr)_auto] gap-3 px-3 py-3 text-sm">
      <span>
        <span className="block font-medium text-slate-900">{label}</span>
        <span className="mt-0.5 block text-xs text-slate-500">{description}</span>
      </span>
      <input type="checkbox" checked={checked} onChange={onChange} />
    </label>
  )
}

function SettingsSelect({
  description,
  label,
  onChange,
  options,
  value,
}: {
  description: string
  label: string
  onChange: (value: string) => void
  options: string[]
  value: string
}) {
  return (
    <div className="grid gap-2 px-3 py-3 text-sm md:grid-cols-[minmax(0,1fr)_220px] md:items-center">
      <span>
        <span className="block font-medium text-slate-900">{label}</span>
        <span className="mt-0.5 block text-xs text-slate-500">{description}</span>
      </span>
      <select value={value} onChange={(event) => onChange(event.target.value)} className="h-7 border border-slate-300 bg-white px-2 text-xs text-slate-700">
        {options.map((option) => (
          <option key={option}>{option}</option>
        ))}
      </select>
    </div>
  )
}

function RuleExplorerPage({
  currentIssues,
  error,
  onOpenIssue,
  onSelectRule,
  rules,
  selectedRuleId,
}: {
  currentIssues: AnalysisIssue[]
  error: string | null
  onOpenIssue: (issueId: string) => void
  onSelectRule: (ruleId: string) => void
  rules: RuleDocumentation[]
  selectedRuleId: string | null
}) {
  const [query, setQuery] = useState('')
  const [categoryFilter, setCategoryFilter] = useState<CategoryFilter>('All')
  const normalizedQuery = query.trim().toLowerCase()
  const activeRuleSummaries = currentIssues.reduce((summaries, issue) => {
    const ruleId = getRuleId(issue)
    const current = summaries.get(ruleId) ?? { count: 0, maxSeverity: 0 }
    summaries.set(ruleId, {
      count: current.count + 1,
      maxSeverity: Math.max(current.maxSeverity, severityRank[issue.severity]),
    })
    return summaries
  }, new Map<string, { count: number; maxSeverity: number }>())
  const filteredRules = rules
    .map((rule) => ({
      ...rule,
      activeCount: activeRuleSummaries.get(rule.ruleId)?.count ?? 0,
      activeSeverityRank: activeRuleSummaries.get(rule.ruleId)?.maxSeverity ?? severityRank[rule.severity],
    }))
    .filter((rule) => {
    const activeMatches = rule.activeCount > 0
    const categoryMatches = categoryFilter === 'All' || rule.category === categoryFilter
    const queryMatches =
      !normalizedQuery ||
      [
        rule.ruleId,
        rule.title,
        rule.category,
        rule.problemSummary,
        rule.detectionLogic,
        rule.recommendedPattern,
        rule.falsePositiveGuidance,
      ]
        .join(' ')
        .toLowerCase()
        .includes(normalizedQuery)

    return activeMatches && categoryMatches && queryMatches
  })
    .sort((left, right) => {
      if (currentIssues.length > 0) {
        const activeDelta = Number(right.activeCount > 0) - Number(left.activeCount > 0)
        if (activeDelta !== 0) return activeDelta

        const severityDelta = right.activeSeverityRank - left.activeSeverityRank
        if (severityDelta !== 0) return severityDelta

        const countDelta = right.activeCount - left.activeCount
        if (countDelta !== 0) return countDelta
      }

      return left.ruleId.localeCompare(right.ruleId)
    })
  const selectedRule =
    filteredRules.find((rule) => rule.ruleId === selectedRuleId)
    ?? filteredRules[0]
    ?? null
  const selectedRuleFindings = selectedRule
    ? currentIssues.filter((issue) => getRuleId(issue) === selectedRule.ruleId)
    : []
  const selectedRuleFindingGroups = getGroupedRuleFindings(selectedRuleFindings)
  const relatedRules = selectedRule
    ? selectedRule.relatedRules
        .map((ruleId) => rules.find((rule) => rule.ruleId === ruleId))
        .filter((rule): rule is RuleDocumentation => Boolean(rule && activeRuleSummaries.has(rule.ruleId)))
    : []

  return (
    <div className="det-rule-explorer-page grid min-h-0 flex-1 grid-cols-[280px_minmax(0,1fr)_320px] overflow-hidden">
      <aside className="det-rule-nav min-h-0 border-r border-slate-300 bg-white">
        <div className="border-b border-slate-200 bg-slate-50 px-3 py-2">
          <h2 className="text-sm font-semibold text-slate-950">Rule Explorer</h2>
          <p className="mt-1 text-xs text-slate-500">{filteredRules.length || 'No'} rules detected in this analysis.</p>
        </div>
        <div className="border-b border-slate-200 p-2">
          <label className="sr-only" htmlFor="rule-search">
            Search rules
          </label>
          <div className="flex items-center gap-2 border border-slate-300 bg-white px-2">
            <Search className="h-3.5 w-3.5 text-slate-500" aria-hidden="true" />
            <input
              id="rule-search"
              value={query}
              onChange={(event) => setQuery(event.target.value)}
              placeholder="Search rules"
              className="h-7 min-w-0 flex-1 bg-transparent text-xs outline-none"
            />
          </div>
          <select
            value={categoryFilter}
            onChange={(event) => setCategoryFilter(event.target.value as CategoryFilter)}
            className="mt-2 h-7 w-full border border-slate-300 bg-white px-2 text-xs text-slate-700 outline-none focus:border-teal-500"
          >
            <option value="All">All categories</option>
            {categoryDefinitions.map((category) => (
              <option key={category.key} value={category.key}>
                {category.reportLabel}
              </option>
            ))}
          </select>
        </div>
        {error ? <div className="m-2 p-2 text-xs text-red-700">{error}</div> : null}
        <div className="h-[calc(100%-90px)] overflow-auto">
          {filteredRules.map((rule) => {
            const findingCount = rule.activeCount
            const selected = selectedRule?.ruleId === rule.ruleId

            return (
              <button
                key={rule.ruleId}
                type="button"
                onClick={() => onSelectRule(rule.ruleId)}
                className={`block w-full px-3 py-2 text-left transition ${
                  selected ? `${selectedRowClass} font-semibold` : 'text-slate-800 hover:bg-slate-50'
                }`}
              >
                <div className="flex items-center gap-2">
                  <span className="font-mono text-[11px] font-semibold text-slate-500">{rule.ruleId}</span>
                  <SeverityLabel severity={rule.severity} />
                </div>
                <p className="mt-1 line-clamp-2 text-xs font-semibold leading-4">{rule.title}</p>
                <div className="mt-1 flex items-center justify-between gap-2 text-[11px] text-slate-500">
                  <span className="truncate">{getCategoryLabel(rule.category)}</span>
                  <span>{findingCount} active</span>
                </div>
              </button>
            )
          })}
          {filteredRules.length === 0 ? <p className="p-3 text-xs text-slate-500">No detected rules match the current filters.</p> : null}
        </div>
      </aside>

      <main className="det-rule-document min-h-0 overflow-auto bg-slate-50 p-4">
        {selectedRule ? (
          <article className="det-rule-article mx-auto max-w-5xl border border-slate-300 bg-white">
            <header className="border-b border-slate-200 bg-[#f3f2f1] px-4 py-3">
              <div className="flex flex-wrap items-center gap-2">
                <span className="font-mono text-xs font-semibold text-slate-600">{selectedRule.ruleId}</span>
                <SeverityLabel severity={selectedRule.severity} />
                <ConfidenceLabel confidence={selectedRule.confidence} />
                <span className="text-xs text-slate-500">{selectedRule.detectionMethod}</span>
              </div>
              <h2 className="mt-2 text-xl font-semibold text-slate-950">{selectedRule.title}</h2>
              <p className="mt-2 max-w-3xl text-sm leading-6 text-slate-700">{selectedRule.problemSummary}</p>
            </header>

            <div className="grid gap-0 lg:grid-cols-2">
              <RuleDocBlock title="Problem Summary">
                <p>{selectedRule.problemSummary}</p>
              </RuleDocBlock>
              <RuleDocBlock title="Why It Matters">
                <p>{selectedRule.whyItMatters}</p>
              </RuleDocBlock>
              <RuleDocBlock title="Detection Logic">
                <p>{selectedRule.detectionLogic}</p>
              </RuleDocBlock>
              <RuleDocBlock title="Confidence Explanation">
                <p>{selectedRule.confidenceExplanation}</p>
              </RuleDocBlock>
              <RuleDocBlock title="Recommended Pattern">
                <p>{selectedRule.recommendedPattern}</p>
              </RuleDocBlock>
              <RuleDocBlock title="Suggested Implementation">
                <p>{selectedRule.suggestedImplementation}</p>
              </RuleDocBlock>
              <RuleDocBlock title="False Positive Guidance">
                <p>{selectedRule.falsePositiveGuidance}</p>
              </RuleDocBlock>
              <RuleDocBlock title="Microsoft Documentation">
                {selectedRule.documentationLinks.length > 0 ? (
                  <ul className="space-y-1.5">
                    {selectedRule.documentationLinks.map((link) => (
                      <li key={link.href}>
                        <a className="inline-flex items-center gap-1 font-medium text-teal-700 hover:text-teal-900" href={link.href} target="_blank" rel="noreferrer">
                          {link.label}
                          <ExternalLink className="h-3 w-3" aria-hidden="true" />
                        </a>
                      </li>
                    ))}
                  </ul>
                ) : (
                  <p>No documentation links are registered for this rule.</p>
                )}
              </RuleDocBlock>
            </div>

            {(selectedRule.suggestedCodeSnippet || selectedRule.badExample || selectedRule.goodExample) ? (
              <div className="det-rule-code-flow border-t border-slate-200 p-4">
                <h3 className="mb-3 text-xs font-semibold uppercase tracking-wide text-slate-500">Code Examples</h3>
                <div className="space-y-4">
                  {selectedRule.suggestedCodeSnippet ? <RuleCodeExample title="Suggested Implementation" code={selectedRule.suggestedCodeSnippet} /> : null}
                  {selectedRule.badExample ? <RuleCodeExample title="Bad Example" code={selectedRule.badExample} tone="bad" /> : null}
                  {selectedRule.goodExample ? <RuleCodeExample title="Good Example" code={selectedRule.goodExample} tone="good" /> : null}
                </div>
              </div>
            ) : null}
          </article>
        ) : (
          <div className="flex h-full items-center justify-center text-sm text-slate-500">Rule documentation is loading.</div>
        )}
      </main>

      <aside className="det-rule-context min-h-0 overflow-auto border-l border-slate-300 bg-white">
        <div className="border-b border-slate-200 bg-slate-50 px-3 py-2">
          <h3 className="text-sm font-semibold text-slate-950">Rule Context</h3>
          <p className="mt-1 text-xs text-slate-500">Current findings and related rules.</p>
        </div>
        {selectedRule ? (
          <div className="p-3">
            <DetailBlock title="Active Findings">
              {selectedRuleFindingGroups.length > 0 ? (
                <div className="space-y-1.5">
                  {selectedRuleFindingGroups.map((group) => (
                    <button
                      key={group.issue.id}
                      type="button"
                      onClick={() => onOpenIssue(group.issue.id)}
                      className="block w-full px-2 py-1.5 text-left transition hover:bg-slate-50"
                    >
                      <span className="text-[11px] font-semibold text-slate-500">
                        {group.issue.projectName ?? 'Solution'}{group.count > 1 ? ` - ${group.count} findings` : ''}
                      </span>
                      <span className="mt-0.5 block text-xs font-medium leading-4 text-slate-900">{group.issue.title}</span>
                      <span className="mt-0.5 block truncate text-[11px] text-slate-500">
                        {group.issue.filePath ? formatPath(group.issue.filePath) : 'No file location'}
                      </span>
                    </button>
                  ))}
                </div>
              ) : (
                <p className="text-xs text-slate-500">This rule did not produce findings in the current analysis.</p>
              )}
            </DetailBlock>

            <DetailBlock title="Related Rules">
              {relatedRules.length > 0 ? (
                <div className="space-y-1.5">
                  {relatedRules.map((rule) => (
                    <button
                      key={rule.ruleId}
                      type="button"
                      onClick={() => onSelectRule(rule.ruleId)}
                      className="block w-full px-2 py-1.5 text-left transition hover:bg-slate-50"
                    >
                      <span className="font-mono text-[11px] font-semibold text-slate-500">{rule.ruleId}</span>
                      <span className="mt-0.5 block text-xs font-medium leading-4 text-slate-900">{rule.title}</span>
                    </button>
                  ))}
                </div>
              ) : (
                <p className="text-xs text-slate-500">No related rules are registered.</p>
              )}
            </DetailBlock>
          </div>
        ) : null}
      </aside>
    </div>
  )
}

function RuleDocBlock({ children, title }: { children: React.ReactNode; title: string }) {
  return (
    <section className="border-b border-r border-slate-200 p-4 text-sm leading-6 text-slate-700">
      <h3 className="mb-2 text-xs font-semibold uppercase tracking-wide text-slate-500">{title}</h3>
      {children}
    </section>
  )
}

function RuleCodeExample({
  code,
  title,
  tone = 'neutral',
}: {
  code: string
  title: string
  tone?: 'neutral' | 'bad' | 'good'
}) {
  const toneClass =
    tone === 'bad'
      ? 'border-red-900/40'
      : tone === 'good'
        ? 'border-emerald-900/40'
        : 'border-slate-700'

  return (
    <section>
      <h3 className="mb-1 text-xs font-semibold uppercase tracking-wide text-slate-500">{title}</h3>
      <pre className={`max-h-96 overflow-auto whitespace-pre-wrap break-words border ${toneClass} bg-slate-950 p-3 text-[11px] leading-4 text-slate-100`}>
        <code>{code}</code>
      </pre>
    </section>
  )
}

function CodeExplorerPage({
  editorFontFamily,
  files,
  getDisposition,
  onNextFinding,
  onOpenRule,
  onPreviousFinding,
  onSelectFile,
  onSelectIssue,
  onUpdateDisposition,
  projectIssueCounts,
  relatedIssues,
  result,
  selectedFile,
  selectedIssue,
  showGutterMarkers,
  showMinimapMarkers,
}: {
  editorFontFamily: EditorFontFamily
  files: CodeFile[]
  getDisposition: (issue: AnalysisIssue) => FindingDisposition
  onNextFinding: () => void
  onOpenRule: (ruleId: string) => void
  onPreviousFinding: () => void
  onSelectFile: (fileId: string) => void
  onSelectIssue: (issueId: string) => void
  onUpdateDisposition: (issue: AnalysisIssue, disposition: FindingDisposition) => void
  projectIssueCounts: Map<string, number>
  relatedIssues: AnalysisIssue[]
  result: AnalysisResult
  selectedFile: CodeFile | null
  selectedIssue: AnalysisIssue | null
  showGutterMarkers: boolean
  showMinimapMarkers: boolean
}) {
  const [explorerWidth, setExplorerWidth] = useState(() => clamp(getStoredPanelWidth(explorerWidthStorageKey, 220), 220, 560))
  const [guideWidth, setGuideWidth] = useState(() => clamp(getStoredPanelWidth(guideWidthStorageKey, 320), 280, 680))
  const fileIssues = selectedFile ? result.issues.filter((issue) => getFileId(issue.filePath) === selectedFile.id) : []
  const selectedIssueIndex = Math.max(
    0,
    fileIssues.findIndex((issue) => issue.id === selectedIssue?.id),
  )

  useEffect(() => {
    localStorage.setItem(explorerWidthStorageKey, String(explorerWidth))
  }, [explorerWidth])

  useEffect(() => {
    localStorage.setItem(guideWidthStorageKey, String(guideWidth))
  }, [guideWidth])

  function selectIssueInCode(issueId: string) {
    const issue = result.issues.find((candidate) => candidate.id === issueId)
    if (issue?.filePath) {
      onSelectFile(getFileId(issue.filePath))
    }
    onSelectIssue(issueId)
  }

  function startResize(panel: ResizePanel, event: React.MouseEvent<HTMLButtonElement>) {
    event.preventDefault()
    const startX = event.clientX
    const startExplorerWidth = explorerWidth
    const startGuideWidth = guideWidth

    function onMouseMove(moveEvent: MouseEvent) {
      if (panel === 'explorer') {
        setExplorerWidth(clamp(startExplorerWidth + moveEvent.clientX - startX, 220, 560))
      } else {
        setGuideWidth(clamp(startGuideWidth + startX - moveEvent.clientX, 280, 680))
      }
    }

    function onMouseUp() {
      document.body.style.cursor = ''
      document.body.style.userSelect = ''
      window.removeEventListener('mousemove', onMouseMove)
      window.removeEventListener('mouseup', onMouseUp)
    }

    document.body.style.cursor = 'col-resize'
    document.body.style.userSelect = 'none'
    window.addEventListener('mousemove', onMouseMove)
    window.addEventListener('mouseup', onMouseUp)
  }

  return (
    <div
      className="grid h-[calc(100vh-74px)] min-h-[560px] flex-1 gap-0 overflow-hidden"
      style={{ gridTemplateColumns: `${explorerWidth}px 7px minmax(420px, 1fr) 7px ${guideWidth}px` }}
    >
      <CodeSolutionExplorer
        files={files}
        onSelectFile={onSelectFile}
        projectIssueCounts={projectIssueCounts}
        projects={result.projectGraph.projects}
        result={result}
        selectedFileId={selectedFile?.id ?? null}
      />

      <PanelResizeHandle label="Resize Solution Explorer" onMouseDown={(event) => startResize('explorer', event)} />

      <main className="flex min-h-0 flex-col overflow-hidden border-x border-slate-300 bg-slate-950">
        <div className="flex h-8 items-center justify-between gap-3 border-b border-slate-800 bg-slate-900 px-2 text-slate-200">
          <div className="flex min-w-0 items-baseline gap-2">
            <h2 className="truncate text-sm font-semibold">{selectedFile?.name ?? 'No file selected'}</h2>
            <span className="min-w-0 truncate text-xs text-slate-500">{selectedFile ? getDirectoryPath(selectedFile.path) : 'Select a source file'}</span>
          </div>
          <div className="flex shrink-0 items-center gap-1.5">
            <span className="text-xs text-slate-400">
              {fileIssues.length > 0 ? `Finding ${selectedIssueIndex + 1} of ${fileIssues.length}` : 'No findings'}
            </span>
            <span className="border border-slate-700 bg-slate-950 px-1.5 py-0.5 text-xs text-slate-300">{fileIssues.length} Findings</span>
            <button
              type="button"
              onClick={onPreviousFinding}
              disabled={fileIssues.length < 2 || selectedIssueIndex === 0}
              className="h-6 border border-slate-700 px-2 text-xs font-medium text-slate-200 transition hover:border-teal-500 disabled:cursor-not-allowed disabled:text-slate-600"
            >
              Previous
            </button>
            <button
              type="button"
              onClick={onNextFinding}
              disabled={fileIssues.length < 2 || selectedIssueIndex >= fileIssues.length - 1}
              className="h-6 border border-slate-700 px-2 text-xs font-medium text-slate-200 transition hover:border-teal-500 disabled:cursor-not-allowed disabled:text-slate-600"
            >
              Next
            </button>
          </div>
        </div>

        <SourceViewer
          editorFontFamily={editorFontFamily}
          file={selectedFile}
          issues={fileIssues}
          onSelectIssue={selectIssueInCode}
          selectedIssue={selectedIssue}
          showGutterMarkers={showGutterMarkers}
          showMinimapMarkers={showMinimapMarkers}
          sourcePreviewUnavailableReason={getSourcePreviewUnavailableReason(result)}
        />
      </main>

      <PanelResizeHandle label="Resize Engineering Guide" onMouseDown={(event) => startResize('guide', event)} />

      <EngineeringGuidePanel
        disposition={selectedIssue ? getDisposition(selectedIssue) : 'Open'}
        issue={selectedIssue}
        onOpenRule={onOpenRule}
        onSelectIssue={selectIssueInCode}
        onUpdateDisposition={onUpdateDisposition}
        relatedIssues={relatedIssues}
      />
    </div>
  )
}

function PanelResizeHandle({
  label,
  onMouseDown,
}: {
  label: string
  onMouseDown: (event: React.MouseEvent<HTMLButtonElement>) => void
}) {
  return (
    <button
      type="button"
      aria-label={label}
      onMouseDown={onMouseDown}
      className="det-splitter group flex cursor-col-resize items-stretch justify-center transition"
    >
      <span className="my-2 w-px transition" />
    </button>
  )
}

function CodeSolutionExplorer({
  files,
  onSelectFile,
  projectIssueCounts,
  projects,
  result,
  selectedFileId,
}: {
  files: CodeFile[]
  onSelectFile: (fileId: string) => void
  projectIssueCounts: Map<string, number>
  projects: ProjectNode[]
  result: AnalysisResult
  selectedFileId: string | null
}) {
  const [expandedProjects, setExpandedProjects] = useState(() => new Set([...projects.map((project) => project.name), 'Solution']))
  const [showAllFiles, setShowAllFiles] = useState(() => result.sourcePreviewAvailable !== false)
  const sourcePreviewUnavailableReason = getSourcePreviewUnavailableReason(result)
  const filesByProject = groupFilesByProject(files)
  const explorerProjects = getCodeExplorerProjects(projects, filesByProject)
  const issuesByFile = result.issues.reduce((groups, issue) => {
    const fileId = getFileId(issue.filePath)
    if (!fileId) return groups

    const group = groups.get(fileId) ?? []
    group.push(issue)
    groups.set(fileId, group)
    return groups
  }, new Map<string, AnalysisIssue[]>())
  const visibleProjects = sourcePreviewUnavailableReason
    ? []
    : showAllFiles
      ? explorerProjects
      : explorerProjects.filter((project) => hasProjectFileFindings(project.name, filesByProject, issuesByFile))

  function toggleProject(projectName: string) {
    setExpandedProjects((current) => {
      const next = new Set(current)
      if (next.has(projectName)) {
        next.delete(projectName)
      } else {
        next.add(projectName)
      }
      return next
    })
  }

  return (
    <aside className="flex min-h-0 flex-col overflow-hidden bg-white text-sm">
      <div className="border-b border-slate-200 px-2 py-1.5">
        <div className="flex items-center justify-between gap-2">
          <span className="flex min-w-0 items-center gap-2">
            <FolderGit2 className="h-3.5 w-3.5 text-teal-700" aria-hidden="true" />
            <h2 className="truncate text-xs font-semibold uppercase tracking-wide text-slate-600">Solution Explorer</h2>
          </span>
          <label className="shrink-0 text-[11px] text-slate-500">
            <input
              type="checkbox"
              checked={showAllFiles}
              onChange={(event) => setShowAllFiles(event.target.checked)}
              className="mr-1 align-[-1px]"
            />
            Show all
          </label>
        </div>
        <p className="mt-0.5 truncate text-xs text-slate-500">{result.solutionName}</p>
        {result.sourcePreviewCapped && result.sourcePreviewCappedReason ? (
          <p className="mt-1 text-[11px] leading-4 text-amber-700" title={result.sourcePreviewCappedReason}>
            Preview limited: {result.sourcePreviewOmittedFileCount ?? 0} file(s) omitted.
          </p>
        ) : null}
      </div>

      <div className="min-h-0 flex-1 overflow-auto p-1.5">
        {visibleProjects.map((project) => {
          const projectFiles = (filesByProject.get(project.name) ?? [])
            .filter((file) => showAllFiles || (issuesByFile.get(file.id)?.length ?? 0) > 0)
          const expanded = expandedProjects.has(project.name)

          return (
            <div key={project.name} className="mb-1">
              <button
                type="button"
                onClick={() => toggleProject(project.name)}
                className="flex w-full items-center justify-between gap-2 rounded px-1.5 py-1 text-left text-xs hover:bg-slate-50"
              >
                <span className="flex min-w-0 items-center gap-1.5">
                  <ChevronRight className={`h-3 w-3 shrink-0 text-slate-500 transition ${expanded ? 'rotate-90' : ''}`} aria-hidden="true" />
                  <span className="truncate font-semibold text-slate-800">{getProjectFolderName(project.name)}</span>
                </span>
                <span className="shrink-0 text-[11px] font-medium tabular-nums text-slate-500">
                  {projectIssueCounts.get(project.name) ?? 0}
                </span>
              </button>

              {expanded ? (
                <div className="ml-3.5 mt-0.5 space-y-0.5 border-l border-slate-200 pl-1.5">
                  {projectFiles.map((file) => {
                    const fileIssues = issuesByFile.get(file.id) ?? []
                    const highestSeverity = getHighestSeverity(fileIssues)

                    return (
                      <button
                        key={file.id}
                        type="button"
                        onClick={() => onSelectFile(file.id)}
                        className={`flex w-full items-center justify-between gap-1.5 px-1.5 py-1 text-left text-xs transition ${
                          selectedFileId === file.id ? `${selectedRowClass} font-semibold` : 'hover:bg-slate-50'
                        } ${fileIssues.length === 0 ? 'text-slate-400' : 'text-slate-700'}`}
                      >
                        <span className="flex min-w-0 items-center gap-1.5">
                          <FileText className="h-3 w-3 shrink-0" aria-hidden="true" />
                          <span className="truncate">{file.name}</span>
                        </span>
                        {fileIssues.length > 0 ? (
                          <span className={`shrink-0 text-[11px] font-medium tabular-nums ${getSeverityCountClass(highestSeverity)}`}>
                            {fileIssues.length}
                          </span>
                        ) : null}
                      </button>
                    )
                  })}
                  {projectFiles.length === 0 ? (
                    <p className="px-1.5 py-1 text-xs text-slate-500">
                      {showAllFiles ? 'No source files available.' : 'No file-level findings.'}
                    </p>
                  ) : null}
                </div>
              ) : null}
            </div>
          )
        })}
        {visibleProjects.length === 0 ? (
          <p className="p-2 text-xs leading-5 text-slate-500">
            {sourcePreviewUnavailableReason ?? 'No files with findings. Turn on Show all to browse the source tree.'}
          </p>
        ) : null}
      </div>
    </aside>
  )
}

function SourceViewer({
  editorFontFamily,
  file,
  issues,
  onSelectIssue,
  selectedIssue,
  showGutterMarkers,
  showMinimapMarkers,
  sourcePreviewUnavailableReason,
}: {
  editorFontFamily: EditorFontFamily
  file: CodeFile | null
  issues: AnalysisIssue[]
  onSelectIssue: (issueId: string) => void
  selectedIssue: AnalysisIssue | null
  showGutterMarkers: boolean
  showMinimapMarkers: boolean
  sourcePreviewUnavailableReason?: string
}) {
  if (!file) {
    return (
      <div className="flex flex-1 items-center justify-center bg-slate-950 px-8 text-sm text-slate-400">
        <div className="max-w-xl text-center">
          <p className="font-medium text-slate-200">
            {sourcePreviewUnavailableReason ? 'Source preview unavailable' : 'Select a file to inspect source findings.'}
          </p>
          {sourcePreviewUnavailableReason ? (
            <p className="mt-2 leading-6 text-slate-400">{sourcePreviewUnavailableReason}</p>
          ) : null}
        </div>
      </div>
    )
  }

  const handleMount: OnMount = (editor, monaco) => {
    monaco.editor.defineTheme('forge-readonly', {
      base: 'vs-dark',
      inherit: true,
      rules: [
        { token: 'keyword', foreground: 'ff7b72' },
        { token: 'string', foreground: 'a5d6ff' },
        { token: 'number', foreground: '79c0ff' },
        { token: 'comment', foreground: '8b949e', fontStyle: 'italic' },
        { token: 'type', foreground: 'ffa657' },
        { token: 'class', foreground: 'ffa657' },
        { token: 'function', foreground: 'd2a8ff' },
        { token: 'identifier', foreground: 'c9d1d9' },
      ],
      colors: {
        'editor.background': '#0d1117',
        'editor.foreground': '#c9d1d9',
        'editorLineNumber.foreground': '#6e7681',
        'editorGutter.background': '#0d1117',
        'editor.selectionBackground': '#264f78',
        'editor.lineHighlightBackground': '#161b22',
        'editorCursor.foreground': '#c9d1d9',
        'editorIndentGuide.background1': '#21262d',
        'editorIndentGuide.activeBackground1': '#30363d',
      },
    })
    monaco.editor.setTheme('forge-readonly')

    const model = editor.getModel()
    if (model) {
      monaco.editor.setModelMarkers(
        model,
        'forge',
        issues
          .filter((issue) => issue.lineNumber)
          .map((issue) => ({
            endColumn: 120,
            endLineNumber: issue.lineNumber ?? 1,
            message: `${issue.title}\n${issue.recommendation}`,
            severity: getMonacoMarkerSeverity(monaco, issue.severity),
            source: `.DET ${getRuleId(issue)}`,
            startColumn: 1,
            startLineNumber: issue.lineNumber ?? 1,
          })),
      )
    }

    const decorations = issues
      .filter((issue) => issue.lineNumber)
      .map((issue) => ({
        range: new monaco.Range(issue.lineNumber ?? 1, 1, issue.lineNumber ?? 1, 1),
        options: {
          isWholeLine: true,
          className: getEditorLineClass(issue.severity, selectedIssue?.id === issue.id),
          hoverMessage: {
            value: `**${issue.title}**\n\nRule: ${getRuleId(issue)}\n\nSeverity: ${issue.severity}\n\nProduction Risk: ${getProductionRisk(issue)}\n\nRecommendation: ${issue.recommendation}`,
          },
          lineNumberClassName: selectedIssue?.id === issue.id ? 'forge-editor-line-number-selected' : undefined,
          ...(showGutterMarkers
            ? {
                glyphMarginClassName: getEditorGlyphClass(issue.severity),
                glyphMarginHoverMessage: {
                  value: `**${issue.title}**\n\nSeverity: ${issue.severity}\n\nProduction Risk: ${getProductionRisk(issue)}\n\nRecommendation: ${issue.recommendation}`,
                },
              }
            : {}),
          ...(showMinimapMarkers
            ? {
                minimap: {
                  color: getEditorMarkerColor(issue.severity),
                  position: 1,
                },
                overviewRuler: {
                  color: getEditorMarkerColor(issue.severity),
                  position: 7,
                },
              }
            : {}),
        },
      }))

    editor.deltaDecorations([], decorations)

    if (selectedIssue?.lineNumber) {
      editor.setPosition({ column: 1, lineNumber: selectedIssue.lineNumber })
      editor.revealLineInCenter(selectedIssue.lineNumber, monaco.editor.ScrollType.Smooth)
    }

    editor.onMouseDown((event) => {
      const lineNumber = event.target.position?.lineNumber
      const issue = issues.find((candidate) => candidate.lineNumber === lineNumber)
      if (issue) {
        onSelectIssue(issue.id)
      }
    })

    function onEditorWheel(event: WheelEvent) {
      const currentScrollTop = editor.getScrollTop()
      const maxScrollTop = Math.max(0, editor.getScrollHeight() - editor.getLayoutInfo().height)
      const nextScrollTop = clamp(currentScrollTop + event.deltaY, 0, maxScrollTop)

      if (nextScrollTop !== currentScrollTop) {
        editor.setScrollTop(nextScrollTop)
        event.preventDefault()
        event.stopPropagation()
      }
    }

    const editorElement = editor.getDomNode()
    editorElement?.addEventListener('wheel', onEditorWheel, { passive: false })
    editor.onDidDispose(() => editorElement?.removeEventListener('wheel', onEditorWheel))
  }

  return (
    <div className="min-h-0 flex-1 overflow-hidden">
      <Editor
        key={`${file.id}-${selectedIssue?.id ?? 'none'}-${showGutterMarkers}-${showMinimapMarkers}-${editorFontFamily}`}
        height="100%"
        language={file.language ?? getEditorLanguage(file.name)}
        onMount={handleMount}
        options={{
          contextmenu: false,
          fontFamily:
            editorFontFamily === 'Cascadia Code'
              ? 'Cascadia Code, Consolas, monospace'
              : 'Consolas, Cascadia Code, monospace',
          fontSize: 12,
          glyphMargin: showGutterMarkers,
          lineDecorationsWidth: 14,
          lineHeight: 17,
          lineNumbers: 'on',
          minimap: {
            enabled: true,
            renderCharacters: false,
            scale: 0.85,
            showSlider: 'mouseover',
            size: 'proportional',
          },
          padding: { bottom: 4, top: 4 },
          readOnly: true,
          renderLineHighlight: 'all',
          scrollBeyondLastLine: false,
          scrollbar: {
            alwaysConsumeMouseWheel: false,
            vertical: 'visible',
          },
          wordWrap: 'off',
        }}
        theme="forge-readonly"
        value={file.content}
      />
    </div>
  )
}

function CompactMetric({ label, tone, value }: { label: string; tone: 'danger' | 'warn' | 'ok' | 'neutral'; value: number | string }) {
  const toneClass = {
    danger: 'text-red-700',
    warn: 'text-amber-700',
    ok: 'text-slate-400',
    neutral: 'text-slate-950',
  }[tone]

  return (
    <div>
      <dt className="text-xs font-medium text-slate-500">{label}</dt>
      <dd className={`mt-0.5 text-xl font-semibold tabular-nums ${toneClass}`}>{value}</dd>
    </div>
  )
}

function FindingsToolbar({
  activeCategory,
  activeProject,
  activeSeverity,
  filteredCount,
  hideSuppressed,
  onCategoryChange,
  onHideSuppressedChange,
  onProjectChange,
  onQueryChange,
  onSeverityChange,
  onSortModeChange,
  projects,
  query,
  sortMode,
  totalCount,
}: {
  activeCategory: CategoryFilter
  activeProject: ProjectFilter
  activeSeverity: SeverityFilter
  filteredCount: number
  hideSuppressed: boolean
  onCategoryChange: (category: CategoryFilter) => void
  onHideSuppressedChange: (hide: boolean) => void
  onProjectChange: (project: ProjectFilter) => void
  onQueryChange: (query: string) => void
  onSeverityChange: (severity: SeverityFilter) => void
  onSortModeChange: (sortMode: SortMode) => void
  projects: ProjectNode[]
  query: string
  sortMode: SortMode
  totalCount: number
}) {
  return (
    <div className="bg-slate-50">
      <div className="flex flex-col gap-2 px-3 py-2 md:flex-row md:items-center">
        <div className="flex min-w-0 flex-1 items-center gap-2">
          <Search className="h-4 w-4 text-slate-500" aria-hidden="true" />
          <label className="sr-only" htmlFor="finding-search">
            Search findings
          </label>
          <input
            id="finding-search"
            value={query}
            onChange={(event) => onQueryChange(event.target.value)}
            placeholder="Search findings, files, projects, recommendations"
            className="h-8 min-w-0 flex-1 rounded border border-slate-300 bg-white px-2 text-sm outline-none focus:border-teal-500"
          />
        </div>

        <div className="flex flex-wrap items-center gap-2">
          <Filter className="h-4 w-4 text-slate-500" aria-hidden="true" />
          <select
            value={activeSeverity}
            onChange={(event) => onSeverityChange(event.target.value as SeverityFilter)}
            className="h-8 rounded border border-slate-300 bg-white px-2 text-sm text-slate-700 outline-none focus:border-teal-500"
          >
            <option value="All">All severities</option>
            <option value="Critical">Critical</option>
            <option value="Error">Error</option>
            <option value="Warning">Warning</option>
            <option value="Info">Info</option>
          </select>
          <select
            value={activeCategory}
            onChange={(event) => onCategoryChange(event.target.value as CategoryFilter)}
            className="h-8 rounded border border-slate-300 bg-white px-2 text-sm text-slate-700 outline-none focus:border-teal-500"
          >
            <option value="All">All sections</option>
            {categoryDefinitions.map((category) => (
              <option key={category.key} value={category.key}>
                {category.reportLabel}
              </option>
            ))}
          </select>
          <select
            value={activeProject}
            onChange={(event) => onProjectChange(event.target.value as ProjectFilter)}
            className="h-8 rounded border border-slate-300 bg-white px-2 text-sm text-slate-700 outline-none focus:border-teal-500"
          >
            <option value="All">All projects</option>
            {projects.map((project) => (
              <option key={project.name} value={project.name}>
                {project.name}
              </option>
            ))}
          </select>
          <select
            value={sortMode}
            onChange={(event) => onSortModeChange(event.target.value as SortMode)}
            className="h-8 rounded border border-slate-300 bg-white px-2 text-sm text-slate-700 outline-none focus:border-teal-500"
          >
            <option value="Severity">Sort: Severity</option>
            <option value="Category">Sort: Category</option>
            <option value="Project">Sort: Project</option>
            <option value="File">Sort: File</option>
          </select>
          <span className="text-xs text-slate-500">
            {filteredCount} of {totalCount} findings
          </span>
          <label className="inline-flex h-8 items-center gap-1.5 border border-slate-300 bg-white px-2 text-xs font-medium text-slate-700">
            <input
              type="checkbox"
              checked={hideSuppressed}
              onChange={(event) => onHideSuppressedChange(event.target.checked)}
            />
            Hide suppressed
          </label>
        </div>
      </div>
    </div>
  )
}

function FindingsTable({
  getDisposition,
  issues,
  onSelectIssue,
  selectedIssueId,
}: {
  getDisposition: (issue: AnalysisIssue) => FindingDisposition
  issues: AnalysisIssue[]
  onSelectIssue: (issueId: string) => void
  selectedIssueId: string | null
}) {
  if (issues.length === 0) {
    return (
      <div className="flex flex-1 items-center justify-center p-8 text-center">
        <div>
          <CheckCircle2 className="mx-auto mb-3 h-8 w-8 text-teal-700" aria-hidden="true" />
          <p className="font-semibold text-slate-950">No findings detected for this category.</p>
          <p className="mt-1 text-sm text-slate-500">Adjust filters or run analysis on another solution.</p>
        </div>
      </div>
    )
  }

  const showDispositionColumn = issues.some((issue) => getDisposition(issue) !== 'Open')

  return (
    <div className="min-h-0 flex-1 overflow-auto">
      <table className="min-w-[1280px] w-full border-collapse text-left text-sm">
        <thead className="sticky top-0 z-10 bg-slate-100 text-xs font-semibold uppercase text-slate-600">
          <tr>
            <th className="px-3 py-2">Issue</th>
            <th className="w-28 px-3 py-2">Severity</th>
            <th className="w-28 px-3 py-2">Confidence</th>
            <th className="w-44 px-3 py-2">Detection</th>
            {showDispositionColumn ? <th className="w-32 px-3 py-2">Disposition</th> : null}
            <th className="w-44 px-3 py-2">Category</th>
            <th className="w-52 px-3 py-2">Project</th>
            <th className="w-56 px-3 py-2">File</th>
            <th className="w-16 px-3 py-2">Line</th>
            <th className="w-72 px-3 py-2">Recommendation</th>
          </tr>
        </thead>
        <tbody className="det-findings-body bg-white">
          {issues.map((issue) => {
            const disposition = getDisposition(issue)
            return (
              <tr
                key={issue.id}
                onClick={() => onSelectIssue(issue.id)}
                className={`det-finding-row cursor-pointer align-top transition hover:bg-slate-50 ${
                  selectedIssueId === issue.id ? selectedRowClass : ''
                } ${disposition !== 'Open' ? 'opacity-75' : ''}`}
              >
                <td className={`border-l-4 px-3 py-2 ${severityBorderTone[issue.severity]} ${selectedIssueId === issue.id ? 'font-semibold' : ''}`}>
                  <div className="flex flex-wrap items-center gap-2 text-slate-950">
                    <span>{issue.problemSummary ?? issue.title}</span>
                    {issue.suppression ? <SuppressedLabel suppression={issue.suppression} /> : null}
                  </div>
                  <div className="mt-1 line-clamp-2 text-xs leading-5 text-slate-500">{issue.description}</div>
                </td>
                <td className="px-3 py-2">
                  <SeverityLabel severity={issue.severity} />
                </td>
                <td className="px-3 py-2">
                  <ConfidenceLabel confidence={issue.confidence ?? 'Medium'} />
                </td>
                <td className="px-3 py-2 text-xs leading-4 text-slate-500">{issue.detectionMethod ?? getDetectionMethod(issue)}</td>
                {showDispositionColumn ? (
                  <td className="px-3 py-2">
                    <DispositionLabel disposition={disposition} />
                  </td>
                ) : null}
                <td className="px-3 py-2">
                  <CategoryText category={issue.category} />
                </td>
                <td className="px-3 py-2 text-slate-500">{issue.projectName ?? 'Solution'}</td>
                <td className="px-3 py-2 text-xs text-slate-500">{issue.filePath ? formatPath(issue.filePath) : '-'}</td>
                <td className="px-3 py-2 text-slate-600">{issue.lineNumber ?? '-'}</td>
                <td className="px-3 py-2 text-xs leading-5 text-slate-600">{getConciseRecommendation(issue.recommendation)}</td>
              </tr>
            )
          })}
        </tbody>
      </table>
    </div>
  )
}

function EngineeringGuidePanel({
  disposition,
  issue,
  onOpenRule,
  onSelectIssue,
  onUpdateDisposition,
  relatedIssues,
}: {
  disposition: FindingDisposition
  issue: AnalysisIssue | null
  onOpenRule: (ruleId: string) => void
  onSelectIssue: (issueId: string) => void
  onUpdateDisposition: (issue: AnalysisIssue, disposition: FindingDisposition) => void
  relatedIssues: AnalysisIssue[]
}) {
  const [copied, setCopied] = useState(false)
  const suggestedFix = issue ? getSuggestedSnippet(issue) : ''
  const suggestedImplementation = issue?.suggestedImplementation ?? issue?.recommendation ?? ''
  const documentationLinks = issue ? getDocumentationLinks(issue) : []

  async function copySuggestedFix() {
    if (!suggestedFix) {
      return
    }

    await navigator.clipboard.writeText(suggestedFix)
    setCopied(true)
    window.setTimeout(() => setCopied(false), 1400)
  }

  return (
    <aside className="det-engineering-guide min-h-0 overflow-hidden bg-white">
      <div className="det-panel-title px-2.5 py-2">
        <h2 className="text-xs font-semibold uppercase tracking-wide text-slate-600">Engineering Guide</h2>
      </div>
      {issue ? (
        <div className="det-guide-content h-[calc(100%-30px)] overflow-auto px-3 pb-4 pt-3">
          <div className="mb-2 flex items-center justify-between gap-2">
            <SeverityLabel severity={issue.severity} />
            <div className="flex items-center gap-1">
              <ConfidenceLabel confidence={issue.confidence ?? 'Medium'} />
              <span className="font-mono text-[11px] text-slate-500">{getRuleId(issue)}</span>
            </div>
          </div>
          <h3 className="text-sm font-semibold leading-5 text-slate-950">{issue.title}</h3>

          <DetailBlock title="Rule">
            <dl className="space-y-0.5 text-xs">
              <DetailRow label="ID" value={getRuleId(issue)} />
              <DetailRow label="Severity" value={issue.severity} />
              <DetailRow label="Confidence" value={issue.confidence ?? 'Medium'} />
              <DetailRow label="Detection" value={issue.detectionMethod ?? getDetectionMethod(issue)} />
              <DetailRow label="Category" value={getCategoryLabel(issue.category)} />
            </dl>
            <button
              type="button"
              onClick={() => onOpenRule(getRuleId(issue))}
              className="mt-2 inline-flex h-7 items-center gap-1.5 border border-slate-300 bg-white px-2 text-[11px] font-semibold text-slate-700 transition hover:border-teal-500 hover:text-slate-950"
            >
              <FileText className="h-3.5 w-3.5" aria-hidden="true" />
              Open Rule Documentation
            </button>
          </DetailBlock>

          <DetailBlock title="Disposition">
            {issue.suppression ? (
              <div className="mb-2 border border-slate-200 bg-slate-50 p-2 text-xs leading-5 text-slate-700">
                <div className="font-semibold text-slate-900">Repository suppression</div>
                <div>Reason: {issue.suppression.reason}</div>
                <div>Created: {new Date(issue.suppression.createdDate).toLocaleDateString()}</div>
                {issue.suppression.expiration ? <div>Expires: {new Date(issue.suppression.expiration).toLocaleDateString()}</div> : null}
              </div>
            ) : null}
            <div className="det-disposition-control" aria-label="Finding disposition">
              {(['Open', 'Accepted Risk', 'False Positive', 'Ignore'] as const).map((option) => (
                <button
                  key={option}
                  type="button"
                  onClick={() => onUpdateDisposition(issue, option)}
                  className={`det-action-button ${
                    disposition === option
                      ? 'det-action-button-active'
                      : ''
                  }`}
                >
                  {option}
                </button>
              ))}
            </div>
          </DetailBlock>

          <DetailBlock title="Problem">
            <p className="text-xs font-semibold leading-5 text-slate-900">{issue.problemSummary ?? issue.title}</p>
            <p className="mt-1 text-xs leading-5 text-slate-700">{issue.description}</p>
          </DetailBlock>

          <DetailBlock title="Production Risk">
            <p className="text-xs font-semibold text-slate-900">{getProductionRisk(issue)}</p>
            <p className="mt-1 text-xs leading-5 text-slate-700">{getProductionImpact(issue)}</p>
          </DetailBlock>

          <DetailBlock title="Why .DET Detected It">
            <p className="text-xs leading-5 text-slate-700">{getDetectionReason(issue)}</p>
          </DetailBlock>

          {issue.evidence?.length ? (
            <DetailBlock title="Evidence">
              <ul className="space-y-1 text-xs leading-5 text-slate-700">
                {issue.evidence.map((item, index) => (
                  <li key={`${item.label}-${item.filePath ?? ''}-${item.lineNumber ?? index}`}>
                    <span className="font-semibold text-slate-900">{item.label}</span>
                    <span className="text-slate-500"> - {item.detail}</span>
                  </li>
                ))}
              </ul>
            </DetailBlock>
          ) : null}

          <DetailBlock title="Location">
            <dl className="space-y-1 text-xs">
              <DetailRow label="Project" value={issue.projectName ?? 'Solution'} />
              <DetailRow label="File" value={issue.filePath ? formatPath(issue.filePath) : 'Not available'} />
              <DetailRow label="Line" value={issue.lineNumber?.toString() ?? 'Not available'} />
            </dl>
          </DetailBlock>

          <DetailBlock title="Why It Matters">
            <p className="text-xs leading-5 text-slate-700">{getWhyItMatters(issue)}</p>
          </DetailBlock>

          <DetailBlock title="Recommended Pattern">
            <p className="text-xs leading-5 text-slate-700">{issue.recommendedPattern ?? issue.recommendation}</p>
          </DetailBlock>

          <DetailBlock title="Suggested Implementation">
            {suggestedImplementation ? <p className="mb-1.5 text-xs leading-5 text-slate-700">{suggestedImplementation}</p> : null}
            {suggestedFix ? (
              <>
                <div className="mb-1.5 flex justify-end">
                  <button
                    type="button"
                    onClick={copySuggestedFix}
                    className="inline-flex h-6 items-center gap-1.5 rounded border border-slate-300 bg-white px-2 text-[11px] font-medium text-slate-700 transition hover:border-teal-500 hover:text-slate-950"
                  >
                    <Copy className="h-3 w-3" aria-hidden="true" />
                    {copied ? 'Copied' : 'Copy Suggested Fix'}
                  </button>
                </div>
                <pre className="overflow-auto rounded border border-slate-300 bg-slate-950 p-2 text-[11px] leading-4 text-slate-100">
                  <code>{suggestedFix}</code>
                </pre>
              </>
            ) : null}
          </DetailBlock>

          {(issue.badExample || issue.goodExample) ? (
            <DetailBlock title="Good vs Bad Example">
              {issue.badExample ? (
                <>
                  <div className="mb-1 text-[11px] font-semibold uppercase tracking-wide text-red-700">Bad</div>
                  <pre className="mb-2 overflow-auto rounded border border-red-900/40 bg-slate-950 p-2 text-[11px] leading-4 text-slate-100">
                    <code>{issue.badExample}</code>
                  </pre>
                </>
              ) : null}
              {issue.goodExample ? (
                <>
                  <div className="mb-1 text-[11px] font-semibold uppercase tracking-wide text-emerald-700">Good</div>
                  <pre className="overflow-auto rounded border border-emerald-900/40 bg-slate-950 p-2 text-[11px] leading-4 text-slate-100">
                    <code>{issue.goodExample}</code>
                  </pre>
                </>
              ) : null}
            </DetailBlock>
          ) : null}

          <DetailBlock title="Documentation">
            <ul className="space-y-1.5 text-xs text-slate-700">
              {documentationLinks.map((link) => (
                <li key={link.label}>
                  <a className="inline-flex items-center gap-1 font-medium text-teal-700 hover:text-teal-900" href={link.href} target="_blank" rel="noreferrer">
                    {link.label}
                    <ExternalLink className="h-3 w-3" aria-hidden="true" />
                  </a>
                </li>
              ))}
            </ul>
          </DetailBlock>

          <DetailBlock title="Related Findings">
            {relatedIssues.length > 0 ? (
              <div className="space-y-1.5">
                {relatedIssues.map((related) => (
                  <button
                    key={related.id}
                    type="button"
                    onClick={() => onSelectIssue(related.id)}
                    className="block w-full px-2 py-1.5 text-left transition hover:bg-slate-50"
                  >
                    <span className="text-[11px] font-semibold text-slate-500">{getRuleId(related)}</span>
                    <span className="mt-0.5 block text-xs font-medium leading-4 text-slate-900">{related.title}</span>
                  </button>
                ))}
              </div>
            ) : (
              <p className="text-xs text-slate-500">No closely related findings were detected.</p>
            )}
          </DetailBlock>
        </div>
      ) : (
        <div className="p-3 text-xs text-slate-500">Select a finding to inspect risk, detection reason, and remediation guidance.</div>
      )}
    </aside>
  )
}

function DetailBlock({ children, title }: { children: React.ReactNode; title: string }) {
  return (
    <section className="det-detail-block">
      <h4 className="text-[11px] font-semibold uppercase tracking-wide text-slate-500">{title}</h4>
      <div className="mt-1">
      {children}
      </div>
    </section>
  )
}

function DetailRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="grid grid-cols-[64px_1fr] gap-2 py-0.5">
      <dt className="text-slate-500">{label}</dt>
      <dd className="min-w-0 break-words font-medium text-slate-800">{value}</dd>
    </div>
  )
}

function ArchitecturePage({
  onOpenIssue,
  result,
}: {
  onOpenIssue: (issueId: string) => void
  result: AnalysisResult
}) {
  return (
    <div className="det-dashboard-page flex-1 overflow-auto px-5 py-5 2xl:px-6">
      <div className="det-overview-report mx-auto">
        <header className="det-overview-title">
          <div className="text-xs font-semibold uppercase tracking-wide text-teal-700">Architecture</div>
          <h1 className="mt-2 text-2xl font-semibold text-slate-950">Project dependency map</h1>
          <p className="mt-2 text-sm text-slate-500">Inspect project references, dependency direction, and boundary risks.</p>
        </header>
        <ArchitectureGraphPanel architectureMap={result.architectureMap} graph={result.projectGraph} issues={result.issues} onOpenIssue={onOpenIssue} />
      </div>
    </div>
  )
}

function ArchitectureGraphPanel({
  architectureMap,
  graph,
  issues,
  onOpenIssue,
}: {
  architectureMap?: ArchitectureMap
  graph: ProjectGraph
  issues: AnalysisIssue[]
  onOpenIssue: (issueId: string) => void
}) {
  const map = architectureMap ?? buildFallbackArchitectureMap(graph, issues)
  const firstDependency = map.dependencies[0]
  const [selectedDependencyKey, setSelectedDependencyKey] = useState(() => getArchitectureDependencyKey(firstDependency))
  const [selectedProjectName, setSelectedProjectName] = useState<string | null>(map.projects[0]?.name ?? null)
  const selectedDependency =
    map.dependencies.find((dependency) => getArchitectureDependencyKey(dependency) === selectedDependencyKey) ?? firstDependency ?? null
  const selectedProject = selectedProjectName ? map.projects.find((project) => project.name === selectedProjectName) ?? null : null
  const relatedIssue = selectedDependency?.relatedFindingId
    ? issues.find((issue) => issue.id === selectedDependency.relatedFindingId)
    : selectedDependency
      ? getDependencyRelatedFinding(selectedDependency, issues)
      : undefined
  const selectedProjectIssues = selectedProject ? getProjectRelatedFindings(selectedProject.name, issues) : []
  const invalidCount = map.dependencies.filter((dependency) => dependency.isViolation).length

  return (
    <section className="det-overview-section">
      <div className="det-report-section-heading flex flex-wrap items-start justify-between gap-3">
        <div>
          <div className="flex items-center gap-2">
          <GitBranch className="h-4 w-4 text-teal-700" aria-hidden="true" />
            <h2 className="text-lg font-semibold text-slate-950">Architecture Graph</h2>
          </div>
          <p className="mt-2 text-sm text-slate-500">Project dependencies, layer direction, and detected architecture boundary risks.</p>
        </div>
        <span className="text-xs text-slate-500">
          {map.dependencies.length} references - {invalidCount} boundary risks
        </span>
      </div>

      {map.dependencies.length === 0 ? (
        <div className="p-4 text-sm text-slate-500">No project references were discovered.</div>
      ) : (
        <div className="mt-5 grid gap-5 xl:grid-cols-[minmax(0,1fr)_380px]">
          <div className="det-architecture-canvas min-h-56 overflow-hidden border border-slate-200 bg-slate-50">
            <div className="grid min-h-56 gap-0 lg:grid-cols-[1fr_180px_1fr_1fr]">
              {map.layers
                .slice()
                .sort((left, right) => right.order - left.order || left.name.localeCompare(right.name))
                .map((layer) => (
                  <ArchitectureLayerColumn
                    key={layer.name}
                    dependencies={map.dependencies}
                    layer={layer}
                    projects={map.projects}
                    selectedDependency={selectedDependency}
                    selectedProjectName={selectedProjectName}
                    onSelectDependency={(dependency) => {
                      setSelectedDependencyKey(getArchitectureDependencyKey(dependency))
                      setSelectedProjectName(dependency.sourceProjectName)
                    }}
                    onSelectProject={setSelectedProjectName}
                  />
                ))}
            </div>

            <div className="border-t border-slate-200 bg-white">
              <div className="grid max-h-64 overflow-auto md:grid-cols-2">
                {map.dependencies.map((dependency) => {
                  const selected = getArchitectureDependencyKey(dependency) === selectedDependencyKey
                return (
                  <button
                    key={`${dependency.sourceProjectName}-${dependency.targetProjectName}`}
                    type="button"
                      onClick={() => {
                        setSelectedDependencyKey(getArchitectureDependencyKey(dependency))
                        setSelectedProjectName(dependency.sourceProjectName)
                      }}
                      className={`flex items-center gap-2 border-b border-r px-3 py-2 text-left text-xs transition ${
                        dependency.isViolation
                        ? selected
                          ? `${selectedRowClass} text-red-700`
                          : 'text-red-700 hover:bg-slate-50'
                        : selected
                          ? selectedRowClass
                          : 'text-slate-700 hover:bg-slate-50'
                    }`}
                  >
                    <span className="truncate font-medium">{dependency.sourceProjectName}</span>
                      <ChevronRight className={`h-3.5 w-3.5 shrink-0 ${dependency.isViolation ? 'text-red-700' : 'text-teal-700'}`} aria-hidden="true" />
                    <span className="truncate">{dependency.targetProjectName}</span>
                      <span className="ml-auto shrink-0 text-[10px] uppercase tracking-wide text-slate-500">{dependency.direction}</span>
                  </button>
                )
              })}
              </div>
            </div>
          </div>

          <DependencyEdgeDetails
            dependency={selectedDependency}
            issues={issues}
            relatedIssue={relatedIssue}
            selectedProject={selectedProject}
            selectedProjectIssues={selectedProjectIssues}
            violations={map.violations}
            onOpenIssue={onOpenIssue}
            onSelectDependency={(dependency) => setSelectedDependencyKey(getArchitectureDependencyKey(dependency))}
          />
        </div>
      )}
    </section>
  )
}

function ArchitectureLayerColumn({
  dependencies,
  layer,
  onSelectDependency,
  onSelectProject,
  projects,
  selectedDependency,
  selectedProjectName,
}: {
  dependencies: ArchitectureMapDependency[]
  layer: ArchitectureLayer
  onSelectDependency: (dependency: ArchitectureMapDependency) => void
  onSelectProject: (projectName: string) => void
  projects: ArchitectureMapProject[]
  selectedDependency: ArchitectureMapDependency | null
  selectedProjectName: string | null
}) {
  const layerProjects = layer.projectNames
    .map((projectName) => projects.find((project) => project.name === projectName))
    .filter((project): project is ArchitectureMapProject => Boolean(project))

  return (
    <div className="border-r border-slate-200">
      <div className="border-b border-slate-200 bg-[#f3f2f1] px-2 py-1.5">
        <div className="text-[11px] font-semibold uppercase tracking-wide text-slate-600">{layer.name}</div>
        <div className="text-[11px] text-slate-500">{layerProjects.length} project{layerProjects.length === 1 ? '' : 's'}</div>
      </div>
      <div className="space-y-2 p-2">
        {layerProjects.map((project) => {
          const selected = selectedProjectName === project.name
          const connected = selectedDependency
            ? selectedDependency.sourceProjectName === project.name || selectedDependency.targetProjectName === project.name
            : false
          const projectDependencies = dependencies.filter(
            (dependency) => dependency.sourceProjectName === project.name || dependency.targetProjectName === project.name,
          )
          const violationCount = projectDependencies.filter((dependency) => dependency.isViolation).length

          return (
            <div
              key={project.name}
              className={`det-architecture-project w-full px-2 py-2 text-left transition ${
                selected
                  ? `${selectedRowClass} font-semibold`
                  : connected
                    ? 'text-slate-900'
                    : 'text-slate-800 hover:bg-slate-50'
              }`}
            >
              <button type="button" onClick={() => onSelectProject(project.name)} className="block w-full text-left">
                <div className="flex items-center gap-2">
                  <span className={`h-2 w-2 shrink-0 rounded-full ${violationCount > 0 ? 'bg-red-600' : 'bg-slate-500'}`} />
                  <span className="min-w-0 flex-1 truncate text-xs font-semibold">{project.name}</span>
                  {project.criticalOrErrorCount > 0 ? (
                    <span className="text-[10px] font-medium tabular-nums text-red-700">
                      {project.criticalOrErrorCount}
                    </span>
                  ) : null}
                </div>
                <div className="mt-1 flex items-center justify-between gap-2 text-[11px] text-slate-500">
                  <span className="truncate">{project.namespaceRoot}</span>
                  <span>{project.issueCount} findings</span>
                </div>
              </button>
              {projectDependencies.length > 0 ? (
                <div className="mt-2 space-y-1">
                  {projectDependencies.slice(0, 3).map((dependency) => (
                    <button
                      key={getArchitectureDependencyKey(dependency)}
                      type="button"
                      onClick={(event) => {
                        event.stopPropagation()
                        onSelectDependency(dependency)
                      }}
                      className={`flex w-full items-center gap-1 text-[10px] ${
                        dependency.isViolation ? 'text-red-700' : 'text-slate-500 hover:text-teal-700'
                      }`}
                    >
                      <GitBranch className="h-3 w-3" aria-hidden="true" />
                      <span className="truncate">
                        {dependency.sourceProjectName === project.name ? `to ${dependency.targetProjectName}` : `from ${dependency.sourceProjectName}`}
                      </span>
                    </button>
                  ))}
                </div>
              ) : null}
            </div>
          )
        })}
      </div>
    </div>
  )
}

function DependencyEdgeDetails({
  dependency,
  issues,
  onOpenIssue,
  onSelectDependency,
  relatedIssue,
  selectedProject,
  selectedProjectIssues,
  violations,
}: {
  dependency: ArchitectureMapDependency | null
  issues: AnalysisIssue[]
  onOpenIssue: (issueId: string) => void
  onSelectDependency: (dependency: ArchitectureMapDependency) => void
  relatedIssue?: AnalysisIssue
  selectedProject: ArchitectureMapProject | null
  selectedProjectIssues: AnalysisIssue[]
  violations: ArchitectureMapViolation[]
}) {
  if (!dependency) {
    return <div className="border border-slate-200 bg-slate-50 p-3 text-sm text-slate-500">Select a dependency edge to inspect it.</div>
  }

  const edgeViolations = violations.filter(
    (violation) =>
      violation.sourceProjectName === dependency.sourceProjectName
      && violation.targetProjectName === dependency.targetProjectName,
  )

  return (
    <aside className="border border-slate-200 bg-slate-50 p-3">
      <div className="mb-3 flex items-center justify-between gap-2">
        <h3 className="text-sm font-semibold text-slate-950">Dependency Edge</h3>
        <span className={`text-xs font-medium ${dependency.isViolation ? 'text-red-700' : 'text-slate-500'}`}>
          {dependency.isViolation ? 'Violation' : 'Allowed'}
        </span>
      </div>
      <dl className="space-y-2 text-sm">
        <DetailRow label="Source" value={dependency.sourceProjectName} />
        <DetailRow label="Target" value={dependency.targetProjectName} />
        <DetailRow label="Layers" value={`${dependency.sourceLayer} -> ${dependency.targetLayer}`} />
        <DetailRow label="Direction" value={dependency.direction} />
        <DetailRow label="Rule" value={dependency.ruleId ?? 'Allowed project reference'} />
      </dl>
      <div className="mt-4">
        <h4 className="text-xs font-semibold uppercase tracking-wide text-slate-500">Why It Matters</h4>
        <p className="mt-2 text-sm leading-6 text-slate-700">
          {dependency.reason ??
            (dependency.isViolation
              ? 'This edge points against the intended application layering and can pull infrastructure or delivery concerns into lower-level code.'
              : 'This edge follows the expected direction of dependency flow for a layered .NET solution.')}
        </p>
      </div>
      {relatedIssue ? (
        <div className="mt-4 border border-white bg-white p-3">
          <div className="text-xs font-semibold uppercase tracking-wide text-slate-500">Related Finding</div>
          <p className="mt-1 text-sm font-semibold text-slate-950">{relatedIssue.title}</p>
          <button
            type="button"
            onClick={() => onOpenIssue(relatedIssue.id)}
            className="mt-3 inline-flex h-8 items-center rounded border border-slate-300 px-3 text-xs font-semibold text-slate-700 transition hover:border-teal-500 hover:text-slate-950"
          >
            Open in Code Explorer
          </button>
        </div>
      ) : null}
      {edgeViolations.length > 0 ? (
        <div className="mt-4 border border-red-200 bg-red-50 p-3">
          <div className="text-xs font-semibold uppercase tracking-wide text-red-700">Architecture Violation</div>
          <div className="mt-2 space-y-2">
            {edgeViolations.map((violation) => {
              const violationIssue = violation.relatedFindingId ? issues.find((issue) => issue.id === violation.relatedFindingId) : undefined
              return (
                <div key={violation.id}>
                  <p className="text-sm font-semibold text-red-900">{violation.title}</p>
                  <p className="mt-1 text-xs leading-5 text-red-800">{violation.description}</p>
                  {violationIssue ? (
                    <button
                      type="button"
                      onClick={() => onOpenIssue(violationIssue.id)}
                      className="mt-2 inline-flex h-7 items-center border border-red-300 bg-white px-2 text-xs font-semibold text-red-700 transition hover:border-red-500"
                    >
                      Open related finding
                    </button>
                  ) : null}
                </div>
              )
            })}
          </div>
        </div>
      ) : null}
      {selectedProject ? (
        <div className="mt-4 border border-slate-200 bg-white p-3">
          <div className="text-xs font-semibold uppercase tracking-wide text-slate-500">Selected Project</div>
          <p className="mt-1 truncate text-sm font-semibold text-slate-950">{selectedProject.name}</p>
          <dl className="mt-2 space-y-2 text-sm">
            <DetailRow label="Layer" value={selectedProject.layer} />
            <DetailRow label="Issues" value={`${selectedProject.issueCount} findings, ${selectedProject.criticalOrErrorCount} critical/error`} />
          </dl>
          {selectedProjectIssues.length > 0 ? (
            <div className="mt-3 space-y-1">
              {selectedProjectIssues.slice(0, 3).map((issue) => (
                <button
                  key={issue.id}
                  type="button"
                  onClick={() => onOpenIssue(issue.id)}
                  className="block w-full truncate text-left text-xs text-slate-600 hover:text-teal-700"
                >
                  {issue.severity}: {issue.title}
                </button>
              ))}
            </div>
          ) : null}
        </div>
      ) : null}
      {violations.length > 0 ? (
        <div className="mt-4">
          <div className="text-xs font-semibold uppercase tracking-wide text-slate-500">Violation Index</div>
          <div className="mt-2 space-y-1">
            {violations.slice(0, 5).map((violation) => {
              const dependency = violation.sourceProjectName && violation.targetProjectName
                ? {
                    direction: 'Outward',
                    isViolation: true,
                    reason: violation.description,
                    ruleId: violation.ruleId,
                    sourceLayer: '',
                    sourceProjectName: violation.sourceProjectName,
                    targetLayer: '',
                    targetProjectName: violation.targetProjectName,
                    relatedFindingId: violation.relatedFindingId,
                  }
                : null
              return (
                <button
                  key={violation.id}
                  type="button"
                  onClick={() => {
                    if (dependency) {
                      onSelectDependency(dependency)
                    }
                    if (violation.relatedFindingId) {
                      onOpenIssue(violation.relatedFindingId)
                    }
                  }}
                  className="block w-full border border-slate-200 bg-white px-2 py-1.5 text-left text-xs text-slate-700 transition hover:border-red-300 hover:text-red-700"
                >
                  <span className="font-semibold">{violation.ruleId}</span> {violation.title}
                </button>
              )
            })}
          </div>
        </div>
      ) : null}
    </aside>
  )
}

function buildCodeFiles(result: AnalysisResult) {
  const files = new Map<string, CodeFile>()

  for (const sourceFile of result.sourceFiles ?? []) {
    const displayPath = sourceFile.relativePath || sourceFile.filePath
    const id = getFileId(sourceFile.filePath)

    files.set(id, {
      content: sourceFile.content,
      folder: getFolderName(displayPath),
      id,
      language: sourceFile.language,
      name: getFileName(displayPath),
      path: sourceFile.filePath,
      projectName: sourceFile.projectName,
    })
  }

  if (result.sourcePreviewAvailable === false) {
    return [...files.values()]
      .sort((left, right) => left.projectName.localeCompare(right.projectName) || left.name.localeCompare(right.name))
  }

  for (const issue of result.issues) {
    if (!issue.filePath) {
      continue
    }

    const id = getFileId(issue.filePath)
    if (!files.has(id)) {
      files.set(id, {
        content: getUnavailableSourceMessage(issue.filePath),
        folder: getFolderName(issue.filePath),
        id,
        name: getFileName(issue.filePath),
        path: issue.filePath,
        projectName: issue.projectName ?? 'Solution',
      })
    }
  }

  return [...files.values()]
    .sort((left, right) => left.projectName.localeCompare(right.projectName) || left.name.localeCompare(right.name))
}

function getSourcePreviewUnavailableReason(result: AnalysisResult) {
  return result.sourcePreviewAvailable === false
    ? result.sourcePreviewUnavailableReason
      ?? 'Source preview is not available for this analysis result.'
    : undefined
}

function getUnavailableSourceMessage(filePath: string) {
  return [
    '// Source content was not included in this analysis response.',
    `// File: ${filePath}`,
    '// Re-run analysis after confirming the file is inside the analyzed solution and is not generated/build output.',
  ].join('\n')
}

function groupFilesByProject(files: CodeFile[]) {
  return files.reduce((groups, file) => {
    const group = groups.get(file.projectName) ?? []
    group.push(file)
    groups.set(file.projectName, group)
    return groups
  }, new Map<string, CodeFile[]>())
}

function getCodeExplorerProjects(projects: ProjectNode[], filesByProject: Map<string, CodeFile[]>) {
  const existingProjectNames = new Set(projects.map((project) => project.name))
  const virtualProjects = [...filesByProject.keys()]
    .filter((projectName) => !existingProjectNames.has(projectName))
    .sort((left, right) => {
      if (left === 'Solution') return -1
      if (right === 'Solution') return 1
      return left.localeCompare(right)
    })
    .map((projectName) => ({
      filePath: projectName,
      isAspNetCoreEntryPoint: false,
      isTestProject: false,
      logicalLayer: projectName === 'Solution' ? 'Solution' : undefined,
      name: projectName,
    }))

  return [...virtualProjects, ...projects]
}

function hasProjectFileFindings(
  projectName: string,
  filesByProject: Map<string, CodeFile[]>,
  issuesByFile: Map<string, AnalysisIssue[]>,
) {
  return (filesByProject.get(projectName) ?? []).some((file) => (issuesByFile.get(file.id)?.length ?? 0) > 0)
}

function getHighestSeverity(issues: AnalysisIssue[]) {
  return issues.reduce<Severity>((highest, issue) => (severityRank[issue.severity] > severityRank[highest] ? issue.severity : highest), 'Info')
}

function getSeverityCountClass(severity: Severity) {
  return severityTextTone[severity]
}

function getEditorLineClass(severity: Severity, selected: boolean) {
  const base = {
    Critical: 'forge-editor-line-critical',
    Error: 'forge-editor-line-error',
    Warning: 'forge-editor-line-warning',
    Info: 'forge-editor-line-info',
  }[severity]

  return selected ? `${base} forge-editor-line-selected` : base
}

function getEditorGlyphClass(severity: Severity) {
  return {
    Critical: 'forge-editor-glyph-critical',
    Error: 'forge-editor-glyph-error',
    Warning: 'forge-editor-glyph-warning',
    Info: 'forge-editor-glyph-info',
  }[severity]
}

function getEditorMarkerColor(severity: Severity) {
  return {
    Critical: '#991b1b',
    Error: '#dc2626',
    Warning: '#f59e0b',
    Info: '#0ea5e9',
  }[severity]
}

function getMonacoMarkerSeverity(monaco: Parameters<OnMount>[1], severity: Severity) {
  if (severity === 'Critical' || severity === 'Error') return monaco.MarkerSeverity.Error
  if (severity === 'Warning') return monaco.MarkerSeverity.Warning
  return monaco.MarkerSeverity.Info
}

function getEditorLanguage(fileName: string) {
  if (fileName.endsWith('.json')) return 'json'
  if (fileName.endsWith('.csproj')) return 'xml'
  return 'csharp'
}

function getStoredPanelWidth(key: string, fallback: number) {
  const stored = Number(localStorage.getItem(key))
  return Number.isFinite(stored) && stored > 0 ? stored : fallback
}

function loadStoredWorkbenchState(): StoredWorkbenchState {
  localStorage.removeItem(lastAnalysisSolutionPathStorageKey)

  return {
    activeCategory: getStoredString(activeCategoryStorageKey, 'All', ['All', ...categoryDefinitions.map((category) => category.key)] as const),
    activePage: getStoredString(activePageStorageKey, 'Code Explorer', ['Overview', 'Findings', 'Code Explorer', 'Architecture', 'Rule Explorer', 'Settings', 'Docs', 'Contact'] as const),
    activeProject: localStorage.getItem(activeProjectStorageKey) || 'All',
    activeSeverity: getStoredString(activeSeverityStorageKey, 'All', ['All', 'Info', 'Warning', 'Error', 'Critical'] as const),
    query: localStorage.getItem(findingQueryStorageKey) ?? '',
    result: loadStoredAnalysisResult(),
    selectedFileId: localStorage.getItem(selectedFileStorageKey),
    selectedIssueId: localStorage.getItem(selectedIssueStorageKey),
  }
}

function getStartPageFromPath(hasStoredResult: boolean): StartPage {
  if (hasStoredResult) {
    return 'Dashboard'
  }

  const path = window.location.pathname.toLowerCase()
  if (path.startsWith('/docs')) return 'Docs'
  if (path.startsWith('/contact')) return 'Contact'
  if (path.startsWith('/changelog')) return 'Changelog'
  if (path.startsWith('/dashboard')) return 'Dashboard'
  if (path.startsWith('/analyze')) return 'Analyze'
  if (path.startsWith('/reports')) return 'Reports'
  if (path.startsWith('/rules')) return 'Dashboard'
  if (path.startsWith('/settings')) return 'Settings'
  return 'Home'
}

function getPathForStartPage(page: StartPage) {
  return {
    Analyze: '/analyze',
    Changelog: '/changelog',
    Contact: '/contact',
    Dashboard: '/dashboard',
    Docs: '/docs',
    Home: '/',
    Reports: '/reports',
    Settings: '/settings',
  }[page]
}

function formatSourceType(sourceType: AnalysisSourceType) {
  return {
    GitHubRepo: 'GitHub',
    LocalDevPath: 'LocalDev',
    SampleProject: 'Sample',
    ZipUpload: 'Upload ZIP',
  }[sourceType]
}

function formatHistoryDate(value: string) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) {
    return '-'
  }

  return date.toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' })
}

function isValidGitHubRepositoryInput(value: string) {
  const input = value.trim()
  if (!input || /\s/.test(input)) {
    return false
  }

  const normalized = input
    .replace(/^https?:\/\/github\.com\//i, '')
    .replace(/^github\.com\//i, '')
    .replace(/\.git$/i, '')
    .replace(/^\/|\/$/g, '')
  const parts = normalized.split('/')
  return parts.length === 2
    && /^[A-Za-z0-9](?:[A-Za-z0-9-]{0,37}[A-Za-z0-9])?$/.test(parts[0])
    && /^[A-Za-z0-9._-]{1,100}$/.test(parts[1])
}

function navigateToPath(path: string, replace = false) {
  if (window.location.pathname === path) {
    return
  }

  if (replace) {
    window.history.replaceState(null, '', path)
  } else {
    window.history.pushState(null, '', path)
  }
}

function loadStoredAnalysisResult() {
  const stored = localStorage.getItem(lastAnalysisResultStorageKey)
  if (!stored) {
    return null
  }

  try {
    const parsed = JSON.parse(stored) as AnalysisResult
    if (!parsed?.solutionName || !Array.isArray(parsed.issues) || !parsed.projectGraph) {
      return null
    }

    const sanitized = sanitizeAnalysisResultForBrowserStorage(parsed)
    safeSetLocalStorage(lastAnalysisResultStorageKey, JSON.stringify(sanitized))
    return sanitized
  } catch {
    localStorage.removeItem(lastAnalysisResultStorageKey)
    return null
  }
}

function clearCachedAnalysisState() {
  [
    lastAnalysisResultStorageKey,
    lastAnalysisSolutionPathStorageKey,
    selectedIssueStorageKey,
    selectedFileStorageKey,
  ].forEach((key) => localStorage.removeItem(key))
}

function sanitizeAnalysisResultForBrowserStorage(result: AnalysisResult): AnalysisResult {
  return stripSourceBearingAnalysisFields(result, browserStorageSourcePreviewUnavailableReason)
}

function sanitizeAnalysisResultForExport(result: AnalysisResult): AnalysisResult {
  return stripSourceBearingAnalysisFields(result, exportSourcePreviewUnavailableReason)
}

function stripSourceBearingAnalysisFields(result: AnalysisResult, unavailableReason: string): AnalysisResult {
  const resultWithOptionalSourcePreview = result as AnalysisResult & { sourcePreview?: unknown }
  const {
    sourceFiles: _sourceFiles,
    sourcePreview: _sourcePreview,
    solutionPath: _solutionPath,
    repositoryRoot: _repositoryRoot,
    suppressionFilePath: _suppressionFilePath,
    ...safeResult
  } = resultWithOptionalSourcePreview

  return {
    ...safeResult,
    sourcePreviewAvailable: false,
    sourcePreviewUnavailableReason: unavailableReason,
    sourcePreviewCapped: false,
    sourcePreviewCappedReason: undefined,
    sourcePreviewIncludedFileCount: 0,
    sourcePreviewOmittedFileCount: 0,
    sourcePreviewIncludedBytes: 0,
    issues: result.issues.map(sanitizeIssuePaths),
    projectGraph: sanitizeProjectGraphPaths(result.projectGraph),
    architectureMap: result.architectureMap ? sanitizeArchitectureMapPaths(result.architectureMap) : undefined,
    engineeringAssessment: result.engineeringAssessment
      ? sanitizeEngineeringAssessmentPaths(result.engineeringAssessment)
      : undefined,
  }
}

function sanitizeIssuePaths(issue: AnalysisIssue): AnalysisIssue {
  return {
    ...issue,
    filePath: sanitizePathForBrowserPersistence(issue.filePath),
    description: sanitizeTextForBrowserPersistence(issue.description) ?? issue.description,
    recommendation: sanitizeTextForBrowserPersistence(issue.recommendation) ?? issue.recommendation,
    problemSummary: sanitizeTextForBrowserPersistence(issue.problemSummary),
    whyDetected: sanitizeTextForBrowserPersistence(issue.whyDetected),
    whyItMatters: sanitizeTextForBrowserPersistence(issue.whyItMatters),
    recommendedPattern: sanitizeTextForBrowserPersistence(issue.recommendedPattern),
    suggestedImplementation: sanitizeTextForBrowserPersistence(issue.suggestedImplementation),
    rootCauseKey: sanitizeRootCauseKeyForBrowserPersistence(issue.rootCauseKey),
    evidence: issue.evidence?.map((item) => ({
      ...item,
      label: sanitizeTextForBrowserPersistence(item.label) ?? item.label,
      detail: sanitizeTextForBrowserPersistence(item.detail) ?? item.detail,
      filePath: sanitizePathForBrowserPersistence(item.filePath),
    })),
  }
}

function sanitizeProjectGraphPaths(projectGraph: ProjectGraph): ProjectGraph {
  return {
    ...projectGraph,
    projects: projectGraph.projects.map((project) => ({
      ...project,
      filePath: sanitizePathForBrowserPersistence(project.filePath) ?? project.filePath,
    })),
  }
}

function sanitizeArchitectureMapPaths(architectureMap: ArchitectureMap): ArchitectureMap {
  return {
    ...architectureMap,
    projects: architectureMap.projects.map((project) => ({
      ...project,
      filePath: sanitizePathForBrowserPersistence(project.filePath) ?? project.filePath,
    })),
    dependencies: architectureMap.dependencies.map((dependency) => ({
      ...dependency,
      reason: sanitizeTextForBrowserPersistence(dependency.reason),
    })),
    violations: architectureMap.violations.map((violation) => ({
      ...violation,
      description: sanitizeTextForBrowserPersistence(violation.description) ?? violation.description,
    })),
  }
}

function sanitizeEngineeringAssessmentPaths(assessment: EngineeringAssessmentSummary): EngineeringAssessmentSummary {
  const sanitizeList = (items: string[]) => items.map((item) => sanitizeTextForBrowserPersistence(item) ?? item)
  return {
    ...assessment,
    overallProductionReadiness: sanitizeTextForBrowserPersistence(assessment.overallProductionReadiness) ?? assessment.overallProductionReadiness,
    scoreExplanation: sanitizeTextForBrowserPersistence(assessment.scoreExplanation) ?? assessment.scoreExplanation,
    strongAreas: sanitizeList(assessment.strongAreas),
    highestRisks: sanitizeList(assessment.highestRisks),
    architecturalObservations: sanitizeList(assessment.architecturalObservations),
    securityObservations: sanitizeList(assessment.securityObservations),
    apiReadinessObservations: sanitizeList(assessment.apiReadinessObservations),
    maintainabilityObservations: sanitizeList(assessment.maintainabilityObservations),
    recommendedPriorities: sanitizeList(assessment.recommendedPriorities),
  }
}

function sanitizePathForBrowserPersistence(path?: string) {
  if (!path) {
    return path
  }

  const normalized = path.replace(/\\/g, '/')
  if (/^[A-Za-z]:\//.test(normalized) || normalized.startsWith('/') || normalized.startsWith('//')) {
    return normalized.split('/').filter(Boolean).at(-1) ?? undefined
  }

  return normalized
}

function sanitizeTextForBrowserPersistence(value?: string) {
  if (!value) {
    return value
  }

  const safeTail = (path: string) => path.replace(/\\/g, '/').split('/').filter(Boolean).at(-1) ?? 'file'
  return value
    .replace(/(?:[A-Za-z]:[\\/])[^\s"'<>|]+/g, (path) => safeTail(path))
    .replace(/\/(?:tmp|var|home|Users|private|mnt)\/[^\s"'<>|]+/gi, (path) => safeTail(path))
}

function sanitizeRootCauseKeyForBrowserPersistence(rootCauseKey?: string) {
  if (!rootCauseKey) {
    return rootCauseKey
  }

  return rootCauseKey
    .split('|')
    .map((segment) => {
      const trimmed = segment.trim()
      if (/^[A-Za-z]:[\\/]/.test(trimmed) || /^\/(?:tmp|var|home|Users|private|mnt)\//i.test(trimmed)) {
        return '<unknown-file>'
      }

      return sanitizeTextForBrowserPersistence(segment) ?? segment
    })
    .join('|')
}

function safeSetLocalStorage(key: string, value: string) {
  try {
    localStorage.setItem(key, value)
  } catch {
    localStorage.removeItem(key)
  }
}

function getStoredString<T extends string>(key: string, fallback: T, allowed: readonly T[]) {
  const stored = localStorage.getItem(key) as T | null
  return stored && allowed.includes(stored) ? stored : fallback
}

function getStoredBoolean(key: string, fallback: boolean) {
  const stored = localStorage.getItem(key)
  if (stored === 'true') return true
  if (stored === 'false') return false
  return fallback
}

function clamp(value: number, min: number, max: number) {
  return Math.min(max, Math.max(min, value))
}

function getProjectFolderName(projectName: string) {
  return projectName.split('.').at(-1) ?? projectName
}

function getFolderName(path: string) {
  const parts = path.split(/[\\/]/)
  return parts.length > 1 ? parts[parts.length - 2] : 'Source'
}

function getFileName(path: string) {
  return path.split(/[\\/]/).at(-1) ?? path
}

function getDirectoryPath(path: string) {
  const normalized = path.replace(/\\/g, '/')
  const index = normalized.lastIndexOf('/')
  return index >= 0 ? normalized.slice(0, index + 1) : ''
}

function getFileId(path?: string) {
  if (!path) {
    return ''
  }

  return path.replace(/\\/g, '/').toLowerCase()
}

function SeverityLabel({ severity }: { severity: Severity }) {
  const Icon = severity === 'Critical' || severity === 'Error' ? XCircle : severity === 'Warning' ? AlertTriangle : Info
  const label = severity === 'Critical' ? 'Error' : severity

  return (
    <span className={`inline-flex items-center gap-1 text-xs font-medium ${severityTextTone[severity]}`}>
      <Icon className="h-3.5 w-3.5" aria-hidden="true" />
      {label}
    </span>
  )
}

function ConfidenceLabel({ confidence }: { confidence: IssueConfidence }) {
  return <span className="text-xs text-slate-500">{confidence}</span>
}

function DispositionLabel({ disposition }: { disposition: FindingDisposition }) {
  return <span className="text-xs text-slate-500">{disposition}</span>
}

function SuppressedLabel({ suppression }: { suppression: FindingSuppression }) {
  return <span className="text-[11px] font-medium text-slate-500">Suppressed: {suppression.status}</span>
}

function CategoryText({ category }: { category: Category }) {
  const definition = categoryDefinitions.find((candidate) => candidate.key === category)
  const Icon = definition?.icon ?? Circle

  return (
    <span className={`inline-flex items-center gap-1.5 text-xs font-medium ${categoryTextClass(category)}`}>
      <Icon className="h-3.5 w-3.5" aria-hidden="true" />
      {getCategoryLabel(category)}
    </span>
  )
}

function getSeverityCounts(issues: AnalysisIssue[]) {
  return issues.reduce<Record<Severity, number>>(
    (counts, issue) => {
      counts[issue.severity] += 1
      return counts
    },
    { Critical: 0, Error: 0, Warning: 0, Info: 0 },
  )
}

function getProjectIssueCounts(issues: AnalysisIssue[]) {
  return issues.reduce((counts, issue) => {
    if (issue.projectName) {
      counts.set(issue.projectName, (counts.get(issue.projectName) ?? 0) + 1)
    }

    return counts
  }, new Map<string, number>())
}

function getGrade(score: number) {
  if (score >= 97) return 'A+'
  if (score >= 93) return 'A'
  if (score >= 90) return 'A-'
  if (score >= 87) return 'B+'
  if (score >= 83) return 'B'
  if (score >= 80) return 'B-'
  if (score >= 70) return 'C'
  if (score >= 60) return 'D'
  return 'F'
}

function getReadinessDecision(score: number, counts: Record<Severity, number>) {
  if (score < 50) return 'Not Ready'
  if (counts.Critical > 0 || counts.Error > 0 || counts.Warning > 0 || score < 85) return 'Needs Review'
  return 'Ready'
}

function getOverviewNarrative(result: AnalysisResult, counts: Record<Severity, number>) {
  const blockerCount = counts.Critical + counts.Error

  if (blockerCount > 0) {
    const concerns = getPrimaryConcerns(result)
    const concernText = concerns.length > 0 ? concerns.join(', ').toLowerCase() : 'active findings'
    return `This solution needs review before production hardening. .DET found ${blockerCount} high-severity findings, with risk concentrated in ${concernText}.`
  }

  if (counts.Warning > 0) {
    return `This solution has no critical blockers, but ${counts.Warning} warning-level findings should be reviewed before release hardening.`
  }

  return '.DET did not detect production blockers in the analyzed architecture, configuration, persistence, dependency injection, or API readiness checks.'
}

function getOverviewLead(counts: Record<Severity, number>) {
  const blockerCount = counts.Critical + counts.Error

  if (blockerCount > 0) {
    return `${blockerCount} high-severity findings should be reviewed before production deployment.`
  }

  if (counts.Warning > 0) {
    return `No release blockers were detected, but ${counts.Warning} warning-level findings need review before release hardening.`
  }

  return 'No production blockers were detected across the analyzed architecture, configuration, persistence, dependency injection, or API readiness checks.'
}

function getRiskAreas(result: AnalysisResult) {
  const areas = categoryDefinitions
    .map((category) => {
      const issues = result.issues
        .filter((issue) => issue.category === category.key)
        .filter(isRiskSummaryIssue)
      const criticalCount = issues.filter((issue) => issue.severity === 'Critical' || issue.severity === 'Error').length

      return {
        category,
        criticalCount,
        issueCount: issues.length,
        score: result.categoryScores[category.scoreKey],
      }
    })

  return areas
    .filter((area) => area.criticalCount > 0 || area.issueCount > 0)
    .sort((left, right) => left.score - right.score || right.criticalCount - left.criticalCount)
}

function getPrimaryConcerns(result: AnalysisResult) {
  const concerns = categoryDefinitions
    .map((category) => {
      const issues = result.issues
        .filter((issue) => issue.category === category.key)
        .filter(isRiskSummaryIssue)
      const criticalCount = issues.filter((issue) => issue.severity === 'Critical' || issue.severity === 'Error').length

      return {
        label: getConcernLabel(category.key),
        score: result.categoryScores[category.scoreKey],
        weight: criticalCount * 20 + issues.length * 3 + (100 - result.categoryScores[category.scoreKey]),
      }
    })

  const activeConcerns = concerns.filter((area) => area.weight > 0)

  return activeConcerns
    .sort((left, right) => right.weight - left.weight || left.score - right.score)
    .slice(0, 3)
    .map((area) => area.label)
}

function isRiskSummaryIssue(issue: AnalysisIssue) {
  return issue.severity !== 'Info' && (issue.confidence ?? 'Medium') !== 'Low'
}

function isActiveProductionFinding(issue: AnalysisIssue) {
  return isRiskSummaryIssue(issue)
}

function getConcernLabel(category: Category) {
  return {
    Architecture: 'Architecture Boundaries',
    DependencyInjection: 'Dependency Injection Reliability',
    EfCore: 'EF Core & Migration Risk',
    Security: 'Security & Configuration',
    ApiReadiness: 'API Reliability',
  }[category]
}

function getScoreBarClass(score: number) {
  if (score >= 85) return 'h-full bg-green-600'
  if (score >= 70) return 'h-full bg-[#629f43]'
  if (score >= 50) return 'h-full bg-amber-500'
  return 'h-full bg-rose-600'
}

function getCategoryStatus(score: number, issueCount: number) {
  if (issueCount === 0) return 'No findings detected'
  if (score < 70) return 'Needs remediation'
  if (score < 90) return 'Review recommended'
  return 'Minor cleanup'
}

function getCategoryLabel(category: Category) {
  return categoryDefinitions.find((definition) => definition.key === category)?.reportLabel ?? category
}

function getGroupedRecommendedActions(issues: AnalysisIssue[]): RecommendedAction[] {
  const grouped = new Map<string, RecommendedAction>()

  for (const issue of issues.filter((candidate) =>
    (candidate.severity === 'Critical' || candidate.severity === 'Error' || candidate.severity === 'Warning')
    && (!candidate.suppression || candidate.suppression.isExpired)
  )) {
    const key = getRootCauseKey(issue)
    const existing = grouped.get(key)

    if (!existing) {
      grouped.set(key, { ...issue, groupedCount: 1, groupedProjectCount: issue.projectName ? 1 : 0 })
      continue
    }

    const existingRank = severityRank[existing.severity]
    const issueRank = severityRank[issue.severity]
    const representative = issueRank > existingRank ? issue : existing
    const projectNames = new Set(
      issues
        .filter((candidate) => getRootCauseKey(candidate) === key)
        .map((candidate) => candidate.projectName)
        .filter(Boolean) as string[],
    )

    grouped.set(key, {
      ...representative,
      groupedCount: (existing.groupedCount ?? 1) + 1,
      groupedProjectCount: projectNames.size,
    })
  }

  return [...grouped.values()]
    .sort((left, right) => severityRank[right.severity] - severityRank[left.severity] || (right.groupedCount ?? 1) - (left.groupedCount ?? 1))
    .slice(0, 5)
}

function getGroupedRuleFindings(issues: AnalysisIssue[]) {
  const grouped = new Map<string, { count: number; issue: AnalysisIssue }>()

  for (const issue of issues) {
    const key = `${issue.projectName ?? 'Solution'}|${issue.filePath ?? ''}|${issue.title}`
    const existing = grouped.get(key)

    if (!existing) {
      grouped.set(key, { count: 1, issue })
      continue
    }

    const representative = severityRank[issue.severity] > severityRank[existing.issue.severity] ? issue : existing.issue
    grouped.set(key, { count: existing.count + 1, issue: representative })
  }

  return [...grouped.values()]
    .sort((left, right) => severityRank[right.issue.severity] - severityRank[left.issue.severity] || right.count - left.count)
}

function getRecommendedActionTitle(issue: RecommendedAction) {
  if (!issue.groupedCount || issue.groupedCount <= 1) {
    return issue.title
  }

  if (issue.groupedProjectCount && issue.groupedProjectCount > 1) {
    return `${issue.title} in ${issue.groupedProjectCount} production projects`
  }

  return `${issue.title} (${issue.groupedCount} findings)`
}

function categoryTextClass(category: Category) {
  return {
    Architecture: 'text-slate-400',
    Security: 'text-orange-400',
    EfCore: 'text-slate-400',
    DependencyInjection: 'text-teal-400',
    ApiReadiness: 'text-emerald-400',
  }[category]
}

function getRuleId(issue: AnalysisIssue) {
  if (issue.ruleId) {
    return issue.ruleId
  }

  const prefix = {
    Architecture: 'ARCH',
    DependencyInjection: 'DI',
    EfCore: 'EF',
    Security: 'SEC',
    ApiReadiness: 'API',
  }[issue.category]

  const numeric = issue.id.match(/\d+/)?.[0]?.padStart(3, '0') ?? String(Math.abs(hashString(issue.title)) % 900 + 100)
  return `${prefix}${numeric.slice(-3)}`
}

function getRootCauseKey(issue: AnalysisIssue) {
  return issue.rootCauseKey
    ?? `${getRuleId(issue)}|${issue.category}|${issue.projectName ?? 'Solution'}|${issue.filePath ?? ''}|${issue.title}`
}

function getHighestRiskActiveRuleId(rules: RuleDocumentation[], issues: AnalysisIssue[]) {
  if (rules.length === 0 || issues.length === 0) {
    return null
  }

  const knownRuleIds = new Set(rules.map((rule) => rule.ruleId))
  const activeRules = issues
    .filter((issue) => knownRuleIds.has(getRuleId(issue)))
    .reduce((summary, issue) => {
      const ruleId = getRuleId(issue)
      const current = summary.get(ruleId) ?? { count: 0, maxSeverity: 0 }
      summary.set(ruleId, {
        count: current.count + 1,
        maxSeverity: Math.max(current.maxSeverity, severityRank[issue.severity]),
      })
      return summary
    }, new Map<string, { count: number; maxSeverity: number }>())

  return [...activeRules.entries()]
    .sort((left, right) => {
      const severityDelta = right[1].maxSeverity - left[1].maxSeverity
      if (severityDelta !== 0) return severityDelta

      const countDelta = right[1].count - left[1].count
      if (countDelta !== 0) return countDelta

      return left[0].localeCompare(right[0])
    })[0]?.[0] ?? null
}

function hashString(value: string) {
  return [...value].reduce((hash, char) => (hash * 31 + char.charCodeAt(0)) | 0, 0)
}

function exportReport(
  result: AnalysisResult | null,
  options: { codeFiles: CodeFile[]; dispositions: Record<string, FindingDisposition>; format: ExportFormat; includeSourcePreview: boolean },
) {
  if (!result) {
    return
  }

  const sanitizedResult = sanitizeAnalysisResultForExport(result)
  const relevantDispositions = getRelevantFindingDispositions(result, options.dispositions)
  const canIncludeSourcePreview = options.includeSourcePreview && !isLiveRepositoryAnalysisResult(result)
  const sourcePreview = canIncludeSourcePreview
    ? options.codeFiles.map((file) => ({
        content: file.content,
        name: file.name,
        path: sanitizePathForBrowserPersistence(file.path),
        projectName: file.projectName,
      }))
    : undefined
  const extension = options.format === 'Markdown' ? 'md' : options.format === 'HTML' ? 'html' : 'json'
  const content =
    options.format === 'Markdown'
      ? createMarkdownReport(sanitizedResult, relevantDispositions)
      : options.format === 'HTML'
        ? createHtmlReport(sanitizedResult, relevantDispositions)
        : JSON.stringify(sourcePreview ? { ...sanitizedResult, findingDispositions: relevantDispositions, sourcePreview } : { ...sanitizedResult, findingDispositions: relevantDispositions }, null, 2)
  const blob = new Blob([content], { type: options.format === 'Markdown' ? 'text/markdown' : options.format === 'HTML' ? 'text/html' : 'application/json' })
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = `${getReportFileName(result.solutionName)}.${extension}`
  anchor.click()
  URL.revokeObjectURL(url)
}

function getRelevantFindingDispositions(
  result: AnalysisResult,
  dispositions: Record<string, FindingDisposition>,
) {
  return Object.fromEntries(
    result.issues
      .map((issue) => getFindingDispositionKey(result.solutionName, issue))
      .filter((key) => dispositions[key] && dispositions[key] !== 'Open')
      .map((key) => [key, dispositions[key]]),
  ) as Record<string, FindingDisposition>
}

function isLiveRepositoryAnalysisResult(result: AnalysisResult) {
  return !result.isHistoricalSnapshot
    && !result.solutionPath
    && !result.repositoryRoot
    && (result.sourceFiles?.length ?? 0) > 0
}

function getReportFileName(solutionName: string) {
  const slug = solutionName.replace(/[^a-z0-9.-]+/gi, '-').replace(/^-+|-+$/g, '').toLowerCase()
  return `${slug || 'dotdet'}-dotdet-report`
}

function createMarkdownReport(result: AnalysisResult, dispositions: Record<string, FindingDisposition>) {
  const openIssues = getOpenFindings(result, dispositions).filter(isActiveProductionFinding)
  const suppressedIssues = getSuppressedFindings(result, dispositions)
  const openCounts = getSeverityCounts(openIssues)
  const activeResult = rebuildAnalysisResultForActiveFindings(result, openIssues)
  const topRisks = getTopRiskIssues(openIssues).slice(0, 8)
  const roadmap = buildRecommendedRoadmap(activeResult, openCounts)
  const lines = [
    '# DotDet Production Readiness Report',
    '',
    `**Solution:** ${result.solutionName}`,
    `**Analyzed:** ${new Date(result.analyzedAt).toLocaleString()}`,
    `**Production Readiness:** ${activeResult.overallScore}/100 (${getGrade(activeResult.overallScore)})`,
    `**Status:** ${getReadinessDecision(activeResult.overallScore, openCounts)}`,
    '',
    '## Executive Summary',
    '',
    activeResult.engineeringAssessment?.overallProductionReadiness ?? getOverviewNarrative(activeResult, openCounts),
    '',
    `**Score explanation:** ${activeResult.engineeringAssessment?.scoreExplanation ?? 'DotDet calculated the readiness score from weighted category scores and severity caps.'}`,
    '',
    '## Category Scores',
    '',
    ...categoryDefinitions.map((category) => `- **${category.reportLabel}:** ${activeResult.categoryScores[category.scoreKey]}/100`),
    '',
    '## Top Open Risks',
    '',
    ...(topRisks.length > 0
      ? topRisks.map((issue) => `- **${getRuleId(issue)} ${issue.title}** (${issue.severity}, ${getCategoryLabel(issue.category)}): ${issue.recommendation}`)
      : ['- No open high-priority risks detected.']),
    '',
    '## Recommended Priorities',
    '',
    ...roadmap.map((item) => `- ${item}`),
    '',
    '## Engineering Assessment',
    '',
    ...getAssessmentMarkdownLines(activeResult.engineeringAssessment),
    '',
    '## Architecture And Project Graph',
    '',
    ...getArchitectureMapMarkdownLines(result.architectureMap, result.projectGraph),
    '',
    '## Open Findings By Category',
    '',
  ]

  if (openIssues.length === 0) {
    lines.push('No open findings detected.', '')
  } else {
    for (const category of categoryDefinitions) {
      const issues = openIssues
        .filter((issue) => issue.category === category.key)
        .sort((left, right) => severityRank[right.severity] - severityRank[left.severity] || getRuleId(left).localeCompare(getRuleId(right)))
      if (!issues.length) continue

      lines.push(`### ${category.reportLabel}`, '')
      for (const issue of issues) {
        appendFindingMarkdown(lines, issue, 'Open')
      }
    }
  }

  lines.push('## Suppressed / Accepted Risks', '')
  if (suppressedIssues.length === 0) {
    lines.push('No suppressed, ignored, false-positive, or accepted-risk findings.', '')
  } else {
    for (const issue of suppressedIssues) {
      appendFindingMarkdown(lines, issue, getFindingDisposition(result.solutionName, issue, dispositions), { concise: true })
    }
  }

  lines.push('## Rule Explanations', '')
  for (const issue of getUniqueRuleExplanations(result.issues)) {
    const documentationLinks = getDocumentationLinks(issue)
    lines.push(
      `### ${getRuleId(issue)} - ${issue.title}`,
      '',
      `- **Problem:** ${issue.problemSummary ?? issue.description}`,
      `- **Why it matters:** ${getWhyItMatters(issue)}`,
      `- **Recommended pattern:** ${issue.recommendedPattern ?? issue.recommendation}`,
    )
    if (documentationLinks.length) {
      lines.push('- **Microsoft documentation:**')
      for (const link of documentationLinks) {
        lines.push(`  - [${link.label}](${link.href})`)
      }
    }
    lines.push('')
  }

  return lines.join('\n')
}

function appendFindingMarkdown(
  lines: string[],
  issue: AnalysisIssue,
  disposition: FindingDisposition,
  options: { concise?: boolean } = {},
) {
  const documentationLinks = getDocumentationLinks(issue)
  const suggestedSnippet = getSuggestedSnippet(issue)
  lines.push(
    `#### ${getRuleId(issue)} - ${issue.title}`,
    '',
    `- **Severity:** ${issue.severity}`,
    `- **Confidence:** ${issue.confidence ?? 'Medium'}`,
    `- **Detection method:** ${issue.detectionMethod ?? getDetectionMethod(issue)}`,
    `- **Disposition:** ${disposition}`,
    `- **Project:** ${issue.projectName ?? 'Solution'}`,
    `- **File:** ${issue.filePath ? formatPath(issue.filePath) : 'Not available'}`,
    `- **Line:** ${issue.lineNumber ?? 'Not available'}`,
    '',
    `**Problem:** ${issue.problemSummary ?? issue.description}`,
    '',
    `**Why DotDet detected it:** ${getDetectionReason(issue)}`,
    '',
    `**Why it matters:** ${getWhyItMatters(issue)}`,
    '',
    `**Recommended solution:** ${issue.recommendedPattern ?? issue.recommendation}`,
    '',
  )

  appendEvidenceMarkdown(lines, issue)

  if (!options.concise) {
    lines.push(`**Suggested implementation:** ${issue.suggestedImplementation ?? issue.recommendation}`, '')
    if (suggestedSnippet && suggestedSnippet.length <= 1600) {
      lines.push('```csharp', suggestedSnippet, '```', '')
    }
    if (issue.badExample && issue.badExample.length <= 1600) {
      lines.push('**Bad example:**', '', '```csharp', issue.badExample, '```', '')
    }
    if (issue.goodExample && issue.goodExample.length <= 1600) {
      lines.push('**Good example:**', '', '```csharp', issue.goodExample, '```', '')
    }
  }

  if (documentationLinks.length) {
    lines.push('**Documentation:**')
    for (const link of documentationLinks) {
      lines.push(`- [${link.label}](${link.href})`)
    }
    lines.push('')
  }
}

function appendEvidenceMarkdown(lines: string[], issue: AnalysisIssue) {
  if (!issue.evidence?.length) {
    return
  }

  lines.push('**Evidence:**')
  for (const item of issue.evidence) {
    const location = item.filePath ? ` (${formatPath(item.filePath)}${item.lineNumber ? `:${item.lineNumber}` : ''})` : ''
    lines.push(`- ${item.label}: ${item.detail}${location}`)
  }
  lines.push('')
}

function createHtmlReport(
  result: AnalysisResult,
  dispositions: Record<string, FindingDisposition>,
) {
  const openIssues = getOpenFindings(result, dispositions).filter(isActiveProductionFinding)
  const suppressedIssues = getSuppressedFindings(result, dispositions)
  const openCounts = getSeverityCounts(openIssues)
  const activeResult = rebuildAnalysisResultForActiveFindings(result, openIssues)
  const status = getReadinessDecision(activeResult.overallScore, openCounts)
  const grade = getGrade(activeResult.overallScore)
  const concerns = getPrimaryConcerns(activeResult)
  const topRisks = getTopRiskIssues(openIssues).slice(0, 8)
  const groupedFindings = categoryDefinitions.map((category) => ({
    category,
    issues: openIssues.filter((issue) => issue.category === category.key),
    score: activeResult.categoryScores[category.scoreKey],
  }))
  const architectureMap = result.architectureMap ?? buildFallbackArchitectureMap(result.projectGraph, result.issues)
  const roadmap = buildRecommendedRoadmap(activeResult, openCounts)
  const ruleExplanations = getUniqueRuleExplanations(result.issues)
  const generatedAt = new Date().toLocaleString()

  return `<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>${escapeHtml(result.solutionName)} - DotDet Production Readiness Report</title>
  <style>
    :root {
      color-scheme: light;
      --det-green: #0d660d;
      --det-green-dark: #084a08;
      --ink: #111827;
      --muted: #5b6472;
      --line: #d8dee8;
      --panel: #ffffff;
      --surface: #f5f7fa;
      --surface-2: #eef2f7;
      --red: #b42318;
      --amber: #a15c07;
      --blue: #2563eb;
      --teal: #0f766e;
    }
    * { box-sizing: border-box; }
    body {
      margin: 0;
      background: #e9edf3;
      color: var(--ink);
      font-family: "Segoe UI Variable", "Segoe UI", Arial, sans-serif;
      font-size: 14px;
      line-height: 1.55;
    }
    a { color: var(--det-green); overflow-wrap: anywhere; text-decoration: none; }
    a:hover { text-decoration: underline; }
    .report { max-width: 1180px; margin: 0 auto; background: var(--panel); min-height: 100vh; }
    .cover {
      min-height: 720px;
      display: grid;
      align-content: space-between;
      padding: 56px;
      color: #fff;
      background:
        linear-gradient(135deg, rgba(13, 102, 13, 0.96), rgba(8, 74, 8, 0.96)),
        linear-gradient(45deg, #0d660d, #134e4a);
      break-after: page;
    }
    .brand { display: flex; align-items: center; gap: 14px; }
    .brand img { width: 52px; height: 52px; object-fit: contain; border: 1px solid rgba(255,255,255,.32); background: rgba(0,0,0,.12); }
    .brand-mark { display: none; place-items: center; width: 52px; height: 52px; border: 1px solid rgba(255,255,255,.35); font-weight: 700; }
    .brand-title { font-size: 28px; font-weight: 650; letter-spacing: .2px; }
    .subtitle { color: rgba(255,255,255,.78); font-size: 13px; text-transform: uppercase; letter-spacing: .08em; }
    .cover h1 { max-width: 820px; margin: 90px 0 18px; font-size: clamp(38px, 7vw, 68px); line-height: 1.02; letter-spacing: -.02em; }
    .cover-summary { max-width: 720px; color: rgba(255,255,255,.84); font-size: 18px; }
    .cover-grid { display: grid; grid-template-columns: repeat(4, 1fr); gap: 12px; margin-top: 42px; }
    .cover-metric { border: 1px solid rgba(255,255,255,.22); background: rgba(0,0,0,.12); padding: 16px; }
    .cover-metric span { display: block; color: rgba(255,255,255,.72); font-size: 11px; text-transform: uppercase; letter-spacing: .08em; }
    .cover-metric strong { display: block; margin-top: 8px; font-size: 28px; line-height: 1; }
    .cover-footer { display: flex; justify-content: space-between; gap: 24px; color: rgba(255,255,255,.75); font-size: 13px; }
    .content { padding: 34px 42px 56px; }
    section { margin: 0 0 28px; break-inside: avoid; }
    h2 { margin: 0 0 12px; padding-bottom: 8px; border-bottom: 2px solid var(--line); color: #0f172a; font-size: 20px; }
    h3 { margin: 0 0 8px; color: #172033; font-size: 14px; text-transform: uppercase; letter-spacing: .06em; }
    .muted { color: var(--muted); }
    .grid { display: grid; gap: 12px; }
    .grid-2 { grid-template-columns: repeat(2, minmax(0, 1fr)); }
    .grid-3 { grid-template-columns: repeat(3, minmax(0, 1fr)); }
    .grid-5 { grid-template-columns: repeat(5, minmax(0, 1fr)); }
    .panel { border: 1px solid var(--line); background: var(--panel); padding: 16px; }
    .panel-soft { border: 1px solid var(--line); background: var(--surface); padding: 16px; }
    .section-note { margin: -2px 0 12px; color: var(--muted); }
    .score-row { display: grid; grid-template-columns: 220px 1fr; gap: 18px; align-items: center; }
    .score-number { font-size: 58px; font-weight: 700; line-height: 1; }
    .score-number small { font-size: 24px; color: var(--muted); }
    .bar { height: 9px; background: #d7dee8; overflow: hidden; }
    .bar > span { display: block; height: 100%; background: var(--det-green); }
    .category-score { display: flex; justify-content: space-between; gap: 10px; padding: 10px 0; border-bottom: 1px solid var(--line); }
    .category-score:last-child { border-bottom: 0; }
    .badge { display: inline-flex; align-items: center; gap: 5px; border: 1px solid var(--line); background: var(--surface); padding: 2px 7px; font-size: 11px; font-weight: 650; white-space: nowrap; }
    .sev-Critical, .sev-Error { border-color: #f0b4ae; background: #fff1f0; color: var(--red); }
    .sev-Warning { border-color: #f2d39a; background: #fff7e6; color: var(--amber); }
    .sev-Info { border-color: #bed4ff; background: #eff6ff; color: var(--blue); }
    .status-suppressed { border-color: #cbd5e1; background: #f1f5f9; color: #475569; }
    table { width: 100%; border-collapse: collapse; border: 1px solid var(--line); background: var(--panel); }
    th, td { border-bottom: 1px solid var(--line); padding: 9px 10px; text-align: left; vertical-align: top; }
    th { background: var(--surface-2); color: #334155; font-size: 11px; text-transform: uppercase; letter-spacing: .06em; }
    tr.suppressed { opacity: .74; background: #f8fafc; }
    .risk-list { display: grid; gap: 10px; }
    .risk-item { border-left: 4px solid var(--red); background: var(--surface); padding: 12px; }
    .risk-item.warning { border-left-color: var(--amber); }
    .assessment-list { margin: 0; padding-left: 18px; }
    .assessment-list li { margin: 4px 0; }
    .architecture-layers { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 10px; }
    .layer { border: 1px solid var(--line); background: var(--surface); }
    .layer h4 { margin: 0; padding: 8px 10px; border-bottom: 1px solid var(--line); background: var(--surface-2); font-size: 12px; }
    .node { margin: 8px; border: 1px solid var(--line); background: #fff; padding: 8px; font-size: 12px; }
    pre {
      overflow: auto;
      margin: 8px 0 0;
      border: 1px solid #1f2937;
      background: #0d1117;
      color: #e5edf5;
      padding: 12px;
      font-family: "Cascadia Code", Consolas, monospace;
      font-size: 11px;
      line-height: 1.5;
      white-space: pre-wrap;
    }
    .roadmap { counter-reset: step; display: grid; gap: 10px; }
    .roadmap-item { position: relative; border: 1px solid var(--line); background: var(--surface); padding: 12px 12px 12px 44px; }
    .roadmap-item::before { counter-increment: step; content: counter(step); position: absolute; left: 12px; top: 12px; display: grid; place-items: center; width: 22px; height: 22px; background: var(--det-green); color: #fff; font-weight: 700; font-size: 12px; }
    .rule-card { border: 1px solid var(--line); padding: 14px; break-inside: avoid; }
    .rule-card + .rule-card { margin-top: 10px; }
    .finding-details { display: grid; gap: 10px; }
    .finding-details p { margin: 0; }
    .finding-meta { display: flex; flex-wrap: wrap; gap: 6px; margin-bottom: 8px; }
    .doc-links { margin: 8px 0 0; padding-left: 18px; }
    .print-note { margin-top: 16px; color: rgba(255,255,255,.72); font-size: 12px; }
    .footer { border-top: 1px solid var(--line); padding: 18px 42px 28px; color: var(--muted); font-size: 12px; }
    @media (max-width: 860px) {
      .cover { padding: 34px 24px; min-height: auto; }
      .cover-grid, .grid-2, .grid-3, .grid-5, .score-row { grid-template-columns: 1fr; }
      .content { padding: 24px 18px; }
      table { font-size: 12px; }
    }
    @media print {
      body { background: #fff; }
      .report { max-width: none; }
      .content { padding: 22mm 16mm; }
      a { color: #0645ad; text-decoration: underline; }
      .panel, .panel-soft, .rule-card, .risk-item, table { break-inside: avoid; }
      h2 { break-after: avoid; }
      .cover {
        background: #fff !important;
        color: #111827 !important;
        border-bottom: 3px solid var(--det-green);
      }
      .cover-summary, .cover-footer, .print-note, .subtitle { color: #374151 !important; }
      .cover-metric { background: #f8fafc; border-color: #d8dee8; }
      .cover-metric span { color: #5b6472; }
      .brand img, .brand-mark { border-color: #d8dee8; background: #fff; }
    }
  </style>
</head>
<body>
  <main class="report">
    <section class="cover">
      <div>
        <div class="brand">
          <img src="dotdet-logo.png" alt="DotDet logo" onerror="this.style.display='none';this.nextElementSibling.style.display='grid';" />
          <div class="brand-mark">.DET</div>
          <div>
            <div class="brand-title">DotDet</div>
            <div class="subtitle">.NET Development Engineering Toolkit</div>
          </div>
        </div>
        <h1>Production Readiness Report</h1>
        <p class="cover-summary">${escapeHtml(activeResult.engineeringAssessment?.overallProductionReadiness ?? getOverviewNarrative(activeResult, openCounts))}</p>
        <div class="cover-grid">
          ${coverMetricHtml('Solution', result.solutionName)}
          ${coverMetricHtml('Score', `${activeResult.overallScore}/100`)}
          ${coverMetricHtml('Grade', grade)}
          ${coverMetricHtml('Status', status)}
        </div>
      </div>
      <div>
        <div class="cover-footer">
          <span>Analyzed ${escapeHtml(new Date(result.analyzedAt).toLocaleString())}</span>
          <span>${result.projectGraph.projects.length} projects &middot; ${openIssues.length} open findings &middot; ${suppressedIssues.length} suppressed or accepted</span>
        </div>
        <div class="print-note">Prepared for engineering review, pull request discussion, and release readiness assessment.</div>
      </div>
    </section>

    <div class="content">
      <section>
        <h2>Executive Summary</h2>
        <div class="grid grid-2">
          <div class="panel">
            <h3>Production Readiness Score</h3>
            <div class="score-row">
              <div class="score-number">${activeResult.overallScore}<small>/100</small></div>
              <div>
                <div class="muted">Grade ${escapeHtml(grade)} · ${escapeHtml(status)}</div>
                <div class="bar" aria-hidden="true"><span style="width:${Math.max(0, Math.min(100, activeResult.overallScore))}%"></span></div>
                <p>${escapeHtml(getOverviewNarrative(activeResult, openCounts))}</p>
                <p><strong>Score explanation:</strong> ${escapeHtml(activeResult.engineeringAssessment?.scoreExplanation ?? 'DotDet calculated the readiness score from weighted category scores and severity caps.')}</p>
              </div>
            </div>
          </div>
          <div class="panel-soft">
            <h3>Primary Concerns</h3>
            <ul class="assessment-list">${concerns.map((concern) => `<li>${escapeHtml(concern)}</li>`).join('')}</ul>
          </div>
        </div>
      </section>

      <section>
        <h2>Category Scores</h2>
        <div class="grid grid-5">
          ${categoryDefinitions.map((category) => categoryScoreHtml(category.reportLabel, activeResult.categoryScores[category.scoreKey], openIssues.filter((issue) => issue.category === category.key).length)).join('')}
        </div>
      </section>

      <section>
        <h2>Engineering Assessment Summary</h2>
        ${assessmentHtml(activeResult.engineeringAssessment)}
      </section>

      <section>
        <h2>Architecture</h2>
        <p class="section-note">Logical layers, project dependencies, and architecture violations inferred from the analyzed solution.</p>
        <div class="architecture-layers">
          ${architectureMap.layers.map((layer) => `
            <div class="layer">
              <h4>${escapeHtml(layer.name)}</h4>
              ${layer.projectNames.map((name) => {
                const project = architectureMap.projects.find((candidate) => candidate.name === name)
                return `<div class="node"><strong>${escapeHtml(name)}</strong><br /><span class="muted">${escapeHtml(project?.namespaceRoot ?? '')}</span><br /><span>${project?.issueCount ?? 0} findings</span></div>`
              }).join('')}
            </div>
          `).join('')}
        </div>
        <h3 style="margin-top:18px">Project Graph Summary</h3>
        ${projectGraphHtml(architectureMap)}
      </section>

      <section>
        <h2>Top Open Risks</h2>
        <p class="section-note">Open findings with the highest release impact. Suppressed, ignored, false-positive, and accepted-risk findings are listed separately.</p>
        <div class="risk-list">
          ${topRisks.map((issue) => riskItemHtml(issue, dispositions, result.solutionName)).join('') || '<div class="panel-soft">No significant production risks detected.</div>'}
        </div>
      </section>

      <section>
        <h2>Recommended Remediation Roadmap</h2>
        <div class="roadmap">
          ${roadmap.map((item) => `<div class="roadmap-item">${escapeHtml(item)}</div>`).join('')}
        </div>
      </section>

      <section>
        <h2>Findings By Category</h2>
        ${groupedFindings.map((group) => findingsCategoryHtml(group.category.reportLabel, group.score, group.issues, dispositions, result.solutionName)).join('')}
      </section>

      <section>
        <h2>Suppressions And Accepted Risks</h2>
        <p class="section-note">These findings remain visible for auditability, but they are separated from the open remediation queue.</p>
        ${suppressedFindingsHtml(suppressedIssues, dispositions, result.solutionName)}
      </section>

      <section>
        <h2>Rule Explanations</h2>
        ${ruleExplanations.map(ruleExplanationHtml).join('')}
      </section>
    </div>
    <footer class="footer">
      <strong>Generated by DotDet</strong> &middot; ${escapeHtml(generatedAt)} &middot; JSON export remains available for machine-readable analysis details.
    </footer>
  </main>
</body>
</html>`
}

function getOpenFindings(result: AnalysisResult, dispositions: Record<string, FindingDisposition>) {
  return result.issues.filter((issue) => getFindingDisposition(result.solutionName, issue, dispositions) === 'Open')
}

function getSuppressedFindings(result: AnalysisResult, dispositions: Record<string, FindingDisposition>) {
  return result.issues.filter((issue) => getFindingDisposition(result.solutionName, issue, dispositions) !== 'Open')
}

function rebuildAnalysisResultForActiveFindings(result: AnalysisResult, activeIssues: AnalysisIssue[]): AnalysisResult {
  const categoryScores = calculateCategoryScores(activeIssues)
  const overallScore = calculateOverallScore(categoryScores, activeIssues)

  return {
    ...result,
    categoryScores,
    overallScore,
    issues: activeIssues,
    engineeringAssessment: result.engineeringAssessment
      ? {
          ...result.engineeringAssessment,
          scoreExplanation: buildScoreExplanation(overallScore, categoryScores, activeIssues),
          highestRisks: getRiskAreas({ ...result, issues: activeIssues, categoryScores, overallScore })
            .slice(0, 3)
            .map((area) =>
              area.criticalCount > 0
                ? `${area.category.reportLabel}: ${area.criticalCount} critical/error findings and score ${area.score}/100.`
                : `${area.category.reportLabel}: score ${area.score}/100 with ${area.issueCount} findings to review.`,
            ),
          recommendedPriorities: buildRecommendedRoadmap(
            { ...result, issues: activeIssues, categoryScores, overallScore, engineeringAssessment: undefined },
            getSeverityCounts(activeIssues),
          ),
        }
      : result.engineeringAssessment,
  }
}

function buildScoreExplanation(score: number, scores: CategoryScores, activeIssues: AnalysisIssue[]) {
  if (activeIssues.length === 0) {
    return `DotDet calculated the ${score}/100 readiness score from weighted category scores (Security ${scores.security}, API ${scores.apiReadiness}, EF Core ${scores.efCore}, Dependency Injection ${scores.dependencyInjection}, Architecture ${scores.architecture}), finding severity, confidence, suppression state, and release-impact caps. No active production root-cause findings were detected.`
  }

  const rootCauseCount = new Set(activeIssues.map(getRootCauseKey)).size
  const topRisks = getTopRiskIssues(activeIssues)
    .slice(0, 3)
    .map((issue) => `${getRuleId(issue)} ${issue.title}`)
  const rootCauseText = topRisks.length > 0 ? ` Major root causes include ${topRisks.join('; ')}.` : ''

  return `DotDet calculated the ${score}/100 readiness score from weighted category scores (Security ${scores.security}, API ${scores.apiReadiness}, EF Core ${scores.efCore}, Dependency Injection ${scores.dependencyInjection}, Architecture ${scores.architecture}), finding severity, confidence, suppression state, and release-impact caps across ${rootCauseCount} active production root-cause finding(s).${rootCauseText}`
}

function calculateCategoryScores(issues: AnalysisIssue[]): CategoryScores {
  return {
    architecture: calculateScoreForIssues(issues.filter((issue) => issue.category === 'Architecture')),
    dependencyInjection: calculateScoreForIssues(issues.filter((issue) => issue.category === 'DependencyInjection')),
    efCore: calculateScoreForIssues(issues.filter((issue) => issue.category === 'EfCore')),
    security: calculateScoreForIssues(issues.filter((issue) => issue.category === 'Security')),
    apiReadiness: calculateScoreForIssues(issues.filter((issue) => issue.category === 'ApiReadiness')),
  }
}

function calculateOverallScore(scores: CategoryScores, issues: AnalysisIssue[]) {
  const weightedScore = Math.round(
    (scores.security * 0.25)
      + (scores.apiReadiness * 0.20)
      + (scores.efCore * 0.20)
      + (scores.dependencyInjection * 0.20)
      + (scores.architecture * 0.15),
  )

  return Math.min(weightedScore, getCriticalScoreCap(issues))
}

function calculateScoreForIssues(issues: AnalysisIssue[]) {
  const groups = new Map<string, number[]>()

  for (const issue of issues.filter(isActiveProductionFinding)) {
    const key = getRootCauseKey(issue)
    groups.set(key, [...(groups.get(key) ?? []), getFindingPenalty(issue)])
  }

  const penalty = [...groups.values()].reduce((total, penalties) => {
    const ordered = [...penalties].sort((left, right) => right - left)
    const basePenalty = ordered[0] ?? 0
    const repeatPenalty = ordered
      .slice(1)
      .reduce((subtotal, value) => subtotal + Math.max(1, Math.floor(value / 3)), 0)

    return total + basePenalty + Math.min(repeatPenalty, basePenalty)
  }, 0)

  return Math.max(0, 100 - penalty)
}

function getCriticalScoreCap(issues: AnalysisIssue[]) {
  const activeCriticalRoots = new Set(
    issues
      .filter((issue) => issue.severity === 'Critical')
      .filter(isActiveProductionFinding)
      .map(getRootCauseKey),
  )

  if (activeCriticalRoots.size === 0) return 100
  if (activeCriticalRoots.size <= 3) return 82
  if (activeCriticalRoots.size <= 8) return 68
  return 49
}

function getFindingPenalty(issue: AnalysisIssue) {
  const severityPenalty = {
    Critical: 15,
    Error: 8,
    Warning: 4,
    Info: 1,
  }[issue.severity]

  return issue.confidence === 'Low' ? Math.min(severityPenalty, 1) : severityPenalty
}

function coverMetricHtml(label: string, value: string | number) {
  return `<div class="cover-metric"><span>${escapeHtml(label)}</span><strong>${escapeHtml(String(value))}</strong></div>`
}

function categoryScoreHtml(label: string, score: number, issueCount: number) {
  return `
    <div class="panel">
      <h3>${escapeHtml(label)}</h3>
      <div class="score-number" style="font-size:34px">${score}<small>/100</small></div>
      <div class="bar" aria-hidden="true"><span style="width:${Math.max(0, Math.min(100, score))}%"></span></div>
      <p class="muted">${issueCount} findings · ${escapeHtml(getCategoryStatus(score, issueCount))}</p>
    </div>`
}

function assessmentHtml(assessment?: EngineeringAssessmentSummary) {
  if (!assessment) {
    return '<div class="panel-soft">No engineering assessment was included in this analysis response.</div>'
  }

  const sections: Array<[string, string[]]> = [
    ['Strong Areas', assessment.strongAreas],
    ['Highest Risks', assessment.highestRisks],
    ['Architecture', assessment.architecturalObservations],
    ['Security', assessment.securityObservations],
    ['API Readiness', assessment.apiReadinessObservations],
    ['Maintainability', assessment.maintainabilityObservations],
  ]

  return `<div class="panel-soft">
      <h3>Score Explanation</h3>
      <p>${escapeHtml(assessment.scoreExplanation)}</p>
    </div>
    <div class="grid grid-2" style="margin-top:10px">${sections
    .map(([title, items]) => `
      <div class="panel-soft">
        <h3>${escapeHtml(title)}</h3>
        <ul class="assessment-list">${items.map((item) => `<li>${escapeHtml(item)}</li>`).join('') || '<li>No observations.</li>'}</ul>
      </div>
    `)
    .join('')}</div>`
}

function projectGraphHtml(map: ArchitectureMap) {
  if (map.dependencies.length === 0) {
    return '<div class="panel-soft">No project dependencies were discovered.</div>'
  }

  return `
    <table>
      <thead><tr><th>Source</th><th>Target</th><th>Direction</th><th>Assessment</th></tr></thead>
      <tbody>
        ${map.dependencies.map((dependency) => `
          <tr>
            <td>${escapeHtml(dependency.sourceProjectName)}<br /><span class="muted">${escapeHtml(dependency.sourceLayer)}</span></td>
            <td>${escapeHtml(dependency.targetProjectName)}<br /><span class="muted">${escapeHtml(dependency.targetLayer)}</span></td>
            <td>${escapeHtml(dependency.direction)}</td>
            <td>${dependency.isViolation ? '<span class="badge sev-Error">Violation</span>' : '<span class="badge">Allowed</span>'}<br /><span class="muted">${escapeHtml(dependency.reason ?? '')}</span></td>
          </tr>
        `).join('')}
      </tbody>
    </table>`
}

function riskItemHtml(issue: AnalysisIssue, dispositions: Record<string, FindingDisposition>, solutionName: string) {
  const disposition = getFindingDisposition(solutionName, issue, dispositions)
  const severityClass = issue.severity === 'Warning' ? 'warning' : ''

  return `
    <div class="risk-item ${severityClass}">
      <div>${severityBadgeHtml(issue.severity)} ${disposition !== 'Open' ? `<span class="badge status-suppressed">${escapeHtml(disposition)}</span>` : ''} <span class="badge">${escapeHtml(getRuleId(issue))}</span></div>
      <h3 style="margin-top:8px">${escapeHtml(issue.title)}</h3>
      <p>${escapeHtml(issue.problemSummary ?? issue.description)}</p>
      <p><strong>Recommended action:</strong> ${escapeHtml(issue.recommendation)}</p>
    </div>`
}

function findingsCategoryHtml(
  label: string,
  score: number,
  issues: AnalysisIssue[],
  dispositions: Record<string, FindingDisposition>,
  solutionName: string,
) {
  const rows = issues.map((issue) => {
    const disposition = getFindingDisposition(solutionName, issue, dispositions)
    const snippet = getSuggestedSnippet(issue)
    const docs = getDocumentationLinks(issue)

    return `
      <tr class="${disposition !== 'Open' ? 'suppressed' : ''}">
        <td>${severityBadgeHtml(issue.severity)} ${disposition !== 'Open' ? `<span class="badge status-suppressed">${escapeHtml(disposition)}</span>` : ''}</td>
        <td><strong>${escapeHtml(getRuleId(issue))}</strong><br />${escapeHtml(issue.title)}<br /><span class="muted">${escapeHtml(issue.problemSummary ?? issue.description)}</span>${evidenceListHtml(issue)}${snippet ? `<pre><code>${escapeHtml(snippet)}</code></pre>` : ''}</td>
        <td>${escapeHtml(issue.projectName ?? 'Solution')}<br /><span class="muted">${escapeHtml(issue.filePath ? formatPath(issue.filePath) : 'Not available')}:${escapeHtml(String(issue.lineNumber ?? '-'))}</span></td>
        <td>${escapeHtml(issue.recommendation)}${docs.length ? `<ul class="doc-links">${docs.map((link) => `<li><a href="${escapeAttribute(link.href)}">${escapeHtml(link.label)}</a></li>`).join('')}</ul>` : ''}</td>
      </tr>`
  })

  return `
    <div class="rule-card">
      <h3>${escapeHtml(label)} · ${score}/100 · ${issues.length} findings</h3>
      ${issues.length ? `
        <table>
          <thead><tr><th>Severity</th><th>Finding</th><th>Location</th><th>Recommendation</th></tr></thead>
          <tbody>${rows.join('')}</tbody>
        </table>
      ` : '<p class="muted">No findings detected for this category.</p>'}
    </div>`
}

function evidenceListHtml(issue: AnalysisIssue) {
  if (!issue.evidence?.length) {
    return ''
  }

  return `<ul class="doc-links">${issue.evidence
    .map((item) => {
      const location = item.filePath ? ` (${formatPath(item.filePath)}${item.lineNumber ? `:${item.lineNumber}` : ''})` : ''
      return `<li><strong>${escapeHtml(item.label)}</strong>: ${escapeHtml(item.detail)}${escapeHtml(location)}</li>`
    })
    .join('')}</ul>`
}

function suppressedFindingsHtml(
  issues: AnalysisIssue[],
  dispositions: Record<string, FindingDisposition>,
  solutionName: string,
) {
  if (issues.length === 0) {
    return '<div class="panel-soft">No suppressed, ignored, false-positive, or accepted-risk findings.</div>'
  }

  const rows = issues.map((issue) => {
    const disposition = getFindingDisposition(solutionName, issue, dispositions)
    const suppression = issue.suppression

    return `
      <tr class="suppressed">
        <td><span class="badge status-suppressed">${escapeHtml(disposition)}</span></td>
        <td>${severityBadgeHtml(issue.severity)} <span class="badge">${escapeHtml(getRuleId(issue))}</span><br /><strong>${escapeHtml(issue.title)}</strong><br /><span class="muted">${escapeHtml(issue.problemSummary ?? issue.description)}</span></td>
        <td>${escapeHtml(issue.projectName ?? 'Solution')}<br /><span class="muted">${escapeHtml(issue.filePath ? formatPath(issue.filePath) : 'Not available')}:${escapeHtml(String(issue.lineNumber ?? '-'))}</span></td>
        <td>${escapeHtml(suppression?.reason ?? `${disposition} set locally in DotDet.`)}${suppression?.expiration ? `<br /><span class="muted">Expires ${escapeHtml(new Date(suppression.expiration).toLocaleDateString())}</span>` : ''}</td>
      </tr>`
  })

  return `
    <table>
      <thead><tr><th>Disposition</th><th>Finding</th><th>Location</th><th>Reason</th></tr></thead>
      <tbody>${rows.join('')}</tbody>
    </table>`
}

function ruleExplanationHtml(issue: AnalysisIssue) {
  const docs = getDocumentationLinks(issue)

  return `
    <div class="rule-card">
      <div>${severityBadgeHtml(issue.severity)} <span class="badge">${escapeHtml(getRuleId(issue))}</span> <span class="badge">${escapeHtml(issue.detectionMethod ?? getDetectionMethod(issue))}</span></div>
      <h3 style="margin-top:8px">${escapeHtml(issue.title)}</h3>
      <p><strong>Problem:</strong> ${escapeHtml(issue.problemSummary ?? issue.description)}</p>
      <p><strong>Why it matters:</strong> ${escapeHtml(getWhyItMatters(issue))}</p>
      <p><strong>Recommended pattern:</strong> ${escapeHtml(issue.recommendedPattern ?? issue.recommendation)}</p>
      ${issue.goodExample ? `<p><strong>Good example:</strong></p><pre><code>${escapeHtml(issue.goodExample)}</code></pre>` : ''}
      ${issue.badExample ? `<p><strong>Bad example:</strong></p><pre><code>${escapeHtml(issue.badExample)}</code></pre>` : ''}
      ${docs.length ? `<p><strong>Microsoft documentation:</strong></p><ul class="doc-links">${docs.map((link) => `<li><a href="${escapeAttribute(link.href)}">${escapeHtml(link.label)}</a></li>`).join('')}</ul>` : ''}
    </div>`
}

function severityBadgeHtml(severity: Severity) {
  return `<span class="badge sev-${severity}">${escapeHtml(severity)}</span>`
}

function getTopRiskIssues(issues: AnalysisIssue[]) {
  const byRootCause = new Map<string, AnalysisIssue>()
  const candidates = issues.filter((candidate) => candidate.severity !== 'Info')
  const preferredCandidates = candidates.some((candidate) => (candidate.confidence ?? 'Medium') !== 'Low')
    ? candidates.filter((candidate) => (candidate.confidence ?? 'Medium') !== 'Low')
    : candidates

  for (const issue of preferredCandidates) {
    const key = getRootCauseKey(issue)
    const existing = byRootCause.get(key)
    if (!existing || compareRiskIssues(issue, existing) < 0) {
      byRootCause.set(key, issue)
    }
  }

  return [...byRootCause.values()].sort(compareRiskIssues)
}

function compareRiskIssues(left: AnalysisIssue, right: AnalysisIssue) {
  return severityRank[right.severity] - severityRank[left.severity]
    || confidenceRank[right.confidence ?? 'Medium'] - confidenceRank[left.confidence ?? 'Medium']
    || getRiskPriorityRank(left) - getRiskPriorityRank(right)
    || getRuleId(left).localeCompare(getRuleId(right))
}

function getRiskPriorityRank(issue: AnalysisIssue) {
  if (issue.category === 'Security' && /auth|jwt/i.test(getRuleId(issue) + issue.title)) return 1
  if (issue.category === 'Security') return 2
  if (issue.category === 'Architecture') return 3
  if (issue.category === 'DependencyInjection') return 4
  if (issue.category === 'EfCore') return 5
  if (issue.category === 'ApiReadiness') return 6
  return 7
}

function buildRecommendedRoadmap(result: AnalysisResult, counts: Record<Severity, number>) {
  const hasProductionFindings = counts.Critical + counts.Error + counts.Warning > 0
  const assessmentPriorities = hasProductionFindings ? result.engineeringAssessment?.recommendedPriorities ?? [] : []
  const riskRecommendations = getTopRiskIssues(result.issues)
    .slice(0, 6)
    .map((issue) => `${getRuleId(issue)}: ${issue.recommendation}`)
  const baseline = hasProductionFindings
    ? [
        counts.Critical > 0
          ? 'Resolve confirmed critical blockers before production release approval.'
          : counts.Error > 0
            ? 'Review high-severity findings with the release owner and document accepted residual risk.'
            : 'Review remaining warnings and confirm release owners accept the residual risk.',
        'Re-run DotDet after remediation and attach the updated report to the pull request or release ticket.',
      ]
    : ['No immediate remediation priorities were identified.']

  return [...assessmentPriorities, ...riskRecommendations, ...baseline]
    .filter(Boolean)
    .filter((item, index, items) => items.indexOf(item) === index)
    .slice(0, 10)
}

function getUniqueRuleExplanations(issues: AnalysisIssue[]) {
  const byRule = new Map<string, AnalysisIssue>()

  for (const issue of issues) {
    const ruleId = getRuleId(issue)
    const existing = byRule.get(ruleId)
    if (!existing || severityRank[issue.severity] > severityRank[existing.severity]) {
      byRule.set(ruleId, issue)
    }
  }

  return [...byRule.values()].sort((left, right) => getRuleId(left).localeCompare(getRuleId(right)))
}

function escapeHtml(value: string | number) {
  return String(value)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#039;')
}

function escapeAttribute(value: string) {
  return escapeHtml(value).replace(/`/g, '&#096;')
}

function getAssessmentMarkdownLines(assessment?: EngineeringAssessmentSummary) {
  if (!assessment) {
    return ['Assessment details were not provided by this analysis response.', '']
  }

  const sections: Array<[string, string[]]> = [
    ['Strong Areas', assessment.strongAreas],
    ['Highest Risks', assessment.highestRisks],
    ['Architectural Observations', assessment.architecturalObservations],
    ['Security Observations', assessment.securityObservations],
    ['API Readiness Observations', assessment.apiReadinessObservations],
    ['Maintainability Observations', assessment.maintainabilityObservations],
    ['Recommended Priorities', assessment.recommendedPriorities],
  ]

  return sections.flatMap(([title, items]) => [
    `### ${title}`,
    '',
    ...(items.length > 0 ? items.map((item) => `- ${item}`) : ['- No observations.']),
    '',
  ])
}

function getArchitectureMapMarkdownLines(architectureMap: ArchitectureMap | undefined, graph: ProjectGraph) {
  const map = architectureMap ?? buildFallbackArchitectureMap(graph, [])

  return [
    `- Projects: ${map.projects.length}`,
    `- Dependencies: ${map.dependencies.length}`,
    `- Architecture violations: ${map.violations.length}`,
    '',
    '### Layers',
    '',
    ...map.layers.map((layer) => `- ${layer.name}: ${layer.projectNames.join(', ') || 'No projects'}`),
    '',
    '### Dependencies',
    '',
    ...(map.dependencies.length > 0
      ? map.dependencies.map((dependency) => {
          const marker = dependency.isViolation ? 'Violation' : 'Allowed'
          return `- ${dependency.sourceProjectName} -> ${dependency.targetProjectName} (${dependency.sourceLayer} -> ${dependency.targetLayer}, ${marker})`
        })
      : ['- No project dependencies discovered.']),
  ]
}

function getWhyItMatters(issue: AnalysisIssue) {
  if (issue.whyItMatters) {
    return issue.whyItMatters
  }

  switch (issue.category) {
    case 'Architecture':
      return 'Architecture violations make layer boundaries harder to enforce and usually increase coupling between business logic, infrastructure, and delivery mechanisms.'
    case 'DependencyInjection':
      return 'Dependency injection issues can become runtime startup failures, ambiguous service lifetimes, or hard-to-debug behavior when multiple registrations compete.'
    case 'EfCore':
      return 'Persistence and migration risks can lead to data loss, brittle deployments, or runtime model failures after a production release.'
    case 'Security':
      return 'Configuration and security gaps can expose secrets, weaken authentication assumptions, or make production APIs reachable in unsafe ways.'
    case 'ApiReadiness':
      return 'API readiness gaps make services harder to operate, observe, validate, and recover when they fail under production traffic.'
    default:
      return 'This finding indicates a production-readiness concern that should be reviewed before release.'
  }
}

function getProductionImpact(issue: AnalysisIssue) {
  if (issue.severity === 'Critical') {
    return 'This can block a production release because it indicates a high-risk failure mode or an unsafe deployment posture.'
  }

  if (issue.severity === 'Error') {
    return 'This should be addressed before release because it can cause runtime failures, unsafe defaults, or operational blind spots.'
  }

  if (issue.severity === 'Warning') {
    return 'This may be acceptable temporarily, but it increases maintenance or operations risk and should be tracked before production hardening.'
  }

  return 'This is advisory context that can improve the solution posture without necessarily blocking release.'
}

function getProductionRisk(issue: AnalysisIssue) {
  if (issue.severity === 'Critical') return 'Critical'
  if (issue.severity === 'Error') return 'High'
  if (issue.severity === 'Warning') return 'Medium'
  return 'Low'
}

function getDetectionReason(issue: AnalysisIssue) {
  if (issue.whyDetected) {
    return issue.whyDetected
  }

  const location = issue.filePath ? ` in ${formatPath(issue.filePath)}` : ''

  switch (issue.category) {
    case 'Architecture':
      return `.DET inspected project references and package references${location}, then matched them against expected .NET layering rules.`
    case 'DependencyInjection':
      return `.DET compared constructor-injected services with registrations found in Program.cs or Startup.cs${location}.`
    case 'EfCore':
      return `.DET scanned EF Core references, DbContext patterns, entity shapes, and migration operations${location}.`
    case 'Security':
      return `.DET inspected configuration and middleware setup${location} for unsafe production defaults or exposed secrets.`
    case 'ApiReadiness':
      return `.DET inspected API startup code, controllers, middleware, OpenAPI, health checks, validation, and exception-handling patterns${location}.`
    default:
      return `.DET matched this code against production-readiness rules${location}.`
  }
}

function getDetectionMethod(issue: AnalysisIssue) {
  if (issue.detectionMethod) {
    return issue.detectionMethod
  }

  if (issue.category === 'Architecture' || issue.ruleId === 'EF001') {
    return 'MSBuild / Project Configuration'
  }

  if (issue.category === 'Security' && ['SECJWT', 'SECSECRET', 'SECCONN'].includes(issue.ruleId ?? '')) {
    return 'Heuristic Analysis'
  }

  return issue.lineNumber ? 'Roslyn Syntax Analysis' : 'Heuristic Analysis'
}

function loadFindingDispositions() {
  try {
    const stored = localStorage.getItem(findingDispositionsStorageKey)
    if (!stored) {
      return {}
    }

    const parsed = JSON.parse(stored) as Record<string, FindingDisposition>
    return Object.fromEntries(
      Object.entries(parsed).filter(([, value]) => ['Ignore', 'False Positive', 'Accepted Risk'].includes(value)),
    ) as Record<string, FindingDisposition>
  } catch {
    return {}
  }
}

function getFindingDisposition(solutionName: string, issue: AnalysisIssue, dispositions: Record<string, FindingDisposition>) {
  if (issue.suppression && !issue.suppression.isExpired) {
    return issue.suppression.status
  }

  return dispositions[getFindingDispositionKey(solutionName, issue)] ?? 'Open'
}

function applyIssueSuppression(result: AnalysisResult, issueId: string, suppression: RepositorySuppression): AnalysisResult {
  const alreadySuppressed = result.issues.some((issue) => issue.id === issueId && issue.suppression)

  return {
    ...result,
    issues: result.issues.map((issue) =>
      issue.id === issueId
        ? {
            ...issue,
            suppression: {
              createdDate: suppression.createdDate,
              expiration: suppression.expiration,
              file: suppression.file,
              id: suppression.id,
              isExpired: false,
              project: suppression.project,
              reason: suppression.reason,
              ruleId: suppression.ruleId,
              status: suppression.status,
            },
          }
        : issue,
    ),
    suppressionCount: (result.suppressionCount ?? 0) + (alreadySuppressed ? 0 : 1),
  }
}

function removeIssueSuppression(result: AnalysisResult, issueId: string): AnalysisResult {
  return {
    ...result,
    issues: result.issues.map((issue) => {
      if (issue.id !== issueId) {
        return issue
      }

      return {
        ...issue,
        suppression: undefined,
      }
    }),
    suppressionCount: Math.max(0, (result.suppressionCount ?? 0) - 1),
  }
}

function getFindingDispositionKey(solutionName: string, issue: AnalysisIssue) {
  return [
    solutionName,
    getRuleId(issue),
    issue.projectName ?? 'Solution',
    issue.filePath ? formatPath(issue.filePath) : '',
    issue.lineNumber ?? '',
    issue.title,
  ].join('|')
}

function getRelatedFindings(issue: AnalysisIssue | null, issues: AnalysisIssue[]) {
  if (!issue) {
    return []
  }

  if (issue.relatedFindingIds?.length) {
    const relatedById = new Map(issues.map((candidate) => [candidate.id, candidate]))
    return issue.relatedFindingIds
      .map((relatedId) => relatedById.get(relatedId))
      .filter((candidate): candidate is AnalysisIssue => candidate !== undefined)
      .slice(0, 4)
  }

  return issues
    .filter((candidate) => candidate.id !== issue.id)
    .filter(
      (candidate) =>
        candidate.category === issue.category ||
        (candidate.projectName && candidate.projectName === issue.projectName) ||
        (candidate.filePath && candidate.filePath === issue.filePath),
    )
    .sort((left, right) => severityRank[right.severity] - severityRank[left.severity])
    .slice(0, 4)
}

function getDocumentationLinks(issue: AnalysisIssue) {
  if (issue.documentationLinks?.length) {
    return issue.documentationLinks
  }

  switch (issue.category) {
    case 'Architecture':
      return [{ label: 'Microsoft architecture fundamentals', href: 'https://learn.microsoft.com/dotnet/architecture/' }]
    case 'DependencyInjection':
      return [{ label: 'ASP.NET Core dependency injection', href: 'https://learn.microsoft.com/aspnet/core/fundamentals/dependency-injection' }]
    case 'EfCore':
      return [{ label: 'EF Core migrations', href: 'https://learn.microsoft.com/ef/core/managing-schemas/migrations/' }]
    case 'Security':
      return [{ label: 'ASP.NET Core security overview', href: 'https://learn.microsoft.com/aspnet/core/security/' }]
    case 'ApiReadiness':
      return [{ label: 'ASP.NET Core web API guidance', href: 'https://learn.microsoft.com/aspnet/core/web-api/' }]
    default:
      return [{ label: 'Microsoft .NET documentation', href: 'https://learn.microsoft.com/dotnet/' }]
  }
}

function buildFallbackArchitectureMap(graph: ProjectGraph, issues: AnalysisIssue[]): ArchitectureMap {
  const projects = graph.projects.map((project) => {
    const projectIssues = issues.filter((issue) => issue.projectName === project.name)
    const layer = project.logicalLayer ?? inferProjectLayer(project.name, project.isTestProject)

    return {
      criticalOrErrorCount: projectIssues.filter((issue) => issue.severity === 'Critical' || issue.severity === 'Error').length,
      filePath: project.filePath,
      issueCount: projectIssues.length,
      layer,
      name: project.name,
      namespaceRoot: project.name.split('.').slice(0, 2).join('.') || project.name,
    }
  })
  const dependencies = graph.dependencies.map((dependency) => {
    const sourceProject = graph.projects.find((project) => project.name === dependency.sourceProjectName)
    const targetProject = graph.projects.find((project) => project.name === dependency.targetProjectName)
    const sourceLayer = sourceProject?.logicalLayer ?? inferProjectLayer(dependency.sourceProjectName, sourceProject?.isTestProject)
    const targetLayer = targetProject?.logicalLayer ?? inferProjectLayer(dependency.targetProjectName, targetProject?.isTestProject)
    const isViolation = isInvalidDependency(dependency)
    const relatedFinding = isViolation ? getDependencyRelatedFinding(dependency, issues) : null

    return {
      direction: getArchitectureDirection(sourceLayer, targetLayer),
      isViolation,
      reason: isViolation ? getDependencyRule(dependency) : 'Allowed project reference.',
      relatedFindingId: relatedFinding?.id,
      ruleId: isViolation ? 'ARCHMAP000' : undefined,
      sourceLayer,
      sourceProjectName: dependency.sourceProjectName,
      targetLayer,
      targetProjectName: dependency.targetProjectName,
    }
  })
  const layers = [...new Set(projects.map((project) => project.layer))]
    .map((name) => ({
      name,
      order: getArchitectureLayerOrder(name),
      projectNames: projects.filter((project) => project.layer === name).map((project) => project.name),
    }))
    .sort((left, right) => right.order - left.order || left.name.localeCompare(right.name))

  return {
    dependencies,
    layers,
    projects,
    violations: dependencies
      .filter((dependency) => dependency.isViolation)
      .map((dependency, index) => ({
        description: dependency.reason ?? getDependencyRule(dependency),
        id: `ARCHMAP-FALLBACK-${index + 1}`,
        relatedFindingId: dependency.relatedFindingId,
        ruleId: dependency.ruleId ?? 'ARCHMAP000',
        sourceProjectName: dependency.sourceProjectName,
        targetProjectName: dependency.targetProjectName,
        title: 'Architecture boundary risk',
      })),
  }
}

function getArchitectureDependencyKey(dependency?: Pick<ArchitectureMapDependency, 'sourceProjectName' | 'targetProjectName'>) {
  return dependency ? `${dependency.sourceProjectName}->${dependency.targetProjectName}` : ''
}

function getArchitectureDirection(sourceLayer: string, targetLayer: string) {
  const sourceOrder = getArchitectureLayerOrder(sourceLayer)
  const targetOrder = getArchitectureLayerOrder(targetLayer)

  if (sourceOrder === targetOrder) return 'Lateral'
  return sourceOrder > targetOrder ? 'Inward' : 'Outward'
}

function getArchitectureLayerOrder(layer: string) {
  return {
    Presentation: 4,
    Infrastructure: 3,
    Application: 2,
    Domain: 1,
    Shared: 0,
    Test: 0,
    Unknown: 0,
  }[layer] ?? 0
}

function inferProjectLayer(projectName: string, isTestProject = false) {
  const name = projectName.toLowerCase()
  if (isTestProject || name.includes('tests') || name.includes('unittests') || name.includes('integrationtests') || name.includes('functionaltests')) return 'Test'
  if (name.includes('publicapi') || name.includes('blazoradmin') || name.includes('api') || name.includes('web') || name.includes('presentation')) return 'Presentation'
  if (name.includes('applicationcore') || name.includes('domain') || name.endsWith('core')) return 'Domain'
  if (name.includes('application')) return 'Application'
  if (name.includes('infrastructure') || name.includes('persistence') || name.includes('data')) return 'Infrastructure'
  if (name.includes('shared') || name.includes('common') || name.includes('contracts')) return 'Shared'
  return 'Unknown'
}

function getProjectRelatedFindings(projectName: string, issues: AnalysisIssue[]) {
  return issues
    .filter((issue) => issue.projectName === projectName)
    .sort((left, right) => severityRank[right.severity] - severityRank[left.severity])
    .slice(0, 5)
}

function isInvalidDependency(dependency: ProjectDependency) {
  const source = dependency.sourceProjectName.toLowerCase()
  const target = dependency.targetProjectName.toLowerCase()
  if (source.includes('tests') || target.includes('tests') || source.includes('unittests') || target.includes('unittests') || source.includes('integrationtests') || target.includes('integrationtests') || source.includes('functionaltests') || target.includes('functionaltests')) {
    return false
  }

  const sourceIsLowerLayer = source.includes('domain') || source.includes('application') || source.includes('infrastructure')
  const targetIsDeliveryLayer = target.includes('api') || target.includes('web')

  return (
    ((source.includes('domain') || source.includes('applicationcore') || source.endsWith('core')) && (target.includes('infrastructure') || targetIsDeliveryLayer)) ||
    (source.includes('application') && target.includes('infrastructure')) ||
    (sourceIsLowerLayer && targetIsDeliveryLayer)
  )
}

function getDependencyRule(dependency: ProjectDependency) {
  const source = dependency.sourceProjectName.toLowerCase()
  const target = dependency.targetProjectName.toLowerCase()

  if (source.includes('domain')) return 'Domain projects should not reference infrastructure, EF Core, API, or web projects.'
  if (source.includes('application') && target.includes('infrastructure')) return 'Application projects should depend on abstractions, not Infrastructure directly.'
  if (target.includes('api') || target.includes('web')) return 'Lower layers should not reference delivery-layer projects.'
  return 'Project reference should follow inward dependency direction.'
}

function getDependencyRelatedFinding(dependency: ProjectDependency, issues: AnalysisIssue[]) {
  const source = dependency.sourceProjectName.toLowerCase()
  const target = dependency.targetProjectName.toLowerCase()
  const expectedRuleId = getFallbackDependencyRuleId(dependency)

  return issues.find((issue) => {
    const text = `${issue.title} ${issue.description} ${issue.projectName ?? ''}`.toLowerCase()
    return issue.category === 'Architecture'
      && (!expectedRuleId || getRuleId(issue) === expectedRuleId)
      && text.includes(source)
      && text.includes(target)
  })
}

function getFallbackDependencyRuleId(dependency: ProjectDependency) {
  const source = dependency.sourceProjectName.toLowerCase()
  const target = dependency.targetProjectName.toLowerCase()

  if ((source.includes('domain') || source.includes('applicationcore') || source.endsWith('core')) && (target.includes('infrastructure') || target.includes('api') || target.includes('web'))) return 'ARCH001'
  if (source.includes('application') && target.includes('infrastructure')) return 'ARCH003'
  if ((source.includes('domain') || source.includes('application') || source.includes('infrastructure')) && (target.includes('api') || target.includes('web'))) return 'ARCH004'
  return null
}

function getSuggestedSnippet(issue: AnalysisIssue) {
  if (issue.suggestedSnippet) {
    return issue.suggestedSnippet
  }

  const title = issue.title.toLowerCase()

  if (title.includes('cors')) {
    return `builder.Services.AddCors(options =>
{
    options.AddPolicy("ProductionCors", policy =>
        policy.WithOrigins("https://app.example.com")
              .AllowAnyHeader()
              .AllowAnyMethod());
});`
  }

  if (title.includes('https')) {
    return `app.UseHttpsRedirection();`
  }

  if (title.includes('authentication')) {
    return `app.UseAuthentication();
app.UseAuthorization();`
  }

  if (title.includes('health')) {
    return `builder.Services.AddHealthChecks();
app.MapHealthChecks("/health");`
  }

  if (title.includes('exception')) {
    return `builder.Services.AddProblemDetails();
app.UseExceptionHandler();`
  }

  if (issue.category === 'DependencyInjection') {
    return `builder.Services.AddScoped<IServiceContract, ServiceImplementation>();`
  }

  if (issue.category === 'EfCore') {
    return `// Review migration impact before deployment.
// Prefer expand/contract changes and backup plans for destructive operations.`
  }

  if (issue.category === 'Architecture') {
    return `// Move abstractions to the inner layer.
// Implement infrastructure concerns at the composition root.`
  }

  return `// Apply the recommendation and rerun .DET to verify the finding is resolved.`
}

function formatPath(path: string) {
  const parts = path.replace(/\\/g, '/').split('/').filter(Boolean)
  if (parts.length === 0) return path
  if (parts.length === 1) return parts[0]
  return `${parts[parts.length - 2]}/${parts[parts.length - 1]}`
}

function getConciseRecommendation(recommendation: string) {
  const firstSentence = recommendation.split(/[.!?]/)[0]?.trim()
  if (!firstSentence) return recommendation
  if (firstSentence.length <= 80) return firstSentence
  return `${firstSentence.slice(0, 77)}...`
}

export default App
