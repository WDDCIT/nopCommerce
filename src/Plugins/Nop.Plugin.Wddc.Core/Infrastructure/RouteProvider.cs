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
            throw new System.NotImplementedException();
        }
        public int Priority => -1;
    }
}
