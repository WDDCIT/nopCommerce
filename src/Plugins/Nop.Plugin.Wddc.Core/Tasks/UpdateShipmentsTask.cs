using Newtonsoft.Json;
using Nop.Core;
using Nop.Core.Domain.Shipping;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Shipping;
using Nop.Services.Stores;
using Sentry;
using System;
using System.Collections.Generic;
using System.Linq;
using Wddc.Core.Domain.Orders;
using WddcApiClient.Services.Orders;

namespace Nop.Plugin.Wddc.Core.Tasks
{
    public class UpdateShipmentsTask : IAutomatedDeliveryTask
    {
        private readonly IShipmentService _shipmentService;
        private readonly IOrderService _orderService;
        private readonly IStoreService _storeService;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly WddcOrderService _wddcOrderService;
        private readonly ILogger _logger;

        public string Name => "UpdateShipmentsTask";

        public bool Enabled => true;

        public UpdateShipmentsTask(
            IShipmentService shipmentService,
            IOrderService orderService,
            IStoreService storeService,
            IOrderProcessingService orderProcessingService,
            WddcOrderService wddcOrderService,
            ILogger logger)
        {
            _shipmentService = shipmentService;
            _orderService = orderService;
            _storeService = storeService;
            _orderProcessingService = orderProcessingService;
            _wddcOrderService = wddcOrderService;
            _logger = logger;
        }

        public void Execute()
        {
            var stores = _storeService.GetAllStores();
            var error = false;
            foreach (var store in stores)
            {
                var orderListResult = _wddcOrderService.ListOrders(new OrderListOptions
                {
                    CustomerId = store.CustomerId,
                    OrderStatuses = new List<OrderStatus>
                    {
                        OrderStatus.Processed,
                        OrderStatus.Shipped,
                        OrderStatus.PartiallyShipped,
                        OrderStatus.NotProcessed,
                    },
                    PageSize = int.MaxValue,
                });

                foreach (var wddcOrder in orderListResult.Results)
                {
                    foreach (var shipment in wddcOrder.OrderShipments)
                    {
                        try
                        {
                            // get client vantage order
                            CreateShipment(shipment);
                        }
                        catch (Exception ex)
                        {
                            error = true;
                            _logger.Error($"Error creating shipment for order {wddcOrder.OriginalOrderId}", ex);
                        }
                    }
                }
            }
            if (error)
                throw new Exception("error processing task, see log for more details");
        }

        protected void CreateShipment(global::Wddc.Core.Domain.Shipping.Shipment wddcShipment)
        {
            // get order from API
            var wddcOrder = _wddcOrderService.GetOrder(wddcShipment.OrderId);

            if (wddcOrder == null)
                throw new AutomatedDeliveryException($"Api order {wddcShipment.OrderId} not found");

            if (!wddcOrder.OriginalOrderId.HasValue)
                throw new NopException("OriginalOrderId is null");

            // get client vantage order
            var order = _orderService.GetOrderById(wddcOrder.OriginalOrderId.Value);

            SentrySdk.AddBreadcrumb("creating shipment for order: "
                + JsonConvert.SerializeObject(order));

            if (order == null)
                throw new NopException($"ClientVantage order id '{wddcOrder.OriginalOrderId.Value}' not found.");

            var shipments = order
                .Shipments
                .Where(_ => _.TrackingNumber == wddcShipment.TrackingNumber);

            if (shipments.Count() > 1)
                throw new Exception($"tracking number {wddcShipment.TrackingNumber} is not unique");

            var shipment = shipments.FirstOrDefault();

            if (shipment == null) // create
            {
                shipment = new Shipment
                {
                    AdminComment = "created automatically",
                    CreatedOnUtc = DateTime.UtcNow,
                    Order = order,
                    TrackingNumber = wddcShipment.TrackingNumber,
                    OrderId = order.Id,
                    TotalWeight = wddcShipment.Weight
                };

                // insert shipment
                _shipmentService.InsertShipment(shipment);


                // add shipment items
                foreach (var shipmentItem in wddcShipment.ShipmentItems)
                {
                    SentrySdk.AddBreadcrumb("adding shipmentItem: "
                        + JsonConvert.SerializeObject(shipmentItem));

                    // get order item
                    var orderItems = order
                        .OrderItems
                        .Where(_ => _.Product.Sku == shipmentItem.ProductSku);

                    if (orderItems.Count() > 1)
                        throw new Exception($"product sku {shipmentItem.ProductSku} not unique");

                    var orderItem = orderItems.FirstOrDefault();

                    if (orderItem == null)
                        throw new AutomatedDeliveryException($"Error creating shipment: No order item found with sku {shipmentItem.ProductSku}");

                    // add order item to shipment
                    shipment.ShipmentItems.Add(new ShipmentItem
                    {
                        Quantity = shipmentItem.Quantity,
                        OrderItemId = orderItem.Id,
                        Shipment = shipment,
                        ShipmentId = shipment.Id,
                    });
                }

                // update shipment
                _shipmentService.UpdateShipment(shipment);

                // if direct to home, ship it
                if (order.ShippingRateComputationMethodSystemName == "Shipping.Purolator")
                    _orderProcessingService.Ship(shipment, true);

                // update order status through api
                UpdateWddcOrderStatus(wddcOrder.Id);
            }
        }

        protected void UpdateWddcOrderStatus(int wddcOrderId)
        {
            // get api order
            var wddcOrder = _wddcOrderService
                .GetOrder(wddcOrderId);

            if (wddcOrder == null)
                throw new NopException($"Attempting to update wddc order status however wddc order '{wddcOrderId}' not found");

            if (!wddcOrder.OriginalOrderId.HasValue)
                throw new NopException("OriginalOrderId is null");

            // get associated order from client vantage
            var order = _orderService.GetOrderById(wddcOrder.OriginalOrderId.Value);

            if (wddcOrder == null)
                throw new NopException($"Attempting to update wddc order status however order in client vantage with id '{wddcOrder.OriginalOrderId}' not found");

            OrderStatus orderStatus = wddcOrder.OrderStatus;
            if (order.ShippingRateComputationMethodSystemName == "Shipping.Purolator")
            {
                // when direct to home the order process ends when
                // the product has been shipped from WDDC
                if (order.ShippingStatus == ShippingStatus.Shipped)
                    orderStatus = OrderStatus.Completed;
                else if (order.ShippingStatus == ShippingStatus.PartiallyShipped)
                    orderStatus = OrderStatus.PartiallyShipped;
            }
            else if (order.ShippingRateComputationMethodSystemName == "Pickup.PickupInStore")
            {
                // when ship to clinic the order process ends when
                // the product has been received by the customer
                if (order.OrderStatus == Core.Domain.Orders.OrderStatus.Complete ||
                    order.ShippingStatus == ShippingStatus.Delivered ||
                    order.ShippingStatus == ShippingStatus.Shipped)
                    orderStatus = OrderStatus.Completed;
                else if (order.HasItemsToAddToShipment())
                    orderStatus = OrderStatus.PartiallyShipped;
                else
                    orderStatus = OrderStatus.Shipped;
            }
            wddcOrder.OrderStatus = orderStatus;
            // update wddc order status through api
            _wddcOrderService.UpdateOrder(wddcOrder);
        }
    }
}
