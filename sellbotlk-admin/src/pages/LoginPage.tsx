import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { sellbotApi } from '../api/sellbotApi'
import { Button, Card, Field, Input } from '../components/ui'

export function LoginPage() {
  const navigate = useNavigate()
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError('')
    setLoading(true)

    try {
      await sellbotApi.login(username, password)
      navigate('/orders', { replace: true })
    } catch {
      setError('Invalid username or password')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div
      style={{
        minHeight: '100vh',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        padding: 24,
      }}
    >
      <div style={{ width: '100%', maxWidth: 400 }}>
        <div style={{ textAlign: 'center', marginBottom: 24 }}>
          <div className="brandMark" style={{ margin: '0 auto 12px', width: 48, height: 48, fontSize: 20 }}>
            SB
          </div>
          <h1 className="h1" style={{ margin: 0 }}>SellBotLK</h1>
          <div className="muted">Admin Dashboard</div>
        </div>

        <Card title="Sign in">
          <form onSubmit={handleSubmit} style={{ display: 'grid', gap: 12 }}>
            <Field label="Username">
              <Input
                value={username}
                onChange={(e) => setUsername(e.target.value)}
                autoFocus
                required
              />
            </Field>
            <Field label="Password">
              <Input
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                required
              />
            </Field>
            {error && <div style={{ color: 'var(--bad)', fontSize: 13 }}>{error}</div>}
            <Button type="submit" disabled={loading}>
              {loading ? 'Signing in...' : 'Sign in'}
            </Button>
          </form>
        </Card>
      </div>
    </div>
  )
}
