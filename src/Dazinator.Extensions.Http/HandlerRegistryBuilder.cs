namespace Dazinator.Extensions.Http
{
    using Microsoft.Extensions.DependencyInjection;

    public class HandlerRegistryBuilder
    {
        public HandlerRegistryBuilder(
            IServiceCollection services,
            // IConfiguration configuration,
            HttpClientHandlerRegistry registry
        )
        {
            // Configuration = configuration;
            // Services = services;
            Services = services;
            Registry = registry;
        }

        public IServiceCollection Services { get; }

        //public IConfiguration Configuration { get; }
        public HttpClientHandlerRegistry Registry { get; }


        /// <summary>
        /// Register handler with custom factory. Use this to control how the instance of the handler is created, and create different instances based on the
        /// named http client being configured.
        /// </summary>
        /// <typeparam name="THandler"></typeparam>
        /// <param name="handlerName"></param>
        /// <param name="configure"></param>
        public HandlerRegistryBuilder Register<THandler>(string handlerName, Action<HttpClientHandlerRegistration> configure)
            where THandler : DelegatingHandler
        {
            Registry.Register<THandler>(handlerName, configure);
            return this;
        }
    }
}
