using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.AspNet.Builder;
using Microsoft.AspNet.FileProviders;
using Microsoft.AspNet.Hosting;
using Microsoft.AspNet.Http;
using Microsoft.AspNet.Http.Features;
using Microsoft.AspNet.StaticFiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.OptionsModel;
using Microsoft.Extensions.PlatformAbstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.RollingFile;
using Slamby.API.Filters;
using Slamby.API.Helpers;
using Slamby.API.Helpers.Swashbuckle;
using Slamby.API.Middlewares;
using Slamby.Common.Config;
using Slamby.Common.DI;
using Slamby.Common.Helpers;
using Slamby.Elastic.Factories;
using StackExchange.Redis;
using Swashbuckle.SwaggerGen.Generator;

namespace Slamby.API
{
    public class Startup
    {
        #region Local variables 

        /// <summary>
        /// https://blog.kloud.com.au/2016/03/23/aspnet-core-tips-and-tricks-global-exception-handling/
        /// </summary>
        private const string ExceptionsOnStartup = "Startup";
        private const string ExceptionsOnConfigureServices = "ConfigureServices";

        private readonly Dictionary<string, List<Exception>> Exceptions = new Dictionary<string, List<Exception>>
                                   {
                                       { ExceptionsOnStartup, new List<Exception>() },
                                       { ExceptionsOnConfigureServices, new List<Exception>() },
                                   };

        public IConfigurationRoot Configuration { get; set; }

        public IHostingEnvironment HostingEnvironment { get; set; }

        public ILoggerFactory LoggerFactory { get; private set; }

        public SiteConfig SiteConfig { get; set; } = new SiteConfig();

        #endregion

        public Startup(IHostingEnvironment env, ILoggerFactory loggerFactory, IApplicationEnvironment app)
        {
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                
                HostingEnvironment = env;
                LoggerFactory = loggerFactory;

                // Set up configuration sources.
                var builder = new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json")
                    .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                    .AddJsonFile("project.json")
                    .AddEnvironmentVariables();

                Configuration = builder.Build();
                Configuration.GetSection("SlambyApi").Bind(SiteConfig);

                StartupLogger(app, env);
            }
            catch (Exception ex)
            {
                Exceptions[ExceptionsOnStartup].Add(ex);
            }
        }

        #region Startup

        private void StartupLogger(IApplicationEnvironment env, IHostingEnvironment hostingEnv)
        {
            var minimumLevel = Configuration.GetEnumValue("SlambyApi:Serilog:MinimumLevel", LogEventLevel.Debug);
            var retainedFileCountLimit = Configuration.Get("SlambyApi:Serilog:RetainedFileCountLimit", 7);
            var output = Configuration["SlambyApi:Serilog:Output"];

            var loggerConfiguration = new LoggerConfiguration()
               .MinimumLevel.Is(minimumLevel)
               .Enrich.FromLogContext();
            if (hostingEnv.IsDevelopment()) loggerConfiguration.WriteTo.LiterateConsole(minimumLevel);

            if (!string.IsNullOrWhiteSpace(output))
            {
                if (!Path.IsPathRooted(output))
                {
                    output = Path.GetFullPath(Path.Combine(env.ApplicationBasePath, output));
                }

                loggerConfiguration.WriteTo.RollingFile(
                        Path.Combine(output, "log-{Date}.txt"),
                        restrictedToMinimumLevel: minimumLevel,
                        retainedFileCountLimit: retainedFileCountLimit,
                        fileSizeLimitBytes: null);
            }

            Log.Logger = loggerConfiguration.CreateLogger();
        }

