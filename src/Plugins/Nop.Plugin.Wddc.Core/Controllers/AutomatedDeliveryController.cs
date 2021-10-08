using Nop.Core;
using Nop.Core.Data;
using Nop.Plugin.Wddc.Core.Models;
using Nop.Services;
using Nop.Services.Catalog;
using Nop.Services.Configuration;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Security;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Kendoui;
using Nop.Web.Framework.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using Wddc.Core.Domain.Orders;
using WddcApiClient.Services.Orders;

namespace Nop.Plugin.Wddc.Core.Controllers
{
    /// <summary>
    /// Plugin controller
    /// </summary>
    public class AutomatedDeliveryController : BasePluginController
    {
        #region fields

        private readonly IPermissionService _permissionService;
        private readonly ILocalizationService _localizationService;
        private readonly IProductService _productService;
        private readonly IDateTimeHelper _dateTimeHelper;
        private readonly ISettingService _settingService;
        private readonly IPriceFormatter _priceFormatter;
        private readonly IStoreContext _storeContext;
        private readonly WddcOrderService _wddcOrderService;
        private readonly AutomatedDeliverySettings _automatedDeliverySettings;
        private readonly DataSettings _dataSettings;
        private readonly ILogger _logger;

        #endregion

        #region ctor

        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="automatedDeliveryLogService"></param>
        /// <param name="automatedDeliveryService"></param>
        /// <param name="wddcOrderService"></param>
        /// <param name="logger"></param>
        public AutomatedDeliveryController(IPermissionService permissionService,
            ILocalizationService localizationService,
            IProductService productService,
            IDateTimeHelper dateTimeHelper,
            ISettingService settingService,
            IPriceFormatter priceFormatter,
            IStoreContext storeContext,
            WddcOrderService wddcOrderService,
            AutomatedDeliverySettings automatedDeliverySettings,
            DataSettings dataSettings,
            ILogger logger)
        {
            _permissionService = permissionService;
            _localizationService = localizationService;
            _productService = productService;
            _dateTimeHelper = dateTimeHelper;
            _settingService = settingService;
            _priceFormatter = priceFormatter;
            _storeContext = storeContext;
            _wddcOrderService = wddcOrderService;
            _automatedDeliverySettings = automatedDeliverySettings;
            _dataSettings = dataSettings;
            _logger = logger;
        }

        #endregion

        #region actions

        [AdminAuthorize]
        public virtual ActionResult Configure()
        {
            var model = new ConfigureModel
            {
                AutomaticallyProcessOrdersWithWddc = _automatedDeliverySettings.AutomaticallyProcessOrders
            };
            return View("~/Plugins/Wddc.AutomatedDelivery/Views/Configure.cshtml", model);
        }

        [HttpPost]
        [AdminAuthorize]
        public virtual ActionResult Configure(ConfigureModel model)
        {
            if (ModelState.IsValid)
            {
                _automatedDeliverySettings.AutomaticallyProcessOrders
                    = model.AutomaticallyProcessOrdersWithWddc;

                _settingService.SaveSetting(_automatedDeliverySettings);

                SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));
            }
            return Configure();
        }

        [AdminAuthorize]
        public virtual ActionResult List(
            [ModelBinder(typeof(CommaSeparatedModelBinder))] List<string> orderStatusIds = null,
            [ModelBinder(typeof(CommaSeparatedModelBinder))] List<string> shippingStatusIds = null)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageOrders))
                return AccessDeniedView();

            // web order statuses
            var model = new AutomatedOrderListModel
            {
                AvailableOrderStatuses = OrderStatus.Completed.ToSelectList(false).ToList()
            };

            model.AvailableOrderStatuses.Insert(0, new SelectListItem
            { Text = _localizationService.GetResource("Admin.Common.All"), Value = "0", Selected = true });
            if (orderStatusIds != null && orderStatusIds.Any())
            {
                foreach (var item in model.AvailableOrderStatuses.Where(os => orderStatusIds.Contains(os.Value)))
                    item.Selected = true;
                model.AvailableOrderStatuses.First().Selected = false;
            }

            return View("~/Plugins/Wddc.AutomatedDelivery/Views/AutomatedDelivery/List.cshtml", model);
        }

        [HttpPost]
        public virtual ActionResult WebOrderList(DataSourceRequest command, AutomatedOrderListModel model)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageOrders))
                return AccessDeniedKendoGridJson();

            DateTime? startDateValue = (model.StartDate == null) ? null
                            : (DateTime?)_dateTimeHelper.ConvertToUtcTime(model.StartDate.Value, _dateTimeHelper.CurrentTimeZone);

            DateTime? endDateValue = (model.EndDate == null) ? null
                            : (DateTime?)_dateTimeHelper.ConvertToUtcTime(model.EndDate.Value, _dateTimeHelper.CurrentTimeZone).AddDays(1);

            var product = _productService.GetProductById(model.ProductId);
            try
            {
                //load orders
                var response = _wddcOrderService.ListOrders(new OrderListOptions
                {
                    CustomerId = _storeContext.CurrentStore.CustomerId,
                    BillingEmail = model.BillingEmail,
                    EndDateUtc = model.EndDate?.ToUniversalTime(),
                    StartDateUtc = model.StartDate?.ToUniversalTime(),
                    ProductSku = product?.Sku,
                    Page = command.Page,
                    PageSize = command.PageSize
                });

                var gridModel = new DataSourceResult
                {
                    Data = response.Results.Select(x =>
                    {
                        return new WebOrderModel
                        {
                            Id = x.Id,
                            OrderTotal = _priceFormatter.FormatPrice(x.OrderTotal ?? 0, true, false),
                            WebOrderStatus = x.OrderStatus.ToString(),
                            WebOrderStatusId = x.OrderStatusId,
                            CustomerEmail = x.BillingAddress.Email,
                            CustomerFullName = string.Format("{0} {1}", x.BillingAddress.FirstName, x.BillingAddress.LastName),
                            CreatedOn = _dateTimeHelper.ConvertToUserTime(x.CreatedUtc, DateTimeKind.Utc),
                            NopOrderId = x.OriginalOrderId ?? 0
                        };
                    }),
                    Total = response.Total
                };

                return Json(gridModel);
            }
            catch (Exception ex)
            {
                _logger.Error(ex.Message, ex);
                ErrorNotification("Error loading grid");
                return Json(null);
            }
        }

        public virtual ActionResult ProductSearchAutoComplete(string term)
        {
            const int searchTermMinimumLength = 3;
            if (String.IsNullOrWhiteSpace(term) || term.Length < searchTermMinimumLength)
                return Content("");

            //products
            const int productNumber = 15;
            var products = _productService.SearchProducts(
                keywords: term,
                pageSize: productNumber,
                showHidden: true);

            var result = (from p in products
                          select new
                          {
                              label = p.Name,
                              productid = p.Id
                          })
                          .ToList();
            return Json(result, JsonRequestBehavior.AllowGet);
        }

        #endregion
    }
}
