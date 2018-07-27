using Nop.Core.Configuration;

namespace Nop.Plugin.Payments.Iugu
{
    public class IuguPaymentSettings : ISettings
    {
        public string CustomApiToken { get; set; }
        public bool AdicionarNotaPrazoFabricaoEnvio { get; set; }
        public bool AdicionarNotaExcluir { get; set; }
        public int QuantidadeDiasBoleto { get; set; }
        public string NomePluginAmigavelMensagemConfirmacao { get; set; }
    }
}
