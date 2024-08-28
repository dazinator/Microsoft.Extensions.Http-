namespace Microsoft.Extensions.DependencyInjection
{
    public static class HttpClientFactoryServiceProviderExensions
    {
        public static Func<HttpClient> UseHttpClientFactory(this IServiceProvider sp, string factoryName)
        {
            var httpFactory = sp.GetRequiredService<IHttpClientFactory>();
            Func<HttpClient> factory = () =>
            {
                var httpClient = httpFactory.CreateClient(factoryName);
                return httpClient;
            };
            return factory;
        }

        /// <summary>
        /// Uses <see cref="IHttpClientFactory"/> to create the named http client. HttpClientFactory must already be registered for DI and the httpclient with the specified name should have configuration present.
        /// </summary>
        /// <param name="sp"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static HttpClient CreateHttpClient(this IServiceProvider sp, string name)
        {
            var httpFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpFactory.CreateClient(name);
            return httpClient;
        }
    }
}
