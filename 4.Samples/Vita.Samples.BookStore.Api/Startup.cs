using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vita.Data;
using Vita.Data.MsSql;
using Vita.Entities;
using Vita.Web;

namespace Vita.Samples.BookStore.Api {
  public class Startup {
    public Startup(IConfiguration configuration) {
      Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    // This method gets called by the runtime. Use this method to add services to the container.
    public void ConfigureServices(IServiceCollection services) {
      services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IHostingEnvironment env) {
      if (env.IsDevelopment()) {
        app.UseDeveloperExceptionPage();
      } else {
        app.UseHsts();
      }

      app.UseHttpsRedirection();

      var connStr = Configuration["MsSqlConnectionString"];
      var entApp = SetupBooksApp(connStr);
      var handler = new WebCallContextHandler(entApp, null);
      app.UseWebCallContextHandler(handler);


      app.UseAuthentication();

      app.UseMvc();
    }

    private BooksEntityApp SetupBooksApp(string connString) {
      var booksApp = new BooksEntityApp();
      booksApp.EntityClassProvider = Vita.Entities.Emit.EntityClassEmitter.CreateEntityClassProvider();
      booksApp.LogPath = "..\\AppData\\_booksLog.log";
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
