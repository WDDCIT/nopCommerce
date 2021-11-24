using System.Collections.Generic;
using System.Threading.Tasks;
using Nop.Core;
using Nop.Core.Domain.Cms;
using Nop.Core.Domain.Tasks;
using Nop.Core.Infrastructure;
using Nop.Plugin.Wddc.Core.Tasks;
using Nop.Services.Cms;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Plugins;
using Nop.Services.Security;
using Nop.Services.Shipping.Tracking;
using Nop.Services.Stores;
using Nop.Services.Tasks;
using Nop.Web.Framework.Infrastructure;
using Nop.Web.Framework.Menu;
using Task = System.Threading.Tasks.Task;

namespace Nop.Plugin.Wddc.Core
{
    /// <summary>
    /// Plugin provider
    /// </summary>
    public class AutomatedDeliveryProvider : BasePlugin, IMiscPlugin, IAdminMenuPlugin
    {
        #region Fields

        private readonly IAddressService _addressService;
        private readonly ICountryService _countryService;
        private readonly ILocalizationService _localizationService;
        private readonly IStateProvinceService _stateProvinceService;
        private readonly ISettingService _settingService;
        private readonly IScheduleTaskService _scheduleTaskService;
        private readonly IPermissionService _permissionService;
        private readonly AutomatedDeliverySettings _automatedDeliverySettings;
        private readonly ILanguageService _languageService;
        private readonly IWebHelper _webHelper;

        #endregion

        #region Ctor

        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="addressService"></param>
        /// <param name="countryService"></param>
        /// <param name="localizationService"></param>
        /// <param name="stateProvinceService"></param>
        /// <param name="automatedDeliveryObjectContext"></param>
        /// <param name="webOrderingObjectContext"></param>
        /// <param name="settingService"></param>
        public AutomatedDeliveryProvider(IAddressService addressService,
            ICountryService countryService,
            ILocalizationService localizationService,
            IStateProvinceService stateProvinceService,
            ISettingService settingService,
            IScheduleTaskService scheduleTaskService,
            IPermissionService permissionService,
            AutomatedDeliverySettings automatedDeliverySettings, 
            ILanguageService languageService,
            IWebHelper webHelper)
        {
            _addressService = addressService;
            _countryService = countryService;
            _localizationService = localizationService;
            _stateProvinceService = stateProvinceService;
            _settingService = settingService;
            _scheduleTaskService = scheduleTaskService;
            _permissionService = permissionService;
            _automatedDeliverySettings = automatedDeliverySettings;
            _languageService = languageService;
            _webHelper = webHelper;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets a shipment tracker
        /// </summary>
        public IShipmentTracker ShipmentTracker
        {
            get { return null; }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Gets a configuration page URL
        /// </summary>
        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation()}Admin/WddcCore/Configure";
        }

        public async Task RevertLocaleResources()
        {

            await _localizationService.AddLocaleResourceAsync(new Dictionary<string, string>
            {
                ["Enums.Nop.Core.Domain.Shipping.ShippingStatus.NotYetShipped"] = "Not yet shipped",
                ["Admin.SalesReport.Incomplete.TotalNotShippedOrders"] = "Total not yet shipped orders",
                ["Admin.Orders.List.ShippingStatus.Hint"] = "Search by a specific shipping statuses e.g. Not yet shipped.",
                ["Admin.Customers.Reports.BestBy.ShippingStatus.Hint"] = "Search by a specific shipping statuses e.g. Not yet shipped.", // seems to be a dupe?
                ["Admin.Affiliates.Orders.ShippingStatus.Hint"] = "Search by a specific shipping statuses e.g. Not yet shipped.", // seems to be a dupe?
                ["Admin.Orders.Shipments.ShipSelected"] = "Set as shipped (selected)",
            });

        }

