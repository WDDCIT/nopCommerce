using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Discounts;
using Nop.Core.Domain.Media;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Vendors;
using Nop.Core.Infrastructure;
using Nop.Plugin.Wddc.Core.Areas.Admin.Models.Catalog;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Discounts;
using Nop.Services.ExportImport;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Media;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Security;
using Nop.Services.Seo;
using Nop.Services.Shipping;
using Nop.Services.Stores;
using Nop.Services.Vendors;
using Nop.Web.Areas.Admin.Factories;
using Nop.Web.Areas.Admin.Infrastructure.Mapper.Extensions;
using Nop.Web.Areas.Admin.Models.Catalog;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Wddc.Core.Areas.Admin
{
    /// <summary>
    /// <inheritdoc cref="ProductController"/>
    /// </summary>
    public class ProductController : Nop.Web.Areas.Admin.Controllers.ProductController
    {
        private readonly IProductService _productService;
        private readonly ICategoryService _categoryService;
        private readonly IManufacturerService _manufacturerService;
        private readonly IWorkContext _workContext;
        private readonly ILocalizationService _localizationService;
        private readonly IPictureService _pictureService;
        private readonly ICustomerActivityService _customerActivityService;
        private readonly IPermissionService _permissionService;
        private readonly IStoreService _storeService;
        private readonly IVendorService _vendorService;
        private readonly IShippingService _shippingService;
        private readonly IStaticCacheManager _staticCacheManager;
        private readonly IBackInStockSubscriptionService _backInStockSubscriptionService;
        private readonly VendorSettings _vendorSettings;

        /// <summary>
        /// Key for caching
        /// </summary>
        /// <remarks>
        /// {0} : show hidden records?
        /// {1} : product ID
        /// {2} : current customer ID
        /// </remarks>
        private const string PRODUCTCATEGORIES_ALLBYPRODUCTID_KEY = "Nop.productcategory.allbyproductid-{0}-{1}-{2}";

        public ProductController(IProductService productService, IProductTemplateService productTemplateService, ICategoryService categoryService, IManufacturerService manufacturerService, ICustomerService customerService, IUrlRecordService urlRecordService, IWorkContext workContext, ILanguageService languageService, ILocalizationService localizationService, ILocalizedEntityService localizedEntityService, ISpecificationAttributeService specificationAttributeService, IPictureService pictureService, ITaxCategoryService taxCategoryService, IProductTagService productTagService, ICopyProductService copyProductService, IPdfService pdfService, IExportManager exportManager, IImportManager importManager, ICustomerActivityService customerActivityService, IPermissionService permissionService, IAclService aclService, IStoreService storeService, IOrderService orderService, IStoreMappingService storeMappingService, IVendorService vendorService, IDateRangeService dateRangeService, IShippingService shippingService, IShipmentService shipmentService, ICurrencyService currencyService, CurrencySettings currencySettings, IMeasureService measureService, MeasureSettings measureSettings, ICacheManager cacheManager, IDateTimeHelper dateTimeHelper, IDiscountService discountService, IProductAttributeService productAttributeService, IBackInStockSubscriptionService backInStockSubscriptionService, IShoppingCartService shoppingCartService, IProductAttributeFormatter productAttributeFormatter, IProductAttributeParser productAttributeParser, IDownloadService downloadService, ISettingService settingService, TaxSettings taxSettings, VendorSettings vendorSettings) : base(productService, productTemplateService, categoryService, manufacturerService, customerService, urlRecordService, workContext, languageService, localizationService, localizedEntityService, specificationAttributeService, pictureService, taxCategoryService, productTagService, copyProductService, pdfService, exportManager, importManager, customerActivityService, permissionService, aclService, storeService, orderService, storeMappingService, vendorService, dateRangeService, shippingService, shipmentService, currencyService, currencySettings, measureService, measureSettings, cacheManager, dateTimeHelper, discountService, productAttributeService, backInStockSubscriptionService, shoppingCartService, productAttributeFormatter, productAttributeParser, downloadService, settingService, taxSettings, vendorSettings)
        {
            _productService = productService;
            _categoryService = categoryService;
            _manufacturerService = manufacturerService;
            _workContext = workContext;
            _localizationService = localizationService;
            _pictureService = pictureService;
            _customerActivityService = customerActivityService;
            _permissionService = permissionService;
            _storeService = storeService;
            _vendorService = vendorService;
            _shippingService = shippingService;
            _staticCacheManager = cacheManager;
            _backInStockSubscriptionService = backInStockSubscriptionService;
            _vendorSettings = vendorSettings;
        }

        public virtual async Task<IActionResult> List()
        {
            if (!(await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManageProducts)))
                return AccessDeniedView();

            var model = new WddcProductListModel();
            //a vendor should have access only to his products
            model.IsLoggedInAsVendor = _workContext.CurrentVendor != null;
            model.AllowVendorsToImportProducts = _vendorSettings.AllowVendorsToImportProducts;

            //categories
            model.AvailableCategories.Add(new SelectListItem { Text = _localizationService.GetResource("Admin.Common.All"), Value = "0" });
            var categories = SelectListHelper.GetCategoryList(_categoryService, _staticCacheManager, true);
            foreach (var c in categories)
                model.AvailableCategories.Add(c);

            //manufacturers
            model.AvailableManufacturers.Add(new SelectListItem { Text = _localizationService.GetResource("Admin.Common.All"), Value = "0" });
            var manufacturers = SelectListHelper.GetManufacturerList(_manufacturerService, _staticCacheManager, true);
            foreach (var m in manufacturers)
                model.AvailableManufacturers.Add(m);

            //stores
            model.AvailableStores.Add(new SelectListItem { Text = _localizationService.GetResource("Admin.Common.All"), Value = "0" });
            foreach (var s in _storeService.GetAllStores())
                model.AvailableStores.Add(new SelectListItem { Text = s.Name, Value = s.Id.ToString() });

            //warehouses
            model.AvailableWarehouses.Add(new SelectListItem { Text = _localizationService.GetResource("Admin.Common.All"), Value = "0" });
            foreach (var wh in _shippingService.GetAllWarehouses())
                model.AvailableWarehouses.Add(new SelectListItem { Text = wh.Name, Value = wh.Id.ToString() });

            //vendors
            model.AvailableVendors.Add(new SelectListItem { Text = _localizationService.GetResource("Admin.Common.All"), Value = "0" });
            var vendors = SelectListHelper.GetVendorList(_vendorService, _staticCacheManager, true);
            foreach (var v in vendors)
                model.AvailableVendors.Add(v);

            //product types
            model.AvailableProductTypes = ProductType.SimpleProduct.ToSelectList(false).ToList();
            model.AvailableProductTypes.Insert(0, new SelectListItem { Text = _localizationService.GetResource("Admin.Common.All"), Value = "0" });

            model.AvailableOrderByOptions = ProductSortingEnum.CreatedOn.ToSelectList(true).ToList();
            model.SortOrderById = (int)ProductSortingEnum.CreatedOn;

            //"published" property
            //0 - all (according to "ShowHidden" parameter)
            //1 - published only
            //2 - unpublished only
            model.AvailablePublishedOptions.Add(new SelectListItem { Text = _localizationService.GetResource("Admin.Catalog.Products.List.SearchPublished.All"), Value = "0" });
            model.AvailablePublishedOptions.Add(new SelectListItem { Text = _localizationService.GetResource("Admin.Catalog.Products.List.SearchPublished.PublishedOnly"), Value = "1" });
            model.AvailablePublishedOptions.Add(new SelectListItem { Text = _localizationService.GetResource("Admin.Catalog.Products.List.SearchPublished.UnpublishedOnly"), Value = "2" });

            return View("~/Plugins/Wddc.Core/Views/Admin/Product/List.cshtml", model);
        }

        [HttpPost]
        public virtual ActionResult WddcProductList(DataSourceRequest command, WddcProductListModel model)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageProducts))
                return AccessDeniedKendoGridJson();

            //a vendor should have access only to his products
            if (_workContext.CurrentVendor != null)
            {
                model.SearchVendorId = _workContext.CurrentVendor.Id;
            }

            var categoryIds = new List<int> { model.SearchCategoryId };
            //include subcategories
            if (model.SearchIncludeSubCategories && model.SearchCategoryId > 0)
                categoryIds.AddRange(GetChildCategoryIds(model.SearchCategoryId));

            //0 - all (according to "ShowHidden" parameter)
            //1 - published only
            //2 - unpublished only
            bool? overridePublished = null;
            if (model.SearchPublishedId == 1)
                overridePublished = true;
            else if (model.SearchPublishedId == 2)
                overridePublished = false;

            var products = _productService.SearchProducts(
                categoryIds: categoryIds,
                manufacturerId: model.SearchManufacturerId,
                storeId: model.SearchStoreId,
                vendorId: model.SearchVendorId,
                warehouseId: model.SearchWarehouseId,
                productType: model.SearchProductTypeId > 0 ? (ProductType?)model.SearchProductTypeId : null,
                keywords: model.SearchProductName,
                pageIndex: command.Page - 1,
                pageSize: command.PageSize,
                showHidden: true,
                orderBy: (ProductSortingEnum)model.SortOrderById,
                overridePublished: overridePublished
            );
            var gridModel = new DataSourceResult();
            gridModel.Data = products.Select(x =>
            {
                var productModel = x.ToModel();
                //little performance optimization: ensure that "FullDescription" is not returned
                productModel.FullDescription = "";

                //picture
                var defaultProductPicture = _pictureService.GetPicturesByProductId(x.Id, 1).FirstOrDefault();
                productModel.PictureThumbnailUrl = _pictureService.GetPictureUrl(defaultProductPicture, 75, true);
                //product type
                productModel.ProductTypeName = x.ProductType.GetLocalizedEnum(_localizationService, _workContext);
                //friendly stock qantity
                //if a simple product AND "manage inventory" is "Track inventory", then display
                if (x.ProductType == ProductType.SimpleProduct && x.ManageInventoryMethod == ManageInventoryMethod.ManageStock)
                    productModel.StockQuantityStr = x.GetTotalStockQuantity().ToString();
                return productModel;
            });
            gridModel.Total = products.TotalCount;

            return Json(gridModel);
        }

        [NonAction]
        protected virtual void SaveCategoryMappings(Product product, BulkEditProductModel model)
        {
            var existingProductCategories = _categoryService.GetProductCategoriesByProductId(product.Id, true);

            //delete categories
            foreach (var existingProductCategory in existingProductCategories)
                if (!model.SelectedCategoryIds.Contains(existingProductCategory.CategoryId))
                    _categoryService.DeleteProductCategory(existingProductCategory);

            //add categories
            foreach (var categoryId in model.SelectedCategoryIds)
                if (existingProductCategories.FindProductCategory(product.Id, categoryId) == null)
                {
                    //find next display order
                    var displayOrder = 1;
                    var existingCategoryMapping = _categoryService.GetProductCategoriesByCategoryId(categoryId, showHidden: true);
                    if (existingCategoryMapping.Any())
                        displayOrder = existingCategoryMapping.Max(x => x.DisplayOrder) + 1;
                    _categoryService.InsertProductCategory(new ProductCategory
                    {
                        ProductId = product.Id,
                        CategoryId = categoryId,
                        DisplayOrder = displayOrder
                    });
                }

            var pattern = string.Format(PRODUCTCATEGORIES_ALLBYPRODUCTID_KEY, true, product.Id, _workContext.CurrentCustomer.Id);
            _staticCacheManager.RemoveByPattern(pattern);
        }
    }
}
