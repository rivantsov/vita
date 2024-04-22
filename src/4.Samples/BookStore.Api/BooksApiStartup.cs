using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vita.Data;
using Vita.Data.MsSql;
using Vita.Web;

namespace BookStore.Api {

  public class BooksApiStartup {
    public BooksApiStartup(IConfiguration configuration) {
      Configuration = configuration; 
    }

    public IConfiguration Configuration { get; }
    public BooksEntityApp BooksEntityApp;

    // This method gets called by the runtime. Use this method to add services to the container.
    public void ConfigureServices(IServiceCollection services) {
      // entity builder
      var connStr = Configuration["MsSqlConnectionString"];
      BooksEntityApp = CreateBooksEntityApp(connStr);

      // Setup Authentication with jwt token
      var jwtSecret = Configuration["JwtSecret"];
      var jwtTokenHandler = new VitaJwtTokenHandler(BooksEntityApp, services, jwtSecret);
      services.Add(new ServiceDescriptor(typeof(IAuthenticationTokenHandler), jwtTokenHandler));

      var loginAssembly = typeof(Vita.Modules.Login.Api.LoginController).Assembly;
      services.AddControllers()
        // setup Json options explicitly; default settings are 'strange'
        .AddJsonOptions(options => {
          var serOptions = options.JsonSerializerOptions;
          serOptions.PropertyNameCaseInsensitive = true;
          serOptions.PropertyNamingPolicy = null; //names as-is
          serOptions.IncludeFields = true;
          serOptions.WriteIndented = true;
          serOptions.Converters.Add(new JsonStringEnumConverter());
        })
        // make API controllers in Vita.Modules.Login.dll discovered by ASP.NET infrastructure
        .PartManager.ApplicationParts.Add(new AssemblyPart(loginAssembly));
        
      // clear out default configuration - it installs Console output logger which sends out tons of garbage
      //  https://weblog.west-wind.com/posts/2018/Dec/31/Dont-let-ASPNET-Core-Default-Console-Logging-Slow-your-App-down
      services.AddLogging(config => {
        config.ClearProviders();
      });
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder builder, IWebHostEnvironment env) {
      if (env.IsDevelopment()) {
        builder.UseDeveloperExceptionPage();
      }
      builder.UseHttpsRedirection();

      // Vita middleware
      builder.UseMiddleware<VitaWebMiddleware>(BooksEntityApp);
      
      builder.UseAuthentication();
      builder.UseRouting();
      builder.UseAuthorization();
      builder.UseEndpoints(endpoints =>
      {
        endpoints.MapControllers();
      });
    }

    private BooksEntityApp CreateBooksEntityApp(string connString) {
      // If we are running WebTests, the BooksApp is already setup
      if (BooksEntityApp.Instance != null)
        return BooksEntityApp.Instance;

      var booksApp = new BooksEntityApp();
      booksApp.Init();
      
      //connect to db
      var driver = new MsSqlDbDriver();
      var dbOptions = driver.GetDefaultOptions();
      var dbSettings = new DbSettings(driver, dbOptions, connString, upgradeMode: DbUpgradeMode.Always);
      booksApp.ConnectTo(dbSettings);
      return booksApp; 
    }

  }
}
