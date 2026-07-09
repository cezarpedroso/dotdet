import {
  AlertTriangle,
  Blocks,
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
  FileText,
  Filter,
  FolderGit2,
  Info,
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
import { type ChangeEvent, type FormEvent, useEffect, useMemo, useState } from 'react'

type Severity = 'Info' | 'Warning' | 'Error' | 'Critical'
type IssueConfidence = 'High' | 'Medium' | 'Low'
type Category = 'Architecture' | 'DependencyInjection' | 'EfCore' | 'Security' | 'ApiReadiness'
type CategoryFilter = 'All' | Category
type SeverityFilter = 'All' | Severity
type FindingDisposition = 'Open' | 'Ignore' | 'False Positive' | 'Accepted Risk'
type ProjectFilter = 'All' | string
type SortMode = 'Severity' | 'Category' | 'Project' | 'File'
type AnalysisMode = 'path' | 'zip'
type AppPage = 'Overview' | 'Findings' | 'Code Explorer' | 'Architecture' | 'Rule Explorer' | 'Settings'
type StartPage = 'Home' | 'Dashboard' | 'Analyze' | 'Settings'
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
  solutionName: string
  analyzedAt: string
  overallScore: number
  categoryScores: CategoryScores
  issues: AnalysisIssue[]
  projectGraph: ProjectGraph
  sourceFiles?: AnalysisSourceFile[]
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
  analysisSolutionPath: string | null
  query: string
  result: AnalysisResult | null
  selectedFileId: string | null
  selectedIssueId: string | null
}

