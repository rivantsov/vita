using System;
using System.Linq;
using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;


using Vita.Common;
using Vita.Entities;
using Vita.Entities.Model;
using Vita.Data;
using Vita.Data.Model;
using Vita.UnitTests.Common;
using Vita.Data.Driver;
using Vita.Modules.DbInfo;
using Vita.Data.Upgrades;

namespace Vita.UnitTests.Basic.SchemaUpdates {

  [TestClass] 
  public class SchemaUpdateTests {
    public const string SchemaName = "upd";

    #region Version1
    //Original model consisting of 2 tables
    public class EntityModuleV1 : EntityModule {
      [Entity, Index("IntProp,StringProp")]
      public interface IParentEntity {
        [PrimaryKey, Auto, ClusteredIndex]
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
    }// module

    public class EntityAppV1 : EntityApp {
      public EntityAppV1() : base("SchemaUpdateApp") {
        var area = this.AddArea(SchemaName);
        var module = new EntityModuleV1(area);
        var dbInfo = new DbInfoModule(area);

      }
    }

    #endregion

    #region Model V2
    //Changed model
    public class EntityModuleV2 : EntityModule {
      [Entity, Index("IntProp:desc,StringProp:asc")]
      public interface IParentEntity {
        [PrimaryKey, Auto]
        Guid Id { get; }
        [Size(50)] //change size; clustered index changes as well, but we specify it now using Companion type.
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
        string Insert { get; set; }
      }

      [Entity]
      public interface INewTable {
        [PrimaryKey, Auto]
        Guid Id { get; set; }
        [Size(Sizes.Name)]
        string Name { get; set; }
      }

      [ClusteredIndex("StringProp,Id")]
      public interface IParentEntityKeys : IParentEntity {
      }

      public EntityModuleV2(EntityArea area) : base(area, "TestModule", version: new Version("1.1.0.0")) {
        base.RegisterEntities(typeof(IParentEntity), typeof(IChildEntityRenamed), typeof(ITable), typeof(INewTable));
        base.RegisterCompanionTypes(typeof(IParentEntityKeys));
      }

      public override void RegisterMigrations(DbMigrationSet migrations) {
        //ChildEntityRenamed has added reference column OtherParent (not null); we have to add a script that initializes the column for existing records,
        // otherwise adding foreign key constraint would fail. We set it to the same reference as Parent_Id column.
        // The script timing is 'Middle' - it will be executed AFTER column is added (as nullable), but before it is switched to NOT NULL and ref constraint is added. 
        // SQLite does not allow renaming tables
        string tableName = migrations.ServerType == DbServerType.Sqlite ? "ChildEntity" : migrations.GetFullTableName<IChildEntityRenamed>();
        var sql = string.Format(@"UPDATE {0} SET ""OtherParent_Id"" = ""Parent_Id"" WHERE ""OtherParent_Id"" IS NULL;", tableName); 
        migrations.AddSql("1.1.0.0", "InitOtherParentColumn", "Initialize values of IChildEntityRenamed.OtherParent column.", sql, timing: DbMigrationTiming.Middlle);

        // Let's add a post-upgrade action - it is a code that will be executed after upgrade is done and entity session is available
        // We add a couple of records to a new table.
        migrations.AddPostUpgradeAction("1.1.0.0", "InitNewTable", "Initializes NewTable", session => {
          var ent1 = session.NewEntity<INewTable>();
          ent1.Name = "Name1";
          var ent2 = session.NewEntity<INewTable>();
          ent2.Name = "Name2";
          session.SaveChanges(); // this is optional, SaveChanges will be called after all actions are executed. 
        });
      } //method

    }//class

    public class EntityAppV2 : EntityApp {
      public EntityAppV2() : base("SchemaUpdateApp") {
        var area = this.AddArea(SchemaName);
        var module = new EntityModuleV2(area);
        var dbInfo = new DbInfoModule(area);
      }
    }
    #endregion

