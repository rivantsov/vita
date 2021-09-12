using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MyEntityModel;
using Vita.Data;
using Vita.Data.MsSql;
using Vita.Entities;

namespace MyConsoleApp
{
  class Program
  {
    static void Main(string[] args)
    {
      try
      {
        Setup.Init();
        CreateCustomer();
        var cust = ReadCustomer();
        if(cust == null)
          Console.WriteLine("Error: failed to load a customer");
        else
          Console.WriteLine("Success: created a customer and loaded him back.");

      } catch(Exception ex)
      {
        Console.WriteLine("===================== Error ==============================");
        Console.WriteLine(ex.ToString());
      }
      Console.WriteLine("press any key to exit...");
      Console.ReadKey();
    }

    static void CreateCustomer()
    {
      var session = Setup.App.OpenSession();
      var cust = session.NewEntity<ICustomer>();
      cust.Name = "JohnD";
      cust.Email = "john@mail.com";
      session.SaveChanges(); 
    }

    static ICustomer ReadCustomer()
    {
      var session = Setup.App.OpenSession();
      var cust = session.EntitySet<ICustomer>().FirstOrDefault();
      return cust; 
    }

  }
}
