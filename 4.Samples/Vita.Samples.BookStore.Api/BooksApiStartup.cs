﻿using System;
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

      services.AddRouting();
      services.AddMvc()
        .AddXmlDataContractSerializerFormatters()
        // Register controllers in Login.Api assembly - they provide Login functionality
        .AddApplicationPart(typeof(Vita.Modules.Login.Api.LoginController).Assembly)
        .AddControllersAsServices()
        ;
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env) {
      app.UseHttpsRedirection();

      // Vita middleware
      var stt = new VitaWebMiddlewareSettings(); 
      app.UseMiddleware<VitaWebMiddleware>(BooksEntityApp, stt);
      
      app.UseAuthentication();
      app.UseRouting(); 
      // app.UseMvc();
      app.UseEndpoints(endpoints => {  });
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
