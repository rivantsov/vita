using System;
using System.Linq;
using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;


using Vita.Entities;
using Vita.Entities.Model;
using Vita.Data;
using Vita.Data.Model;
using Vita.Data.Driver;
using Vita.Entities.DbInfo;
using Vita.Data.Upgrades;
using Vita.Entities.Logging;
using System.Collections.Generic;
using Vita.Tools;

namespace Vita.Testing.BasicTests.SchemaUpdates {

  [TestClass] 
  public class SchemaUpdateTests {
    public const string SchemaName = "upd";

    #region Version1
    //Original model consisting of 2 tables
    public class EntityModuleV1 : EntityModule {

      [Entity, Index("IntProp,StringProp")]
      public interface IParentEntity: IParentEntityComputed {
        [PrimaryKey, Auto]
        Guid Id { get; set; }
        [Size(40)]
        string StringProp { get; set; }
        int IntProp { get; set; }
        [Column(Default="123")]
        int IntPropWithDefault { get; set; }
        double DoubleProp { get; set; }
        System.Single SingleProp { get; set; }
        [Size(20)]
        string StringProp2_OldName { get; set; }
        IEntityToDelete EntityToDeleteRef { get; set; }
      }

      [Entity]
      public interface IChildEntity {
        [PrimaryKey, Auto]
        Guid Id { get; set; }
        IParentEntity Parent { get; set; }
        IParentEntity ParentRefToDelete { get; set; }
        [Size(20)]
        string Name { get; set; }
        [Size(50), Nullable]
        string Description { get; set; }
      }

      [Entity]
      public interface IEntityToDelete {
        [PrimaryKey, Auto]
        Guid Id { get; set; }
        string Name { get; set; }
        IList<IParentEntity> ParentEnts { get; }
      }

      // An entity with some reserved words used as names, to test that it is handled properly
      [Entity]
      public interface ITable {
        [PrimaryKey, Auto]
        Guid Id { get; set; }
        DateTime Begin { get; set; }
        DateTime End { get; set; }
        string User { get; set; }
        string Insert { get; set; }
      }

      public EntityModuleV1(EntityArea area) : base(area, "TestModule", version: new Version("1.0.0.0")) {
        RegisterEntities(typeof(IParentEntity), typeof(IChildEntity), typeof(IEntityToDelete), typeof(ITable));
      }

      public override void RegisterMigrations(DbMigrationSet migrations) {
        // Test Ignore list for db upgradeds. Add an index thru script and put it on Ignore list, so that auto db upgrade does not touch it
        if(migrations.ServerType == DbServerType.MsSql) {
          migrations.AddSql("1.0.0.0", "CustomIndex", "Ignore list test",
            $@"CREATE NONCLUSTERED INDEX [IX_Table_User] ON [upd].[Table] ( [User] ASC)");
          // now register this index as ignored. We have to use full name with schema
          migrations.IgnoreDbObjectChanges("upd.IX_Table_User");
        }
      }
    }// module

    public class EntityAppV1 : EntityApp {
      public EntityAppV1() : base("SchemaUpdateApp") {
        var area = this.AddArea(SchemaName);
        var module = new EntityModuleV1(area);
        var dbInfo = new DbInfoModule(area);
      }
    }

    #endregion

    #region version2
    //Changed model
    public class EntityModuleV2 : EntityModule {
      [Entity, Index("IntProp:desc,StringProp:asc")]
      [ClusteredIndex("StringProp,Id")]
      public interface IParentEntity: IParentEntityComputed {
        [PrimaryKey, Auto]
        Guid Id { get; }
        [Size(50)] //change size; clustered index changes as well
        string StringProp { get; set; }
        int IntProp { get; set; }
        double DoubleProp { get; set; }
        [Size(20), OldNames("StringProp2_OldName")]
        string StringProp2_NewName { get; set; }

        // Add non-nullable properties
        int IntPropAdded { get; set; }
        DateTime DateTimePropAdded { get; set; }
      }