        #endregion

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            try
            {
                if (!string.IsNullOrEmpty(Configuration.Get("SlambyApi:Elm:Key", string.Empty)))
                {
                    services.AddElm();
                    services.ConfigureElm(options =>
                    {
                        options.Path = new PathString("/elm");
                        options.Filter = (name, level) => level >= LogLevel.Information;
                    });
                }

                ConfigureMvc(services);
                ConfigureSwagger(services);
                ConfigureOptions(services);
                ConfigureDependencies(services);
                ConfigureResourceDependentVars(services);
            }
            catch (Exception ex)
            {
                Exceptions[ExceptionsOnConfigureServices].Add(ex);
            }
        }

        private void ConfigureResourceDependentVars(IServiceCollection services)
        {
            var maxIndexBulkSize = Configuration.Get("SlambyApi:Resources:MaxIndexBulkSize", 0);
            var maxIndexBulkCount = Configuration.Get("SlambyApi:Resources:MaxIndexBulkCount", 0);
            var maxSearchBulkCount = Configuration.Get("SlambyApi:Resources:MaxSearchBulkCount", 0);

            if (maxIndexBulkSize == 0 || maxIndexBulkCount == 0 || maxSearchBulkCount == 0)
            {
                var machineResourceService = services.BuildServiceProvider().GetService<Common.Services.MachineResourceService>();
                machineResourceService.UpdateResourcesManually();

                maxIndexBulkSize = Convert.ToInt32(machineResourceService.Status.TotalMemory * 1024 * 1024 / 2 / 500);
                maxIndexBulkCount = Convert.ToInt32(machineResourceService.Status.TotalMemory / 12);

                services.Configure<SiteConfig>(sc => sc.Resources.MaxIndexBulkSize = maxIndexBulkSize);
                services.Configure<SiteConfig>(sc => sc.Resources.MaxIndexBulkCount = maxIndexBulkCount);
                services.Configure<SiteConfig>(sc => sc.Resources.MaxSearchBulkCount = maxIndexBulkCount);
            }
        }

        #region ConfigureServices

        private void ConfigureOptions(IServiceCollection services)
        {
            // Required to use the Options<T> pattern
            services.AddOptions();

            // Add settings from configuration
            services.Configure<SiteConfig>(Configuration.GetSection("SlambyApi"));
            services.Configure<SiteConfig>(sc => sc.Version = Configuration["version"]);
        }

        private void ConfigureDependencies(IServiceCollection services)
        {
            services.AddSingleton(sp => sp.GetService<IOptions<SiteConfig>>().Value);
            services.AddTransient(sp => ElasticClientFactory.GetClient(sp));

            //if (SiteConfig.Redis.Enabled)
            {
                services.AddSingleton<ConnectionMultiplexer>(sp =>
                {
                    var options = ConfigurationOptions.Parse(Configuration["SlambyApi:Redis:Configuration"]);
                    if (options.WriteBuffer < 64 * 1024)
                    {
                        options.WriteBuffer = 64 * 1024;
                    }
                    return ConnectionMultiplexer.Connect(options);
                });
            }

            // Add DependencyAttribute (ScopedDependency, SingletonDependency, TransientDependency) to the class in order to be scanned
            services.AddDependencyScanning()
                .Scan();

            //services.Dump();
        }

        private void ConfigureSwagger(IServiceCollection services)
        {
            services.AddSwaggerGen();
            services.ConfigureSwaggerGen(options =>
            {
                options.SingleApiVersion(new Info
                {
                    Version = "latest",
                    Title = "Slamby API",
                    Description = "Slamby API",
                    TermsOfService = "None"
                });
                //options.OrderActionGroupsBy(new SwaggerGroupNameComparer());
                options.GroupActionsBy(SwaggerHelper.GetActionGroup);
                //options.SecurityDefinitions.Add(new KeyValuePair<string, SecurityScheme>("Slamby", new ApiKeyScheme() { Type = "apiKey", In = "header", Name = "api_secret", Description = "Http authentication. Ex: Authorization: Slamby <apisecret>" }));
                //options.DocumentFilter(new SwaggerSchemaDocumentFilter(new List<string> { "http" }, "localhost:29689", "/"));
                options.DescribeAllEnumsAsStrings();
                options.ModelFilter<RegexModelFilter>();

                var sdkXmlDocumentation = typeof(SDK.Net.ApiClient)
                    .GetTypeInfo()
                    .Assembly
                    .Location
                    .Replace(".dll", ".xml");
                options.IncludeXmlComments(sdkXmlDocumentation);
            });
        }

        private void ConfigureMvc(IServiceCollection services)
        {
            var mvcBuilder = services.AddMvc();

            //if (!HostingEnvironment.IsDevelopment())
            //{
            //    mvcBuilder.AddMvcOptions(o => o.Filters.Add(typeof(SdkVersionFilter)));
            //}
            mvcBuilder.AddMvcOptions(o => o.Filters.Add(new ModelValidationFilter()));
            mvcBuilder.AddMvcOptions(o => o.Filters.Add(new GlobalExceptionFilter(LoggerFactory, SiteConfig)));

            if (SiteConfig.Redis.Enabled)
            {
                mvcBuilder.AddMvcOptions(o => o.Filters.Add(typeof(ThrottleActionFilter)));
            }

            mvcBuilder.AddJsonOptions(o => o.SerializerSettings.Converters.Add(new StringEnumConverter()));

            services.ConfigureRouting(routeOptions =>
            {
                routeOptions.AppendTrailingSlash = true;
                routeOptions.LowercaseUrls = true;
            });

            services.AddCaching();
            services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30);
                options.CookieName = ".SlambyAPI";
            });

            services.AddCors();
        }

        #endregion

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory, StartupService startupService, SiteConfig siteConfig)
        {
            loggerFactory.AddSerilog();

            if (env.IsDevelopment())
            {
                loggerFactory.AddConsole(Configuration.GetSection("Logging"));
                loggerFactory.AddDebug();
                app.UseDeveloperExceptionPage();
            }

            app.UseSession();

            app.UseRequestSizeLimit();

            if (!string.IsNullOrEmpty(Configuration.Get("SlambyApi:Elm:Key", string.Empty))) {
                app.UseElmSecurity();
                app.UseElmStyleUrlFix();
                app.UseElmPage(); // Shows the logs at the specified path
                app.UseElmCapture(); // Adds the ElmLoggerProvider
            }
            
            app.UseCors(builder => builder
                .AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod());

            var logger = loggerFactory.CreateLogger<Startup>();

            if (Exceptions.Any(p => p.Value.Any()))
            {
                app.WriteExceptionsResponse(logger, Exceptions, "Startup Error");
                return;
            }

            try
            {
                startupService.Startup();

                app.UseIISPlatformHandler();

                if (env.IsDevelopment())
                {
                    app.UseSwaggerGen();
                    app.UseSwaggerUi("swagger/ui", "/swagger/latest/swagger.json");
                }

                app.UseGzip();
                app.UseRequestLogger();

                // Set up custom content types - associating file extension to MIME type
                var provider = new FileExtensionContentTypeProvider();
                app.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = new PhysicalFileProvider(siteConfig.Directory.User),
                    RequestPath = new PathString(Common.Constants.FilesPath),
                    ContentTypeProvider = provider
                });

                app.UseApiHeaderVersion();
                app.UseApiHeaderAuthentication();
                app.UseElapsedTime();

                app.UseMvc();

                app.UseNotFound();
                app.Run(async (context) =>
                {
                    var response = JsonConvert.SerializeObject(new { Name = "Slamby.API", Version = siteConfig.Version }, Formatting.Indented);
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(response);
                });
            }
            catch (Exception ex)
            {
                app.WriteExceptionResponse(logger, ex, "Startup Runtime Error");
            }
        }

        // Entry point for the application.
        public static void Main(string[] args) => WebApplication.Run<Startup>(args);
    }
}