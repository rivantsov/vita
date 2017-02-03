using System;
using System.ComponentModel;
using System.Linq;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Vita.Entities;
using Vita.Entities.Runtime;
using Vita.Data;
using Vita.Data.Model;
using Vita.UnitTests.Common;


namespace Vita.UnitTests.Basic {
  using Binary = Vita.Common.Binary;
  using Vita.Data.Driver;


  [TestClass] 
  public class DataTypesTests {

    public enum SimpleEnum {
      Zero,
      One,
      Two,
      Three,
    }

    [Flags]
    public enum BitEnum : byte {
      None,
      Bit0 = 1,
      Bit1 = 1 << 1,
      Bit2 = 1 << 2,
    }

    [Entity]
    //testing really long index name
    //[Index(IndexName ="IX_LongIndex", MemberNames = "StringProp,ByteProp,Int16Prop,Int32Prop,Int64Prop,Int32NullProp,DoubleProp,DoublePropNull,SingleProp,DecProp,MoneyProp,DateTimeProp,EnumProp")]
    public interface IDataTypesEntity {

      // Guid
      [PrimaryKey]
      Guid Id { get; set; }
        
      // strings
      [Size(20), Nullable]
      string StringProp { get; set; }
      [Unlimited, Nullable]
      string MemoProp { get; set; }

      //bool 
      bool BoolProp { get; set; }

      // integer types
      byte ByteProp { get; set; }
      Int16 Int16Prop { get; set; }
      Int32 Int32Prop { get; set; }
      Int64 Int64Prop { get; set; }
      Int32? Int32NullProp { get; set; }

      // floating point types
      double DoubleProp { get; set; }
      double? DoublePropNull { get; set; }
      float SingleProp { get; set; } //equiv System.Single
      // decimal/ money
      decimal DecProp { get; set; }
      [Currency]
      decimal MoneyProp { get; set; }

      // date-time
      DateTime DateTimeProp { get; set; }
      TimeSpan? TimeProp { get; set; }

      //enums
      SimpleEnum EnumProp { get; set; }
      SimpleEnum? EnumNullProp { get; set; }
      BitEnum BitsProp { get; set; }
      BitEnum? BitsNullProp { get; set; }

      // binary 
      [Size(64)]
      byte[] ByteArrayProp { get; set; }
      [Nullable, Size(128)]
      Binary BinaryProp { get; set; }

      [Nullable, Unlimited]
      Binary BigBinaryProp { get; set; }

      //These types are not supported by SQL Server directly - but framework provides support for them and converts automatically when needed.
      sbyte SByteProp { get; set; }
      UInt16 UInt16Prop { get; set; }
      UInt32 UInt32Prop { get; set; }
      UInt64 UInt64Prop { get; set; }

      char CharProp { get; set; }
    }

    [Entity]
    //special MS SQL types - entity is registered only for MS SQl test
    public interface IMsSqlDataTypesEntity {
      // Guid
      [PrimaryKey]
      Guid Id { get; set; }

      DateTimeOffset DateTimeOffsetProp { get; set; }

      [Column(DbType = DbType.AnsiStringFixedLength, Size = 10)]
      string CharNProp { get; set; }

      [Column(DbType = DbType.StringFixedLength, Size = 10)]
      string NCharNProp { get; set; }

      [Column(DbType = DbType.AnsiString, Size = 10)]
      string VarCharProp { get; set; }

      [Column(DbType = DbType.Date)]
      DateTime DateProp { get; set; }

      [Column(DbType = DbType.DateTime)] //not DateTime2 which is default
      DateTime DateTimeProp { get; set; }

      [Column(DbType = DbType.Time)]
      TimeSpan TimeProp { get; set; }

      [Column(DbTypeSpec = "SmallDateTime")]
      DateTime SmallDateTimeProp { get; set; }

      [RowVersion]
      byte[] TimeStampProp { get; set; }

      [Column(DbTypeSpec="geography"), Nullable]
      byte[] GeographyProp { get; set; }

