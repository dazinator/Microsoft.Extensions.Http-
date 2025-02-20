## Configuring Http Clients at runtime - solved

Microsoft provide `IHttpClientFactory` which is great.

However it only lets you configure named `HttpClients` once - when building your DI container on application startup. Once you use the http client for the first time, that configuration is locked in.

What if within your application, you want to allow the configuration for the `HttpClient` to be amended - for example the `BaseAddress` or the `Handler's that are active.

This library addresses this problem, not through allowing you to "mutate" any existing objects thats `IHttpClientFactory` knows about, but instead, allowing you to introduce newly named `HttpClient`s which will be lazily built on demand. You can therefore request a named http client with a name like "foo-v1" and then later, when you know you have new confiugration to apply, you can request "foo-v2" and at that point a new http client will be built and you can apply the latest configuration during that process.

## Usage

There are different usage patterns, starting simple then varying in sophistication.

### Simple

```cs
   services.AddHttpClient();
  
   services.ConfigureHttpClientFactoryOptions((sp, httpClientName, options) =>
   {
       // configure this named http client however you see fit
       options.HttpClientActions.Add(a =>
       {
           a.BaseAddress = new Uri($"http://{httpClientName}.localhost/");
       });
   });


   var sp = services.BuildServiceProvider();
   var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();

   // Now get your http client, and version the name at runtime when your configuration is changed:
   using var httpClient = sut.CreateClient("foo-v1");
   Assert.Equal($"http://foo-v1.localhost/", httpClient.BaseAddress.ToString());

   // Configuration of the http client was changed somewhere.. use a new name.
   using var httpClient2 = sut.CreateClient("foo-v2");
   Assert.Equal($"http://foo-v2.localhost/", httpClient.BaseAddress.ToString());

```

### More advanced

Rather than configuring the `HttpClientFactoryOptions` directly, you can configure httpclients from a "simpler" set of options provided by this library that will wrap and configure the underlying `HttpClientFactoryOptions`.
These options can be configured lazily upon request of the named client, either via a configure action delegate, or from an IConfiguration.


```cs
  services.AddHttpClient();
  services.ConfigureHttpClientOptions((sp, name, options) =>
  {
      // load settings from some store using unique http client name (which can version)
      if (name.StartsWith("foo-"))
      {
          options.UseCookies = true;
          options.BaseAddress = $"http://{name}.localhost";
          options.EnableBypassInvalidCertificate = true;
          options.MaxResponseContentBufferSize = 2000;
          options.Timeout = TimeSpan.FromMinutes(2);
          // options.Handlers.Add(statusOkHandlerName);
      }
      if (name.StartsWith("bar-"))
      {
          options.UseCookies = false;
          options.BaseAddress = $"http://{name}.localhost";
          options.EnableBypassInvalidCertificate = true;
          options.MaxResponseContentBufferSize = 2000;
          options.Timeout = TimeSpan.FromMinutes(2);
         // options.Handlers.Add(statusNotFoundHandlerName);
      }
  });

```

Or use bind these options from `IConfiguration`

```cs

services.ConfigureHttpClientOptions((name) =>
            {
                return config.GetSection(name);
            });

```

Note: If you know your http client names at the point of registration you can also use the normal AddHttpClient() style:

```cs
   services.AddHttpClient("foo-v1")
                    .ConfigureOptions((options) =>
                    {
                        options.BaseAddress = $"http://foo-v1.localhost";
                        options.EnableBypassInvalidCertificate = true;
                        options.MaxResponseContentBufferSize = 2000;
                        options.Timeout = TimeSpan.FromMinutes(2);
                        options.Handlers.Add("status-handler");
                    });                  

```

Or bind from config

```

   services.AddHttpClient("foo-v1")                   
             .ConfigureOptions(GetConfiguration().GetSection("foo-v1"));

```


The simpler options object is easier to configure that manipulating the HttpClientFactoryOptions directly, the heavy lifting is done for you.

## Aven more advanced - using the Handler registry to map reusable handlers.

A powerful feature for being able to map different handlers to different clients is available.
Each handler can be configured differently per named http client.

The following is a walkthrough of creating a custom handler, and usig it with a couple of different http clients, and confiugring it with different options for each.

1. Create the handler.
   Here is an example generic handler that simply invokes invokes whatever Func you supply in the constructor.
   It also gets passed in the http client name, and takes an `IOptionsMontitor<TOptions>`. By injecting these two services we can have the handler load its options for the specific named http client, or fall back to a default set of options.
   This allows us to control its behaviour for each named http client by ensuring we configure its named options for that http client name.


```cs

    public class DelegatingHandlerWithOptions<TOptions> : DelegatingHandler
    {
        private readonly string _httpClientName;
        private readonly IOptionsMonitor<TOptions> _optionsMontitor;
        private readonly Func<HttpRequestMessage, TOptions, CancellationToken, Task<HttpResponseMessage>> _sendAsync;


        public DelegatingHandlerWithOptions(string httpClientName, IOptionsMonitor<TOptions> optionsMontitor, Func<HttpRequestMessage, TOptions, CancellationToken, Task<HttpResponseMessage>> sendAsync)
        {
            _httpClientName = httpClientName;
            _optionsMontitor = optionsMontitor;
            _sendAsync = sendAsync;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var handlerOptonsForNamedHttpClient = _optionsMontitor.Get(_httpClientName);
            return await _sendAsync.Invoke(request, handlerOptonsForNamedHttpClient, cancellationToken);

        }
    }

    public class StatusHandlerOptions
    {
        public System.Net.HttpStatusCode StatusCode { get; set; }
    }



