import { lazy, Suspense, type ReactNode } from 'react'
import { Navigate, Route, Routes } from 'react-router-dom'
import { getToken } from './api/http'
import { MODE } from './data'
import { AdminLayout } from './layouts/AdminLayout'
import { ShowcaseLayout } from './layouts/ShowcaseLayout'

const Landing = lazy(() => import('./pages/Landing'))
const Login = lazy(() => import('./pages/Login'))
const Overview = lazy(() => import('./pages/Overview'))
const Orders = lazy(() => import('./pages/Orders'))
const OrderDetail = lazy(() => import('./pages/OrderDetail'))
const Customers = lazy(() => import('./pages/Customers'))
const CustomerDetail = lazy(() => import('./pages/CustomerDetail'))
const Inbox = lazy(() => import('./pages/Inbox'))
const Products = lazy(() => import('./pages/Products'))
const Payments = lazy(() => import('./pages/Payments'))
const Delivery = lazy(() => import('./pages/Delivery'))
const Analytics = lazy(() => import('./pages/Analytics'))
const Documents = lazy(() => import('./pages/Documents'))
const Integrations = lazy(() => import('./pages/Integrations'))
const Playground = lazy(() => import('./pages/Playground'))
const Journey = lazy(() => import('./pages/Journey'))
const States = lazy(() => import('./pages/States'))
const Architecture = lazy(() => import('./pages/Architecture'))
const Api = lazy(() => import('./pages/Api'))
const Security = lazy(() => import('./pages/Security'))

/** Live mode requires the owner's JWT; demo mode is open (fictional data only). */
function RequireAuth({ children }: { children: ReactNode }) {
  if (MODE === 'live' && !getToken()) return <Navigate to="/login" replace />
  return <>{children}</>
}

export default function App() {
  return (
    <Suspense fallback={null}>
      <Routes>
        <Route path="/" element={<Landing />} />
        <Route path="/login" element={<Login />} />
        <Route path="/states" element={<States />} />
        <Route element={<ShowcaseLayout />}>
          <Route path="/architecture" element={<Architecture />} />
          <Route path="/api" element={<Api />} />
          <Route path="/security" element={<Security />} />
        </Route>
        <Route path="/app" element={<RequireAuth><AdminLayout /></RequireAuth>}>
          <Route index element={<Overview />} />
          <Route path="orders" element={<Orders />} />
          <Route path="orders/:id" element={<OrderDetail />} />
          <Route path="payments" element={<Payments />} />
          <Route path="delivery" element={<Delivery />} />
          <Route path="products" element={<Products />} />
          <Route path="products/:id" element={<Products />} />
          <Route path="customers" element={<Customers />} />
          <Route path="customers/:id" element={<CustomerDetail />} />
          <Route path="inbox" element={<Inbox />} />
          <Route path="playground" element={<Playground />} />
          <Route path="journey" element={<Journey />} />
          <Route path="analytics" element={<Analytics />} />
          <Route path="documents" element={<Documents />} />
          <Route path="integrations" element={<Integrations />} />
        </Route>
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </Suspense>
  )
}
