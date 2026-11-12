import { useCallback, useEffect, useState } from 'react'

export type AsyncState<T> =
  | { status: 'loading' }
  | { status: 'error'; error: string }
  | { status: 'ready'; data: T }

/** Runs `load` on mount and whenever `reload()` is called. Stale responses are ignored. */
export function useAsync<T>(load: () => Promise<T>, deps: readonly unknown[]): [AsyncState<T>, () => void] {
  const [state, setState] = useState<AsyncState<T>>({ status: 'loading' })
  const [tick, setTick] = useState(0)
  // eslint-disable-next-line react-hooks/exhaustive-deps
  const run = useCallback(load, deps)
  useEffect(() => {
    let live = true
    run().then(
      (data) => { if (live) setState({ status: 'ready', data }) },
      (e: unknown) => { if (live) setState({ status: 'error', error: e instanceof Error ? e.message : 'Something went wrong.' }) },
    )
    return () => { live = false }
  }, [run, tick])
  const reload = useCallback(() => { setState({ status: 'loading' }); setTick((t) => t + 1) }, [])
  return [state, reload]
}