      [Column(DbTypeSpec = "geometry"), Nullable]
      byte[] GeometryProp { get; set; }

      [Column(DbTypeSpec = "hierarchyid"), Nullable]
      byte[] HierarchyIdProp { get; set; }

      [Column(DbTypeSpec = "image")]
      byte[] ImageProp { get; set; }

      [Column(DbTypeSpec = "ntext")]
      string NTextProp { get; set; }

      [Column(DbTypeSpec = "text")]
      string TextProp { get; set; }

      [Column(DbTypeSpec = "xml")]
      string XmlProp { get; set; }

      [Column(DbTypeSpec = "smallmoney")]
      Decimal SmallMoneyProp { get; set; }

      [Column(DbTypeSpec = "sql_variant")] 
      object SqlVariantProp { get; set; }

      [Column("DisplayUrl", DbTypeSpec = "varchar(max)"), Unlimited, Nullable]
      //[Column("DisplayUrl", DbType = DbType.AnsiString), Unlimited, Nullable] // this works ok, but column type is 'Text'
      string DisplayUrl { get; set; }

    }

    [Entity]
    public interface IMsSqlRowVersionedProduct {
      // Guid
      [PrimaryKey, Identity]
      int Id { get; }
      string Name { get; set; }
      [Size(100)]
      string Description { get; set; }
      double Price { get; set; }

      [RowVersion]
      byte[] RowVersion { get; set; }
    }


    // We skip defining custom entity module and use base EntityModule class
    public class DataTypesTestEntityApp : EntityApp {
      public DataTypesTestEntityApp() {
        var area = AddArea("types");
        var mainModule = new EntityModule(area, "MainModule");
        mainModule.RegisterEntities(typeof(IDataTypesEntity));
        switch(Startup.ServerType) {
          case DbServerType.MsSql: 
            mainModule.RegisterEntities(typeof(IMsSqlDataTypesEntity), typeof(IMsSqlRowVersionedProduct));
            break; 

        }
      }

    }//class

    DataTypesTestEntityApp _app;

    [TestInitialize]
    public void Init() {
      Startup.DropSchemaObjects("types");
      _app = new DataTypesTestEntityApp();
      Startup.ActivateApp(_app);
    }

    [TestCleanup]
    public void Cleanup() {
      if(_app != null)
        _app.Shutdown(); 
    }

    private object SQLiteDateToString(object x) {
      if(x == null || x == DBNull.Value)
        return DBNull.Value;
      var dt = (DateTime)x;
      var result = Vita.Common.ConvertHelper.DateTimeToUniString(dt);
      return result;
    }

    private object SQLiteStringToDate(object x) {
      if(x == null || x == DBNull.Value)
        return DBNull.Value;
      var str = x as string;
      if(string.IsNullOrWhiteSpace(str))
        return DBNull.Value;
      DateTime result = DateTime.Parse(str, CultureInfo.InvariantCulture); //, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
      return result;
    }

