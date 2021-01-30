using System;
using System.Threading.Tasks;
using BookStore.SampleData;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NGraphQL.Server;
using NGraphQL.Server.AspNetCore;
using Vita.Data;
using Vita.Data.MsSql;
using Vita.Entities.DbInfo;
using Vita.Tools;

namespace BookStore.GraphQLServer {

  public class GraphQLAspNetServerStartup
  {
    public static bool StartGrpaphiql = true; // test project sets this to false

    public GraphQLAspNetServerStartup(IConfiguration configuration)
    {
      Configuration = configuration;
    }

    public IConfiguration Configuration { get; }
    GraphQLHttpServer _graphQLServer;

    // This method gets called by the runtime. Use this method to add services to the container.
    public void ConfigureServices(IServiceCollection services) {
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env) {
      if (env.IsDevelopment())
      {
        app.UseDeveloperExceptionPage();
      }
      app.UseHttpsRedirection();
      app.UseRouting();

      // create books app connected to database; 
      CreateBooksEntityApp();
      // create GraphQL Http server and configure GraphQL endpoints
      _graphQLServer = CreateGraphQLHttpServer(); 
      app.UseEndpoints(endpoints => {
        endpoints.MapPost("graphql", HandleRequest);
        endpoints.MapGet("graphql", HandleRequest);
        endpoints.MapGet("graphql/schema", HandleRequest);
      });
      // Use GraphiQL UI
      if (StartGrpaphiql)
        app.UseGraphiQLServer();
    }

    private Task HandleRequest(HttpContext context) {
      return _graphQLServer.HandleGraphQLHttpRequestAsync(context);
    }

    private GraphQLHttpServer CreateGraphQLHttpServer() {
      var server = new NGraphQL.Server.GraphQLServer(BooksEntityApp.Instance);
      server.RegisterModules(new BooksGraphQLModule());
      return new GraphQLHttpServer(server);
    }

    private BooksEntityApp CreateBooksEntityApp() {
      var connStr = Configuration["MsSqlConnectionString"];
      if (BooksEntityApp.Instance != null)
        return BooksEntityApp.Instance;

      var booksApp = new BooksEntityApp();
      booksApp.Init();

      //connect to db
      var driver = new MsSqlDbDriver();
      var dbOptions = driver.GetDefaultOptions();
      var dbSettings = new DbSettings(driver, dbOptions, connStr, upgradeMode: DbUpgradeMode.Always);
      booksApp.ConnectTo(dbSettings);
      return booksApp;
    }

    private static void CreateSampleData() {
      DataUtility.DeleteAllData(BooksEntityApp.Instance, 
        exceptEntities: new Type[] { typeof(IDbInfo), typeof(IDbModuleInfo) });
      SampleDataGenerator.CreateUnitTestData(BooksEntityApp.Instance);
    }


  }
}