      [Entity, OldNames("IChildEntity")]
      public interface IChildEntityRenamed {
        [PrimaryKey]
        Guid Id { get; set; }
        IParentEntity Parent { get; set; }
        [Size(20)]
        string Name { get; set; }
        [Size(50), Nullable]
        string Description { get; set; }
        // we are adding this non-nullable reference; we have to add migration script to initialize values for existing records
        IParentEntity OtherParent { get; set; }
      }

      [Entity]
      public interface ITable {
        [PrimaryKey, Auto]
        Guid Id { get; set; }
        DateTime Begin { get; set; }
        DateTime End { get; set; }
        string User { get; set; }
        string Insert { get; set; }
      }

      [Entity]
      public interface INewTable {
        [PrimaryKey, Auto]
        Guid Id { get; set; }
        [Size(Sizes.Name)]
        string Name { get; set; }
      }

      public EntityModuleV2(EntityArea area) : base(area, "TestModule", version: new Version("1.1.0.0")) {
        base.RegisterEntities(typeof(IParentEntity), typeof(IChildEntityRenamed), typeof(ITable), typeof(INewTable));
      }

      public override void RegisterMigrations(DbMigrationSet migrations) { 
        //ChildEntityRenamed has added reference column OtherParent (not null); we have to add a script that initializes the column for existing records,
        // otherwise adding foreign key constraint would fail. We set it to the same reference as Parent_Id column.
        // The script timing is 'Middle' - it will be executed AFTER column is added (as nullable), but before it is switched to NOT NULL and ref constraint is added. 
        // SQLite does not allow renaming tables
        string tableName = migrations.ServerType == DbServerType.SQLite ? 
          "upd_ChildEntity" : migrations.GetFullTableName<IChildEntityRenamed>();
        var sql = string.Format(
          @"UPDATE {0} SET ""OtherParent_Id"" = ""Parent_Id"" WHERE ""OtherParent_Id"" IS NULL;", tableName); 
        migrations.AddSql("1.1.0.0", "InitOtherParentColumn", "Initialize values of IChildEntityRenamed.OtherParent column.",
            sql, timing: DbMigrationTiming.Middlle);

        // Let's add a post-upgrade action - it is a code that will be executed after upgrade is done and entity session is available
        // We add a couple of records to a new table.
        migrations.AddPostUpgradeAction("1.1.0.0", "InitNewTable", "Initializes NewTable", session => {
          var ent1 = session.NewEntity<INewTable>();
          ent1.Name = "Name1";
          var ent2 = session.NewEntity<INewTable>();
          ent2.Name = "Name2";
          session.SaveChanges(); // this is optional, SaveChanges will be called after all actions are executed. 
        });

        // Tell the system to ignore this index - it was created by migration SQL in v1 
        // Note: manually check after test run that this index exists, there's no automatic check
        migrations.IgnoreDbObjectChanges("upd.IX_Table_User");

      } //method

    }//class

    public class EntityAppV2 : EntityApp {
      public EntityAppV2() : base("SchemaUpdateApp") {
        var area = this.AddArea(SchemaName);
        var module = new EntityModuleV2(area);
        var dbInfo = new DbInfoModule(area);
      }
    }

    // computed field for ParentEntity
    public interface IParentEntityComputed {
      DateTime SomeDt { get; set; }

      [DbComputed(DbComputedKind.StoredColumn)]
      //  Servers reformat the expr internally (stupid! - keep the original somewhere!)
      //  so in order for schema upgrade work properly, you need to put here expr formatted exactly
      //  like server stores it. VITA prints out expr values where there's mismatch - see Output window
      //  replace original expr with the one already in database so that schema comparison works
      [SqlExpression(DbServerType.MsSql, "(dateadd(year,(5),[SomeDt]))")] // "DATEADD(YEAR, 5, SomeDt)")] - original
      [SqlExpression(DbServerType.MySql, "(`SomeDt` + interval 5 year)")] // "DATE_ADD(SomeDt, INTERVAL 5 YEAR)")]
      [SqlExpression(DbServerType.Postgres, "(\"SomeDt\" + ((5)::double precision * '1 year'::interval))")] // "(\"SomeDt\" + 5 * INTERVAL '1 year')")]
      [SqlExpression(DbServerType.Oracle, "ADD_MONTHS(\"SomeDt\", 12 * 5)")]
      [SqlExpression(DbServerType.SQLite, "DATE(SomeDt, '5 years')")]
      DateTime SomeDtPlus5 { get; }
    }
    #endregion

