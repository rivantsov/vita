using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BookStore.GraphQL
{
  public class Program {
    public static void Main(string[] args) {
      ServerSetup.SetupServer().Wait();
    }

  }
}
