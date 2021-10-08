using Nop.Core.Configuration;

namespace Nop.Plugin.Wddc.Core
{
    public class AutomatedDeliverySettings : ISettings
    {
        /// <summary>
        /// Whether or not to automatically place orders with WDDC
        /// </summary>
        public bool AutomaticallyProcessOrders { get; set; }
    }
}