    [TestMethod]
    public void TestSchemaUpdate() {
      if(Startup.Driver == null)
        Startup.SetupForTestExplorerMode();
      // SQLite: too many troubles because SQLite does not support modifying tables
      if(Startup.ServerType == DbServerType.SQLite)
        return;
      try {
        TestSchemaUpdateImpl(); 
      } catch (Exception ex) {
        // write to debug stream - it will appear in test report
        Debug.WriteLine(ex.ToString());
        throw;
      }
    }

    private void TestSchemaUpdateImpl() {
      // Prepare Db settings, wipe out old tables - we have to do it this way, because of complexities with Oracle
      var dbSettings = new DbSettings(Startup.Driver, Startup.DbOptions, 
          Startup.ConnectionString, upgradeMode: DbUpgradeMode.Always, upgradeOptions: DbUpgradeOptions.Default);
      DataUtility.DropTablesSafe(dbSettings, SchemaName, "ChildEntity", "ChildEntityRenamed", "ParentEntity", 
          "Table", "NewTable", "DbInfo", "DbModuleInfo");

      var supportsClustIndex = Startup.Driver.Supports(DbFeatures.ClusteredIndexes);

      //version 1 of model/schema
      {
        var app = new EntityAppV1();

        SpecialActivateApp(app, dbSettings, true); 

        // Load DbModel and verify it is correct
        var dbModel = Startup.LoadDbModel(app);
        Assert.AreEqual(6, dbModel.Tables.Count, "Expected 6 tables."); //2 tables in DbInfo + 4 tables in our module
        var parTable = FindTable(dbModel, SchemaName, "ParentEntity");
        Assert.AreEqual(10, parTable.Columns.Count, "Invalid number of columns in parent table.");
        var keyCount = CountKeys(parTable);
        //Keys: PK, FK to IEntityToDelete, index on FK to IEntityToDelete, index on IntProp,StringProp 
        Assert.AreEqual(4, keyCount, "Invalid # of keys in parent table.");

        //child entity
        var childTable = FindTable(dbModel, SchemaName, "ChildEntity");
        Assert.AreEqual(5, childTable.Columns.Count, "Invalid number of columns in child table.");
        // some servers like MySql create FK and Index on it under the same name. When loading, such a key should be marked with 2 flags
        // so let's count these separately; should be 3: PK + 2 foreign keys
        //   note:  indexes on FK are not auto-created
        keyCount = CountKeys(childTable);
        // For MySql, for every FK a supporting index is created (automatically), so we have 2 extra indexes on FKs
        // for other servers indexes on FK not created.
        var expectedKeyCount = Startup.ServerType == DbServerType.MySql ? 5 : 3;
        Assert.AreEqual(expectedKeyCount, keyCount, "Invalid # of keys in child table.");

        //Create a few records
        var session = app.OpenSession();
        var parent = session.NewEntity<EntityModuleV1.IParentEntity>();
        parent.IntProp = 4;
        parent.StringProp = "Some string";
        parent.SingleProp = 4.56f;
        parent.DoubleProp = 5.67;
        parent.IntPropWithDefault = 567;
        parent.StringProp2_OldName = "Old string";
        var child = session.NewEntity<EntityModuleV1.IChildEntity>();
        child.Parent = parent;
        child.ParentRefToDelete = parent;
        child.Name = "Child name";
        var entToDelete = session.NewEntity<EntityModuleV1.IEntityToDelete>();
        entToDelete.Name = "some-name";
        parent.EntityToDeleteRef = entToDelete;
        session.SaveChanges();
        app.Shutdown();
      }

      //Now change to version 2 ================================================================
      {
        var app = new EntityAppV2();
        // use fresh dbSettings to avoid sharing db model (we could drop the DbOptions.ShareDbModel flag instead)
        dbSettings = new DbSettings(Startup.Driver, Startup.DbOptions,
            Startup.ConnectionString, upgradeMode: DbUpgradeMode.Always, upgradeOptions: DbUpgradeOptions.Default | DbUpgradeOptions.DropUnknownObjects);
        SpecialActivateApp(app, dbSettings, false);

        //At this point the schema should have been updated; let's check it      
        // Load DbModel and verify it is correct
        var dbModel = Startup.LoadDbModel(app);
        // 2 tables in DbInfo module, 4 tables in test app
        Assert.AreEqual(6, dbModel.Tables.Count, "Expected 6 tables after update.");
        var parTable = FindTable(dbModel, SchemaName, "ParentEntity");
        Assert.AreEqual(9, parTable.Columns.Count, "Invalid number of columns in parent table after schema update.");
        Assert.AreEqual(3, parTable.Keys.Count, //PK, Clustered index, index on IntProp,StringProp
           "Invalid # of keys in parent table after update.");
        if (supportsClustIndex) {
          //SQL CE does not support clustered indexes
          var parCI = parTable.Keys.First(k => k.KeyType.IsSet(KeyType.ClusteredIndex));
          Assert.AreEqual(2, parCI.KeyColumns.Count, "Invalid number of fields in clustered index."); //
        }
        //child entity
        var childTable = FindTable(dbModel, SchemaName, "ChildEntityRenamed");
        Assert.AreEqual(5, childTable.Columns.Count, "Invalid number of columns in child table after update.");
        var keyCount = CountKeys(childTable); // = 3:  Clustered PK, FK to parent, FK to OtherParent; (indexes on FKs are not auto-created)
        // For MySql, for every FK a supporting index is created (automatically), so we have 2 extra indexes on FKs
        // for other servers indexes on FK not created.
        var expectedKeyCount = Startup.ServerType == DbServerType.MySql ? 5 : 3;
        Assert.AreEqual(expectedKeyCount, keyCount, "Invalid # of keys in child table after update.");
        //Check that post-upgrade action is executed - check records are added to INewTable
        var session = app.OpenSession();
        var newTableEntCount = session.EntitySet<EntityModuleV2.INewTable>().Count();
        Assert.AreEqual(2, newTableEntCount, "Expected 2 entities in INewTable.");

        //  Now create model again, compare it and make sure no schema updates
        var ds = app.GetDefaultDataSource();
        var dbUpdater = new DbUpgradeManager(ds.Database, app.ActivationLog);
        var upgradeInfo = dbUpdater.BuildUpgradeInfo();
        // if we have upgrade scripts, this is error
        if (upgradeInfo.AllScripts.Count > 0) {
          var strUpdates = upgradeInfo.AllScripts.GetAllAsText();
          Debug.WriteLine("Detected updates when no schema changes should be present:");
          Debug.WriteLine(strUpdates);
          Assert.IsTrue(false, "Schema changes count should be zero.");
        }
        app.Flush();
      }
    }//method

