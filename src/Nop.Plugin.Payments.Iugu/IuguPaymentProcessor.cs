using iugu.net.Entity;
using iugu.net.Lib;
using iugu.net.Request;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Plugins;
using Nop.Plugin.Payments.Iugu.Controllers;
using Nop.Services.Common;
using Nop.Services.Payments;
using SmartenUP.Core.Services;
using SmartenUP.Core.Util.Helper;
using System;
using System.Collections.Generic;
using System.Web.Routing;

namespace Nop.Plugin.Payments.Iugu
{
    public class IuguPaymentProcessor : BasePlugin, IPaymentMethod
    {
        private readonly IWorkContext _workContext;
        private readonly IAddressAttributeParser _addressAttributeParser;
        private readonly IStoreContext _storeContext;
        private readonly IuguPaymentSettings _iuguPaymentSettings;
        private readonly IOrderNoteService _orderNoteService;


        public IuguPaymentProcessor(
            IWorkContext workContext,
            IAddressAttributeParser addressAttributeParser,
            IStoreContext storeContext,
            IuguPaymentSettings iuguPaymentSettings,
            IOrderNoteService orderNoteService
            )
        {
            _workContext = workContext;
            _addressAttributeParser = addressAttributeParser;
            _storeContext = storeContext;
            _iuguPaymentSettings = iuguPaymentSettings;
            _orderNoteService = orderNoteService;
        }

        public bool SupportCapture => false;

        public bool SupportPartiallyRefund => false;

        public bool SupportRefund => false;

        public bool SupportVoid => false;

        public RecurringPaymentType RecurringPaymentType => RecurringPaymentType.NotSupported;

        public PaymentMethodType PaymentMethodType => PaymentMethodType.Redirection;

        public bool SkipPaymentInfo => false;

        public CancelRecurringPaymentResult CancelRecurringPayment(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            var result = new CancelRecurringPaymentResult();
            result.AddError("Recurring payment not supported");
            return result;
        }

        public bool CanRePostProcessPayment(Order order)
        {
            if (order == null)
                throw new ArgumentNullException("bool CanRePostProcessPayment");

            //payment status should be Pending
            if (order.PaymentStatus != PaymentStatus.Pending)
                return false;

            //let's ensure that at least 1 minute passed after order is placed
            if ((DateTime.UtcNow - order.CreatedOnUtc).TotalMinutes < 1)
                return false;

            return true;
        }

        public CapturePaymentResult Capture(CapturePaymentRequest capturePaymentRequest)
        {
            var result = new CapturePaymentResult();
            result.AddError("Capture method not supported");
            return result;
        }

        public decimal GetAdditionalHandlingFee(IList<ShoppingCartItem> cart)
        {
            return 0;
            //return _pagSeguroPaymentSettings.ValorFrete;
        }

        public void GetConfigurationRoute(out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "Configure";
            controllerName = "PaymentIugu";
            routeValues = new RouteValueDictionary
            {
                {"Namespaces", "Nop.Plugin.Payments.Iugu.Controllers"},
                {"area", null}
            };
        }

        public Type GetControllerType()
        {
            return typeof(PaymentIuguController);
        }

        public void GetPaymentInfoRoute(out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "PaymentInfo";
            controllerName = "PaymentIugu";
            routeValues = new RouteValueDictionary
            {
                {"Namespaces", "Nop.Plugin.Payments.Iugu.Controllers"},
                {"area", null}
            };
        }

        public bool HidePaymentMethod(IList<ShoppingCartItem> cart)
        {
            //you can put any logic here
            //for example, hide this payment method if all products in the cart are downloadable
            //or hide this payment method if current customer is from certain country
            return false;
        }

        