    [TestMethod]
    public void TestDataTypes() {
      var session = _app.OpenSession();
      //Create 2 entities, to verify how batch updates work for all data types; batch is used only when there's more than 1 update
      var ent1 = CreateDataTypesEntity(session, "abcd", "Unlimited property 1");
      var ent2 = CreateDataTypesEntity(session, "abcdefgh", "Unlimited property 2");
      //var ent3 = CreateDataTypesEntity(session, string.Empty, string.Empty);
      // var ent4 = CreateDataTypesEntity(session, null, null);

      var ent1Id = ent1.Id; 
      session.SaveChanges();

      var session2 = _app.OpenSession();
      var ent1copy = session2.GetEntity<IDataTypesEntity>(ent1Id);
      Assert.IsNotNull(ent1copy, "Failed to load saved record");

      Assert.AreEqual(ent1.StringProp, ent1copy.StringProp.Trim());
      Assert.AreEqual(ent1.MemoProp, ent1copy.MemoProp); 
      Assert.AreEqual(ent1.BoolProp, ent1copy.BoolProp);
      Assert.AreEqual(ent1.CharProp, ent1copy.CharProp);
      Assert.AreEqual(ent1.ByteProp, ent1copy.ByteProp);
      Assert.AreEqual(ent1.Int16Prop, ent1copy.Int16Prop);
      Assert.AreEqual(ent1.Int32Prop, ent1copy.Int32Prop);
      Assert.AreEqual(ent1.Int64Prop, ent1copy.Int64Prop);
      Assert.AreEqual(ent1.Int32NullProp, ent1copy.Int32NullProp);

      Assert.AreEqual(ent1.DoubleProp, ent1copy.DoubleProp);
      Assert.AreEqual(ent1.DoublePropNull, ent1copy.DoublePropNull);
      Assert.AreEqual(ent1.SingleProp, ent1copy.SingleProp);
      Assert.AreEqual(ent1.DecProp, ent1copy.DecProp);
      Assert.AreEqual(ent1.MoneyProp, ent1copy.MoneyProp);

      Assert.IsTrue(Equal(ent1.DateTimeProp, ent1copy.DateTimeProp));

      Assert.AreEqual(ent1.EnumProp, ent1copy.EnumProp);
      Assert.AreEqual(ent1.EnumNullProp, ent1copy.EnumNullProp);
      Assert.AreEqual(ent1.BitsProp, ent1copy.BitsProp);
      Assert.AreEqual(ent1.BitsNullProp, ent1copy.BitsNullProp);


      Assert.IsTrue(ent1.ByteArrayProp.EqualsTo(ent1copy.ByteArrayProp));
      Assert.IsTrue(ent1.BinaryProp.Equals(ent1copy.BinaryProp));
      Assert.IsTrue(ent1.BigBinaryProp.Equals(ent1copy.BigBinaryProp));

      Assert.AreEqual(ent1.SByteProp, ent1copy.SByteProp);
      Assert.AreEqual(ent1.UInt16Prop, ent1copy.UInt16Prop);
      Assert.AreEqual(ent1.UInt32Prop, ent1copy.UInt32Prop);
      Assert.AreEqual(ent1.UInt64Prop, ent1copy.UInt64Prop);

      //try to set nullable double to Null
      ent1.DoublePropNull = null;
      ent1.Int64Prop = 123; //this is test for SQLite, check how small numbers in big ints are saved/loaded
      session.SaveChanges();
      session = _app.OpenSession();
      ent1 = session.GetEntity<IDataTypesEntity>(ent1Id);
      Assert.AreEqual(null, ent1.DoublePropNull, "Null expected in Nullable property");
      Assert.AreEqual(123, ent1.Int64Prop, "Int64 property test failed.");

    }//method

    [TestMethod]
    public void TestDataTypesProviderSpecific() {
      switch(Startup.ServerType) {
        case DbServerType.MsSql: 
          TestMsSqlTypes();
          TestMsSqlRowVersion(); 
          break; 
      }
    }