    [TestMethod]
    public void TestSchemaUpdate() {
      try {
        //Make sure config file is loaded and ServerType is set
        if(SetupHelper.Driver == null)
          SetupHelper.SetupForTestExplorerMode();
        if(SetupHelper.ServerType == DbServerType.Sqlite)
          TestSchemaUpdateImplSQLite();
        else
          TestSchemaUpdateImpl(); 
      } catch (Exception ex) {
        // write to debug stream - it will appear in test report
        Debug.WriteLine(ex.ToString());
        throw;
      }
    }

    private void TestSchemaUpdateImpl() {

      SetupHelper.DropSchemaObjects(SchemaName);
      var supportsClustIndex = SetupHelper.Driver.Supports(DbFeatures.ClusteredIndexes);

      //version 1 of model/schema
      {
        var app = new EntityAppV1();

        SetupHelper.ActivateApp(app); //updates schema

        // Load DbModel and verify it is correct
        var dbModel = SetupHelper.LoadDbModel(SchemaName, app.ActivationLog);
        Assert.AreEqual(6, dbModel.Tables.Count(), "Expected 6 tables."); //2 tables in DbInfo + 4 tables in our module
        var parTable = dbModel.GetTable(SchemaName, "ParentEntity");
        Assert.AreEqual(8, parTable.Columns.Count, "Invalid number of columns in parent table.");
        var keyCount = CountKeys(parTable);
        //Keys: PK, FK to IEntityToDelete, index on FK to IEntityToDelete, index on IntProp,StringProp, (ClusteredIndex?) 
        Assert.AreEqual(5, keyCount, "Invalid # of keys in parent table.");

        if (supportsClustIndex) {
          var parCI = parTable.Keys.First(k => k.KeyType.IsSet(KeyType.ClusteredIndex));
          Assert.AreEqual(1, parCI.KeyColumns.Count, "Invalid number of fields in clustered index.");
        }

        //child entity
        var childTable = dbModel.GetTable(SchemaName, "ChildEntity");
        Assert.AreEqual(5, childTable.Columns.Count, "Invalid number of columns in child table.");
        // some servers like MySql create FK and Index on it under the same name. When loading, such a key should be marked with 2 flags
        // so let's count these separately; should be 5: PK + 2 foreign keys + 2 indexes on FK
        keyCount = CountKeys(childTable);
        Assert.AreEqual(5, keyCount, "Invalid # of keys in child table.");

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
        SetupHelper.ActivateApp(app, dropUnknownTables: true);

        //At this point the schema should have been updated; let's check it      
        // Load DbModel and verify it is correct
        var dbModel = SetupHelper.LoadDbModel(SchemaName, app.ActivationLog);
        Assert.AreEqual(6, dbModel.Tables.Count(), "Expected 6 tables after update.");
        var parTable = dbModel.GetTable(SchemaName, "ParentEntity");
        Assert.AreEqual(7, parTable.Columns.Count, "Invalid number of columns in parent table after schema update.");
        Assert.AreEqual(3, parTable.Keys.Count, //PK, Clustered index, index on IntProp,StringProp
           "Invalid # of keys in parent table after update.");
        if (supportsClustIndex) {
          //SQL CE does not support clustered indexes
          var parCI = parTable.Keys.First(k => k.KeyType.IsSet(KeyType.ClusteredIndex));
          Assert.AreEqual(2, parCI.KeyColumns.Count, "Invalid number of fields in clustered index."); //
        }
        //child entity
        var childTable = dbModel.GetTable(SchemaName, "ChildEntityRenamed");
        Assert.AreEqual(5, childTable.Columns.Count, "Invalid number of columns in child table after update.");
        var keyCount = CountKeys(childTable); // = 5:  Clustered PK, FK to parent, index on FK, FK to OtherParent, index on FK
        Assert.AreEqual(5, keyCount, "Invalid # of keys in child table after update.");
        //Check that post-upgrade action is executed - check records are added to INewTable
        var session = app.OpenSession();
        var newTableEntCount = session.EntitySet<EntityModuleV2.INewTable>().Count();
        Assert.AreEqual(2, newTableEntCount, "Expected 2 entities in INewTable.");

        //  Now create model again, compare it and make sure no schema updates
        var ds = app.GetDefaultDataSource();
        var dbUpdater = new DbUpgradeManager(ds);
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

    //Separate implementation for SQLite. SQLite does not support column-modify, table-rename, etc. 
    // So we try to account for these in this special version
    private void TestSchemaUpdateImplSQLite() {
      //Start from fresh copy of the database
      File.Copy("..\\..\\VitaTestSQLite.db", "VitaTestSQLite.db", overwrite: true);

      //version 1 of model/schema
      {
        var app = new EntityAppV1();
        SetupHelper.ActivateApp(app); //updates schema

        // Load DbModel and verify it is correct
        var dbModel = SetupHelper.LoadDbModel(SchemaName, app.ActivationLog);
        Assert.AreEqual(6, dbModel.Tables.Count(), "Expected 4 tables.");
        var parTable = dbModel.GetTable(SchemaName, "ParentEntity");
        Assert.AreEqual(8, parTable.Columns.Count, "Invalid number of columns in parent table.");
        var keyCount = CountKeys(parTable);
        //Keys: PK, FK to IEntityToDelete, index on FK to IEntityToDelete, index on IntProp,StringProp
        Assert.AreEqual(4, keyCount, "Invalid # of keys in parent table.");


        //child entity
        var childTable = dbModel.GetTable(SchemaName, "ChildEntity");
        Assert.AreEqual(5, childTable.Columns.Count, "Invalid number of columns in child table.");
        // 3 - PK + 2FKs, no indexes on FKs
        Assert.AreEqual(3, childTable.Keys.Count, "Invalid # of keys in child table.");

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
        app.Flush();
        app.Shutdown();
      }
      //Now change to version 2 ================================================================
      {
        var app = new EntityAppV2();
        SetupHelper.ActivateApp(app, dropUnknownTables: true);

        //At this point the schema should have been updated; let's check it      
        // Load DbModel and verify it is correct
        var dbModel = SetupHelper.LoadDbModel(SchemaName, app.ActivationLog);
        //Note that we still have 4 tables, EntityToDelete is not dropped because of incoming FK
        Assert.AreEqual(7, dbModel.Tables.Count(), "Expected 7 tables after update.");
        var parTable = dbModel.GetTable(SchemaName, "ParentEntity");
        //NO support for dropping columns, so old columns are not deleted; instead of renaming a new column is added
        Assert.AreEqual(11, parTable.Columns.Count, "Invalid number of columns in parent table after schema update.");
        Assert.AreEqual(4, parTable.Keys.Count, //PK, FK->EntityToDelete, indexes (IntProp,StringProp), (StropProp,Id)
           "Invalid # of keys in parent table after update.");
        //child entity
        var childTable = dbModel.GetTable(SchemaName, "ChildEntity");
        Assert.AreEqual(6, childTable.Columns.Count, "Invalid number of columns in child table after update.");
        // = 3:  Clustered PK, FK to parent, index on FK
        Assert.AreEqual(3, childTable.Keys.Count, "Invalid # of keys in child table after update.");
        //Check that post-upgrade action is executed - check records are added to INewTable
        var session = app.OpenSession();
        var newTableEntCount = session.EntitySet<EntityModuleV2.INewTable>().Count();
        Assert.AreEqual(2, newTableEntCount, "Expected 2 entities in INewTable.");

        // Now create model again, compare it and make sure no schema updates
        var ds = app.GetDefaultDataSource();
        var upgradeMgr = new DbUpgradeManager(ds);
        var upgradeInfo = upgradeMgr.BuildUpgradeInfo();
        if (upgradeInfo.AllScripts.Count > 0) {
          var strUpdates = upgradeInfo.AllScripts.GetAllAsText();
          Debug.WriteLine("Detected updates when no schema changes should be present:");
          Debug.WriteLine(strUpdates);
          Assert.IsTrue(false, "Schema changes count should be zero.");
        }
        app.Flush();
      }
    }


  }//class


}
