using Nop.Core.Domain.Shipping;
using Nop.Services.Events;
using Nop.Services.Logging;
using Nop.Services.Stores;
using System.Linq;
using Wddc.Core.Domain.Orders;
using WddcApiClient.Services.Orders;

namespace Nop.Plugin.Wddc.Core.Events
{
    /// <summary>
    /// Handle shipment received, this may be a little confusing
    /// as ShipmentSent = sent to the clinic
    /// </summary>
    public class HandleShipmentSentEvent : IConsumer<ShipmentSentEvent>
    {
        private readonly WddcOrderService _wddcOrderService;
        private readonly IStoreService storeService;
        private readonly ILogger _logger;

        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="wddcOrderService"></param>
        /// <param name="logger"></param>
        public HandleShipmentSentEvent(WddcOrderService wddcOrderService,
            IStoreService storeService,
            ILogger logger)
        {
            _wddcOrderService = wddcOrderService;
            this.storeService = storeService;
            _logger = logger;
        }

        /// <summary>
        /// Handle event
        /// </summary>
        /// <param name="eventMessage"></param>
        public void HandleEvent(ShipmentSentEvent eventMessage)
        {
            // get nop order id
            var orderId = eventMessage.Shipment.OrderId;
            var store = storeService.GetStoreById(eventMessage.Shipment.Order.StoreId);
            // get web order

            var wddcOrder = _wddcOrderService
                .ListOrders(new OrderListOptions { CustomerId = store.CustomerId, OriginalOrderId = orderId })
                .Results
                .FirstOrDefault();

            // null check
            if (wddcOrder == null)
                return;

            wddcOrder.OrderStatus = OrderStatus.Completed;
            _wddcOrderService.UpdateOrder(wddcOrder);
        }
    }
}