    // For some providers keys might be combined; so to properly account for this, we count keys this way - separately by each type.
    private int CountKeys(DbTableInfo table) {
      return CountKeys(table, KeyType.PrimaryKey) + CountKeys(table, KeyType.ForeignKey) + CountKeys(table, KeyType.Index);
    }

    private int CountKeys(DbTableInfo table, KeyType keyType) {
      return table.Keys.Count(k => k.KeyType.IsSet(keyType));
    }

    private DbTableInfo FindTable(DbModel dbModel, string schema, string tableName) {
      return dbModel.GetTable(schema, tableName); 
    }

    // This test requies its own activation process, complications with drop schema and renaming tables - especially in Oracle
     
    private EntityApp SpecialActivateApp(EntityApp app, DbSettings dbSettings, bool dropOldSchema) {
      app.LogPath = Startup.LogFilePath;
      app.ActivationLogPath = Startup.ActivationLogPath;
      try {
        //Setup emitter
        app.Init();
        if(dropOldSchema) {
          Startup.DropSchemaObjects(app, dbSettings);
        }

        app.ConnectTo(dbSettings);
        return app;
      } catch(Exception ex) {
        //Unit test framework shows only ex message, not details; let's write specifics into debug output - it will be shown in test failure report
        var descr = ex.ToLogString(); 
        app.ActivationLog.LogError(descr);
        Debug.WriteLine("EntityApp init exception: ");
        Debug.WriteLine(descr);
        throw;
      }
    }


  }//class


}
