export type ApiEnvelope<T> = {
  success: boolean
  data: T
  count?: number
  message?: string
  error?: string
}

export type OrderItemResponseDto = {
  id: number
  productId: number
  productName: string
  quantity: number
  unitPrice: number
  negotiatedPrice?: number | null
  effectiveUnitPrice: number
  lineTotal: number
}

export type OrderResponseDto = {
  id: number
  orderNumber: string
  customerName: string
  customerPhone: string
  status: string
  paymentStatus: string
  totalAmount: number
  discountAmount: number
  deliveryAddress?: string | null
  deliveryArea?: string | null
  notes?: string | null
  isFraudFlagged: boolean
  fraudReason?: string | null
  createdAt: string
  updatedAt: string
  items: OrderItemResponseDto[]
}

export type DeliveryZoneResponseDto = {
  id: number
  zoneName: string
  deliveryFee: number
  estimatedDays: number
  freeDeliveryThreshold?: number | null
  isActive: boolean
}

export type DeliveryCheckRequestDto = {
  area: string
  orderTotal: number
}

export type DeliveryCheckResponseDto = {
  isServiceable: boolean
  zoneName: string
  deliveryFee: number
  estimatedDays: number
  isFreeDelivery: boolean
  message: string
}

export type UpdateOrderStatusDto = {
  status: string
}

export type UpdateDeliveryStatusDto = {
  status: string
  driverNote?: string | null
}

