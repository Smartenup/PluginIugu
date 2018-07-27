using Nop.Web.Framework.Mvc;

namespace Nop.Plugin.Payments.Iugu.Models
{
    public class PaymentInfoModel : BaseNopModel
    {
        public string Email { get; set; }
        public string Token { get; set; }
        public bool AdicionarNotaPrazoFabricaoEnvio { get; set; }

    }
}