        /// <summary>
        /// Install the plugin
        /// </summary>
        public override async Task InstallAsync()
        {
            // settings
            var selfManagedRes = await _localizationService.GetLocaleStringResourceByNameAsync($"Plugins.Wddc.AutomatedDelivery.IsSelfManaged", 1, false);



            bool.TryParse(selfManagedRes.ResourceValue, out bool selfManaged);

            await _settingService.SaveSettingAsync(new AutomatedDeliverySettings
            {
                AutomaticallyProcessOrders = !selfManaged
            });

            /* first 3 belong in AutomatedDelivery, rest do not seem to be an actual change from default.
            // locales
            await _localizationService.AddLocaleResourceAsync(new Dictionary<string, string>
            {
                ["Nop.Plugin.Wddc.AutomatedDelivery.Settings.TestMode"] = "Test mode",
                ["Plugins.Wddc.AutomatedDelivery.Fields.AutomaticallyProcessOrdersWithWddc"] = "Automate orders",
                ["Plugins.Wddc.AutomatedDelivery.Fields.AutomaticallyProcessOrdersWithWddc.Hint"] = "Check to automatically send orders to WDDC, uncheck for self-managed installs",
                ["Enums.Nop.Core.Domain.Shipping.ShippingStatus.NotYetShipped"] = "Not yet shipped",
                ["Admin.SalesReport.Incomplete.TotalNotShippedOrders"] = "Total not yet shipped orders",
                ["Admin.Orders.List.ShippingStatus.Hint"] = "Search by a specific shipping statuses e.g. Not yet shipped.",
                ["Admin.Customers.Reports.BestBy.ShippingStatus.Hint"] = "Search by a specific shipping statuses e.g. Not yet shipped.", // seems to be a dupe?
                ["Admin.Affiliates.Orders.ShippingStatus.Hint"] = "Search by a specific shipping statuses e.g. Not yet shipped.", // seems to be a dupe?
                ["Admin.Orders.Shipments.ShipSelected"] = "Set as shipped (selected)",
            });
            */
            // get installation setting


            // find all wddc migration tasks
            var wddcTasks = new AppDomainTypeFinder()
                .FindClassesOfType<IAutomatedDeliveryTask>();

            foreach (var type in wddcTasks)
            {
                await _scheduleTaskService.InsertTaskAsync(new ScheduleTask
                {
                    Enabled = true,
                    Name = type.Name,
                    Seconds = 3600,
                    StopOnError = false,
                    Type = $"{type}, {typeof(AutomatedDeliveryProvider).Namespace}",
                });
            }
            await base.InstallAsync();
        }

        /// <summary>
        /// Uninstall the plugin
        /// </summary>
        public override async Task Uninstall()
        {
            //settings
            if (_widgetSettings.ActiveWidgetSystemNames.Contains(SendinblueDefaults.SystemName))
            {
                _widgetSettings.ActiveWidgetSystemNames.Remove(SendinblueDefaults.SystemName);
                await _settingService.SaveSettingAsync(_widgetSettings);
            }
            await _settingService.DeleteSettingAsync<SendinblueSettings>();

            //generic attributes
            foreach (var store in await _storeService.GetAllStoresAsync())
            {
                var messageTemplates = await _messageTemplateService.GetAllMessageTemplatesAsync(store.Id);
                foreach (var messageTemplate in messageTemplates)
                {
                    await _genericAttributeService.SaveAttributeAsync<int?>(messageTemplate, SendinblueDefaults.TemplateIdAttribute, null);
                }
            }


            await DeleteAttributesAsync();

            _settingService.DeleteSetting<AutomatedDeliverySettings>();
            this.DeletePluginLocaleResource("admin.orders.shipments.shipselected");
            var finder = new AppDomainTypeFinder();
            var wddcTasksTypes = finder.FindClassesOfType<IAutomatedDeliveryTask>();
            var tasks = _scheduleTaskService.GetAllTasks(true);

            foreach (var type in wddcTasksTypes)
            {
                var typeName = string.Format("{0}, {1}",
                        type,
                        typeof(AutomatedDeliveryProvider).Namespace);
                var tasksToDelete = tasks.Where(t => t.Type == typeName);
                foreach (var task in tasksToDelete)
                    if (task != null)
                        _scheduleTaskService.DeleteTask(task);
            }
            base.Uninstall();
        }

        public void ManageSiteMap(SiteMapNode rootNode)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageOrders))
                return;

            // adds the following nodes to the admin page
            var pluginNode = new SiteMapNode()
            {
                SystemName = "Wddc.AutomatedDelivery",
                Title = "Wddc Order Status",
                Visible = true,
                ControllerName = "AutomatedDelivery",
                ActionName = "List",
                RouteValues = new RouteValueDictionary() { { "area", null } },
                IconClass = "fa-clipboard",
            };
            rootNode.ChildNodes.Add(pluginNode);
        }

        public System.Threading.Tasks.Task ManageSiteMapAsync(SiteMapNode rootNode)
        {
            throw new System.NotImplementedException();
        }

        #endregion
    }
}
