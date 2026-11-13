import { expect, test } from '@playwright/test'

test('discloses no recovery and waits for an explicit create action', async ({ page, context }) => {
  const provisioningRequests: string[] = []
  page.on('request', (request) => {
    if (request.url().endsWith('/api/workspaces') && request.method() === 'POST') {
      provisioningRequests.push(request.postData() ?? '')
    }
  })

  await page.goto('/')
  await expect(page.getByRole('heading', { name: 'No accessible task space' })).toBeVisible()
  await expect(page.getByText(/There is no account, pairing, reset, or recovery/)).toBeVisible()
  await expect(page.getByRole('button', { name: 'Create new task space' })).toBeVisible()
  expect(provisioningRequests).toHaveLength(0)

  await page.getByRole('button', { name: 'Create new task space' }).click()
  await expect(page.getByRole('status')).toContainText('Your task space is ready')
  expect(provisioningRequests).toEqual([''])

  const cookie = (await context.cookies()).find((item) => item.name === '__Host-taskspace')
  expect(cookie).toBeDefined()
  expect(cookie?.httpOnly).toBe(true)
  expect(cookie?.secure).toBe(true)
  expect(cookie?.sameSite).toBe('Strict')
  expect(cookie?.expires).toBeGreaterThan(Date.now() / 1000)
  expect(await page.evaluate(() => document.cookie)).not.toContain('__Host-taskspace')
  expect(await page.evaluate(() => localStorage.length)).toBe(0)
  expect(await page.evaluate(() => sessionStorage.length)).toBe(0)
  expect(page.url()).not.toContain(cookie?.value)

  await page.reload()
  await expect(page.getByRole('status')).toContainText('Your task space is ready')
  expect(provisioningRequests).toHaveLength(1)
})

test('announces the loading state while checking the browser credential', async ({ page }) => {
  let releaseRequest: (() => void) | undefined
  const requestGate = new Promise<void>((resolve) => {
    releaseRequest = resolve
  })
  await page.route('**/api/workspace', async (route) => {
    await requestGate
    await route.continue()
  })

  await page.goto('/')
  await expect(page.getByRole('status')).toContainText('Checking this browser for a task space')
  releaseRequest?.()
  await expect(page.getByRole('heading', { name: 'No accessible task space' })).toBeVisible()
})

test('supports keyboard creation with visible focus and accessible progress feedback', async ({ page }) => {
  await page.goto('/')
  const createButton = page.getByRole('button', { name: 'Create new task space' })
  await expect(createButton).toBeVisible()

  await page.keyboard.press('Tab')
  await expect(createButton).toBeFocused()
  await expect(createButton).toHaveCSS('outline-style', 'solid')
  await page.keyboard.press('Enter')

  await expect(page.getByRole('status')).toContainText('Your task space is ready')
})

test('announces an inaccessible service state and offers a keyboard-operable retry', async ({ page }) => {
  await page.route('**/api/workspace', (route) => route.abort())
  await page.goto('/')

  await expect(page.getByRole('alert')).toContainText('The task space could not be checked')
  await expect(page.getByRole('button', { name: 'Try again' })).toBeVisible()
})
