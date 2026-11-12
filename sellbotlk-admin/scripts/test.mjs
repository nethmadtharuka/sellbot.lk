// Minimal test runner: Vite (already a dependency) transforms TS/TSX, node:test runs the tests.
// No extra packages. Usage: npm test
import { readdirSync, statSync } from 'node:fs'
import { join, relative } from 'node:path'
import { createServer } from 'vite'

function find(dir) {
  return readdirSync(dir).flatMap((f) => {
    const p = join(dir, f)
    return statSync(p).isDirectory() ? find(p) : /\.test\.tsx?$/.test(f) ? [p] : []
  })
}

const server = await createServer({ server: { middlewareMode: true }, appType: 'custom', logLevel: 'error' })
try {
  for (const file of find('src')) await server.ssrLoadModule('/' + relative('.', file).replace(/\\/g, '/'))
} finally {
  await server.close() // modules are loaded; node:test keeps running the registered tests
}