    public void TestMsSqlTypes() {
      if (Startup.ServerType != DbServerType.MsSql)
        return; 
      // Verify data types
      var db = _app.GetDefaultDatabase();
      var loader = Startup.Driver.CreateDbModelLoader(db.Settings, null);
      var dtColumns = loader.GetColumns();
      CheckDataType(dtColumns, "CharNProp", "char", 10);
      CheckDataType(dtColumns, "NCharNProp", "nchar", 10);
      CheckDataType(dtColumns, "VarCharProp", "varchar", 10);
      CheckDataType(dtColumns, "DateProp", "date");
      CheckDataType(dtColumns, "DateTimeProp", "datetime2");
      CheckDataType(dtColumns, "TimeProp", "time");
      CheckDataType(dtColumns, "SmallDateTimeProp", "smalldatetime");
      CheckDataType(dtColumns, "TimeStampProp", "timestamp");
      CheckDataType(dtColumns, "GeographyProp", "geography");
      CheckDataType(dtColumns, "GeometryProp", "geometry");
      CheckDataType(dtColumns, "HierarchyIdProp", "hierarchyid");
      CheckDataType(dtColumns, "ImageProp", "image");
      CheckDataType(dtColumns, "NTextProp", "ntext");
      CheckDataType(dtColumns, "TextProp", "text");
      CheckDataType(dtColumns, "XmlProp", "xml");
      CheckDataType(dtColumns, "SmallMoneyProp", "smallmoney");
      CheckDataType(dtColumns, "SqlVariantProp", "sql_variant");

      //Create 2 records, to test formatting of data values for batch in batch mode (batch is used only when >1 records are saved)
      var session = _app.OpenSession();
      var ent1 = CreateSpecialDataTypesEntity(session, "ent1");
      var ent2 = CreateSpecialDataTypesEntity(session, "ent2");
      session.SaveChanges();
      //Read back and verify DateTimeOffset is saved properly (fix for a bug)
      var session2 = _app.OpenSession();
      var ent1Copy = session2.GetEntity<IMsSqlDataTypesEntity>(ent1.Id);
      Assert.AreEqual(ent1.DateTimeOffsetProp, ent1Copy.DateTimeOffsetProp, "Datetime offset prop does not match");

      // test single space - reported bug 
      ent1Copy.CharNProp = " ";
      session2.SaveChanges(); //just checking it does not blow up
    }//test method

    private void CheckDataType(DbTable dtColumns, string columnName, string dataType, int size = 0) {
      //find row
      var colRow = dtColumns.FindRow("column_name", columnName);
      Assert.AreEqual(dataType, (string)colRow["Data_Type"], "Data type does not match, column: " + columnName);
      if (size > 0)
        Assert.AreEqual(size, (int)colRow["character_maximum_length"], "Size does not match, column: " + columnName);
    }

    private IDataTypesEntity CreateDataTypesEntity(IEntitySession session, string strProp, string memoProp) {
      var rand = new Random(); 
      var ent = session.NewEntity<IDataTypesEntity>();
      var id = ent.Id = Guid.NewGuid();
      ent.StringProp = strProp;
      ent.MemoProp = memoProp;
      ent.ByteProp = (byte) rand.Next(255);
      ent.BoolProp = true;
      ent.Int16Prop = (Int16)rand.Next(Int16.MaxValue);
      ent.Int32Prop = - rand.Next(int.MaxValue - 1);
      ent.Int64Prop = Int64.MinValue; 
      ent.Int32NullProp = 222;
      ent.CharProp = 'X';

      ent.DoubleProp = 3.456;
      ent.DoublePropNull = 1.2345;
      ent.SingleProp = 4.567f;
      ent.DecProp = 2.34m;
      ent.MoneyProp = 3.45m;
      ent.DateTimeProp = DateTime.Now;
      ent.TimeProp = DateTime.Now.TimeOfDay;

      ent.EnumProp = SimpleEnum.Two;
      ent.EnumNullProp = SimpleEnum.Three;
      ent.BitsProp = BitEnum.Bit1 | BitEnum.Bit2;
      ent.BitsNullProp = BitEnum.Bit0;

      ent.ByteArrayProp = new byte[] { 1, 2, 3 };
      ent.BinaryProp = new Binary(new byte[] { 4, 5, 6 });
      ent.BigBinaryProp = new Binary(new byte[] { 11, 12, 13, 14 });

      ent.SByteProp = 12;
      ent.UInt16Prop = 456;
      ent.UInt32Prop = 567;
      ent.UInt64Prop = 678;

      return ent;
    }

