using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Wddc.Core.Infrastructure
{
    public partial class RouteProvider : IRouteProvider
    {
        public void RegisterRoutes(IEndpointRouteBuilder endpointRouteBuilder)
        {
            //PDT
            endpointRouteBuilder.MapControllerRoute("Plugin.Wddc.Core.", "Plugins/PaymentPayPalStandard/PDTHandler",
                            new { controller = "PaymentPayPalStandard", action = "PDTHandler" });
        }
        public int Priority => -1;
    }
}
