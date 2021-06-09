using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using Vita.Data.Driver;
using Vita.Entities;

namespace Vita.Testing.BasicTests.Misc {

  //Description attr is defined in 2 places, resolving ambiguity. The other def in in Microsoft.VisualStudio.TestTools
  using DescriptionAttribute = System.ComponentModel.DescriptionAttribute;

  [Entity, OrderBy("Model")]
  [Description("Represents vehicle entity.")]
  public interface IVehicle {
    [PrimaryKey, Auto]
    Guid Id { get; set; }

    [Size(30)]
    //we test that these attributes will be passed to the object's property
    [Description("Model of the vehicle.")]
    [Browsable(true), DisplayName("Vehicle model"), System.ComponentModel.CategoryAttribute("Miscellaneous")]
    string Model { get; set; }

    int Year { get; set; }

    [PropagateUpdatedOn]
    IDriver Owner { get; set; }
    [Nullable]
    IDriver Driver { get; set; }
    // Bug fix test - declaring FK column explicitly
    // Guid Owner_Id { get; set; }
  }

  [Entity]
  public interface IDriver {
    [PrimaryKey, Auto]
    Guid Id { get; set; }
    [Utc, Auto(AutoType.UpdatedOn)]
    DateTime UpdatedOn { get; }

    [Size(30), Unique(Alias = "LicenseNumber")]
    string LicenseNumber { get; set; }
    DateTime LicenseIssuedOn { get; set; }

    [Size(Sizes.Name)]
    string FirstName { get; set; }
    [Size(Sizes.Name)]
    string LastName { get; set; }

    [OneToMany("Owner")]
    IList<IVehicle> Vehicles { get; set; }

    [OneToMany("Driver")]
    IList<IVehicle> DrivesVehicles { get; set; }

    //DependsOn is optional, used for auto PropertyChanged firing
    [Computed(typeof(MiscTestsExtensions), "GetFullName"), DependsOn("FirstName,LastName")]
    string FullName { get; }
    // another computer prop - test for a reported bug
    [Computed(typeof(MiscTestsExtensions), "GetLastFirst"), DependsOn("FirstName,LastName")]
    string LastFirst { get; }

    //Persistent computed property
    [Computed(typeof(MiscTestsExtensions), "GetLicenseHash", Persist = true), DependsOn("LicenseNumber")]
    int LicenseHash { get; }

    [Nullable] //test for fix
    IDriver Instructor { get; set; }
  }

  // We skip defining custom entity module and use base EntityModule class
  public class MiscTestsEntityApp : EntityApp {
    public MiscTestsEntityApp() {
      var area = AddArea("misc");
      var mainModule = new EntityModule(area, "MainModule");
      mainModule.RegisterSqlFunctions(typeof(CustomSqlFunctions));
      mainModule.RegisterEntities(typeof(IVehicle), typeof(IDriver));
    }
  }//class

  public static class MiscTestsExtensions {
    public static IVehicle NewVehicle(this IEntitySession session, string model, int year, IDriver owner, IDriver driver = null) {
      var veh = session.NewEntity<IVehicle>();
      veh.Model = model;
      veh.Year = year;
      veh.Owner = owner;
      veh.Driver = driver;
      return veh;
    }
    public static IDriver NewDriver(this IEntitySession session, string licenseNumber, string firstName, string lastName) {
      var driver = session.NewEntity<IDriver>();
      driver.LicenseNumber = licenseNumber;
      driver.LicenseIssuedOn = DateTime.Now.Date;
      driver.FirstName = firstName;
      driver.LastName = lastName;
      return driver;
    }
    public static string GetFullName(IDriver driver) {
      return driver.FirstName + " " + driver.LastName;
    }
    public static string GetLastFirst(IDriver driver) {
      return driver.LastName + ", " + driver.FirstName;
    }
    public static int GetLicenseHash(IDriver driver) {
      return driver.LicenseNumber.GetHashCode();
    }

  }

  // The class should be registered with entity module using RegisterSqlFunctions
  public static class CustomSqlFunctions {

    [SqlExpression(DbServerType.MsSql, "DATEADD(YEAR, {years}, {dt})")]
    [SqlExpression(DbServerType.MySql, "DATE_ADD({dt}, INTERVAL {years} YEAR)")]
    [SqlExpression(DbServerType.Postgres, "({dt} + {years} * INTERVAL '1 year')")]
    [SqlExpression(DbServerType.Oracle, "ADD_MONTHS({dt}, 12 * {years})")]
    [SqlExpression(DbServerType.SQLite, "DATE({dt}, '{years} years')")]
    public static DateTime DbAddYears(this DateTime dt, int years) {
      throw new NotImplementedException("AddYears should not be called directly, only in SQL LINQ expressions.");
    }

  }


}
