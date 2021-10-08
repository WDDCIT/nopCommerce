using Autofac;
using Autofac.Core;
using Nop.Core.Configuration;
using Nop.Core.Data;
using Nop.Core.Infrastructure;
using Nop.Core.Infrastructure.DependencyManagement;
using Nop.Data;
using Nop.Plugin.Wddc.Core.Domain;
using WddcApiClient.Services.Orders;
using WddcApiClient.Services.Shipping;

namespace Nop.Plugin.Wddc.Core.Infrastructure
{
    /// <summary>
    /// Dependency registrar
    /// </summary>
    public class DependencyRegistrar : IDependencyRegistrar
    {
        private const string EDI_ORDERING = "nop_object_context_edi_ordering";
        private const string AUTOMATED_DELIVER = "nop_object_context_automated_delivery";
        /// <summary>
        /// Register services and interfaces
        /// </summary>
        /// <param name="builder">Container builder</param>
        /// <param name="typeFinder">Type finder</param>
        /// <param name="config">Config</param>
        public virtual void Register(ContainerBuilder builder, ITypeFinder typeFinder, NopConfig config)
        {

            #region WddcApi

            builder.RegisterType<WddcOrderService>();
            builder.RegisterType<WddcShipmentService>();
            builder.RegisterType<WddcOrderProcessingService>();

            #endregion

            // repositories
            builder.RegisterType<EfRepository<AutomatedDeliveryLog>>()
                .As<IRepository<AutomatedDeliveryLog>>()
                .WithParameter(ResolvedParameter.ForNamed<IDbContext>(AUTOMATED_DELIVER))
                .InstancePerLifetimeScope();
        }

        /// <summary>
        /// Order of this dependency registrar implementation
        /// </summary>
        public int Order
        {
            get { return 1; }
        }
    }
}
