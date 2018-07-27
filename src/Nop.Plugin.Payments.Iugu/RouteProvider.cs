using Nop.Web.Framework.Mvc.Routes;
using System.Web.Mvc;
using System.Web.Routing;

namespace Nop.Plugin.Payments.Iugu
{
    public partial class RouteProvider : IRouteProvider
    {
        public void RegisterRoutes(RouteCollection routes)
        {
            routes.MapRoute("Plugin.Payments.Iugu.Configure",
                 "Plugins/PaymentIugu/Configure",
                 new { controller = "PaymentIugu", action = "Configure" },
                 new[] { "Nop.Plugin.Payments.Iugu.Controllers" }
            );

            routes.MapRoute("Plugin.Payments.Iugu.PaymentReturn",
                 "Plugins/PaymentIugu/PaymentReturn",
                 new { controller = "PaymentIugu", action = "PaymentReturn" },
                 new[] { "Nop.Plugin.Payments.Iugu.Controllers" }
            );

        }

        public int Priority
        {
            get { return 0; }
        }
    }
}
