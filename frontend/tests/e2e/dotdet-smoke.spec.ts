import { expect, test, type Page } from '@playwright/test'
import fs from 'node:fs/promises'

async function analyzeSample(page: Page) {
  await page.goto('/')
  await expect(page.getByRole('heading', { name: '.DET' })).toBeVisible()
  await page.getByRole('button', { name: 'Analyze Sample' }).click()
  await expect(page.getByRole('button', { name: 'Overview' })).toBeVisible({ timeout: 60_000 })
}

test.describe('DotDet critical smoke flows', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/')
    await page.evaluate(() => localStorage.clear())
  })

  test('public landing and authenticated ZIP dashboard', async ({ page }) => {
    await page.goto('/')
    await expect(page.getByRole('heading', { name: '.DET' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Production-readiness analysis for serious ASP.NET Core teams.' })).toBeVisible()
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
    await expect(page.getByRole('heading', { name: 'Upload Solution ZIP' })).toBeVisible()
    await expect(page.getByRole('button', { name: 'Login with GitHub' })).toHaveCount(0)
    await expect(page.locator('#solution-path')).toHaveCount(0)
    await expect(page.getByRole('button', { name: 'Run Analysis' })).toBeDisabled()
  })

  test('sample analysis, overview, export, and responsive layout', async ({ page }) => {
    await analyzeSample(page)

    await page.getByRole('button', { name: 'Overview' }).click()
    await expect(page.getByRole('heading', { name: 'Engineering readiness report' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Production Readiness' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Architecture' })).toBeVisible()
    await page.getByRole('button', { name: 'Open Architecture' }).click()
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

    const jsonDownloadPromise = page.waitForEvent('download')
    await page.getByRole('button', { name: 'Export Report' }).click()
    await page.getByRole('menuitem', { name: 'JSON data' }).click()
    const jsonDownload = await jsonDownloadPromise
    const jsonPath = await jsonDownload.path()
    expect(jsonPath).toBeTruthy()
    const jsonReport = JSON.parse(await fs.readFile(jsonPath!, 'utf8')) as { solutionName?: string; issues?: unknown[] }
    expect(jsonReport.solutionName).toBeTruthy()
    expect(Array.isArray(jsonReport.issues)).toBe(true)

    await page.setViewportSize({ width: 390, height: 844 })
    const mobileOverflow = await page.evaluate(() => document.body.scrollWidth > window.innerWidth + 2)
    expect(mobileOverflow).toBe(false)

    await page.setViewportSize({ width: 1920, height: 1080 })
    const desktopReport = await page.locator('.det-overview-report').boundingBox()
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
    await expect(page.locator('.monaco-editor')).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Engineering Guide' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Rule' })).toBeVisible()
    await expect(page.getByRole('button', { name: 'Copy Suggested Fix' })).toBeVisible()
    await page.locator('button').filter({ hasText: 'Program.cs' }).first().click()
    await expect(page.locator('.monaco-editor')).toContainText('using Forge.SampleShop.Application.Orders')
    await expect(page.locator('.monaco-editor')).toContainText('builder.Services.AddCors')

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
    await expect(page.getByText(/rules shown/)).toBeVisible()
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
