using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Vita.Data;
using Vita.Data.MsSql;
using Vita.Entities;
using Vita.Web;

namespace Vita.Samples.BookStore.Api {

  public class ApiStartup {
    public ApiStartup(IConfiguration configuration) {
      Configuration = configuration;
    }

    public IConfiguration Configuration { get; }
    public BooksEntityApp EntityApp;

    // This method gets called by the runtime. Use this method to add services to the container.
    public void ConfigureServices(IServiceCollection services) {
      // entity app
      var connStr = Configuration["MsSqlConnectionString"];
      EntityApp = SetupBooksApp(connStr);
      //jwt token handler
      var jwtSecret = Configuration["JwtSecret"];
      var jwtTokenHandler = new VitaJwtTokenHandler(EntityApp, services, jwtSecret);
      services.Add(new ServiceDescriptor(typeof(IAuthenticationTokenHandler), jwtTokenHandler));
      services.AddRouting();
      // add action filter; Json is there by default, we also add xml for testing xml serialization
      services.AddMvc()
        .SetCompatibilityVersion(CompatibilityVersion.Version_2_1)
        .AddXmlDataContractSerializerFormatters()
        ;
      ;

    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IHostingEnvironment env) {

      if (env.IsDevelopment()) {
        app.UseDeveloperExceptionPage();
      } else {
        app.UseHsts();
      }

      app.UseHttpsRedirection();

      // Vita middleware
      var stt = new VitaWebMiddlewareSettings(); 
      app.UseMiddleware<VitaWebMiddleware>(EntityApp, stt);
      
      app.UseAuthentication();

      app.UseMvc();
    }

    private BooksEntityApp SetupBooksApp(string connString) {
      // If we are running WebTests, the BooksApp is already setup
      if (BooksEntityApp.Instance != null)
        return BooksEntityApp.Instance;

      var booksApp = new BooksEntityApp();
      booksApp.EntityClassProvider = Vita.Entities.Emit.EntityClassEmitter.CreateEntityClassProvider();
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
