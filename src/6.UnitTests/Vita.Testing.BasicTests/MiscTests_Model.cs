using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
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
  public interface IDriver: IDriverComputedColumns {
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
    // the second paramter for Computed attr is optional
    [Computed("GetFullName", typeof(MiscModelExtensions)), DependsOn("FirstName,LastName")]
    string FullName { get; }
    // another computer prop - test for a reported bug
    [Computed("GetLastFirst"), DependsOn("FirstName,LastName")]
    string LastFirst { get; }

    //Persistent computed property
    [Computed("GetLicenseHash", Persist = true), DependsOn("LicenseNumber")]
    int LicenseHash { get; }

    [Nullable] //test for fix
    IDriver Instructor { get; set; }
  }

  public interface IDriverComputedColumns {
    // computed dynamically in SQL expression; there is no DbColumn in the table
    [DateOnly, DbComputed(kind: DbComputedKind.NoColumn)]
    [SqlExpression(DbServerType.MsSql, "DATEADD(YEAR, 5, {table}.LicenseIssuedOn)")]
    [SqlExpression(DbServerType.MySql, "DATE_ADD({table}.\"LicenseIssuedOn\", INTERVAL 5 YEAR)")]
    [SqlExpression(DbServerType.Postgres, "({table}.\"LicenseIssuedOn\" + 5 * INTERVAL '1 year')")]
    [SqlExpression(DbServerType.Oracle, "ADD_MONTHS({table}.\"LicenseIssuedOn\", 12 * 5)")]
    [SqlExpression(DbServerType.SQLite, "DATE({table}.\"LicenseIssuedOn\", '5 years')")]
    DateTime LicenseExpiresOn_NoCol { get; }

    // Servers parse and reformat expression when storing it with Db column.
    // To let schema comparison work, we normally change to normalized expr looked up in db AFTER first schema update
    //  for your convenience VITA prints out normalized expr when there's mismatch to Debug.WriteLine
    //  (see it in Output window)
    [DateOnly, DbComputed(kind: DbComputedKind.Column)]
    [SqlExpression(DbServerType.MsSql, "DATEADD(YEAR, 5, LicenseIssuedOn)")] 
    [SqlExpression(DbServerType.MySql, "DATE_ADD(LicenseIssuedOn, INTERVAL 5 YEAR)")] 
    [SqlExpression(DbServerType.Postgres, "(\"LicenseIssuedOn\" + 5 * INTERVAL '1 year')")]
    [SqlExpression(DbServerType.Oracle, "ADD_MONTHS(\"LicenseIssuedOn\", 12 * 5)")]
    [SqlExpression(DbServerType.SQLite, "DATE(LicenseIssuedOn, '5 years')")]
    DateTime LicenseExpiresOn_Col { get; }
  }

  // We skip defining custom entity module and use base EntityModule class
  public class MiscTestsEntityApp : EntityApp {
    public MiscTestsEntityApp() {
      var area = AddArea("misc");
      var mainModule = new EntityModule(area, "MainModule");
      mainModule.RegisterEntities(typeof(IVehicle), typeof(IDriver));
      mainModule.RegisterFunctions(typeof(MiscModelExtensions));
    }
  }//class

  public static class MiscModelExtensions {
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

    // Computed columns -------------------------------------------
    // the GetFullName, GetLastFirst and GetLicenseHash support corresponding IDriver entity members
    //  that are CLR-only; the property is modeled only on c#/client side and no DB/SQL is involved
    public static string GetFullName(IDriver driver) {
      return driver.FirstName + " " + driver.LastName;
    }
    public static string GetLastFirst(IDriver driver) {
      return driver.LastName + ", " + driver.FirstName;
    }
    public static int GetLicenseHash(IDriver driver) {
      return driver.LicenseNumber.GetHashCode();
    }

    // A function can be used in LINQ expressions, it results in SQL expression added to SQL select,
    //  to calculate the value on DB server side. We must provide a separate SQL template for each server type.
    //  SQL template can use placeholders like {dt} matching the names of the CLR function parameters.
    [SqlExpression(DbServerType.MsSql, "DATEADD(YEAR, {years}, {dt})")]
    [SqlExpression(DbServerType.MySql, "DATE_ADD({dt}, INTERVAL {years} YEAR)")]
    [SqlExpression(DbServerType.Postgres, "({dt} + {years} * INTERVAL '1 year')")]
    [SqlExpression(DbServerType.Oracle, "ADD_MONTHS({dt}, 12 * {years})")]
    [SqlExpression(DbServerType.SQLite, "DATE({dt}, '{years} years')")]
    public static DateTime DbAddYears(this DateTime dt, int years) {
      CannotCallDirectly();
      return default;
    }

    private static void CannotCallDirectly([CallerMemberName] string methodName = null) {
      throw new NotImplementedException(
        $"Function '{methodName}' may not be called directly, only in LINQ expressions.");
    }


    // Extra example of IsNull snippet for support issue #217
    // MS SQL IsNull function: https://www.w3schools.com/sql/func_sqlserver_isnull.asp
    [SqlExpression(DbServerType.MsSql, "ISNULL({value}, 0)")]
    public static Decimal NullAsZero(this Decimal value) {
      CannotCallDirectly();
      return default;
    }

  }


}
