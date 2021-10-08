using Nop.Core;
using System;
using Wddc.Core.Domain.Orders;

namespace Nop.Plugin.Wddc.Core.Domain
{
    /// <summary>
    /// Log
    /// </summary>
    public class AutomatedDeliveryLog : BaseEntity
    {
        /// <summary>
        /// Gets or sets the wddc web order id
        /// </summary>
        public int WebOrderId { get; set; }

        /// <summary>
        /// Get or sets the status of the web order
        /// </summary>
        public OrderStatus WebOrderStatus { get; set; }

        /// <summary>
        /// Created time
        /// </summary>
        public DateTime CreatedUtc { get; set; }
    }
}
