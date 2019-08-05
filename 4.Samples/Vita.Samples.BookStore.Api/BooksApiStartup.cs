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

  public class BooksApiStartup {
    public BooksApiStartup(IConfiguration configuration) {
      Configuration = configuration;
    }

    public IConfiguration Configuration { get; }
    public BooksEntityApp EntityApp;

    // This method gets called by the runtime. Use this method to add services to the container.
    public void ConfigureServices(IServiceCollection services) {
      // entity app
      var connStr = Configuration["MsSqlConnectionString"];
      EntityApp = CreateBooksEntityApp(connStr);

      // Setup Authentication with jwt token
      var jwtSecret = Configuration["JwtSecret"];
      WebHelper.SetupJwtTokenAuthentication(services, jwtSecret); 

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