        public void PostProcessPayment(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            var addressHelper = new AddressHelper(_addressAttributeParser, _workContext);

            var invoiceDate = DateTime.Now.AddDays(3);

            string urlRedirect = string.Empty;

            // Act
            using (var apiInvoice = new Invoice())
            {
                string number = string.Empty;
                string complement = string.Empty;
                string cnpjcpf = string.Empty;

                addressHelper.GetCustomNumberAndComplement(postProcessPaymentRequest.Order.BillingAddress.CustomAttributes, out number, out complement, out cnpjcpf);

                InvoiceModel invoice;

                var customVariables = new List<CustomVariables> 
                {
                    new CustomVariables { name = IuguHelper.CODIGO_PEDIDO, value = postProcessPaymentRequest.Order.Id.ToString() }
                };

                var addressModel = new AddressModel()
                {
                    ZipCode = postProcessPaymentRequest.Order.BillingAddress.ZipPostalCode,
                    District = postProcessPaymentRequest.Order.BillingAddress.Address2,
                    State = postProcessPaymentRequest.Order.BillingAddress.StateProvince.Name,
                    Street = postProcessPaymentRequest.Order.BillingAddress.Address1,
                    Number = number,
                    City = postProcessPaymentRequest.Order.BillingAddress.City,
                    Country = postProcessPaymentRequest.Order.BillingAddress.Country.Name
                };

                var invoiceItems = new Item[postProcessPaymentRequest.Order.OrderItems.Count + 1];
                int i = 0;
                var cartItems = postProcessPaymentRequest.Order.OrderItems;
                foreach (var item in cartItems)
                {
                    var productID = string.IsNullOrWhiteSpace(item.Product.Sku)
                        ? item.Product.Id.ToString()
                        : item.Product.Sku;

                    var productName = ProductHelper.GetProcuctName(item);

                    productName = ProductHelper.AddItemDescrition(productName, item);

                    invoiceItems[i] = new Item() { description = productName, price_cents = ObterPrecoCentavos(decimal.Round(item.UnitPriceInclTax, 2)), quantity = item.Quantity };
                    i++;
                }

                invoiceItems[i] = new Item() { description = postProcessPaymentRequest.Order.ShippingMethod, price_cents = ObterPrecoCentavos(decimal.Round(postProcessPaymentRequest.Order.OrderShippingInclTax, 2)), quantity = 1 };

                string name = AddressHelper.GetFullName(postProcessPaymentRequest.Order.BillingAddress);
                string email = postProcessPaymentRequest.Order.Customer.Email;
                string phone = AddressHelper.FormatarCelular(postProcessPaymentRequest.Order.BillingAddress.PhoneNumber);

                string phoneNumber = AddressHelper.ObterNumeroTelefone(phone);
                string phonePrefix = AddressHelper.ObterAreaTelefone(postProcessPaymentRequest.Order.BillingAddress.PhoneNumber);
                string urlRetorno = _storeContext.CurrentStore.Url + "checkout/completed/" + postProcessPaymentRequest.Order.Id.ToString();
                string urlNotification = _storeContext.CurrentStore.Url + "Plugins/PaymentIugu/PaymentReturn";

                int descontoCentavos = 0;

                //Desconto aplicado na ordem subtotal
                if (postProcessPaymentRequest.Order.OrderSubTotalDiscountExclTax > 0)
                {
                    var discount = postProcessPaymentRequest.Order.OrderSubTotalDiscountExclTax;
                    discount = Math.Round(discount, 2);
                    descontoCentavos += ObterPrecoCentavos(decimal.Round(discount, 2));
                }

                //desconto fixo, dado por cupom, os descontos podem ser cumulativos
                if (postProcessPaymentRequest.Order.OrderDiscount > 0)
                {
                    var discount = postProcessPaymentRequest.Order.OrderDiscount;
                    discount = Math.Round(discount, 2);
                    descontoCentavos += ObterPrecoCentavos(decimal.Round(discount, 2));
                }


                var payer = new PayerModel() { CpfOrCnpj = cnpjcpf, Address = addressModel, Email = email, Name = name, Phone = phone, PhonePrefix = phonePrefix };
                var invoiceRequest = new InvoiceRequestMessage(email, invoiceDate, invoiceItems)
                {
                    Payer = payer,
                    ReturnUrl = urlRetorno,
                    NotificationUrl = urlNotification,
                    DiscountCents = descontoCentavos,
                    CustomVariables = customVariables.ToArray()
                };
                
                invoice = apiInvoice.CreateAsync(invoiceRequest, _iuguPaymentSettings.CustomApiToken).ConfigureAwait(false).GetAwaiter().GetResult();
                
                urlRedirect = invoice.secure_url;
                
            };

            
            _orderNoteService.AddOrderNote("Fatura IUGU Bradesco gerada.", true, postProcessPaymentRequest.Order);

            System.Web.HttpContext.Current.Response.Redirect(urlRedirect);
        }

        private int ObterPrecoCentavos(decimal valor)
        {
            decimal valorDecimal = valor;

            valorDecimal = valorDecimal * 100;

            valorDecimal = Math.Truncate(valorDecimal);

            return int.Parse(valorDecimal.ToString());
        }

        public ProcessPaymentResult ProcessPayment(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();
            result.NewPaymentStatus = PaymentStatus.Pending;
            return result;
        }

        public ProcessPaymentResult ProcessRecurringPayment(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();
            result.AddError("Recurring payment not supported");
            return result;
        }

        public RefundPaymentResult Refund(RefundPaymentRequest refundPaymentRequest)
        {
            var result = new RefundPaymentResult();
            result.AddError("Refund method not supported");
            return result;
        }

        public VoidPaymentResult Void(VoidPaymentRequest voidPaymentRequest)
        {
            var result = new VoidPaymentResult();
            result.AddError("Void method not supported");
            return result;
        }



    }
}
