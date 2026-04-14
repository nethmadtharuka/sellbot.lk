import { Navigate, Route, Routes } from 'react-router-dom'
import { AppLayout } from './components/AppLayout'
import { OrdersPage } from './pages/OrdersPage'
import { OrderDetailsPage } from './pages/OrderDetailsPage'
import { DeliveryZonesPage } from './pages/DeliveryZonesPage'

export default function App() {
  return (
    <Routes>
      <Route element={<AppLayout />}>
        <Route path="/" element={<Navigate to="/orders" replace />} />
        <Route path="/orders" element={<OrdersPage />} />
        <Route path="/orders/:id" element={<OrderDetailsPage />} />
        <Route path="/delivery-zones" element={<DeliveryZonesPage />} />
      </Route>
      <Route path="*" element={<Navigate to="/orders" replace />} />
    </Routes>
  )
}
