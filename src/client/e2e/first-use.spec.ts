import { expect, test, type Request } from '@playwright/test'

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

test('coordinates task-space creation across tabs until the credential is confirmed', async ({ page, context }) => {
  const secondPage = await context.newPage()
  const provisioningRequests: string[] = []
  let releaseResponse: (() => void) | undefined
  let markResponseReady: (() => void) | undefined
  const responseGate = new Promise<void>((resolve) => {
    releaseResponse = resolve
  })
  const responseReady = new Promise<void>((resolve) => {
    markResponseReady = resolve
  })

  const trackProvisioning = (request: Request) => {
    if (request.url().endsWith('/api/workspaces') && request.method() === 'POST') {
      provisioningRequests.push(request.headers()['x-initialization-key'] ?? '')
    }
  }
  page.on('request', trackProvisioning)
  secondPage.on('request', trackProvisioning)
  await page.route('**/api/workspaces', async (route) => {
    const response = await route.fetch()
    markResponseReady?.()
    await responseGate
    await route.fulfill({ response })
  })

  await page.goto('/')
  await secondPage.goto('/')
  await expect(page.getByRole('heading', { name: 'No accessible task space' })).toBeVisible()
  await expect(secondPage.getByRole('heading', { name: 'No accessible task space' })).toBeVisible()

  await page.getByRole('button', { name: 'Create new task space' }).click()
  await responseReady

  const pendingKey = await secondPage.evaluate(() => localStorage.getItem('taskspace-initialization-key'))
  expect(pendingKey).toBeTruthy()
  await secondPage.getByRole('button', { name: 'Create new task space' }).click()
  await secondPage.waitForTimeout(100)
  expect(provisioningRequests).toEqual([pendingKey])

  releaseResponse?.()
  await expect(page.getByRole('status')).toContainText('Your task space is ready')
  await expect(secondPage.getByRole('status')).toContainText('Your task space is ready')
  expect(provisioningRequests).toEqual([pendingKey])
  expect(await secondPage.evaluate(() => localStorage.getItem('taskspace-initialization-key'))).toBeNull()
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
