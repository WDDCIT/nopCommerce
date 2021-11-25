using Microsoft.AspNetCore.Http;
using Nop.Core;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Infrastructure;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Payments;
using Nop.Services.Plugins;
using Nop.Services.Stores;
using Nop.WebTail.Stripe.Extensions;
using Nop.WebTail.Stripe.Models;
using Nop.WebTail.Stripe.Validators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using stripe = Stripe;

namespace Nop.WebTail.Stripe
{
    public class StripePaymentPlugin : BasePlugin, IPaymentMethod
    {
        private readonly StripePaymentSettings _stripePaymentSettings;
        private readonly CurrencySettings _currencySettings;

        private readonly IGenericAttributeService _genericAttributeService;
        private readonly IStateProvinceService _stateProvinceService;
        private readonly ILocalizationService _localizationService;
        private readonly ICurrencyService _currencyService;
        private readonly ICustomerService _customerService;        
        private readonly ICountryService _countryService;
        private readonly IPaymentService _paymentService;
        private readonly ISettingService _settingService;
        private readonly IStoreService _storeService;
        private readonly IWebHelper _webHelper;
        private readonly ILogger _logger;

        public StripePaymentPlugin(ILocalizationService localizationService, 
                                   IGenericAttributeService genericAttributeService,
                                   ICurrencyService currencyService,
                                   ICustomerService customerService,
                                   IStateProvinceService stateProvinceService,
                                   ICountryService countryService,
                                   IStoreService storeService,
                                   ISettingService settingService, 
                                   IPaymentService paymentService,
                                   IWebHelper webHelper, 
                                   ILogger logger,
                                   StripePaymentSettings stripePaymentSettings,
                                   CurrencySettings currencySettings)
        {
            _localizationService = localizationService;
            _genericAttributeService = genericAttributeService;
            _currencyService = currencyService;
            _customerService = customerService;
            _stateProvinceService = stateProvinceService;
            _countryService = countryService;
            _webHelper = webHelper;
            _storeService = storeService;
            _settingService = settingService;
            _paymentService = paymentService;
            _stripePaymentSettings = stripePaymentSettings;
            _currencySettings = currencySettings;
            _logger = logger;
        }

        public bool SupportCapture => true;

        public bool SupportPartiallyRefund => true;

        public bool SupportRefund => true;

        public bool SupportVoid => false;

        public RecurringPaymentType RecurringPaymentType => RecurringPaymentType.Manual;

        public PaymentMethodType PaymentMethodType => PaymentMethodType.Standard;

        public bool SkipPaymentInfo => false;

        public async Task<string> PaymentMethodDescriptionAsync()
        { 
            return await _localizationService.GetResourceAsync("WebTail.Payments.Stripe.PaymentMethodDescription");
        }

