using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Mvc.ModelBinding;
using Nop.Web.Framework.Models;
using Nop.Web.Areas.Admin.Models.Catalog;

namespace Nop.Plugin.Wddc.Core.Areas.Admin.Models.Catalog
{
    /// <summary>
    /// Represents a product list model
    /// </summary>
    public partial record WddcProductListModel : BasePagedListModel<ProductModel>
    {
        [NopResourceDisplayName("Catalog.OrderBy")]
        public int SortOrderById { get; set; }
        public IList<SelectListItem> AvailableOrderByOptions { get; set; }

    }
}