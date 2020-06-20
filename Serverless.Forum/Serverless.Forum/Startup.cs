using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PaulMiami.AspNetCore.Mvc.Recaptcha;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Services;
using Serverless.Forum.Utilities;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Serverless.Forum
{
    public class Startup
    {
        public Startup(IConfiguration configuration, IHostEnvironment env)
        {
            Configuration = configuration;
            Env = env;
        }

        public IConfiguration Configuration { get; private set; }
        public IHostEnvironment Env { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

            services.AddTransient<IHttpContextAccessor, HttpContextAccessor>();
            services.AddSingleton(Configuration);

            services.AddDistributedMemoryCache();
            services.AddSession(options => {
                options.IdleTimeout = TimeSpan.FromDays(30);
                options.Cookie.IsEssential = true;
            });
            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies 
                // is needed for a given request.
                options.CheckConsentNeeded = context => false;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

            if (Env.IsStaging() || Env.IsProduction())
            {
                services.AddLogging(log => log.AddEventLog());
            }
            else
            {
                services.AddLogging(log => log.AddConsole());
            }

            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie();

            var builder = services.AddMvc()
                .SetCompatibilityVersion(CompatibilityVersion.Version_3_0)
                .AddRazorOptions(o =>
                {
                    o.PageViewLocationFormats.Add("~/Pages/CustomPartials/{0}.cshtml");
                    o.PageViewLocationFormats.Add("~/Pages/CustomPartials/Admin/{0}.cshtml");
                    o.PageViewLocationFormats.Add("~/Pages/CustomPartials/Email/{0}.cshtml");
                })
                .AddJsonOptions(o =>
                {
                    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                    o.JsonSerializerOptions.Converters.Add(new DateTimeConverter());
                    o.JsonSerializerOptions.IgnoreNullValues = true;
                });

#if DEBUG
            if (Env.IsDevelopment())
            {
                builder.AddRazorRuntimeCompilation();
            }
#endif
            services.Configure<IISServerOptions>(options =>
            {
                options.AutomaticAuthentication = false;
            });

            services.AddDataProtection();
            services.AddAntiforgery(o => o.HeaderName = "XSRF-TOKEN");

            services.AddRecaptcha(new RecaptchaOptions
            {
                SiteKey = Configuration["Recaptcha:SiteKey"],
                SecretKey = Configuration["Recaptcha:SecretKey"],
            });

            services.AddHttpClient();

            services.AddTransient<Utils>();
            services.AddTransient<AdminForumService>();
            services.AddTransient<AdminUserService>();
            services.AddTransient<WritingToolsService>();
            services.AddTransient<CacheService>();
            services.AddTransient<ForumTreeService>();
            services.AddTransient<PostService>();
            services.AddTransient<UserService>();
            services.AddTransient<StorageService>();
            services.AddTransient<ModeratorService>();
            services.AddTransient<BBCodeRenderingService>();
            services.AddDbContext<ForumDbContext>(options => options.UseMySQL(Configuration["ForumDbConnectionString"]), ServiceLifetime.Transient);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json",
                             optional: false,
                             reloadOnChange: true)
                .AddEnvironmentVariables();

            var utils = app.ApplicationServices.GetService<Utils>();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                builder.AddUserSecrets<Startup>();
            }
            else
            {
                app.UseExceptionHandler(errorApp =>
                {
                    errorApp.Run(context =>
                    {
                        var handler = context.Features.Get<IExceptionHandlerPathFeature>();
                        context.Response.Redirect($"/Error?id={utils.HandleError(handler.Error, handler.Path)}");
                        return Task.CompletedTask;
                    });
                });
                app.UseHsts();
            }

            app.UseRouting();
            app.UseRequestLocalization();
            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseCookiePolicy();
            app.UseAuthentication();
            app.UseSession();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapRazorPages();
            });

            Configuration = builder.Build();
        }
    }

    public class DateTimeConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            Debug.Assert(typeToConvert == typeof(DateTime));
            return DateTime.Parse(reader.GetString());
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToUniversalTime().ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ssZ"));
        }
    }
}
