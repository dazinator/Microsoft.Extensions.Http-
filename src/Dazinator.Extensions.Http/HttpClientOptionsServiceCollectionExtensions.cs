namespace Dazinator.Extensions.Http
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Http;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

      public static class HttpClientOptionsServiceCollectionExtensions
    {

        public static IServiceCollection AddHttpClientHandlerRegistry(this IServiceCollection services, Action<HandlerRegistryBuilder> registerHandlers)
        {
            services.AddHttpClient();
            var registry = new HttpClientHandlerRegistry();
            var builder = new HandlerRegistryBuilder(services, registry);
            registerHandlers(builder);
            services.AddSingleton(registry);
            return services;
        }

        public static ConfigureUponRequestBuilder<HttpClientOptions, IServiceCollection> ConfigureHttpClientOptions(this IServiceCollection services)
        {
            // Configures HttpClientFactoryOptions on demand when a distinct httpClientName is requested.
            services.ConfigureHttpClientFactoryOptions().From(SetupHttpClientFactoryOptions);
            return services.ConfigureUponRequest<HttpClientOptions>();
        }

        public static ConfigureUponRequestBuilder<HttpClientFactoryOptions, IServiceCollection> ConfigureHttpClientFactoryOptions(this IServiceCollection services)
        {
            // Configures HttpClientFactoryOptions on demand when a distinct httpClientName is requested.
            return services.ConfigureUponRequest<HttpClientFactoryOptions>();
        }

        /// <summary>
        /// Configure this http client by configuring a simpler proxy options object of type <see cref="HttpClientOptions"/>
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static IHttpClientBuilder ConfigureOptions(this IHttpClientBuilder builder, Action<HttpClientOptions> configure)
        {
            var httpClientName = builder.Name;
            var services = builder.Services;

            builder.ConfigureOptions<HttpClientOptions>(configure);
            builder.AddOptions<HttpClientFactoryOptions>((optionsBuilder) =>
            {
                optionsBuilder.Configure<IServiceProvider>((o, sp) => SetupHttpClientFactoryOptions(sp, httpClientName, o));
            });

            return builder;
        }

        /// <summary>
        /// Configure this http client by configuring a simpler proxy options object of type <see cref="HttpClientOptions"/> from <see cref="IConfiguration"/>
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static IHttpClientBuilder ConfigureOptions(this IHttpClientBuilder builder, IConfiguration config)
        {
            var httpClientName = builder.Name;
            var services = builder.Services;
            services.Configure<HttpClientOptions>(httpClientName, config);
            services.AddOptions<HttpClientFactoryOptions>(httpClientName)
                   .Configure<IServiceProvider>((o, sp) => SetupHttpClientFactoryOptions(sp, httpClientName, o));

            return builder;
            //  return SetupFromHttpClientOptions(builder);
        }


        /// <summary>
        /// Convenience method to ccnfigure a named options using the same name as this http client.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static IHttpClientBuilder ConfigureOptions<TOptions>(this IHttpClientBuilder builder, Action<TOptions> configure)
            where TOptions : class
        {
            builder.Services.Configure(builder.Name, configure);
            return builder;
        }

        /// <summary>
        /// Convenience method to ccnfigure a named options using the same name as the http client you are configuring with the <paramref name="builder"/>.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static IHttpClientBuilder AddOptions<TOptions>(this IHttpClientBuilder builder, Action<OptionsBuilder<TOptions>> configure)
            where TOptions : class
        {
            var services = builder.Services;
            var optionsBuilder = services.AddOptions<TOptions>(builder.Name);
            configure?.Invoke(optionsBuilder);
            return builder;
        }

        /// <summary>
        /// Convenience method to ccnfigure a named options using the same name as this http client.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static IHttpClientBuilder ConfigureOptions<TOptions>(this IHttpClientBuilder builder, IConfiguration config)
            where TOptions : class
        {
            builder.Services.Configure<TOptions>(builder.Name, config);
            return builder;
        }

        private static void SetupHttpClientFactoryOptions(IServiceProvider serviceProvider, string httpClientName, HttpClientFactoryOptions httpClientFactoryOptions)
        {

            var logger = serviceProvider.GetRequiredService<ILogger<HttpMessageHandlerBuilder>>();
            // options.HandlerLifetime = handlerLifetime;
            var httpClientOptionsFactory = serviceProvider.GetRequiredService<IOptionsMonitor<HttpClientOptions>>();
            var httpClientOptions = httpClientOptionsFactory.Get(httpClientName);

            //  options.ConfigureFromOptions(sp, name);
            httpClientFactoryOptions.HttpClientActions.Add((httpClient) => httpClientOptions.Apply(httpClient));

            // configure primary handler.
            httpClientFactoryOptions.HttpMessageHandlerBuilderActions.Add(a =>
            {
                if ((a.PrimaryHandler ?? new HttpClientHandler()) is not HttpClientHandler primaryHandler)
                {
                    logger.LogWarning("Configured Primary Handler for Http Client {HttpClientName} is not a HttpClientHandler and therefore DangerousAcceptAnyServerCertificateValidator and UseCookies cannot be set.", httpClientName);
                    return;
                }

                primaryHandler.UseCookies = httpClientOptions.UseCookies;

                if (httpClientOptions.EnableBypassInvalidCertificate)
                {
                    logger.LogWarning("Http Client {HttpClientName} configured to accept any server certificate.", httpClientName);
                    primaryHandler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                    a.PrimaryHandler = primaryHandler;
                }

            });


            if (httpClientOptions.Handlers?.Any() ?? false)
            {
                httpClientFactoryOptions.HttpMessageHandlerBuilderActions.Add(a =>
                {
                    var registry = serviceProvider.GetRequiredService<HttpClientHandlerRegistry>();

                    foreach (var handlerName in httpClientOptions.Handlers)
                    {
                        logger.LogDebug("Creating handler named: {HandlerName} for HttpClient: {HttpClientName}.", handlerName, httpClientName);

                        var handler = registry.GetHandlerInstance(handlerName, serviceProvider, httpClientName);
                        if (handler == null)
                        {
                            throw new Exception($"Handler named: {handlerName} was not found, for http client named: {httpClientName}");
                        }

                        a.AdditionalHandlers.Add(handler);
                    }
                });

            }
            else
            {
                logger.LogWarning("No handlers configured for HttpClient: {HttpClientName}.", httpClientName);
            }

        }

        /// <summary>
        /// Register a hook that will configure the options for a specific handler for a specic http client, based on the configuration section convention: HttpClient:{HttpClientName}:{HandlerName}
        /// </summary>
        /// <param name="services"></param>
        /// <param name="config"></param>
        /// <param name="handlerConfigSectionName"></param>
        /// <param name="allowFallbackToNonHttpClientSpecificConfiguration"></param>
        /// <typeparam name="THandlerOptions"></typeparam>
        /// <returns></returns>
        public static IServiceCollection ConfigureHandlerOptionsPerHttpClient<THandlerOptions>(this IServiceCollection services, IConfiguration config, string handlerConfigSectionName, bool allowFallbackToNonHttpClientSpecificConfiguration = true)
            where THandlerOptions : class
        {
            services.AddSingleton<IConfigureOptions<THandlerOptions>, ConfigureHttpClientHandlerOptionsFromHttpClientSpecificConfigurationSection<THandlerOptions>>(sp => new ConfigureHttpClientHandlerOptionsFromHttpClientSpecificConfigurationSection<THandlerOptions>(handlerConfigSectionName, allowFallbackToNonHttpClientSpecificConfiguration, config, sp.GetRequiredService<ILogger<ConfigureHttpClientHandlerOptionsFromHttpClientSpecificConfigurationSection<THandlerOptions>>>()));
            return services;
        }


        public static IServiceCollection ConfigureHandlerOptionsUsingConfiguration(this IServiceCollection services, IConfiguration configuration, Action<ConfigureHandlerOptionsBuilder> configure)
        {
            var builder = new ConfigureHandlerOptionsBuilder(services, configuration);
            configure?.Invoke(builder);
            return services;
        }

    }
}
