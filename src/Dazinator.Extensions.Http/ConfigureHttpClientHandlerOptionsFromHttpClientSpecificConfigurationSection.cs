namespace Dazinator.Extensions.Http
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    /// <summary>
    /// Responsible for configuring handler options for a named http client's handler, based on a config section specifically for the named client.
    /// </summary>
    public class ConfigureHttpClientHandlerOptionsFromHttpClientSpecificConfigurationSection<TOptions>
        : IConfigureNamedOptions<TOptions>
        where TOptions : class
    {
        private readonly string _handlerConfigSectionName;
        private readonly bool _allowFallbackToNonHttpClientSpecificConfiguration;
        private readonly IConfiguration _config;
        private readonly ILogger<ConfigureHttpClientHandlerOptionsFromHttpClientSpecificConfigurationSection<TOptions>> _logger;

        public ConfigureHttpClientHandlerOptionsFromHttpClientSpecificConfigurationSection(string handlerConfigSectionName, bool allowFallbackToNonHttpClientSpecificConfiguration, IConfiguration config, ILogger<ConfigureHttpClientHandlerOptionsFromHttpClientSpecificConfigurationSection<TOptions>> logger)
        {
            _handlerConfigSectionName = handlerConfigSectionName;
            _allowFallbackToNonHttpClientSpecificConfiguration = allowFallbackToNonHttpClientSpecificConfiguration;
            _config = config;
            _logger = logger;
        }

        public void Configure(string name, TOptions options)
        {
            // fallback to configuring from handlers section in config
            if (string.IsNullOrWhiteSpace(name))
            {
                if (!_allowFallbackToNonHttpClientSpecificConfiguration)
                {
                    return;
                }

                /// use default section (i.e not for specific named http client)
                name = "Handlers";
            }
            var sectionName = $"HttpClient:{name}:{_handlerConfigSectionName}";
            var section = _config.GetSection(sectionName);
            if (section.Exists())
            {
                section.Bind(options);
            }

        }
        public void Configure(TOptions options) => Configure(Options.DefaultName, options);
    }
}
