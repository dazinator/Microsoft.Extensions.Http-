namespace Dazinator.Extensions.Http
{
    using System.Net.Http;
    using Microsoft.Extensions.DependencyInjection;


   // This should be registered as a singleton, and all the handlers registered with a name.
    public class HttpClientHandlerRegistry
    {
        internal Dictionary<string, HttpClientHandlerRegistration> RegisteredHandlers { get; set; } = new Dictionary<string, HttpClientHandlerRegistration>();

        internal DelegatingHandler? GetHandlerInstance(string handlerName, IServiceProvider serviceProvider, string httpClientName)
        {
            var reg = RegisteredHandlers[handlerName];
            //reg.
            //  var factory = reg.Factory ?? (sp, name)=>serviceProvider.GetRequiredService<>;

            if (reg.Factory == null)
            {
                // return serviceProvider.GetRequiredService(typeof(reg.))
                throw new InvalidOperationException("Handler is registered in registry without a factory method set.");
            }
            return reg.Factory?.Invoke(serviceProvider, httpClientName);
        }

        /// <summary>
        /// Register handler with custom factory. Use this to control how the instance of the handler is created, and create different instances based on the
        /// named http client being configured.
        /// </summary>
        /// <typeparam name="THandler"></typeparam>
        /// <param name="handlerName"></param>
        /// <param name="configure"></param>
        public HttpClientHandlerRegistry Register<THandler>(string handlerName, Action<HttpClientHandlerRegistration> configure)
            where THandler : DelegatingHandler
        {
            var registration = new HttpClientHandlerRegistration();
            // registration.Factory = (sp, httpClientName) => ActivatorUtilities.CreateInstance<THandler>(sp, httpClientName); // sp.GetRequiredService<THandler>();
            configure(registration);
            registration.EnsureIsValid();
            RegisteredHandlers.Add(handlerName, registration);
            return this;
        }


        //public HttpClientHandlerRegistration GetHandlerRegistration(string handlerName) => RegisteredHandlers[handlerName];

    }
}
