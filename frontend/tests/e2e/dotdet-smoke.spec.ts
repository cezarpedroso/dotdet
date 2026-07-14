import { expect, test, type Page } from '@playwright/test'
import fs from 'node:fs/promises'

async function analyzeSample(page: Page) {
  await page.goto('/')
  await expect(page.getByRole('heading', { name: 'Production readiness, grounded in your code.' })).toBeVisible()
  await page.getByRole('button', { name: 'Run sample analysis' }).click()
  await expect(page.getByRole('button', { name: 'Overview' })).toBeVisible({ timeout: 60_000 })
}

test.describe('DotDet critical smoke flows', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/')
    await page.evaluate(() => localStorage.clear())
  })

  test('public landing and authenticated ZIP dashboard', async ({ page }) => {
    await page.goto('/')
    await expect(page.getByRole('link', { name: 'DotDet home' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Production readiness, grounded in your code.' })).toBeVisible()
    await expect(page.getByRole('button', { name: 'Login with GitHub' }).first()).toBeVisible()

    await page.route('**/api/auth/me', async (route) => {
      await route.fulfill({
        contentType: 'application/json',
        body: JSON.stringify({
          isAuthenticated: true,
          user: {
            avatarUrl: null,
            createdDate: new Date().toISOString(),
            displayName: 'Ada Lovelace',
            email: 'ada@example.com',
            githubUserId: '123',
            githubUsername: 'ada',
            lastLoginDate: new Date().toISOString(),
          },
        }),
      })
    })
    await page.goto('/dashboard')
    await expect(page.getByRole('heading', { name: 'Welcome, Ada Lovelace' })).toBeVisible()
    await expect(page.getByRole('button', { name: /Upload ZIP/ })).toBeVisible()
    await expect(page.getByRole('button', { name: /Analyze Sample Project/ })).toBeVisible()
    await expect(page.getByRole('button', { name: 'Login with GitHub' })).toHaveCount(0)
    await expect(page.locator('#solution-path')).toHaveCount(0)
  })

  test('public Docs and Changelog provide connected release documentation', async ({ page }) => {
    await page.goto('/docs')
    await expect(page.getByRole('heading', { name: 'DotDet Documentation' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Security and privacy' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Engine maturity' })).toBeVisible()
    await expect(page.getByRole('link', { name: 'Project README' })).toHaveAttribute('href', /github\.com\/cezarpedroso\/dotdet\/blob\/main\/README\.md/)

    await page.locator('.det-docs-header-actions').getByRole('link', { name: 'View v0.1 changes' }).click()
    await expect(page).toHaveURL(/\/changelog$/)
    await expect(page.getByRole('heading', { name: 'Release notes' })).toBeVisible()
    await expect(page.getByText('v0.1 Preview', { exact: true })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Security and privacy hardening' })).toBeVisible()

    await page.locator('.det-changelog-footer').getByRole('link', { name: 'Open Docs' }).click()
    await expect(page).toHaveURL(/\/docs$/)
    await expect(page.getByRole('heading', { name: 'DotDet Documentation' })).toBeVisible()

    await page.setViewportSize({ width: 390, height: 844 })
    expect(await page.evaluate(() => document.body.scrollWidth > window.innerWidth + 2)).toBe(false)
    await expect(page.getByRole('button', { name: 'Login with GitHub' })).toBeVisible()
  })

  test('sample analysis, overview, export, and responsive layout', async ({ page }) => {
    await analyzeSample(page)

    await page.getByRole('button', { name: 'Overview' }).click()
    await expect(page.getByText('Engineering readiness report')).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Forge.SampleShop' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Production Readiness' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Architecture map' })).toBeVisible()
    await page.getByRole('button', { name: 'Explore map' }).click()
    await expect(page.getByRole('heading', { name: 'Project dependency map' })).toBeVisible()
    await page.reload()
    await expect(page.getByRole('heading', { name: 'Project dependency map' })).toBeVisible()
    await page.getByRole('button', { name: 'Overview' }).click()

    const htmlDownloadPromise = page.waitForEvent('download')
    await page.getByRole('button', { name: 'Export Report' }).click()
    await page.getByRole('menuitem', { name: 'HTML report' }).click()
    const htmlDownload = await htmlDownloadPromise
    const htmlPath = await htmlDownload.path()
    expect(htmlPath).toBeTruthy()
    const htmlReport = await fs.readFile(htmlPath!, 'utf8')
    expect(htmlReport).toContain('Production Readiness Report')
    expect(htmlReport).toContain('Score explanation')
    expect(htmlReport).toContain('DotDet logo')
    expect(htmlReport).toContain('Suppressions And Accepted Risks')
    expect(htmlReport).not.toContain('<h2>Source Preview</h2>')

    const markdownDownloadPromise = page.waitForEvent('download')
    await page.getByRole('button', { name: 'Export Report' }).click()
    await page.getByRole('menuitem', { name: 'Markdown report' }).click()
    const markdownDownload = await markdownDownloadPromise
    const markdownPath = await markdownDownload.path()
    expect(markdownPath).toBeTruthy()
    const markdownReport = await fs.readFile(markdownPath!, 'utf8')
    expect(markdownReport).toContain('Score explanation')
    expect(markdownReport).toContain('## Open Findings By Category')
    expect(markdownReport).toContain('## Suppressed / Accepted Risks')

    await expect.poll(() => page.evaluate(() => localStorage.getItem('det.analysis.lastResult.v1'))).not.toBeNull()
    const persistedAnalysis = await page.evaluate(() => JSON.parse(localStorage.getItem('det.analysis.lastResult.v1') ?? '{}')) as {
      issues?: Array<{ rootCauseKey?: string }>
      solutionPath?: string
      repositoryRoot?: string
      sourceFiles?: unknown[]
    }
    expect(persistedAnalysis.solutionPath).toBeUndefined()
    expect(persistedAnalysis.repositoryRoot).toBeUndefined()
    expect(persistedAnalysis.sourceFiles).toBeUndefined()
    expect(persistedAnalysis.issues?.every((issue) => !/^[^|]*\|[^|]*\|[A-Za-z]:[\\/]/.test(issue.rootCauseKey ?? ''))).toBe(true)

    await page.evaluate(() => {
      localStorage.setItem('det.analysis.lastSolutionPath', 'C:\\server\\private\\Sample.slnx')
      const stored = JSON.parse(localStorage.getItem('det.analysis.lastResult.v1') ?? '{}')
      if (stored.issues?.[0]) {
        stored.issues[0].rootCauseKey = 'SEC001|Api|C:\\server\\private\\Program.cs|Configuration risk'
      }
      localStorage.setItem('det.analysis.lastResult.v1', JSON.stringify(stored))
      localStorage.setItem('det.findingDispositions.v1', JSON.stringify({
        'unrelated-private-file:C:\\secret\\Private.cs': 'Accepted Risk',
      }))
    })
    await page.reload()
    expect(await page.evaluate(() => localStorage.getItem('det.analysis.lastSolutionPath'))).toBeNull()
    const resanitizedAnalysis = await page.evaluate(() => JSON.parse(localStorage.getItem('det.analysis.lastResult.v1') ?? '{}')) as {
      issues?: Array<{ rootCauseKey?: string }>
    }
    expect(resanitizedAnalysis.issues?.[0]?.rootCauseKey).toContain('<unknown-file>')
    expect(JSON.stringify(resanitizedAnalysis)).not.toContain('C:\\server\\private')

    const jsonDownloadPromise = page.waitForEvent('download')
    await page.getByRole('button', { name: 'Export Report' }).click()
    await page.getByRole('menuitem', { name: 'JSON data' }).click()
    const jsonDownload = await jsonDownloadPromise
    const jsonPath = await jsonDownload.path()
    expect(jsonPath).toBeTruthy()
    const jsonReport = JSON.parse(await fs.readFile(jsonPath!, 'utf8')) as {
      findingDispositions?: Record<string, string>
      issues?: Array<{ rootCauseKey?: string }>
      solutionName?: string
      solutionPath?: string
      sourceFiles?: unknown[]
    }
    expect(jsonReport.solutionName).toBeTruthy()
    expect(Array.isArray(jsonReport.issues)).toBe(true)
    expect(jsonReport.solutionPath).toBeUndefined()
    expect(jsonReport.sourceFiles).toBeUndefined()
    expect(jsonReport.findingDispositions).not.toHaveProperty('unrelated-private-file:C:\\secret\\Private.cs')
    expect(jsonReport.issues?.[0]?.rootCauseKey).toContain('<unknown-file>')
    expect(JSON.stringify(jsonReport)).not.toContain('C:\\server\\private')

    await page.setViewportSize({ width: 390, height: 844 })
    const mobileOverflow = await page.evaluate(() => document.body.scrollWidth > window.innerWidth + 2)
    expect(mobileOverflow).toBe(false)

    await page.setViewportSize({ width: 1920, height: 1080 })
    const desktopReport = await page.locator('.det-overview-canvas').boundingBox()
    expect(desktopReport?.width ?? 0).toBeGreaterThan(1000)
    expect(desktopReport?.width ?? 0).toBeLessThanOrEqual(1480)
  })

  test('findings filtering, search, sorting, and empty state', async ({ page }) => {
    await analyzeSample(page)
    await page.getByRole('button', { name: 'Findings' }).click()

    await expect(page.getByRole('heading', { name: 'Findings' })).toBeVisible()
    await expect(page.getByText(/\d+ of \d+ findings/)).toBeVisible()

    await page.locator('select').nth(0).selectOption('Error')
    await page.locator('select').nth(1).selectOption('Security')
    await expect(page.getByText(/1 of \d+ findings/)).toBeVisible()

    await page.getByLabel('Hide suppressed').check()
    await page.locator('select').nth(0).selectOption('Warning')
    await page.getByPlaceholder('Search findings, files, projects, recommendations').fill('CORS')
    await expect(page.getByText(/1 of \d+ findings/)).toBeVisible()
    await expect(page.getByText('A CORS policy allows requests from any browser origin.')).toBeVisible()

    await page.getByPlaceholder('Search findings, files, projects, recommendations').fill('zzzz-not-found')
    await expect(page.getByText('No findings detected for this category.')).toBeVisible()
  })

  test('code explorer, engineering guide, and resizable panes', async ({ page }) => {
    await analyzeSample(page)

    await expect(page.getByRole('heading', { name: 'Solution Explorer' })).toBeVisible()
    await page.locator('button').filter({ hasText: 'Program.cs' }).first().click()
    await expect(page.getByRole('heading', { name: 'Program.cs' })).toBeVisible()
    await expect(page.getByText(/Finding 1 of \d+/)).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Engineering Guide' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Rule' })).toBeVisible()
    await expect(page.getByRole('button', { name: 'Copy Suggested Fix' })).toBeVisible()

    const explorer = page.locator('aside').filter({ hasText: 'SOLUTION EXPLORER' })
    const before = await explorer.boundingBox()
    const splitter = page.getByRole('button', { name: 'Resize Solution Explorer' })
    const splitterBox = await splitter.boundingBox()
    expect(splitterBox).not.toBeNull()
    await page.mouse.move(splitterBox!.x + splitterBox!.width / 2, splitterBox!.y + 40)
    await page.mouse.down()
    await page.mouse.move(splitterBox!.x + splitterBox!.width / 2 + 80, splitterBox!.y + 40)
    await page.mouse.up()
    const after = await explorer.boundingBox()
    expect(after && before ? after.width > before.width : false).toBe(true)
  })

  test('rule explorer and settings pages', async ({ page }) => {
    await analyzeSample(page)

    await page.getByRole('button', { name: 'Rule Explorer' }).click()
    await expect(page.getByRole('heading', { name: 'Rule Explorer' })).toBeVisible()
    await expect(page.getByText(/rules detected in this analysis/)).toBeVisible()
    await expect(page.getByLabel('Finding disposition')).not.toBeVisible()
    await expect(page.getByText('This rule did not produce findings in the current analysis.')).toHaveCount(0)
    await page.getByPlaceholder('Search rules').fill('CORS')
    await expect(page.getByRole('heading', { name: 'CORS policy allows any origin' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Code Examples' })).toBeVisible()
    await expect(page.locator('pre').first()).toBeVisible()

    await page.getByRole('button', { name: 'Settings' }).click()
    await expect(page.getByRole('heading', { name: 'Settings' })).toBeVisible()
    await page.getByRole('button', { name: 'Editor', exact: true }).click()
    await expect(page.getByText('Gutter markers')).toBeVisible()
    await page.getByRole('button', { name: 'Analysis', exact: true }).click()
    await expect(page.getByText('Open first finding')).toBeVisible()
    await page.getByRole('button', { name: 'Export', exact: true }).last().click()
    await expect(page.getByText('Report format')).toBeVisible()
  })
})
