﻿using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Serilog;
using Serilog.Events;
using Slamby.API.Filters;
using Slamby.API.Helpers;
using Slamby.API.Helpers.Swashbuckle;
using Slamby.API.Middlewares;
using Slamby.Common.Config;
using Slamby.Common.DI;
using Slamby.Common.Helpers;
using Slamby.Elastic.Factories;
using StackExchange.Redis;
using Swashbuckle.Swagger.Model;

namespace Slamby.API
{
    public class Startup
    {
        #region Local variables 

        public IConfigurationRoot Configuration { get; set; }


        public ILoggerFactory LoggerFactory { get; private set; }

        public SiteConfig SiteConfig { get; set; } = new SiteConfig();

        #endregion

        public Startup(IHostingEnvironment env)
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            // Set up configuration sources.
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json")
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();

            Configuration = builder.Build();
            Configuration.GetSection("SlambyApi").Bind(SiteConfig);

            StartupLogger(env);
        }

        #region Startup

        private void StartupLogger(IHostingEnvironment env)
        {
            var minimumLevel = Configuration.GetEnumValue("SlambyApi:Serilog:MinimumLevel", LogEventLevel.Debug);
            var retainedFileCountLimit = Configuration.GetValue("SlambyApi:Serilog:RetainedFileCountLimit", 7);
            var output = Configuration["SlambyApi:Serilog:Output"];

            var loggerConfiguration = new LoggerConfiguration()
               .MinimumLevel.Is(minimumLevel)
               .Enrich.FromLogContext();
            if (env.IsDevelopment()) loggerConfiguration.WriteTo.LiterateConsole(minimumLevel);

            if (!string.IsNullOrWhiteSpace(output))
            {
                if (!Path.IsPathRooted(output))
                {
                    output = Path.GetFullPath(Path.Combine(env.ContentRootPath, output));
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
                if (!string.IsNullOrEmpty(Configuration.GetValue("SlambyApi:Elm:Key", string.Empty)))
                {
                    services.AddElm(options =>
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
                Log.Logger.Fatal(ex, "Fatal error occured at ConfigureServices");
                throw;
            }
        }

        #region ConfigureServices

        private void ConfigureOptions(IServiceCollection services)
        {
            // Required to use the Options<T> pattern
            services.AddOptions();

            // Add settings from configuration
            services.Configure<SiteConfig>(Configuration.GetSection("SlambyApi"));
            services.Configure<SiteConfig>(sc => sc.Version = VersionHelper.GetProductVersion(typeof(Startup)));
        }

        private async Task<string> GetIp(string hostname)
             => (await Dns.GetHostEntryAsync(hostname)).AddressList.First(a => a.AddressFamily == AddressFamily.InterNetwork).ToString();

        private void ConfigureDependencies(IServiceCollection services)
        {
            //see: https://github.com/aspnet/Hosting/issues/793
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            services.AddSingleton(sp => sp.GetService<IOptions<SiteConfig>>().Value);
            services.AddTransient(sp => ElasticClientFactory.GetClient(sp));

            // Add DependencyAttribute (ScopedDependency, SingletonDependency, TransientDependency) to the class in order to be scanned
            services.ConfigureAttributedDependencies();

            {
                var options = ConfigurationOptions.Parse(Configuration["SlambyApi:Redis:Configuration"]);
                //HACK: https://github.com/dotnet/corefx/issues/8768
                //this should be removed when https://github.com/dotnet/corefx/issues/11564 is closed
                var dnsEndPoints = options.EndPoints.OfType<DnsEndPoint>().ToList();
                foreach(var dnsEndPoint in dnsEndPoints)
                {
                    options.EndPoints.Remove(dnsEndPoint);
                    options.EndPoints.Add(GetIp(dnsEndPoint.Host).Result, dnsEndPoint.Port);
                }
                if (options.WriteBuffer < 64 * 1024)
                {
                    options.WriteBuffer = 64 * 1024;
                }
                options.ResolveDns = false; //re-resolve dns on re-connect
                services.AddSingleton(options);
                
                services.AddSingleton<ConnectionMultiplexer>(sp => ConnectionMultiplexer.Connect(sp.GetService<ConfigurationOptions>()));
            }
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

                //TODO: check this
                options.SchemaFilter<RegexSchemaFilter>();
                options.OperationFilter<ApplySwaggerResponseFilterAttributesOperationFilter>();

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
            mvcBuilder.AddMvcOptions(o => o.Filters.Add(typeof(ModelValidationFilter)));
            mvcBuilder.AddMvcOptions(o => o.Filters.Add(typeof(GlobalExceptionFilter)));

            if (SiteConfig.Redis.Enabled)
            {
                mvcBuilder.AddMvcOptions(o => o.Filters.Add(typeof(ThrottleActionFilter)));
            }

            mvcBuilder.AddJsonOptions(o =>
            {
                o.SerializerSettings.Converters.Add(new StringEnumConverter());
                //HACK: see https://github.com/aspnet/Mvc/issues/4842
                o.SerializerSettings.ContractResolver = new DefaultContractResolver();
            });

            services.AddRouting(routeOptions =>
            {
                routeOptions.AppendTrailingSlash = true;
                routeOptions.LowercaseUrls = true;
            });

            services.AddDistributedRedisCache(options =>
            {
                //HACK: https://github.com/dotnet/corefx/issues/8768
                //this should be changed back to Configuration["SlambyApi:Redis:Configuration"] when https://github.com/dotnet/corefx/issues/11564 is closed
                options.Configuration = services.BuildServiceProvider().GetService<ConfigurationOptions>().ToString();
            });

            services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30);
                options.CookieName = ".SlambyAPI";
            });

            services.AddCors();
        }

