using Nop.Web.Framework;
using Nop.Web.Framework.Mvc;

namespace Nop.Plugin.Payments.Iugu.Models
{
    public class ConfigurationModel : BaseNopModel
    {
        [NopResourceDisplayName("Plugins.Payments.Iugu.Fields.CustomApiToken")]
        public string CustomApiToken { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Iugu.Fields.AdicionarNotaPrazoFabricaoEnvio")]
        public bool AdicionarNotaPrazoFabricaoEnvio { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Iugu.Fields.AdicionarNotaExcluir")]
        public bool AdicionarNotaExcluir { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Iugu.Fields.QuantidadeDiasBoleto")]
        public int QuantidadeDiasBoleto { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Iugu.Fields.NomePluginAmigavelMensagemConfirmacao")]
        public string NomePluginAmigavelMensagemConfirmacao { get; set; }



    }
}