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
  public class Startup {
    public Startup(IConfiguration configuration) {
      Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    // This method gets called by the runtime. Use this method to add services to the container.
    public void ConfigureServices(IServiceCollection services) {
      var jwtSecret = Configuration["JwtSecret"];
      var jwtSecretBytes = Encoding.ASCII.GetBytes(jwtSecret);
      var jwtKey = new SymmetricSecurityKey(jwtSecretBytes);
      SetupJwtAuthentication(services, jwtKey); 
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

      // entity app
      var connStr = Configuration["MsSqlConnectionString"];
      var entApp = SetupBooksApp(connStr);
      // web call context handler
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

    private void SetupJwtAuthentication(IServiceCollection services, SymmetricSecurityKey secKey) {
      services.AddAuthentication(x =>
      {
        x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
      })
      .AddJwtBearer(x => {
        x.Events = new JwtBearerEvents {
          OnTokenValidated = context =>
          {
            var claims = context.Principal.Claims;
            /*
            //var userService = context.HttpContext.RequestServices.GetRequiredService<IUserService>();
            var userId = int.Parse(context.Principal.Identity.Name); 
            var user = userService.GetById(userId);
            if (user == null) {
                    // return unauthorized if user no longer exists
                    context.Fail("Unauthorized");
            }
            */
            return Task.CompletedTask;
          }
        };
        x.RequireHttpsMetadata = false;
        x.SaveToken = true;
        x.TokenValidationParameters = new TokenValidationParameters {
          ValidateIssuerSigningKey = true,
          IssuerSigningKey = secKey, // new SymmetricSecurityKey(secKkey),
          ValidateIssuer = false,
          ValidateAudience = false
        };
      });


    }
  }
}
