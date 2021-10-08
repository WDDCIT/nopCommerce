using Nop.Core.Domain.Tasks;
using Nop.Core.Infrastructure;
using Nop.Core.Plugins;
using Nop.Plugin.Wddc.Core.Tasks;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Security;
using Nop.Services.Shipping.Tracking;
using Nop.Services.Tasks;
using Nop.Web.Framework.Menu;
using System.Linq;
using System.Web.Routing;

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
            AutomatedDeliverySettings automatedDeliverySettings)
        {
            _addressService = addressService;
            _countryService = countryService;
            _localizationService = localizationService;
            _stateProvinceService = stateProvinceService;
            _settingService = settingService;
            _scheduleTaskService = scheduleTaskService;
            _permissionService = permissionService;
            _automatedDeliverySettings = automatedDeliverySettings;
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

        #region Utilities

        private void UpdateStringResources()
        {
            this.AddOrUpdatePluginLocaleResource("enums.nop.core.domain.shipping.shippingstatus.notyetshipped", "Not yet shipped");
            this.AddOrUpdatePluginLocaleResource("admin.salesreport.incomplete.totalnotshippedorders", "Total not yet shipped orders");
            this.AddOrUpdatePluginLocaleResource("admin.orders.list.shippingstatus.hint", "Search by a specific shipping statuses e.g. Not yet shipped.");
            this.AddOrUpdatePluginLocaleResource("admin.customers.reports.bestby.shippingstatus.hint", "Search by a specific shipping statuses e.g. Not yet shipped.");
            this.AddOrUpdatePluginLocaleResource("admin.affiliates.orders.shippingstatus.hint", "Search by a specific shipping statuses e.g. Not yet shipped.");
            this.AddOrUpdatePluginLocaleResource("admin.orders.shipments.shipselected", "Set as shipped (selected)");
        }

        private void RevertStringResources()
        {
            this.AddOrUpdatePluginLocaleResource("enums.nop.core.domain.shipping.shippingstatus.notyetshipped", "Not yet shipped");
            this.AddOrUpdatePluginLocaleResource("admin.salesreport.incomplete.totalnotshippedorders", "Total not yet shipped orders");
            this.AddOrUpdatePluginLocaleResource("admin.orders.list.shippingstatus.hint", "Search by a specific shipping statuses e.g. Not yet shipped.");
            this.AddOrUpdatePluginLocaleResource("admin.customers.reports.bestby.shippingstatus.hint", "Search by a specific shipping statuses e.g. Not yet shipped.");
            this.AddOrUpdatePluginLocaleResource("admin.affiliates.orders.shippingstatus.hint", "Search by a specific shipping statuses e.g. Not yet shipped.");
            this.AddOrUpdatePluginLocaleResource("admin.orders.shipments.shipselected", "Set as shipped (selected)");
        }

        #endregion

        #region Methods

        /// <summary>
        /// Gets a route for provider configuration
        /// </summary>
        /// <param name="actionName">Action name</param>
        /// <param name="controllerName">Controller name</param>
        /// <param name="routeValues">Route values</param>
        public void GetConfigurationRoute(out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "Configure";
            controllerName = "AutomatedDelivery";
            routeValues = new RouteValueDictionary { { "Namespaces", "Nop.Plugin.Wddc.Core.Controllers" }, { "area", null } };
        }

        /// <summary>
        /// Install the plugin
        /// </summary>
        public override void Install()
        {
            UpdateStringResources();

            this.AddOrUpdatePluginLocaleResource("nop.plugin.wddc.automateddelivery.settings.testmode", "Test mode");
            this.AddOrUpdatePluginLocaleResource("Plugins.Wddc.AutomatedDelivery.Fields.AutomaticallyProcessOrdersWithWddc", "Automate orders");
            this.AddOrUpdatePluginLocaleResource("Plugins.Wddc.AutomatedDelivery.Fields.AutomaticallyProcessOrdersWithWddc.Hint", "Check to automatically send orders to WDDC, uncheck for self-managed installs");

            // get installation setting
            var selfManagedString = _localizationService
                .GetLocaleStringResourceByName("Plugins.Wddc.AutomatedDelivery.IsSelfManaged")
                ?.ResourceValue;

            bool.TryParse(selfManagedString, out bool selfManaged);

            var settings = new AutomatedDeliverySettings
            {
                AutomaticallyProcessOrders = !selfManaged
            };
            _settingService.SaveSetting(settings);

            // find all wddc migration tasks
            var wddcTasks = new AppDomainTypeFinder()
                .FindClassesOfType<IAutomatedDeliveryTask>();

            foreach (var type in wddcTasks)
            {
                _scheduleTaskService.InsertTask(new ScheduleTask
                {
                    Enabled = true,
                    Name = type.Name,
                    Seconds = 3600,
                    StopOnError = false,
                    Type = $"{type}, {typeof(AutomatedDeliveryProvider).Namespace}",
                });
            }
            base.Install();
        }

        /// <summary>
        /// Uninstall the plugin
        /// </summary>
        public override void Uninstall()
        {
            RevertStringResources();

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

        #endregion
    }
}
