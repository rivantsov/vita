using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vita.Data;
using Vita.Data.MsSql;
using Vita.Web;

namespace Vita.Samples.BookStore.Api {

  public class BooksApiStartup {
    public BooksApiStartup(IConfiguration configuration) {
      Configuration = configuration; 
    }

    public IConfiguration Configuration { get; }
    public BooksEntityApp BooksEntityApp;

    // This method gets called by the runtime. Use this method to add services to the container.
    public void ConfigureServices(IServiceCollection services) {
      // entity app
      var connStr = Configuration["MsSqlConnectionString"];
      BooksEntityApp = CreateBooksEntityApp(connStr);

      // Setup Authentication with jwt token
      var jwtSecret = Configuration["JwtSecret"];
      var jwtTokenHandler = new VitaJwtTokenHandler(BooksEntityApp, services, jwtSecret);
      services.Add(new ServiceDescriptor(typeof(IAuthenticationTokenHandler), jwtTokenHandler));

      var loginAssembly = typeof(Vita.Modules.Login.Api.LoginController).Assembly;
      services.AddControllers()
        // Note: by default System.Text.Json ns/serializer is used by AspNetCore 3.1; this serializer is broken piece of sh..
        // - does not serialize fields, does not handle dictionaries, etc. So we put back Newtonsoft serializer.
        .AddNewtonsoftJson()
        // make API controllers in Vita.Modules.Login.dll discovered by ASP.NET infrastructure
        .PartManager.ApplicationParts.Add(new AssemblyPart(loginAssembly));

      // clear out default configuration - it installs Console output logger which sends out tons of garbage
      //  https://weblog.west-wind.com/posts/2018/Dec/31/Dont-let-ASPNET-Core-Default-Console-Logging-Slow-your-App-down
      services.AddLogging(config => {
        config.ClearProviders();
      });

    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env) {
      if (env.IsDevelopment()) {
        app.UseDeveloperExceptionPage();
      }

      app.UseHttpsRedirection();

      // Vita middleware
      app.UseMiddleware<VitaWebMiddleware>(BooksEntityApp);
      
      app.UseAuthentication();
      app.UseRouting();
      app.UseAuthorization();
      app.UseEndpoints(endpoints =>
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
