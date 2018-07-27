using iugu.net.Entity;
using iugu.net.Lib;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.Iugu.Models;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Stores;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Security;
using SmartenUP.Core.Services;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Web.Mvc;

namespace Nop.Plugin.Payments.Iugu.Controllers
{
    public class PaymentIuguController : BasePaymentController
    {
        private readonly ILogger _logger;
        private readonly IOrderService _orderService;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IWorkContext _workContext;
        private readonly IWorkflowMessageService _workflowMessageService;
        private readonly IOrderNoteService _orderNoteService;
        private readonly IuguPaymentSettings _iuguPaymentSettings;
        private readonly IStoreService _storeService;
        private readonly ISettingService _settingService;
        private readonly ILocalizationService _localizationService;

        public PaymentIuguController(ILogger logger,
            IOrderService orderService,
            IOrderProcessingService orderProcessingService,
            IWorkContext workContext,
            IWorkflowMessageService workflowMessageService,
            IOrderNoteService orderNoteService,
            IuguPaymentSettings iuguPaymentSettings,
            IStoreService storeService,
            ISettingService settingService,
            ILocalizationService localizationService)
        {
            
            _logger = logger;
            _orderService = orderService;
            _orderProcessingService = orderProcessingService;
            _workContext = workContext;
            _workflowMessageService = workflowMessageService;
            _orderNoteService = orderNoteService;
            _iuguPaymentSettings = iuguPaymentSettings;
            _storeService = storeService;
            _settingService = settingService;
            _localizationService = localizationService;
        }


        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure()
        {
            //load settings for a chosen store scope
            var storeScope = GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var iuguPaymentSettings = _settingService.LoadSetting<IuguPaymentSettings>(storeScope);
            if (iuguPaymentSettings == null) throw new ArgumentNullException(nameof(iuguPaymentSettings));

            var model = new ConfigurationModel();

            model.CustomApiToken = iuguPaymentSettings.CustomApiToken;
            model.AdicionarNotaPrazoFabricaoEnvio = iuguPaymentSettings.AdicionarNotaPrazoFabricaoEnvio;
            model.AdicionarNotaExcluir = iuguPaymentSettings.AdicionarNotaExcluir;
            model.QuantidadeDiasBoleto = iuguPaymentSettings.QuantidadeDiasBoleto;
            model.NomePluginAmigavelMensagemConfirmacao = iuguPaymentSettings.NomePluginAmigavelMensagemConfirmacao;

            return View("~/Plugins/Payments.Iugu/Views/PaymentIugu/Configure.cshtml", model);

        }

        [HttpPost]
        [AdminAuthorize]
        [AdminAntiForgery]
        [ChildActionOnly]
        public ActionResult Configure(ConfigurationModel model)
        {
            if (!ModelState.IsValid)
                return Configure();

            //load settings for a chosen store scope
            var storeScope = GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var iuguPaymentSettings = _settingService.LoadSetting<IuguPaymentSettings>(storeScope);

            //save settings
            iuguPaymentSettings.CustomApiToken = model.CustomApiToken;
            iuguPaymentSettings.AdicionarNotaExcluir = model.AdicionarNotaExcluir;
            iuguPaymentSettings.AdicionarNotaPrazoFabricaoEnvio = model.AdicionarNotaPrazoFabricaoEnvio;
            iuguPaymentSettings.QuantidadeDiasBoleto = model.QuantidadeDiasBoleto;
            iuguPaymentSettings.NomePluginAmigavelMensagemConfirmacao = model.NomePluginAmigavelMensagemConfirmacao;

            _settingService.SaveSetting(iuguPaymentSettings);

            //now clear settings cache
            _settingService.ClearCache();

            SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            return View("~/Plugins/Payments.Iugu/Views/PaymentIugu/Configure.cshtml", model);
        }


        [ChildActionOnly]
        public ActionResult PaymentInfo()
        {
            return View("~/Plugins/Payments.Iugu/Views/PaymentIugu/PaymentInfo.cshtml");
        }


