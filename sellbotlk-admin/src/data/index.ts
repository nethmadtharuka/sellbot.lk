import { demoSource } from './demoSource'
import { liveSource } from './liveSource'
import type { DataSource } from './source'

/** Build-time switch: VITE_DATA_MODE=live talks to the real API; anything else is demo (default). */
export const MODE: 'demo' | 'live' = import.meta.env.VITE_DATA_MODE === 'live' ? 'live' : 'demo'
export const data: DataSource = MODE === 'live' ? liveSource : demoSource
