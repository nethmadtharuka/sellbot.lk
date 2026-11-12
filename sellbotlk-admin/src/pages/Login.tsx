import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { ApiError } from '../api/http'
import { sellbotApi } from '../api/sellbotApi'
import { MODE } from '../data'
import { Field } from '../ui/primitives'

export default function Login() {
  const navigate = useNavigate()
  const [user, setUser] = useState(MODE === 'demo' ? 'admin@sellbot.lk' : '')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  async function submit(e: FormEvent) {
    e.preventDefault()
    setError('')
    if (MODE === 'demo') { navigate('/app'); return } // demo has no accounts; fictional data only
    if (!user.trim() || !password) { setError('Enter your email and password.'); return }
    setBusy(true)
    try {
      // The backend login takes a username; the owner signs in with the configured admin name.
      await sellbotApi.login(user.trim(), password)
      navigate('/app', { replace: true })
    } catch (err) {
      setError(err instanceof ApiError && err.status === 429 ? 'Too many attempts. Wait a minute and try again.'
        : err instanceof ApiError && err.status === 401 ? 'That email and password do not match.' : 'Could not reach the server. Try again.')
    } finally { setBusy(false) }
  }

  return (
    <main className="login" id="main">
      <div className="login__inner">
        <section className="login-card" aria-labelledby="login-t" data-anim="login-card">
          <h1 id="login-t" className="login-card__title">Owner sign in</h1>
          <p className="login-card__sub">Manage orders, payments and products.</p>
          <form onSubmit={(e) => void submit(e)} noValidate>
            <Field label="Email">{(fid) => <input id={fid} className="input" autoComplete="username" placeholder="admin@sellbot.lk" value={user} onChange={(e) => setUser(e.target.value)} />}</Field>
            <Field label="Password">{(fid) => <input id={fid} className="input" type="password" autoComplete="current-password" placeholder="••••••••••" value={password} onChange={(e) => setPassword(e.target.value)} />}</Field>
            {error && <p className="login-card__error" role="alert">{error}</p>}
            <button type="submit" className="btn btn--green btn--lg" aria-busy={busy}>{busy ? 'Signing in…' : 'Sign in'}</button>
          </form>
          <p className="login-card__foot">Demo credentials are shown on the presentation page.</p>
        </section>
        <div className="login__claim">
          <h2>Your shop,<br />on one screen.</h2>
          <p>Private admin area. Not a public storefront.</p>
        </div>
      </div>
      <p className="login__wordmark" aria-hidden="true">SellBot</p>
    </main>
  )
}
