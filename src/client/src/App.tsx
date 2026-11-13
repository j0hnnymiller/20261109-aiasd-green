import { useCallback, useEffect, useState } from 'react'
import './App.css'

type Screen = 'loading' | 'first-use' | 'ready' | 'error'
type AccessResult = { screen: Screen; message?: string }

const initializationKeyName = 'taskspace-initialization-key'

function clearInitializationKey() {
  try {
    localStorage.removeItem(initializationKeyName)
  } catch {
    return
  }
}

function App() {
  const [screen, setScreen] = useState<Screen>('loading')
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState('')

  const loadAccess = useCallback(async (): Promise<AccessResult> => {
    try {
      const response = await fetch('/api/workspace', {
        cache: 'no-store',
        credentials: 'same-origin',
      })

      if (response.status === 401) {
        return { screen: 'first-use' }
      }

      if (response.ok) {
        const status: unknown = await response.json()
        if (typeof status !== 'object'
          || status === null
          || !('accessible' in status)
          || status.accessible !== true
          || !('confirmed' in status)
          || typeof status.confirmed !== 'boolean') {
          return { screen: 'error', message: 'The task space returned an invalid status. Try again.' }
        }

        if (!status.confirmed) {
          const confirmation = await fetch('/api/workspace/confirm', {
            method: 'POST',
            cache: 'no-store',
            credentials: 'same-origin',
          })

          if (confirmation.status === 401) {
            return {
              screen: 'first-use',
              message: 'This setup could not be confirmed. Select Create new task space to begin again.',
            }
          }

          if (!confirmation.ok) {
            return {
              screen: 'error',
              message: confirmation.status === 429
                ? 'Too many requests. Wait a little, then try again.'
                : 'The task space could not be confirmed. Try again.',
            }
          }
        }

        clearInitializationKey()
        return { screen: 'ready' }
      }

      return {
        screen: 'error',
        message: response.status === 429
          ? 'Too many requests. Wait a little, then try again.'
          : 'The task space could not be checked. Try again.',
      }
    } catch {
      return {
        screen: 'error',
        message: 'The task space could not be checked. Check your connection and try again.',
      }
    }
  }, [])

  useEffect(() => {
    let active = true
    void loadAccess().then((result) => {
      if (active) {
        setScreen(result.screen)
        setMessage(result.message ?? '')
      }
    })

    return () => {
      active = false
    }
  }, [loadAccess])

  const retryAccess = () => {
    setScreen('loading')
    setMessage('')
    void loadAccess().then((result) => {
      setScreen(result.screen)
      setMessage(result.message ?? '')
    })
  }

  const createTaskSpace = async () => {
    setBusy(true)
    setMessage('')

    try {
      if (!navigator.locks) {
        setMessage('This browser cannot safely coordinate task-space setup across tabs. Try a supported browser.')
        return
      }

      await navigator.locks.request('taskspace-initialization', async () => {
        const access = await loadAccess()
        if (access.screen !== 'first-use') {
          setScreen(access.screen)
          setMessage(access.message ?? '')
          return
        }

        let initializationKey: string
        try {
          initializationKey = localStorage.getItem(initializationKeyName) ?? crypto.randomUUID()
          localStorage.setItem(initializationKeyName, initializationKey)
        } catch {
          setMessage('This browser cannot keep the temporary setup reference needed to retry safely. Enable browser storage and try again.')
          return
        }

        const response = await fetch('/api/workspaces', {
          method: 'POST',
          cache: 'no-store',
          credentials: 'same-origin',
          headers: { 'X-Initialization-Key': initializationKey },
        })

        if (response.status === 201 || response.status === 204) {
          const result = await loadAccess()
          setScreen(result.screen)
          setMessage(result.message
            ?? (result.screen === 'first-use'
              ? 'The setup could not be confirmed. Select Create new task space to begin again.'
              : ''))
          return
        }

        if (response.status === 409) {
          clearInitializationKey()
          setMessage('This setup could not be safely resumed. Select Create new task space to begin again.')
          return
        }

        setMessage(response.status === 429
          ? 'Too many setup attempts. Wait a little, then try again.'
          : 'A new task space could not be created. Try again.')
      })
    } catch {
      setMessage('The service could not be reached. You can try the same setup again.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <main className="task-space">
      <header>
        <p className="eyebrow">Task manager</p>
        <h1>Your task space</h1>
      </header>

      {screen === 'loading' && (
        <p className="notice" role="status" aria-live="polite">
          Checking this browser for a task space…
        </p>
      )}

      {screen === 'first-use' && (
        <section aria-labelledby="first-use-heading">
          <h2 id="first-use-heading">No accessible task space</h2>
          <p>
            This task space is tied to this browser. If you lose, clear, or let its
            browser credential expire, the task space becomes inaccessible. There
            is no account, pairing, reset, or recovery, and a new task space will
            not contain your previous information.
          </p>
          <button type="button" onClick={createTaskSpace} disabled={busy}>
            {busy ? 'Creating task space…' : 'Create new task space'}
          </button>
          <p className="notice" role="status" aria-live="polite" aria-atomic="true">
            {busy ? 'Creating your task space…' : message}
          </p>
        </section>
      )}

      {screen === 'ready' && (
        <p className="notice success" role="status" aria-live="polite">
          Your task space is ready in this browser.
        </p>
      )}

      {screen === 'error' && (
        <section aria-labelledby="error-heading">
          <h2 id="error-heading">Task space unavailable</h2>
          <p role="alert" aria-live="assertive">{message}</p>
          <button type="button" onClick={retryAccess}>
            Try again
          </button>
        </section>
      )}
    </main>
  )
}

export default App
