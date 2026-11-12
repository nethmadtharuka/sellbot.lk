import { useCallback, useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { data } from '../data'
import type { Product } from '../data/types'
import { useAsync } from '../hooks/useAsync'
import { lkr } from '../lib/format'
import { Button, Card, Chip, Drawer, EmptyState, Field, InlineError, Meter, PageHeader, Skeleton, Toast, Toggle } from '../ui/primitives'

const STOCK_SCALE = 20 // bar is full at 20 units, as drawn

function StockCell({ p }: { p: Product }) {
  const out = p.stock === 0
  const low = !out && p.stock <= p.lowStockThreshold
  return (
    <span className="stock-cell">
      <span>{p.stock}{out && <span style={{ color: 'var(--c-red)' }}> · Out of stock</span>}{low && <span style={{ color: 'var(--c-amber)' }}> · Low</span>}</span>
      <Meter value={out ? 0.6 : p.stock} max={STOCK_SCALE} tone={out ? 'red' : low ? 'amber' : 'teal'} label={`${p.stock} in stock`} />
    </span>
  )
}

function newProduct(id: number): Product {
  return { id, name: '', category: '', description: '', price: 0, minPrice: 0, stock: 0, lowStockThreshold: 5, active: true }
}

export default function Products() {
  const { id } = useParams()
  const navigate = useNavigate()
  const [state, reload] = useAsync(() => data.products(), [])
  const [toast, setToast] = useState('')
  const [creating, setCreating] = useState(false)
  const clear = useCallback(() => setToast(''), [])
  const products = state.status === 'ready' ? state.data.data : []
  const editing = creating ? newProduct(Math.max(0, ...products.map((p) => p.id)) + 1) : products.find((p) => p.id === Number(id))
  const close = () => { setCreating(false); navigate('/app/products') }

  return (
    <>
      <PageHeader eyebrow="Catalogue" title="Products" right={
        <Button variant="white" disabledReason={data.mode === 'live' ? 'Adding products is done through the API in this version' : undefined} onClick={() => setCreating(true)}>+ Add product</Button>
      } />
      {state.status === 'loading' && <Card><Skeleton /></Card>}
      {state.status === 'error' && <InlineError title="Could not load products" body="Check your connection and try again." onRetry={reload} />}
      {state.status === 'ready' && (
        <Card className="card-pad-table" data-anim="products">
          {products.length === 0 ? <EmptyState title="No products yet" body="Add a product to start selling on WhatsApp." /> : (
            <div className="tbl-scroll">
              <table className="tbl prod-table">
                <thead><tr><th>Product</th><th>Price</th><th>Min price · internal</th><th>Stock</th><th>Status</th></tr></thead>
                <tbody>
                  {products.map((p) => (
                    <tr key={p.id} className="is-link" onClick={() => navigate(`/app/products/${p.id}`)}>
                      <td><button type="button" className="cell-link strong" style={{ textAlign: 'left' }} onClick={(e) => { e.stopPropagation(); navigate(`/app/products/${p.id}`) }}>{p.name}</button><span className="sub">{p.category}</span></td>
                      <td style={{ color: 'var(--c-text)' }}>{lkr(p.price)}</td>
                      <td className="min-price">{p.minPrice !== undefined ? lkr(p.minPrice) : <span className="dim" title="The API does not return min price">—</span>}</td>
                      <td><StockCell p={p} /></td>
                      <td>{p.active ? <Chip tone="green">Active</Chip> : <Chip>Inactive</Chip>}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </Card>
      )}
      {editing && (
        <ProductDrawer key={editing.id} product={editing} isNew={creating} onClose={close}
          onSaved={() => { close(); reload(); setToast(data.mode === 'demo' ? 'Saved. Demo changes reset on reload.' : 'Product saved.') }} />
      )}
      {toast && <div className="toast-host"><Toast message={toast} onDone={clear} /></div>}
    </>
  )
}

function ProductDrawer({ product, isNew, onClose, onSaved }: { product: Product; isNew: boolean; onClose: () => void; onSaved: () => void }) {
  const [p, setP] = useState(product)
  const [touched, setTouched] = useState<Record<string, boolean>>({})
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')
  useEffect(() => setP(product), [product])
  const errs: Record<string, string> = {}
  if (!p.name.trim()) errs.name = 'Enter a product name.'
  if (!(p.price > 0)) errs.price = 'Enter a price above 0.'
  if (p.minPrice !== undefined && (p.minPrice <= 0 || p.minPrice > p.price)) errs.minPrice = 'Min price must be above 0 and not above the price.'
  if (!(p.stock >= 0) || !Number.isInteger(p.stock)) errs.stock = 'Enter a whole number, 0 or more.'
  if (!p.category.trim()) errs.category = 'Enter a category.'
  const show = (k: string) => (touched[k] ? errs[k] : undefined)
  const num = (v: string) => Number(v.replace(/[^\d.]/g, ''))

  async function save() {
    setTouched({ name: true, price: true, minPrice: true, stock: true, category: true })
    if (Object.keys(errs).length) return
    setSaving(true); setError('')
    try { await data.saveProduct(p); onSaved() } catch (e) { setError(e instanceof Error ? e.message : 'Could not save the product.') } finally { setSaving(false) }
  }

  return (
    <Drawer open title={isNew ? 'Add product' : 'Edit product'} onClose={onClose}
      footer={<><Button variant="white" size="md" onClick={() => void save()} aria-busy={saving}>{saving ? 'Saving…' : 'Save changes'}</Button><Button variant="dark" onClick={onClose}>Cancel</Button></>}>
      {error && <InlineError title="Not saved" body={error} />}
      <Field label="Name" hint={show('name')}>{(fid) => <input id={fid} className="input" value={p.name} aria-invalid={Boolean(show('name'))} onBlur={() => setTouched({ ...touched, name: true })} onChange={(e) => setP({ ...p, name: e.target.value })} />}</Field>
      <div>
        <div className="field-row">
          <Field label="Price (LKR)" hint={show('price')}>{(fid) => <input id={fid} className="input input--mono" inputMode="numeric" value={p.price ? p.price.toLocaleString('en-US') : ''} aria-invalid={Boolean(show('price'))} onBlur={() => setTouched({ ...touched, price: true })} onChange={(e) => setP({ ...p, price: num(e.target.value) })} />}</Field>
          <Field label="Min price (LKR)" hint={show('minPrice')}>{(fid) => (
            <input id={fid} className="input input--mono" inputMode="numeric" value={p.minPrice ? p.minPrice.toLocaleString('en-US') : ''} placeholder={p.minPrice === undefined ? 'Unchanged' : undefined}
              aria-invalid={Boolean(show('minPrice'))} onBlur={() => setTouched({ ...touched, minPrice: true })} onChange={(e) => setP({ ...p, minPrice: e.target.value ? num(e.target.value) : undefined })} />
          )}</Field>
        </div>
        <p className="field__hint field__hint--violet" style={{ marginTop: 8 }}>Min price is internal. It is never shown to customers; the bot negotiates down to it.</p>
      </div>
      <div className="field-row">
        <Field label="Stock" hint={show('stock')}>{(fid) => <input id={fid} className="input" inputMode="numeric" value={String(p.stock)} aria-invalid={Boolean(show('stock'))} onBlur={() => setTouched({ ...touched, stock: true })} onChange={(e) => setP({ ...p, stock: num(e.target.value) })} />}</Field>
        <Field label="Category" hint={show('category')}>{(fid) => <input id={fid} className="input" value={p.category} aria-invalid={Boolean(show('category'))} onBlur={() => setTouched({ ...touched, category: true })} onChange={(e) => setP({ ...p, category: e.target.value })} />}</Field>
      </div>
      <Field label="Description">{(fid) => <input id={fid} className="input" value={p.description ?? ''} onChange={(e) => setP({ ...p, description: e.target.value })} />}</Field>
      <Toggle label="Active in catalogue" checked={p.active} onChange={(v) => setP({ ...p, active: v })} />
    </Drawer>
  )
}
