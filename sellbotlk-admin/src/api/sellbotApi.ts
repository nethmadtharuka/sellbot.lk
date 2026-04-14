import { apiGet, apiPost, apiPut } from './http'
import type {
  ApiEnvelope,
  DeliveryCheckRequestDto,
  DeliveryCheckResponseDto,
  DeliveryZoneResponseDto,
  OrderResponseDto,
  UpdateDeliveryStatusDto,
  UpdateOrderStatusDto,
} from './types'

export const sellbotApi = {
  getOrders: (params?: { status?: string; customerId?: number }) => {
    const usp = new URLSearchParams()
    if (params?.status) usp.set('status', params.status)
    if (params?.customerId != null) usp.set('customerId', String(params.customerId))
    const qs = usp.toString()
    return apiGet<ApiEnvelope<OrderResponseDto[]>>(`/api/v1/orders${qs ? `?${qs}` : ''}`)
  },

  getOrderById: (id: number) =>
    apiGet<ApiEnvelope<OrderResponseDto>>(`/api/v1/orders/${id}`),

  updateOrderStatus: (id: number, dto: UpdateOrderStatusDto) =>
    apiPut<ApiEnvelope<OrderResponseDto>>(`/api/v1/orders/${id}/status`, dto),

  updateDeliveryStatus: (id: number, dto: UpdateDeliveryStatusDto) =>
    apiPut<ApiEnvelope<{ message: string }>>(`/api/v1/orders/${id}/delivery-status`, dto),

  cancelOrder: (id: number) =>
    apiPut<ApiEnvelope<OrderResponseDto>>(`/api/v1/orders/${id}/cancel`, {}),

  getDeliveryZones: () =>
    apiGet<ApiEnvelope<DeliveryZoneResponseDto[]>>(`/api/v1/delivery-zones`),

  checkDelivery: (dto: DeliveryCheckRequestDto) =>
    apiPost<ApiEnvelope<DeliveryCheckResponseDto>>(`/api/v1/delivery-zones/check`, dto),
}

