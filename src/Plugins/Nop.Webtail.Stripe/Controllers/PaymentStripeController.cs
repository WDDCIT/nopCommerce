using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Services;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;
using Nop.WebTail.Stripe.Extensions;
using Nop.WebTail.Stripe.Models;

namespace Nop.WebTail.Stripe.Controllers
{
    [AuthorizeAdmin]
    [Area(AreaNames.Admin)]
    public class PaymentStripeController : BasePaymentController
    {
        private readonly ILocalizationService _localizationService;
        private readonly IPermissionService _permissionService;
        private readonly ISettingService _settingService;
        private readonly INotificationService _notificationService;

        private readonly StripePaymentSettings _stripePaymentSettings;

        public PaymentStripeController(ILocalizationService localizationService,
            IPermissionService permissionService,
            ISettingService settingService,
            INotificationService notificationService,
            StripePaymentSettings stripePaymentSettings)
        {
            _localizationService = localizationService;
            _permissionService = permissionService;
            _settingService = settingService;
            _notificationService = notificationService;
            _stripePaymentSettings = stripePaymentSettings;
        }
        
        public async Task<IActionResult> ConfigureAsync()
        {
            //whether user has the authority
            if (!(await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePaymentMethods)))
                return AccessDeniedView();

            //prepare model
            var model = new ConfigurationModel
            {
                LivePublishableKey = _stripePaymentSettings.LivePublishableKey,
                LiveSecretKey = _stripePaymentSettings.LiveSecretKey,
                TestPublishableKey = _stripePaymentSettings.TestPublishableKey,
                TestSecretKey = _stripePaymentSettings.TestSecretKey,
                UseSandbox = _stripePaymentSettings.UseSandbox,
                AdditionalFee = _stripePaymentSettings.AdditionalFee,
                AdditionalFeePercentage = _stripePaymentSettings.AdditionalFeePercentage,
                TransactionModeId = (int)_stripePaymentSettings.TransactionMode,
                TransactionModes = await _stripePaymentSettings.TransactionMode.ToSelectListAsync(),
            };

            return View("~/Plugins/WebTail.Stripe/Views/Configure.cshtml", model);
        }

        [HttpPost]
        [AutoValidateAntiforgeryToken]
        public async Task<IActionResult> Configure(ConfigurationModel model)
        {
            if (!(await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePaymentMethods)))
                return AccessDeniedView();

            if (!ModelState.IsValid)
                return await ConfigureAsync();

            _stripePaymentSettings.TransactionMode = (TransactionMode)model.TransactionModeId;
            _stripePaymentSettings.AdditionalFee = model.AdditionalFee;
            _stripePaymentSettings.UseSandbox = model.UseSandbox;
            _stripePaymentSettings.AdditionalFeePercentage = model.AdditionalFeePercentage;
            _stripePaymentSettings.LivePublishableKey = model.LivePublishableKey;
            _stripePaymentSettings.LiveSecretKey = model.LiveSecretKey;
            _stripePaymentSettings.TestPublishableKey = model.TestPublishableKey;
            _stripePaymentSettings.TestSecretKey = model.TestSecretKey;

            if (_stripePaymentSettings.TryConnect())
            {
                await _settingService.SaveSettingAsync(_stripePaymentSettings);
                _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));
            }
            else
            {
                _notificationService.ErrorNotification("Cannot connect to stripe using provided credentials.");
            }

            return await ConfigureAsync();

        }
    }
}
