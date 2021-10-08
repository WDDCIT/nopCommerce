using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace Nop.Plugin.Wddc.Core.Models
{
    public class AutomatedOrderListModel
    {
        public string BillingEmail { get; set; }

        public int ProductId { get; set; }

        [UIHint("DateNullable")]
        public DateTime? StartDate { get; set; }

        [UIHint("DateNullable")]
        public DateTime? EndDate { get; set; }

        public string BillingLastName { get; set; }

        [UIHint("MultiSelect")]
        public List<int> OrderStatusIds { get; set; }

        public List<SelectListItem> AvailableOrderStatuses { get; set; }
    }
}
