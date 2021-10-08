using Nop.Web.Framework;

namespace Nop.Plugin.Wddc.Core.Models
{
    public class ConfigureModel
    {
        /// <summary>
        /// Selected class category id
        /// </summary>
        [NopResourceDisplayName("Plugins.Wddc.AutomatedDelivery.Fields.AutomaticallyProcessOrdersWithWddc")]
        public bool AutomaticallyProcessOrdersWithWddc { get; set; }
    }
}