    private IMsSqlDataTypesEntity CreateSpecialDataTypesEntity(IEntitySession session, string varCharProp) {
      var ent = session.NewEntity<IMsSqlDataTypesEntity>();
      ent.Id = Guid.NewGuid();
      // Test bug fix
      ent.CharNProp = "12345678"; 
      ent.NCharNProp = "234567";
      ent.VarCharProp = varCharProp;
      ent.TimeProp = DateTime.Now.TimeOfDay;
      ent.DateProp = DateTime.Now.Date;
      ent.DateTimeProp = DateTime.Now;
      ent.SmallDateTimeProp = DateTime.Now;
      ent.DateTimeOffsetProp = new DateTimeOffset(2015, 3, 14, 9, 26, 53, TimeSpan.FromHours(-7)); //PI day/time in Seattle DateTimeOffset.Now;
      // ent.TimeStampProp is set automatically by database
      /* 
            // Have no idea how to properly assign these properties; assigning random data does not work, so made columns nullable and skip assignment 
            ent.GeographyProp = new byte[] { 1, 2, 3, 4 };
            ent.GeometryProp = new byte[] { 5, 6, 7, 8 };
            ent.HierarchyIdProp = new byte[] { 11, 12, 13, 14 };
       */
      ent.ImageProp = new byte[] { 21, 22, 23, 24 };
      ent.NTextProp = "abcd";
      ent.TextProp = "defg";
      ent.XmlProp = "<foo/>";
      ent.SmallMoneyProp = 1.23m;
      ent.SqlVariantProp = "xx"; // 1234;

      return ent; 
    }

    public void TestMsSqlRowVersion() {
      //RowVersion is not supported for SQL CE
      if(Startup.ServerType != DbServerType.MsSql)
        return;
      var session = _app.OpenSession();
      var prod = session.NewEntity<IMsSqlRowVersionedProduct>();
      prod.Name = "Coffee Maker 2000";
      prod.Description = "Automatic coffee maker.";
      prod.Price = 22.99;
      session.SaveChanges();
      var prodId = prod.Id;

      var rv1 = prod.RowVersion;
      prod.Price = 19.99;
      session.SaveChanges();
      var rv2 = prod.RowVersion;
      Assert.IsFalse(rv2.EqualsTo(rv1), "Row version did not increase.");

      // Test LINQ query against table with Row version column - make sure LINQ can read rowversion column
      session = _app.OpenSession();
      var q = from vp in session.EntitySet<IMsSqlRowVersionedProduct>()
              where vp.Price > 5
              select vp;
      var pList = q.ToList();
      Assert.IsTrue(pList.Count > 0, "Failed to run LINQ query.");
      var rv3 = pList[0].RowVersion;
      Assert.IsTrue(rv3.EqualsTo(rv2), "Row version from LINQ query mismatch.");

      // Concurrent update through different sessions
      // Original session and product entity
      session = _app.OpenSession();
      prod = session.GetEntity<IMsSqlRowVersionedProduct>(prodId);
      // other, concurrent session, updates the same entity
      var session2 = _app.OpenSession();
      var prodCopy = session2.GetEntity<IMsSqlRowVersionedProduct>(prodId);
      prodCopy.Price = 18.99;
      session2.SaveChanges();
      // Original session tries to update now stale entity - should get DataAccessException with subtype ConcurrentUpdate
      var dex = TestUtil.ExpectDataAccessException(() => {
        prod.Price = 17.99;
        session.SaveChanges();
      });
      Assert.AreEqual(DataAccessException.SubTypeConcurrentUpdate, dex.SubType);
      var tableName = dex.Data[DataAccessException.KeyTableName];
      Assert.AreEqual("\"types\".\"MsSqlRowVersionedProduct\"", tableName, "TableName mismatch in concurrent update exception");
      Debug.WriteLine("Concurrent update conflict - as expected.");
    }

    //Helper methods to compare 
    private bool Equal(DateTime x, DateTime y) {
      int precMs = 10;
      if(Startup.ServerType == DbServerType.MySql)
        precMs = 1000;
      return y > x.AddMilliseconds(-precMs) && y < x.AddMilliseconds(precMs);
    }
    private bool Equal(DateTimeOffset x, DateTimeOffset y) {
      return y > x.AddMilliseconds(-10) && y < x.AddMilliseconds(10);
    }


  }

}
