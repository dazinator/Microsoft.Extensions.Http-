namespace Dazinator.Extensions.Http
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;

    public class ConfigureHandlerOptionsBuilder
    {
        public ConfigureHandlerOptionsBuilder(IServiceCollection services, IConfiguration configuration)
        {
            this.Configuration = configuration;
            Services = services;
            //   Registry = registry;
        }

        public IServiceCollection Services { get; set; }

        public IConfiguration Configuration { get; set; }

        /// <summary>
        /// Configures a named <see cref="TOptions"/> for a http client handler, that is named the same as the requested http client name, and binds it to the <see cref="IConfiguration"/> section
        /// using a convenntional path that includes the http client name: HttpClient:{HttpClientName}:{HandlerName}
        /// </summary>
        /// <param name="name"></param>
        /// <param name="allowFallbackToNonHttpClientSpecificConfiguration"></param>
        /// <typeparam name="TOptions"></typeparam>
        /// <returns></returns>
        public ConfigureHandlerOptionsBuilder ConfigureFromHttpClientsConfigSection<TOptions>(string name, bool allowFallbackToNonHttpClientSpecificConfiguration)
            where TOptions : class
        {
            var config = Configuration;
            Services.ConfigureHandlerOptionsPerHttpClient<TOptions>(config, name, allowFallbackToNonHttpClientSpecificConfiguration);
            return this;
        }
    }
}