```

2. Now you can do the following as shown in the example below:

- 1) Use the `AddHttpClientHandlerRegistry` method to register your handler in the registry with a specific name. .
- 2) AddHttpClient()'s and configure their options to use the handler, also configure the handler's options for that named http client.

```cs 
           // 1.)
           services.AddHttpClientHandlerRegistry((registry) =>
                {
                    registry.Register<DelegatingHandlerWithOptions<StatusHandlerOptions>>("status-handler", (r) =>
                    {
                        r.Factory = (sp, httpClientName) =>
                        {
                            var optionsMontior = sp.GetRequiredService<IOptionsMonitor<StatusHandlerOptions>>();
                            return new DelegatingHandlerWithOptions<StatusHandlerOptions>(httpClientName, optionsMontior, (request, handlerOptions, cancelToken) =>
                            {
                                var result = new HttpResponseMessage(handlerOptions.StatusCode);
                                return Task.FromResult(result);
                            });
                        };                       
                    });
                }) // 2)
                .AddHttpClient("foo-v1")
                    .ConfigureOptions((options) =>
                    {
                        options.BaseAddress = $"http://foo-v1.localhost";
                        options.EnableBypassInvalidCertificate = true;
                        options.MaxResponseContentBufferSize = 2000;
                        options.Timeout = TimeSpan.FromMinutes(2);
                        options.Handlers.Add("status-handler");
                    })
                    .ConfigureOptions<StatusHandlerOptions>((a) => a.StatusCode = System.Net.HttpStatusCode.OK)
                .Services
                .AddHttpClient("bar-v1")
                    .ConfigureOptions((options) =>
                    {
                        options.BaseAddress = $"http://bar-v1.localhost";
                        options.EnableBypassInvalidCertificate = true;
                        options.MaxResponseContentBufferSize = 2000;
                        options.Timeout = TimeSpan.FromMinutes(2);
                        options.Handlers.Add("status-handler");
                    }).ConfigureOptions<StatusHandlerOptions>((a) => a.StatusCode = System.Net.HttpStatusCode.NotFound);
            });

            var fooClient = sut.CreateClient("foo-v1");
            var barClient = sut.CreateClient("bar-v1");

            var fooResponse = await fooClient.GetAsync("/foo");
            var barResponse = await barClient.GetAsync("/bar");

            Assert.Equal(System.Net.HttpStatusCode.OK, fooResponse.StatusCode);
            Assert.Equal(System.Net.HttpStatusCode.NotFound, barResponse.StatusCode);

```

In the scenario above:-

1. The handler I have implemented allows for different options based on the http client name. It's a useful pattern for me so I chose to demo it, it may not be necessary in your handlers.

When using `ConfigureHttpClientOptions` you do not know the http client name in advance. In this scenario, you must also configure the handler's options in such a way that it can be configured at request time for whatever th http client name is that is requested at runtime. We do this using the `ConfigureUponRequest` extension method:

```cs

 services.AddHttpClientHandlerRegistry((registry) =>
                {
                    registry.Register<DelegatingHandlerWithOptions<StatusHandlerOptions>>("status-handler", (services, r) =>
                    {
                        r.Factory = (sp, httpClientName) =>
                        {
                            var optionsMontior = sp.GetRequiredService<IOptionsMonitor<StatusHandlerOptions>>();
                            return new DelegatingHandlerWithOptions<StatusHandlerOptions>(httpClientName, optionsMontior, (request, handlerOptions, cancelToken) =>
                            {
                                var result = new HttpResponseMessage(handlerOptions.StatusCode);
                                return Task.FromResult(result);
                            });
                        };                     
                    });
                })
                .ConfigureHttpClientOptions((sp, name, options) =>
                {
                    if (name.StartsWith("foo-"))
                    {
                        options.BaseAddress = $"http://{name}.localhost";
                        options.EnableBypassInvalidCertificate = true;
                        options.MaxResponseContentBufferSize = 2000;
                        options.Timeout = TimeSpan.FromMinutes(2);
                        // Both clients have the same handler "status-handler" added.
                        // But as the handler has different named options (named after the http client name) the same
                        // handler ends up configured specific for each http client.
                        options.Handlers.Add("status-handler");
                    }
                    if (name.StartsWith("bar-"))
                    {
                        options.BaseAddress = $"http://{name}.localhost";
                        options.EnableBypassInvalidCertificate = true;
                        options.MaxResponseContentBufferSize = 2000;
                        options.Timeout = TimeSpan.FromMinutes(2);
                        // Both clients have the same handler "status-handler" added.
                        // But as the handler has different named options configured (named after each http client name) the same
                        // handler ends up configured specific for each http client.
                        options.Handlers.Add("status-handler");
                    }
                })
                .ConfigureUponRequest<StatusHandlerOptions>((sp, name, options) =>
                {
                    if (name.StartsWith("foo-"))
                    {
                        options.StatusCode = System.Net.HttpStatusCode.OK;
                    }
                    if (name.StartsWith("bar-"))
                    {
                        options.StatusCode = System.Net.HttpStatusCode.NotFound;
                    }
                });
            });

            var fooClient = sut.CreateClient("foo-v1");
            var barClient = sut.CreateClient("bar-v1");

            var fooResponse = await fooClient.GetAsync("/foo");
            var barResponse = await barClient.GetAsync("/bar");

            Assert.Equal(System.Net.HttpStatusCode.OK, fooResponse.StatusCode);
            Assert.Equal(System.Net.HttpStatusCode.NotFound, barResponse.StatusCode);

```