type AuthUser = {
  githubUserId: string
  githubUsername: string
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

const API_BASE_URL = import.meta.env.VITE_DOTDET_API_URL ?? import.meta.env.VITE_FORGE_API_URL ?? 'http://127.0.0.1:5241'

const explorerWidthStorageKey = 'det.codeExplorer.explorerWidth'
const guideWidthStorageKey = 'det.codeExplorer.guideWidth'
const lastAnalysisResultStorageKey = 'det.analysis.lastResult.v1'
const lastAnalysisSolutionPathStorageKey = 'det.analysis.lastSolutionPath'
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
  const [mode, setMode] = useState<AnalysisMode>('zip')
  const [solutionPath] = useState('')
  const [zipFile, setZipFile] = useState<File | null>(null)
  const [result, setResult] = useState<AnalysisResult | null>(storedWorkbenchState.result)
  const [analysisSolutionPath, setAnalysisSolutionPath] = useState<string | null>(storedWorkbenchState.analysisSolutionPath)
  const [ruleCatalog, setRuleCatalog] = useState<RuleDocumentation[]>([])
  const [ruleCatalogError, setRuleCatalogError] = useState<string | null>(null)
  const [startPage, setStartPage] = useState<StartPage>(() => getStartPageFromPath(Boolean(storedWorkbenchState.result)))
  const [authUser, setAuthUser] = useState<AuthUser | null>(null)
  const [authLoading, setAuthLoading] = useState(true)
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

  const severityCounts = useMemo(() => getSeverityCounts(result?.issues ?? []), [result])
  const projectIssueCounts = useMemo(() => getProjectIssueCounts(result?.issues ?? []), [result])
  const codeFiles = useMemo(() => (result ? buildCodeFiles(result) : []), [result])

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

        setAuthUser(auth.isAuthenticated ? auth.user : null)
        if (auth.isAuthenticated && window.location.pathname === '/') {
          navigateToPath('/dashboard', true)
          setStartPage('Dashboard')
        } else if (!auth.isAuthenticated && window.location.pathname.toLowerCase().startsWith('/dashboard')) {
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
  }, [])

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
      safeSetLocalStorage(lastAnalysisResultStorageKey, JSON.stringify(result))
    } else {
      localStorage.removeItem(lastAnalysisResultStorageKey)
    }
  }, [result])

  useEffect(() => {
    if (analysisSolutionPath) {
      localStorage.setItem(lastAnalysisSolutionPathStorageKey, analysisSolutionPath)
    } else {
      localStorage.removeItem(lastAnalysisSolutionPathStorageKey)
    }
  }, [analysisSolutionPath])

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

  const canAnalyze = mode === 'path' ? solutionPath.trim().length > 0 : zipFile !== null

  async function analyze(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    await runAnalysis()
  }

  async function analyzeSample() {
    setZipFile(null)
    await runAnalysis('sample', '', null)
  }

  async function runAnalysis(requestMode: AnalysisMode | 'sample' = mode, requestPath = solutionPath, requestFile = zipFile) {
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
      setAnalysisSolutionPath(analysis.solutionPath ?? (requestMode === 'path' ? requestPath.trim() : null))
      setStartPage('Analyze')
      setActivePage('Code Explorer')
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
    await fetch(`${API_BASE_URL}/api/auth/logout`, {
      credentials: 'include',
      method: 'POST',
    })
    setAuthUser(null)
    setResult(null)
    setSelectedIssueId(null)
    setSelectedFileId(null)
    setStartPage('Home')
    navigateToPath('/')
  }

  function setStartPageAndPath(page: StartPage) {
    setStartPage(page)
    navigateToPath(getPathForStartPage(page))
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

    const key = getFindingDispositionKey(result.solutionName, issue)
    setFindingDispositions((current) => {
      const next = { ...current }
      if (disposition === 'Open') {
        delete next[key]
      } else {
        next[key] = disposition
      }
      return next
    })

    if (!analysisSolutionPath) {
      return
    }

    try {
      if (disposition === 'Open') {
        if (!issue.suppression) {
          return
        }

        const response = await fetch(
          `${API_BASE_URL}/api/suppressions/${encodeURIComponent(issue.suppression.id)}?solutionPath=${encodeURIComponent(analysisSolutionPath)}`,
          { method: 'DELETE' },
        )

        if (!response.ok && response.status !== 404) {
          throw new Error(`Suppression removal failed with HTTP ${response.status}`)
        }

        setResult((current) => current ? removeIssueSuppression(current, issue.id) : current)
        return
      }

      const response = await fetch(`${API_BASE_URL}/api/suppressions`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          file: issue.filePath,
          project: issue.projectName,
          reason: `${disposition} set from the DotDet workbench.`,
          ruleId: getRuleId(issue),
          solutionPath: analysisSolutionPath,
          status: disposition,
        }),
      })

      if (!response.ok) {
        throw new Error(`Suppression creation failed with HTTP ${response.status}`)
      }

      const suppression = (await response.json()) as RepositorySuppression
      setResult((current) => current ? applyIssueSuppression(current, issue.id, suppression) : current)
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
        ) : !authUser ? (
          <HomePage
            onLogin={loginWithGitHub}
            onAnalyzeSample={analyzeSample}
          />
        ) : startPage === 'Dashboard' || startPage === 'Analyze' ? (
          <DashboardPage
            authUser={authUser}
            canAnalyze={canAnalyze}
            error={error}
            isLoading={isLoading}
            mode={mode}
            onAnalyzeSample={analyzeSample}
            onFileChange={onFileChange}
            onModeChange={setMode}
            onSubmit={analyze}
            zipFile={zipFile}
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
              result={result}
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
        { page: 'Dashboard', icon: FolderSearch, label: 'Analyze Solution' },
      ]
    : [
        { page: 'Home', icon: LayoutDashboard, label: 'Home' },
      ]
  const issueCount = result?.issues.length ?? 0
  const statusText = isLoading ? 'Analyzing' : result ? 'Analysis ready' : 'Ready'
  const displayName = authUser?.displayName || authUser?.githubUsername
  const [isExportMenuOpen, setExportMenuOpen] = useState(false)

  if (!result && !authUser && startPage === 'Home') {
    return (
      <main className={`night-mode det-public-shell min-h-screen overflow-auto text-slate-100 ${density === 'Comfortable' ? 'density-comfortable' : 'density-compact'}`}>
        <header className="det-public-topbar">
          <div className="det-public-topbar-inner">
            <div className="flex min-w-0 items-center gap-3">
              <img src="/dotdet-logo.png" alt=".DET logo" className="h-8 w-8 object-contain" />
              <div className="min-w-0">
                <h1 className="text-sm font-semibold leading-5 text-slate-100">.DET</h1>
                <div className="truncate text-[11px] text-slate-500">.NET Development Engineering Toolkit</div>
              </div>
            </div>
            <nav className="hidden items-center gap-6 text-xs font-medium text-slate-400 md:flex" aria-label="Landing navigation">
              <a href="#analysis" className="transition hover:text-slate-100">Analysis</a>
              <a href="#evidence" className="transition hover:text-slate-100">Evidence</a>
              <a href="#architecture" className="transition hover:text-slate-100">Architecture</a>
            </nav>
            {authLoading ? (
              <span className="text-xs text-slate-500">Checking session</span>
            ) : (
              <button type="button" onClick={onLogin} className="det-public-login-button">
                Login with GitHub
              </button>
            )}
          </div>
        </header>
        <div className="det-public-content">
          {children}
        </div>
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
              <div className="text-sm font-semibold leading-5 text-slate-100">.DET Toolkit</div>
              <div className="truncate text-[11px] text-slate-500">Preview build</div>
            </div>
          </div>

          <nav className="det-sidebar-nav" aria-label="Primary navigation">
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
                  className={`det-sidebar-nav-item ${startPage === item.page || (authUser && item.page === 'Dashboard' && startPage === 'Analyze') ? 'det-sidebar-nav-item-active' : ''}`}
                >
                  <item.icon className="h-4 w-4" aria-hidden="true" />
                  <span>{item.label}</span>
                </button>
              ))
            )}
          </nav>

          <div className="det-sidebar-utility">
            {result ? (
              <>
                <button type="button" aria-label="New Analysis" title="New Analysis" onClick={onRunAnalysisAgain} className="det-sidebar-nav-item">
                  <RefreshCw className="h-4 w-4" aria-hidden="true" />
                  <span>New Analysis</span>
                </button>
              </>
            ) : null}
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
              <div className="flex min-w-0 items-center gap-3">
              <img
                src="/dotdet-logo.png"
                alt=".DET logo"
                className={`${commandBarMode === 'Expanded' ? 'h-7 w-7' : 'h-6 w-6'} border border-black/20 bg-transparent object-contain`}
              />
              <div className="min-w-0">
                <h1 className={`${commandBarMode === 'Expanded' ? 'text-base leading-6' : 'text-sm leading-5'} font-semibold text-slate-100`}>.DET</h1>
                <p className="truncate text-xs text-slate-500">
                  {result ? `${result.solutionName} - analyzed ${new Date(result.analyzedAt).toLocaleString()}` : '.NET Development Engineering Toolkit'}
                </p>
              </div>
            </div>
            <div className="flex flex-wrap items-center gap-1.5">
              {authLoading ? (
                <span className="px-2 text-xs text-slate-500">Checking session</span>
              ) : authUser ? (
                <div className="flex items-center gap-2 border border-slate-700 bg-[#1b1f1d] px-2 py-1">
                  {authUser.avatarUrl ? (
                    <img src={authUser.avatarUrl} alt="" className="h-5 w-5" />
                  ) : (
                    <Circle className="h-4 w-4 text-slate-500" aria-hidden="true" />
                  )}
                  <span className="max-w-40 truncate text-xs font-medium text-slate-200">{displayName}</span>
                </div>
              ) : (
                <button type="button" onClick={onLogin} className={commandButtonClass}>
                  Login with GitHub
                </button>
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

function HomePage({
  onAnalyzeSample,
  onLogin,
}: {
  onAnalyzeSample: () => void
  onLogin: () => void
}) {
  return (
    <section id="analysis" className="det-public-landing flex flex-1 items-center justify-center overflow-auto p-6">
      <div className="w-full max-w-6xl">
        <div className="grid gap-8 lg:grid-cols-[minmax(0,1fr)_360px] lg:items-center">
          <div>
            <div className="text-xs font-semibold uppercase tracking-wide text-[#2596be]">.NET Development Engineering Toolkit</div>
            <h2 className="mt-4 max-w-4xl text-4xl font-semibold tracking-tight text-slate-950">
              Production-readiness analysis for serious ASP.NET Core teams.
            </h2>
            <p className="mt-4 max-w-2xl text-sm leading-6 text-slate-500">
              DotDet inspects architecture, dependency injection, EF Core, security configuration, and API readiness, then maps findings back to source code and Microsoft guidance.
            </p>
            <div className="mt-7 flex flex-wrap gap-2">
              <button type="button" onClick={onLogin} className="det-primary-cta">
                Login with GitHub
                <ChevronRight className="h-4 w-4" aria-hidden="true" />
              </button>
              <button type="button" onClick={onAnalyzeSample} className="det-secondary-cta">
                Analyze Sample
              </button>
            </div>
            <div id="evidence" className="mt-8 grid gap-3 sm:grid-cols-3">
              {[
                ['Architecture', 'Layer boundaries and project dependencies'],
                ['Security', 'Configuration, middleware, and auth posture'],
                ['Code evidence', 'Findings linked to files, lines, and guidance'],
              ].map(([title, body]) => (
                <div key={title} className="det-landing-proof">
                  <div className="text-xs font-semibold uppercase tracking-wide text-slate-400">{title}</div>
                  <div className="mt-2 text-sm leading-5 text-slate-300">{body}</div>
                </div>
              ))}
            </div>
          </div>
          <div id="architecture" className="det-landing-panel">
            <div className="border-b border-slate-700 px-4 py-3">
              <div className="text-xs font-semibold uppercase tracking-wide text-slate-400">Workbench Preview</div>
              <div className="mt-1 text-sm font-semibold text-slate-100">What DotDet Checks</div>
            </div>
            <ul className="divide-y divide-slate-800">
              {categoryDefinitions.map((category) => (
                <li key={category.key} className="flex items-center gap-3 px-4 py-3 text-sm text-slate-300">
                  <category.icon className={`h-4 w-4 ${categoryTextClass(category.key)}`} aria-hidden="true" />
                  <span>{category.reportLabel}</span>
                </li>
              ))}
            </ul>
          </div>
        </div>
      </div>
    </section>
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
  canAnalyze,
  error,
  isLoading,
  mode,
  onAnalyzeSample,
  onFileChange,
  onModeChange,
  onSubmit,
  zipFile,
}: {
  authUser: AuthUser | null
  canAnalyze: boolean
  error: string | null
  isLoading: boolean
  mode: AnalysisMode
  onAnalyzeSample: () => void
  onFileChange: (event: ChangeEvent<HTMLInputElement>) => void
  onModeChange: (mode: AnalysisMode) => void
  onSubmit: (event: FormEvent<HTMLFormElement>) => void
  zipFile: File | null
}) {
  const displayName = authUser?.displayName || authUser?.githubUsername || 'Developer'

  return (
    <section className="det-dashboard-page flex-1 overflow-auto p-5">
      <div className="det-auth-workspace mx-auto">
        <header className="det-overview-title">
          <div className="text-xs font-semibold uppercase tracking-wide text-[#2596be]">Solution Analysis</div>
          <h1 className="mt-2 text-2xl font-semibold text-slate-950">Welcome, {displayName}</h1>
          <p className="mt-2 max-w-2xl text-sm text-slate-500">
            Upload a zipped .NET solution to generate a production-readiness report.
          </p>
        </header>

        <form onSubmit={onSubmit} className="det-auth-upload-panel">
          <div className="grid gap-4 xl:grid-cols-[minmax(0,1fr)_260px]">
            <div>
              <div className="mb-3 flex items-center gap-2">
                <UploadCloud className="h-4 w-4 text-[#2596be]" aria-hidden="true" />
                <h2 className="text-base font-semibold text-slate-950">Upload Solution ZIP</h2>
              </div>
              <div className="mb-3 border border-slate-200 bg-white p-2 text-xs leading-5 text-slate-600">
                Browser-based analysis uses ZIP upload. Include the `.sln` or `.slnx` file and related projects in the archive.
              </div>
              <label className="det-upload-dropzone">
                <FileArchive className="mb-2 h-7 w-7 text-[#2596be]" aria-hidden="true" />
                <span className="text-sm font-semibold text-slate-900">{zipFile?.name ?? 'Choose .zip solution archive'}</span>
                <span className="mt-1 text-xs text-slate-500">DotDet extracts the archive temporarily and returns the report directly.</span>
                <input type="file" accept=".zip" onChange={onFileChange} className="sr-only" />
              </label>

              {isLoading ? <AnalysisProgress /> : null}

              {error ? (
                <div className="mt-3 flex items-start gap-2 border border-rose-200 bg-rose-50 p-2.5 text-sm text-rose-800">
                  <XCircle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
                  <p className="break-words">{error}</p>
                </div>
              ) : null}
            </div>

            <aside className="det-auth-side-panel">
              <button type="button" onClick={() => onModeChange('zip')} className={analysisActionClass(mode === 'zip')}>
                <UploadCloud className="h-5 w-5 text-[#2596be]" aria-hidden="true" />
                <span className="font-semibold text-slate-950">ZIP upload</span>
                <span className="text-xs leading-5 text-slate-500">Analyze an exported solution archive.</span>
              </button>
              <button type="button" onClick={onAnalyzeSample} disabled={isLoading} className={analysisActionClass(false)}>
                <FileCode2 className="h-5 w-5 text-[#2596be]" aria-hidden="true" />
                <span className="font-semibold text-slate-950">Analyze sample</span>
                <span className="text-xs leading-5 text-slate-500">Open the included DotDet sample.</span>
              </button>
            </aside>
          </div>

          <div className="mt-4 flex justify-end border-t border-slate-200 pt-4">
            <button type="submit" disabled={!canAnalyze || isLoading} className="det-run-analysis-button">
              <Play className="h-4 w-4" aria-hidden="true" />
              Run Analysis
            </button>
          </div>
        </form>
      </div>
    </section>
  )
}

function analysisActionClass(active: boolean) {
  return `flex min-h-28 flex-col items-start gap-1.5 border p-3 text-left transition hover:border-teal-500 hover:bg-slate-50 ${
    active ? 'border-teal-600 bg-slate-50' : 'border-slate-200 bg-white'
  }`
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
            <div key={step} className="flex items-center gap-2 text-sm text-slate-300">
              {status === 'completed' ? (
                <CheckCircle2 className="h-3.5 w-3.5 shrink-0 text-emerald-500" aria-hidden="true" />
              ) : status === 'current' ? (
                <Circle className="det-progress-current h-3.5 w-3.5 shrink-0 fill-cyan-500 text-cyan-500" aria-hidden="true" />
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
    <div className="det-dashboard-page flex-1 overflow-auto px-5 py-7 2xl:px-6">
      <div className="det-overview-report mx-auto">
        <header className="det-overview-title">
          <div className="text-xs font-semibold uppercase tracking-wide text-teal-700">Overview</div>
          <h1 className="mt-2 text-2xl font-semibold text-slate-950">Engineering readiness report</h1>
          <p className="mt-2 text-sm text-slate-500">{result.solutionName}</p>
        </header>

        <OverviewSummary projectCount={projectCount} result={result} severityCounts={severityCounts} />
        <AssessmentSummarySection result={result} severityCounts={severityCounts} />
        <BiggestRisksSection onOpenFindings={onOpenFindings} result={result} />
        <EngineeringAssessmentPanel assessment={result.engineeringAssessment} />
        <RecommendedActionsSection issues={quickRecommendations} onOpenIssue={onOpenIssue} />
        <ArchitectureOverviewSection result={result} onOpenArchitecture={onOpenArchitecture} />
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

  return (
    <section className="det-overview-section det-overview-summary">
      <div className="det-report-section-heading">
        <h2 className="text-lg font-semibold text-slate-950">Production Readiness</h2>
        <p className="mt-2 text-sm text-slate-500">Executive status for release readiness and immediate production blockers.</p>
      </div>

      <div className="det-readiness-panel mt-5">
        <div className="det-readiness-primary">
          <div className="det-readiness-stat">
            <dt>Overall Grade</dt>
            <dd className="text-6xl">{grade}</dd>
          </div>
          <div className="det-readiness-stat">
            <dt>Score</dt>
            <dd>
              {result.overallScore} <span>/ 100</span>
            </dd>
          </div>
          <div className="det-readiness-stat">
            <dt>Status</dt>
            <dd className={statusTone}>{status}</dd>
          </div>
        </div>

        <div className="det-readiness-progress">
          <div className="h-2 overflow-hidden rounded bg-slate-200">
            <div className={getScoreBarClass(result.overallScore)} style={{ width: `${result.overallScore}%` }} />
          </div>
        </div>

        <dl className="det-readiness-counts">
          <CompactMetric label="Critical Findings" value={blockerCount} tone={blockerCount > 0 ? 'danger' : 'ok'} />
          <CompactMetric label="Warnings" value={severityCounts.Warning} tone={severityCounts.Warning > 0 ? 'warn' : 'ok'} />
          <CompactMetric label="Projects" value={projectCount} tone="neutral" />
        </dl>
      </div>
    </section>
  )
}

function AssessmentSummarySection({
  result,
  severityCounts,
}: {
  result: AnalysisResult
  severityCounts: Record<Severity, number>
}) {
  const concerns = getPrimaryConcerns(result)

  return (
    <section className="det-overview-section">
      <div className="det-report-section-heading">
        <h2 className="text-lg font-semibold text-slate-950">Assessment Summary</h2>
        <p className="mt-2 text-sm leading-6 text-slate-700">{getOverviewLead(severityCounts)}</p>
      </div>

      <div className="det-summary-layout mt-5">
        <div>
          <h3 className="text-xs font-semibold uppercase tracking-wide text-slate-500">Primary Concerns</h3>
          <ul className="mt-3 grid gap-2 text-sm text-slate-700 md:grid-cols-3">
            {concerns.map((concern) => (
              <li key={concern} className="flex gap-2">
                <span className="mt-2 h-1 w-1 shrink-0 bg-slate-500" aria-hidden="true" />
                <span>{concern}</span>
              </li>
            ))}
          </ul>
        </div>
        <div className="det-summary-callout">
          <h3 className="text-xs font-semibold uppercase tracking-wide text-slate-500">Recommendation</h3>
          <p className="mt-3 text-sm leading-6 text-slate-700">
            Address the findings list in priority order before release. Use the Engineering Guide for remediation details.
          </p>
        </div>
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
    <section className="det-overview-section">
      <div className="det-card-heading-row">
        <div>
          <h2 className="text-lg font-semibold text-slate-950">Biggest Risks</h2>
          <p className="mt-2 text-sm text-slate-500">Ranked by category score and critical/error concentration.</p>
        </div>
        <button type="button" onClick={onOpenFindings} className="det-card-link-button">
          Open Findings
        </button>
      </div>
      <div className="det-risk-grid mt-5">
        {riskAreas.map((area, index) => (
          <div key={area.category.key} className="det-risk-summary">
            <div className="flex items-start justify-between gap-3">
              <div className="flex items-center gap-2">
                <span className="text-xs font-semibold text-slate-500">{index + 1}</span>
                <area.category.icon className={`h-4 w-4 ${categoryTextClass(area.category.key)}`} aria-hidden="true" />
                <span className="font-semibold text-slate-950">{area.category.reportLabel}</span>
              </div>
              <span className="text-sm font-semibold text-slate-950">{area.score}/100</span>
            </div>
            <p className="mt-2 text-xs leading-5 text-slate-500">{area.criticalCount} critical/error - {area.issueCount} total findings</p>
          </div>
        ))}
      </div>
    </section>
  )
}

function EngineeringAssessmentPanel({ assessment }: { assessment?: EngineeringAssessmentSummary }) {
  if (!assessment) {
    return (
      <section className="det-overview-section">
        <h2 className="text-lg font-semibold text-slate-950">Engineering Assessment</h2>
        <p className="mt-1 text-xs text-slate-500">Run analysis again to generate the deterministic architecture assessment.</p>
      </section>
    )
  }

  const sections = [
    { title: 'Strong Areas', items: assessment.strongAreas },
    { title: 'Highest Risks', items: assessment.highestRisks },
    { title: 'Architectural Observations', items: assessment.architecturalObservations },
    { title: 'Security Observations', items: assessment.securityObservations },
    { title: 'API Readiness Observations', items: assessment.apiReadinessObservations },
    { title: 'Maintainability Observations', items: assessment.maintainabilityObservations },
    { title: 'Recommended Priorities', items: assessment.recommendedPriorities },
  ]

  return (
    <section className="det-overview-section">
      <div className="det-report-section-heading">
          <h2 className="text-lg font-semibold text-slate-950">Engineering Assessment</h2>
        <p className="mt-2 text-sm text-slate-500">Readiness observations from the analysis.</p>
      </div>
      <div className="det-assessment-block mt-4">
        <h3 className="text-xs font-semibold uppercase tracking-wide text-slate-500">Score Explanation</h3>
        <p className="mt-2 text-sm leading-5 text-slate-700">{assessment.scoreExplanation}</p>
      </div>
      <div className="det-assessment-grid mt-6 grid gap-x-10 gap-y-7 lg:grid-cols-2 2xl:grid-cols-3">
        {sections.map((section) => (
          <div key={section.title} className="det-assessment-block">
            <h3 className="text-xs font-semibold uppercase tracking-wide text-slate-500">{section.title}</h3>
            <ul className="mt-2 space-y-1.5 text-sm leading-5 text-slate-700">
              {section.items.length > 0 ? (
                section.items.map((item, index) => (
                  <li key={`${section.title}-${index}`}>{item}</li>
                ))
              ) : (
                <li className="text-slate-500">No observations for this section.</li>
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
  return (
    <section className="det-overview-section">
      <div className="det-report-section-heading">
        <h2 className="text-lg font-semibold text-slate-950">Recommended Next Actions</h2>
        <p className="mt-2 text-sm text-slate-500">Fix these first to reduce the highest production risk fastest.</p>
      </div>
      {issues.length > 0 ? (
        <div className="det-recommendation-grid mt-5">
          {issues.map((issue, index) => (
            <button
              key={issue.id}
              type="button"
              onClick={() => onOpenIssue(issue.id)}
              className="det-recommendation-item text-left transition hover:opacity-85"
            >
              <span className="text-sm font-semibold tabular-nums text-slate-500">{index + 1}</span>
              <div className="min-w-0">
                <h3 className="text-sm font-semibold leading-5 text-slate-950">{getRecommendedActionTitle(issue)}</h3>
                <div className="mt-1 flex flex-wrap items-center gap-1.5 text-xs">
                  <SeverityLabel severity={issue.severity} />
                  <span className="text-slate-500">-</span>
                  <CategoryText category={issue.category} />
                  <span className="text-slate-500">-</span>
                  <span className="truncate text-slate-500">{issue.filePath ? formatPath(issue.filePath) : issue.projectName ?? 'Solution'}</span>
                </div>
                <p className="mt-2 text-xs leading-5 text-slate-500">{getConciseRecommendation(issue.recommendation)}</p>
              </div>
            </button>
          ))}
        </div>
      ) : (
        <p className="mt-5 text-sm text-slate-500">No critical recommendations detected.</p>
      )}
    </section>
  )
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
    <section className="det-overview-section">
      <div className="det-card-heading-row">
        <div>
          <h2 className="text-lg font-semibold text-slate-950">Architecture</h2>
          <p className="mt-2 text-sm text-slate-500">Project graph summary and boundary risk count.</p>
        </div>
        <button type="button" onClick={onOpenArchitecture} className="det-card-link-button">
          Open Architecture
        </button>
      </div>
      <dl className="mt-5 grid gap-5 sm:grid-cols-3">
        <CompactMetric label="Projects" value={map.projects.length} tone="neutral" />
        <CompactMetric label="Dependencies" value={map.dependencies.length} tone="neutral" />
        <CompactMetric label="Boundary Risks" value={boundaryRisks} tone={boundaryRisks > 0 ? 'danger' : 'ok'} />
      </dl>
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
  const [showAllRules, setShowAllRules] = useState(false)
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
    const activeMatches = showAllRules || currentIssues.length === 0 || rule.activeCount > 0
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
        .filter((rule): rule is RuleDocumentation => Boolean(rule))
    : []

  return (
    <div className="grid min-h-0 flex-1 grid-cols-[280px_minmax(0,1fr)_320px] overflow-hidden">
      <aside className="min-h-0 border-r border-slate-300 bg-white">
        <div className="border-b border-slate-200 bg-slate-50 px-3 py-2">
          <h2 className="text-sm font-semibold text-slate-950">Rule Explorer</h2>
          <p className="mt-1 text-xs text-slate-500">{filteredRules.length || 'No'} rules shown.</p>
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
          <label className="mt-2 flex h-7 items-center gap-2 text-xs text-slate-600">
            <input
              type="checkbox"
              checked={showAllRules}
              onChange={(event) => setShowAllRules(event.target.checked)}
            />
            Show all rules
          </label>
        </div>
        {error ? <div className="m-2 p-2 text-xs text-red-700">{error}</div> : null}
        <div className="h-[calc(100%-118px)] overflow-auto">
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
          {filteredRules.length === 0 ? <p className="p-3 text-xs text-slate-500">No active rules match the current filters. Turn on Show all rules to browse the catalog.</p> : null}
        </div>
      </aside>

      <main className="min-h-0 overflow-auto bg-slate-50 p-4">
        {selectedRule ? (
          <article className="mx-auto max-w-5xl border border-slate-300 bg-white">
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

      <aside className="min-h-0 overflow-auto border-l border-slate-300 bg-white">
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
  const [expandedProjects, setExpandedProjects] = useState(() => new Set(projects.map((project) => project.name)))
  const [showAllFiles, setShowAllFiles] = useState(false)
  const filesByProject = groupFilesByProject(files)
  const issuesByFile = result.issues.reduce((groups, issue) => {
    const fileId = getFileId(issue.filePath)
    if (!fileId) return groups

    const group = groups.get(fileId) ?? []
    group.push(issue)
    groups.set(fileId, group)
    return groups
  }, new Map<string, AnalysisIssue[]>())
  const visibleProjects = showAllFiles
    ? projects
    : projects.filter((project) => (projectIssueCounts.get(project.name) ?? 0) > 0)

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
    <aside className="min-h-0 overflow-hidden bg-white text-sm">
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
      </div>

      <div className="h-[calc(100%-43px)] overflow-auto p-1.5">
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
          <p className="p-2 text-xs text-slate-500">No files with findings. Turn on Show all to browse the solution.</p>
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
}: {
  editorFontFamily: EditorFontFamily
  file: CodeFile | null
  issues: AnalysisIssue[]
  onSelectIssue: (issueId: string) => void
  selectedIssue: AnalysisIssue | null
  showGutterMarkers: boolean
  showMinimapMarkers: boolean
}) {
  if (!file) {
    return (
      <div className="flex flex-1 items-center justify-center bg-slate-950 text-sm text-slate-400">
        Select a file to inspect source findings.
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
          minimap: showMinimapMarkers ? { enabled: true, scale: 0.85, showSlider: 'mouseover' } : { enabled: false },
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

function CompactMetric({ label, tone, value }: { label: string; tone: 'danger' | 'warn' | 'ok' | 'neutral'; value: number }) {
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
    <aside className="min-h-0 overflow-hidden bg-white">
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
              className={`w-full px-2 py-2 text-left transition ${
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
  return {
    activeCategory: getStoredString(activeCategoryStorageKey, 'All', ['All', ...categoryDefinitions.map((category) => category.key)] as const),
    activePage: getStoredString(activePageStorageKey, 'Code Explorer', ['Overview', 'Findings', 'Code Explorer', 'Architecture', 'Rule Explorer', 'Settings'] as const),
    activeProject: localStorage.getItem(activeProjectStorageKey) || 'All',
    activeSeverity: getStoredString(activeSeverityStorageKey, 'All', ['All', 'Info', 'Warning', 'Error', 'Critical'] as const),
    analysisSolutionPath: localStorage.getItem(lastAnalysisSolutionPathStorageKey),
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
  if (path.startsWith('/dashboard')) return 'Dashboard'
  if (path.startsWith('/analyze')) return 'Analyze'
  if (path.startsWith('/settings')) return 'Settings'
  return 'Home'
}

function getPathForStartPage(page: StartPage) {
  return {
    Analyze: '/analyze',
    Dashboard: '/dashboard',
    Home: '/',
    Settings: '/settings',
  }[page]
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
    return parsed?.solutionName && Array.isArray(parsed.issues) && parsed.projectGraph ? parsed : null
  } catch {
    localStorage.removeItem(lastAnalysisResultStorageKey)
    return null
  }
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
    return `This solution needs review before production hardening. .DET found ${blockerCount} high-severity findings, with risk concentrated in ${getPrimaryConcerns(result).join(', ').toLowerCase()}.`
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
  return categoryDefinitions
    .map((category) => {
      const issues = result.issues.filter((issue) => issue.category === category.key)
      const criticalCount = issues.filter((issue) => issue.severity === 'Critical' || issue.severity === 'Error').length

      return {
        category,
        criticalCount,
        issueCount: issues.length,
        score: result.categoryScores[category.scoreKey],
      }
    })
    .sort((left, right) => left.score - right.score || right.criticalCount - left.criticalCount)
}

function getPrimaryConcerns(result: AnalysisResult) {
  return categoryDefinitions
    .map((category) => {
      const issues = result.issues.filter((issue) => issue.category === category.key)
      const criticalCount = issues.filter((issue) => issue.severity === 'Critical' || issue.severity === 'Error').length

      return {
        label: getConcernLabel(category.key),
        score: result.categoryScores[category.scoreKey],
        weight: criticalCount * 20 + issues.length * 3 + (100 - result.categoryScores[category.scoreKey]),
      }
    })
    .sort((left, right) => right.weight - left.weight || left.score - right.score)
    .slice(0, 3)
    .map((area) => area.label)
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
  if (score >= 85) return 'h-full bg-teal-600'
  if (score >= 70) return 'h-full bg-sky-600'
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
    const key = `${getRuleId(issue)}|${issue.category}`
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
        .filter((candidate) => `${getRuleId(candidate)}|${candidate.category}` === key)
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

  const sourcePreview = options.includeSourcePreview
    ? options.codeFiles.map((file) => ({
        content: file.content,
        name: file.name,
        path: file.path,
        projectName: file.projectName,
      }))
    : undefined
  const extension = options.format === 'Markdown' ? 'md' : options.format === 'HTML' ? 'html' : 'json'
  const content =
    options.format === 'Markdown'
      ? createMarkdownReport(result, options.dispositions)
      : options.format === 'HTML'
        ? createHtmlReport(result, options.dispositions)
        : JSON.stringify(sourcePreview ? { ...result, findingDispositions: options.dispositions, sourcePreview } : { ...result, findingDispositions: options.dispositions }, null, 2)
  const blob = new Blob([content], { type: options.format === 'Markdown' ? 'text/markdown' : options.format === 'HTML' ? 'text/html' : 'application/json' })
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = `${getReportFileName(result.solutionName)}.${extension}`
  anchor.click()
  URL.revokeObjectURL(url)
}

function getReportFileName(solutionName: string) {
  const slug = solutionName.replace(/[^a-z0-9.-]+/gi, '-').replace(/^-+|-+$/g, '').toLowerCase()
  return `${slug || 'dotdet'}-dotdet-report`
}

function createMarkdownReport(result: AnalysisResult, dispositions: Record<string, FindingDisposition>) {
  const counts = getSeverityCounts(result.issues)
  const openIssues = getOpenFindings(result, dispositions)
  const suppressedIssues = getSuppressedFindings(result, dispositions)
  const openCounts = getSeverityCounts(openIssues)
  const topRisks = getTopRiskIssues(openIssues).slice(0, 8)
  const roadmap = buildRecommendedRoadmap({ ...result, issues: openIssues }, openCounts)
  const lines = [
    '# DotDet Production Readiness Report',
    '',
    `**Solution:** ${result.solutionName}`,
    `**Analyzed:** ${new Date(result.analyzedAt).toLocaleString()}`,
    `**Production Readiness:** ${result.overallScore}/100 (${getGrade(result.overallScore)})`,
    `**Status:** ${getReadinessDecision(result.overallScore, counts)}`,
    '',
    '## Executive Summary',
    '',
    result.engineeringAssessment?.overallProductionReadiness ?? getOverviewNarrative(result, counts),
    '',
    `**Score explanation:** ${result.engineeringAssessment?.scoreExplanation ?? 'DotDet calculated the readiness score from weighted category scores and severity caps.'}`,
    '',
    '## Category Scores',
    '',
    ...categoryDefinitions.map((category) => `- **${category.reportLabel}:** ${result.categoryScores[category.scoreKey]}/100`),
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
    ...getAssessmentMarkdownLines(result.engineeringAssessment),
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

function createHtmlReport(
  result: AnalysisResult,
  dispositions: Record<string, FindingDisposition>,
) {
  const counts = getSeverityCounts(result.issues)
  const openIssues = getOpenFindings(result, dispositions)
  const suppressedIssues = getSuppressedFindings(result, dispositions)
  const openCounts = getSeverityCounts(openIssues)
  const status = getReadinessDecision(result.overallScore, counts)
  const grade = getGrade(result.overallScore)
  const concerns = getPrimaryConcerns(result)
  const topRisks = getTopRiskIssues(openIssues).slice(0, 8)
  const groupedFindings = categoryDefinitions.map((category) => ({
    category,
    issues: openIssues.filter((issue) => issue.category === category.key),
    score: result.categoryScores[category.scoreKey],
  }))
  const architectureMap = result.architectureMap ?? buildFallbackArchitectureMap(result.projectGraph, result.issues)
  const roadmap = buildRecommendedRoadmap({ ...result, issues: openIssues }, openCounts)
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
        <p class="cover-summary">${escapeHtml(result.engineeringAssessment?.overallProductionReadiness ?? getOverviewNarrative(result, counts))}</p>
        <div class="cover-grid">
          ${coverMetricHtml('Solution', result.solutionName)}
          ${coverMetricHtml('Score', `${result.overallScore}/100`)}
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
              <div class="score-number">${result.overallScore}<small>/100</small></div>
              <div>
                <div class="muted">Grade ${escapeHtml(grade)} · ${escapeHtml(status)}</div>
                <div class="bar" aria-hidden="true"><span style="width:${Math.max(0, Math.min(100, result.overallScore))}%"></span></div>
                <p>${escapeHtml(getOverviewNarrative(result, counts))}</p>
                <p><strong>Score explanation:</strong> ${escapeHtml(result.engineeringAssessment?.scoreExplanation ?? 'DotDet calculated the readiness score from weighted category scores and severity caps.')}</p>
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
          ${categoryDefinitions.map((category) => categoryScoreHtml(category.reportLabel, result.categoryScores[category.scoreKey], openIssues.filter((issue) => issue.category === category.key).length)).join('')}
        </div>
      </section>

      <section>
        <h2>Engineering Assessment Summary</h2>
        ${assessmentHtml(result.engineeringAssessment)}
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
          ${topRisks.map((issue) => riskItemHtml(issue, dispositions, result.solutionName)).join('') || '<div class="panel-soft">No high-priority risks detected.</div>'}
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
        <td><strong>${escapeHtml(getRuleId(issue))}</strong><br />${escapeHtml(issue.title)}<br /><span class="muted">${escapeHtml(issue.problemSummary ?? issue.description)}</span>${snippet ? `<pre><code>${escapeHtml(snippet)}</code></pre>` : ''}</td>
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
  return [...issues]
    .filter((issue) => issue.severity !== 'Info')
    .sort((left, right) => severityRank[right.severity] - severityRank[left.severity] || left.category.localeCompare(right.category))
}

function buildRecommendedRoadmap(result: AnalysisResult, counts: Record<Severity, number>) {
  const assessmentPriorities = result.engineeringAssessment?.recommendedPriorities ?? []
  const riskRecommendations = getTopRiskIssues(result.issues)
    .slice(0, 6)
    .map((issue) => `${getRuleId(issue)}: ${issue.recommendation}`)
  const baseline = [
    counts.Critical > 0
      ? 'Resolve confirmed critical blockers before production release approval.'
      : counts.Error > 0
        ? 'Review high-severity findings with the release owner and document accepted residual risk.'
      : 'Review remaining warnings and confirm release owners accept the residual risk.',
    'Re-run DotDet after remediation and attach the updated report to the pull request or release ticket.',
  ]

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

  if (issue.lineNumber && ['Architecture', 'DependencyInjection', 'EfCore'].includes(issue.category)) {
    return 'Roslyn Semantic Analysis'
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
