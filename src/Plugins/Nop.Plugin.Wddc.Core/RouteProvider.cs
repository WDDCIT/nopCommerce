using Nop.Web.Framework.Mvc.Routes;
using System.Web.Mvc;
using System.Web.Routing;

namespace Nop.Plugin.Wddc.Core
{
    /// <summary>
    /// Plugin route provider
    /// </summary>
    public partial class RouteProvider : IRouteProvider
    {
        /// <summary>
        /// Register routes
        /// </summary>
        /// <param name="routes"></param>
        public void RegisterRoutes(RouteCollection routes)
        {
            // route for handling stripe webhook events
            routes.MapRoute("Plugin.Wddc.AutomatedDelivery.WebhookEventHandler",
                 "Plugins/AutomatedDelivery/WebhookEventHandler",
                 new { controller = "AutomatedDelivery", action = "WebhookEventHandler" },
                 new[] { "Nop.Plugin.Wddc.Core.Controllers" }
            );

            // route for listing automated deliveries
            routes.MapRoute("Plugin.Wddc.AutomatedDelivery.List",
                 "AutomatedDelivery/List",
                 new { controller = "AutomatedDelivery", action = "List" },
                 new[] { "Nop.Plugin.Wddc.Core.Controllers" }
            );
        }

        /// <summary>
        /// Gets or sets the priority of the plugin routes
        /// </summary>
        public int Priority
        {
            get
            {
                return 0;
            }
        }
    }
}