        [NonAction]
        public override ProcessPaymentRequest GetPaymentInfo(FormCollection form)
        {

            var paymentInfo = new ProcessPaymentRequest();
            return paymentInfo;

        }

        public override IList<string> ValidatePaymentForm(FormCollection form)
        {
            var warnings = new List<string>();
            return warnings;

        }

        

        /// <summary>
        /// PaymentReturn utilizando API de notificação automática do IUGU
        /// </summary>
        /// <param></param>
        /// <returns></returns>
        [ValidateInput(false)]
        public ActionResult PaymentReturn()
        {
            try
            {

                string eventCode = Request["event"];
                string dataId = Request["data[id]"];
                string dataStatus = Request["data[status]"];
                string dataAccountId = Request["data[account_id]"];
                string dataSubscriptionId = Request["data[subscription_id]"];

                var requestMessage = new StringBuilder();
                requestMessage.Append("Plugin.Payments.Iugu:");
                requestMessage.AppendFormat("eventCode: {0} ", eventCode);
                requestMessage.AppendFormat("dataId: {0} ", dataId);
                requestMessage.AppendFormat("dataStatus: {0} ", dataStatus);
                requestMessage.AppendFormat("dataAccountId: {0} ", dataAccountId);
                requestMessage.AppendFormat("dataSubscriptionId: {0} ", dataSubscriptionId);

                _logger.Information(requestMessage.ToString());

                string codigoPedido = string.Empty;

                if (string.IsNullOrEmpty(eventCode))
                {
                    _logger.Error("Plugin.Payments.Iugu: eventCode não encontrado");

                    return new HttpStatusCodeResult(HttpStatusCode.OK);
                }

                InvoiceModel invoice = null;

                using (var apiInvoice = new Invoice())
                {
                    invoice = apiInvoice.GetAsync(dataId, _iuguPaymentSettings.CustomApiToken).ConfigureAwait(false).GetAwaiter().GetResult();
                    foreach ( var variable in invoice.custom_variables)
                        if (variable.name == IuguHelper.CODIGO_PEDIDO)
                            codigoPedido = variable.value;
                }

                if (string.IsNullOrEmpty(codigoPedido))
                {
                    _logger.Error("Plugin.Payments.Iugu: Pedido não encontrado na fatura IUGU");

                    return new HttpStatusCodeResult(HttpStatusCode.InternalServerError);
                }

                Order order = null;

                if (codigoPedido.Length == 36)
                    order = _orderService.GetOrderByGuid(new Guid(codigoPedido));
                else
                    order = _orderService.GetOrderById(int.Parse(codigoPedido));

                if (order == null)
                {
                    _logger.Information("Plugin.Payments.Iugu: Pedido não encontrado. Pedido: " + codigoPedido);

                    return new HttpStatusCodeResult(HttpStatusCode.InternalServerError);
                }

                if (dataStatus == "paid" && order.PaymentStatus == PaymentStatus.Pending)
                {
                    order.PaymentStatus = PaymentStatus.Authorized;
                    _orderProcessingService.MarkAsAuthorized(order);
                    _orderNoteService.AddOrderNote("Pagamento aprovado.", true, order);

                    if(_iuguPaymentSettings.AdicionarNotaExcluir)
                        _orderNoteService.AddOrderNote("Aguardando Impressão - Excluir esse comentário ao imprimir ", false, order);

                    if(_iuguPaymentSettings.AdicionarNotaPrazoFabricaoEnvio)
                        _orderNoteService.AddOrderNote(_orderNoteService.GetOrdeNoteRecievedPayment(order, _iuguPaymentSettings.NomePluginAmigavelMensagemConfirmacao), true, order, true);
                }

            }
            catch (Exception ex)
            {
                string erro = string.Format("Plugin.Payments.Iugu: Erro IUGU - {0}", ex.Message.ToString());

                _logger.Error(erro, ex);

                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError);
            }

            return new HttpStatusCodeResult(HttpStatusCode.OK); 
        }

    }
}
