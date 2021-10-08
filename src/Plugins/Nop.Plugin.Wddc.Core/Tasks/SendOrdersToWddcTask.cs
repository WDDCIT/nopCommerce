using Newtonsoft.Json;
using Nop.Core.Domain.Catalog;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Stores;
using System;
using System.Linq;
using Wddc.Core.Domain.Orders;
using WddcApiClient.Services.Orders;

namespace Nop.Plugin.Wddc.Core.Tasks
{
    public class SendOrdersToWddcTask : IAutomatedDeliveryTask
    {
        private readonly IOrderService _orderService;
        private readonly IStoreService _storeService;
        private readonly WddcOrderService _wddcOrderService;
        private readonly AutomatedDeliverySettings _automatedDeliverySettings;
        private readonly WddcOrderProcessingService _wddcOrderProcessingService;
        private readonly ILogger _logger;

        public string Name => "SendOrdersToWddc";

        public bool Enabled => true;

        public SendOrdersToWddcTask(IOrderService orderService,
            IStoreService storeService,
            WddcOrderService wddcOrderService,
            AutomatedDeliverySettings automatedDeliverySettings,
            WddcOrderProcessingService wddcOrderProcessingService,
            ILogger logger)
        {
            _orderService = orderService;
            _storeService = storeService;
            _wddcOrderService = wddcOrderService;
            _automatedDeliverySettings = automatedDeliverySettings;
            _wddcOrderProcessingService = wddcOrderProcessingService;
            _logger = logger;
        }

        public void Execute()
        {
            // get orders that don't contain drugs
            var orders = _orderService
                .SearchOrders()
                .Where(o => o.PaymentStatus == Core.Domain.Payments.PaymentStatus.Paid)
                .Where(o => o.OrderStatus == Core.Domain.Orders.OrderStatus.Processing);

            foreach (var order in orders)
            {
                var customerId = _storeService
                    .GetStoreById(order.StoreId)
                    ?.CustomerId;

                if (string.IsNullOrEmpty(customerId))
                    throw new NullReferenceException(nameof(customerId));

                // get order from API
                var listOrderResult = _wddcOrderService
                    .ListOrders(new OrderListOptions
                    {
                        OriginalOrderId = order.Id,
                        CustomerId = customerId,
                    });

                if (listOrderResult == null)
                    throw new NullReferenceException(nameof(listOrderResult));

                if (order.BillingAddress == null)
                    throw new NullReferenceException(nameof(order.BillingAddress));

                if (order.BillingAddress.StateProvince == null)
                    throw new NullReferenceException(nameof(order.BillingAddress.StateProvince));

                if (order.BillingAddress.Country == null)
                    throw new NullReferenceException(nameof(order.BillingAddress.Country));

                if (listOrderResult.Total == 0) // does not exist, create
                {
                    var billingAddress = new Address
                    {
                        Address1 = order.BillingAddress.Address1,
                        Address2 = order.BillingAddress.Address2 ?? string.Empty,
                        StateProvinceAbbreviation = order.BillingAddress.StateProvince.Abbreviation,
                        City = order.BillingAddress.City,
                        CountryTwoLetterIsoCode = order.BillingAddress.Country.TwoLetterIsoCode,
                        Email = order.BillingAddress.Email ?? string.Empty,
                        FirstName = order.BillingAddress.FirstName,
                        LastName = order.BillingAddress.LastName,
                        OriginalAddressId = order.Customer.Id,
                        PhoneNumber = order.BillingAddress.PhoneNumber ?? string.Empty,
                        ZipPostalCode = order.BillingAddress.ZipPostalCode,
                    };

                    Address shippingAddress = null;
                    if (order.ShippingRateComputationMethodSystemName == "Shipping.Purolator")
                    {
                        shippingAddress = new Address
                        {
                            Address1 = order.ShippingAddress?.Address1 ?? order.BillingAddress.Address1,
                            Address2 = order.ShippingAddress?.Address2 ?? order.BillingAddress.Address2 ?? string.Empty,
                            StateProvinceAbbreviation = order.ShippingAddress?.StateProvince.Abbreviation ?? order.BillingAddress.StateProvince.Abbreviation,
                            City = order.ShippingAddress?.City ?? order.BillingAddress.City,
                            CountryTwoLetterIsoCode = order.ShippingAddress?.Country.TwoLetterIsoCode ?? order.BillingAddress.Country.TwoLetterIsoCode,
                            Email = order.ShippingAddress?.Email ?? order.BillingAddress.Email ?? String.Empty,
                            FirstName = order.ShippingAddress?.FirstName ?? order.BillingAddress.FirstName,
                            LastName = order.ShippingAddress?.LastName ?? order.BillingAddress.LastName,
                            OriginalAddressId = order.Customer.Id,
                            PhoneNumber = order.ShippingAddress?.PhoneNumber ?? order.BillingAddress.PhoneNumber ?? String.Empty,
                            ZipPostalCode = order.ShippingAddress?.ZipPostalCode ?? order.BillingAddress.ZipPostalCode,
                        };
                    }

                    ShippingMethod shippingMethod;

                    if (_automatedDeliverySettings.AutomaticallyProcessOrders)
                    {
                        shippingMethod = order.ShippingRateComputationMethodSystemName == "Shipping.Purolator" ?
                            ShippingMethod.BoxAndShipToHome :
                            ShippingMethod.BoxAndShipToClinic;
                    }
                    else
                    {
                        shippingMethod = ShippingMethod.NoShippingRequired;
                    }

                    var processOrderRequest = new ProcessOrderRequest
                    {
                        CustomerId = customerId,
                        OriginalOrderId = order.Id,
                        OrderDateUtc = order.CreatedOnUtc,
                        ShippingAddress = shippingAddress,
                        BillingAddress = billingAddress,
                        OrderTotal = order.OrderTotal,
                        PurchaseOrder = order.Id.ToString().PadLeft(18 - order.Id.ToString().Length, '0'),
                        ShippingMethod = shippingMethod.ToString(),
                        OriginalCustomerId = order.CustomerId,
                        OrderItems = order.OrderItems
                            .Select(_ => new OrderItem
                            {
                                OriginalOrderItemId = _.Id,
                                OriginalProductId = _.ProductId,
                                ProductSku = _.Product.Sku,
                                Quantity = _.Quantity,
                                Price = _.PriceExclTax
                            })
                            .ToList(),
                    };

                    // remove any non wddc products
                    foreach (var item in order.OrderItems.Where(_ => _.Product.WddcSubClassId == 0))
                    {
                        var itemToRemove = processOrderRequest
                            .OrderItems
                            .SingleOrDefault(_ => _.OriginalProductId == item.Id);

                        processOrderRequest.OrderItems.Remove(itemToRemove);
                    }

                    try
                    {
                        Sentry.SentrySdk.AddBreadcrumb($"Processing order: {JsonConvert.SerializeObject(processOrderRequest)}");
                        var result = _wddcOrderProcessingService
                            .ProcessOrder(processOrderRequest);

                        if (!result.Success)
                        {
                            _logger.Error("Error sending order to WDDC", new Exception(string.Join(", ", result.Errors)));
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("Error sending order to WDDC", ex);
                    }
                }
            }
        }
    }
}