        /// <summary>
        /// Gets a configuration page URL
        /// </summary>
        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation()}Admin/{StripePaymentDefaults.ControllerName}/Configure";
        }

        public CancelRecurringPaymentResult CancelRecurringPaymentAsync(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            if (cancelPaymentRequest == null)
                throw new ArgumentException(nameof(cancelPaymentRequest));

            //always success
            return new CancelRecurringPaymentResult();
        }

        public bool CanRePostProcessPaymentAsync(Core.Domain.Orders.Order order)
        {
            if (order == null)
                throw new ArgumentNullException(nameof(order));

            //it's not a redirection payment method. So we always return false
            return false;
        }

        public async Task<CapturePaymentResult> CaptureAsync(CapturePaymentRequest capturePaymentRequest)
        {
            if (capturePaymentRequest == null)
                throw new ArgumentNullException(nameof(capturePaymentRequest));

            var currentStore = await EngineContext.Current.Resolve<IStoreContext>().GetCurrentStoreAsync();

            stripe.Charge charge = capturePaymentRequest.CreateCapture(_stripePaymentSettings, currentStore);
            
            if (charge.GetStatus() == StripeChargeStatus.Succeeded)
            {
                //successfully captured
                return new CapturePaymentResult
                {
                    NewPaymentStatus = PaymentStatus.Paid,
                    CaptureTransactionId = charge.Id
                };
            }
            else
            {
                //successfully captured
                return new CapturePaymentResult
                {
                    Errors = new List<string>(new [] { $"An error occured attempting to capture charge {charge.Id}." }),
                    NewPaymentStatus = PaymentStatus.Authorized,
                    CaptureTransactionId = charge.Id
                };
            }

            
            

        }

        public async Task<decimal> GetAdditionalHandlingFeeAsync(IList<ShoppingCartItem> cart)
        {
            var result = await _paymentService.CalculateAdditionalFeeAsync(cart, _stripePaymentSettings.AdditionalFee, _stripePaymentSettings.AdditionalFeePercentage);

            return result;
        }

        public ProcessPaymentRequest GetPaymentInfo(IFormCollection form)
        {
            return new ProcessPaymentRequest()
            {
                CreditCardType = form["CreditCardType"],
                CreditCardName = form["CardholderName"],
                CreditCardNumber = form["CardNumber"],
                CreditCardExpireMonth = int.Parse(form["ExpireMonth"]),
                CreditCardExpireYear = int.Parse(form["ExpireYear"]),
                CreditCardCvv2 = form["CardCode"]
            };
        }

        public string GetPublicViewComponentName()
        {
            return StripePaymentDefaults.ViewComponentName;
        }

        public bool HidePaymentMethodAsync(IList<ShoppingCartItem> cart)
        {

            bool dataMissing = string.IsNullOrEmpty(_stripePaymentSettings.LivePublishableKey) ||
                               string.IsNullOrEmpty(_stripePaymentSettings.LiveSecretKey) ||
                               string.IsNullOrEmpty(_stripePaymentSettings.TestPublishableKey) ||
                               string.IsNullOrEmpty(_stripePaymentSettings.TestSecretKey);
            
            return dataMissing;
        }

        public void PostProcessPayment(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            
        }


        public async Task<RefundPaymentResult> RefundAsync(RefundPaymentRequest refundPaymentRequest)
        {
            var refund = refundPaymentRequest.CreateRefund(_stripePaymentSettings, _currencySettings, _currencyService);

            if (refund.GetStatus() != StripeRefundStatus.Succeeded)
            {
                return new RefundPaymentResult { Errors = new[] { $"Refund is {refund.Status}" }.ToList() };
            }
            else
            {
                return new RefundPaymentResult
                {
                    NewPaymentStatus = refundPaymentRequest.IsPartialRefund ? PaymentStatus.PartiallyRefunded : PaymentStatus.Refunded
                };
            }
            
        }

        public IList<string> ValidatePaymentForm(IFormCollection form)
        {
            var warnings = new List<string>();

            //validate
            var validator = new PaymentInfoValidator(_localizationService);
            var model = new PaymentInfoModel
            {
                CardholderName = form["CardholderName"],
                CardNumber = form["CardNumber"],
                CardCode = form["CardCode"],
                ExpireMonth = form["ExpireMonth"],
                ExpireYear = form["ExpireYear"]
            };
            var validationResult = validator.Validate(model);
            if (!validationResult.IsValid)
                warnings.AddRange(validationResult.Errors.Select(error => error.ErrorMessage));

            return warnings;
        }

        public VoidPaymentResult Void(VoidPaymentRequest voidPaymentRequest)
        {
            throw new NotImplementedException();
        }

        public override async Task InstallAsync()
        {
            //settings
            await _settingService.SaveSettingAsync(new StripePaymentSettings
            {
                UseSandbox = true,
                AdditionalFee = 0,
                AdditionalFeePercentage = false,
                LivePublishableKey = string.Empty,
                LiveSecretKey = string.Empty,
                TestPublishableKey = string.Empty,
                TestSecretKey = string.Empty,
            });
            await _localizationService.AddLocaleResourceAsync(new Dictionary<string, string>
            {
                ["Webtail.Payments.Stripe.Instructions"] = @"
                <p>
                    For plugin configuration follow these steps:<br />
                    <br />
                    1. You will need a Stripe Merchant account. If you don't already have one, you can sign up here: <a href=""https://dashboard.stripe.com/register"" target=""_blank"">https://dashboard.stripe.com/register</a><br />
                    <em>Important: Your merchant account must be approved by Stripe prior to you be able to cash out payments.</em><br />
                    2. Sign in to your Stripe Developer Portal at <a href=""https://dashboard.stripe.com/login"" target=""_blank"">https://dashboard.stripe.com/login</a>; use the same sign in credentials as your merchant account.<br />
                    3. Use the API keys provided at <a href=""https://dashboard.stripe.com/account/apikeys"" target=""_blank"">https://dashboard.stripe.com/account/apikeys</a> to configure the account.
                    <br />
                </p>",

                ["WebTail.Payments.Stripe.Fields.UseSandbox"] = "Use sandbox",
                ["WebTail.Payments.Stripe.Fields.UseSandbox.Hint"] = "Determine whether to use sandbox credentials.",
                ["WebTail.Payments.Stripe.Fields.TransactionMode"] = "Transaction mode",
                ["WebTail.Payments.Stripe.Fields.TransactionMode.Hint"] = "Choose the transaction mode.",

                ["WebTail.Payments.Stripe.Fields.LiveSecretKey"] = "Live Secret Key",
                ["WebTail.Payments.Stripe.Fields.LivePublishableKey"] = "Live Publishable Key",
                ["WebTail.Payments.Stripe.Fields.TestSecretKey"] = "Test Secret Key",
                ["WebTail.Payments.Stripe.Fields.TestPublishableKey"] = "Test Publishable Key",

                ["WebTail.Payments.Stripe.Fields.AdditionalFee"] = "Additional Fee",
                ["WebTail.Payments.Stripe.Fields.AdditionalFeePercentage"] = "Is Fee Percentage",
                ["WebTail.Payments.Stripe.PaymentMethodDescription"] = "Pay By Credit Card",

                ["WebTail.Payments.Labels.ExpirationMonth"] = "Expiry Month",
                ["WebTail.Payments.Labels.ExpirationYear"] = "Expiry Year",
            });

            await base.InstallAsync();
        }

        public override async Task UninstallAsync()
        {
            await _settingService.DeleteSettingAsync<StripePaymentSettings>();

            await _localizationService.DeleteLocaleResourcesAsync("Webtail.Payments.Stripe");

            await base.UninstallAsync();
        }

        public async Task<ProcessPaymentResult> ProcessPaymentAsync(ProcessPaymentRequest processPaymentRequest, bool isRecurringPayment)
        {
            // TODO: this the override for recurring

            var currentStore = await EngineContext.Current.Resolve<IStoreContext>().GetCurrentStoreAsync();
            var chargeResponse = processPaymentRequest.CreateCharge(_stripePaymentSettings,
                                                                    _currencySettings,
                                                                    currentStore,
                                                                    _customerService,
                                                                    _stateProvinceService,
                                                                    _countryService,
                                                                    _currencyService,
                                                                    _genericAttributeService);

            if (chargeResponse.GetStatus() == StripeChargeStatus.Failed)
                throw new NopException(chargeResponse.FailureMessage);

            string transactionResult = $"Transaction was processed by using Stripe. Status is {chargeResponse.GetStatus()}";
            var result = new ProcessPaymentResult()
            {
                NewPaymentStatus = chargeResponse.GetPaymentStatus(_stripePaymentSettings.TransactionMode)
            };

            if (_stripePaymentSettings.TransactionMode == TransactionMode.Authorize)
            {
                result.AuthorizationTransactionId = chargeResponse.Id;
                result.AuthorizationTransactionResult = transactionResult;
            }

            if (_stripePaymentSettings.TransactionMode == TransactionMode.Charge)
            {
                result.CaptureTransactionId = chargeResponse.Id;
                result.CaptureTransactionResult = transactionResult;
            }

            return result;
        }

        public Task<ProcessPaymentResult> ProcessPaymentAsync(ProcessPaymentRequest processPaymentRequest)
        {
            if (processPaymentRequest == null)
                throw new ArgumentException(nameof(processPaymentRequest));

            return ProcessPaymentAsync(processPaymentRequest, false);
        }

        public Task<ProcessPaymentResult> ProcessRecurringPaymentAsync(ProcessPaymentRequest processPaymentRequest)
        {
            if (processPaymentRequest == null)
                throw new ArgumentException(nameof(processPaymentRequest));

            return ProcessPaymentAsync(processPaymentRequest, true);
        }


        public Task PostProcessPaymentAsync(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            throw new NotImplementedException();
        }

        Task<bool> IPaymentMethod.HidePaymentMethodAsync(IList<ShoppingCartItem> cart)
        {
            throw new NotImplementedException();
        }

        public Task<VoidPaymentResult> VoidAsync(VoidPaymentRequest voidPaymentRequest)
        {
            throw new NotImplementedException();
        }

        Task<CancelRecurringPaymentResult> IPaymentMethod.CancelRecurringPaymentAsync(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            throw new NotImplementedException();
        }

        Task<bool> IPaymentMethod.CanRePostProcessPaymentAsync(Order order)
        {
            throw new NotImplementedException();
        }

        public Task<IList<string>> ValidatePaymentFormAsync(IFormCollection form)
        {
            throw new NotImplementedException();
        }

        public Task<ProcessPaymentRequest> GetPaymentInfoAsync(IFormCollection form)
        {
            throw new NotImplementedException();
        }

        public Task<string> GetPaymentMethodDescriptionAsync()
        {
            throw new NotImplementedException();
        }
    }
}
