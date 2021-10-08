using System;

namespace Nop.Plugin.Wddc.Core.Models
{
    public class WebOrderModel
    {
        public int Id { get; set; }
        public string OrderTotal { get; set; }
        public string WebOrderStatus { get; set; }
        public int WebOrderStatusId { get; set; }
        public string CustomerEmail { get; set; }
        public string CustomerFullName { get; set; }
        public DateTime CreatedOn { get; set; }
        public int NopOrderId { get; set; }
    }
}