        private void ConfigureResourceDependentVars(IServiceCollection services)
        {
            var maxIndexBulkSize = Configuration.GetValue("SlambyApi:Resources:MaxIndexBulkSize", 0);
            var maxIndexBulkCount = Configuration.GetValue("SlambyApi:Resources:MaxIndexBulkCount", 0);
            var maxSearchBulkCount = Configuration.GetValue("SlambyApi:Resources:MaxSearchBulkCount", 0);

            if (maxIndexBulkSize == 0 || maxIndexBulkCount == 0 || maxSearchBulkCount == 0)
            {
                var machineResourceService = services.BuildServiceProvider().GetService<Common.Services.MachineResourceService>();
                machineResourceService.UpdateResourcesManually();

                if (machineResourceService.Status.TotalMemory > 0)
                {
                    maxIndexBulkSize = Convert.ToInt32(machineResourceService.Status.TotalMemory * 1024 * 1024 / 2 / 500);
                    maxIndexBulkCount = Convert.ToInt32(machineResourceService.Status.TotalMemory / 12);
                }
                else
                {
                    maxIndexBulkSize = 100000;
                    maxIndexBulkCount = 50;
                }

                services.Configure<SiteConfig>(sc => sc.Resources.MaxIndexBulkSize = maxIndexBulkSize);
                services.Configure<SiteConfig>(sc => sc.Resources.MaxIndexBulkCount = maxIndexBulkCount);
                services.Configure<SiteConfig>(sc => sc.Resources.MaxSearchBulkCount = maxIndexBulkCount);
            }
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

            app.UseSecretValidator();
            app.UseRequestSizeLimit();

            if (!string.IsNullOrEmpty(Configuration.GetValue("SlambyApi:Elm:Key", string.Empty)))
            {
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

            try
            {
                startupService.Startup();


                if (env.IsDevelopment())
                {
                    app.UseSwagger();
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
                app.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = new PhysicalFileProvider(Path.Combine(env.WebRootPath, "assets")),
                    RequestPath = new PathString("/assets"),
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
    }
}