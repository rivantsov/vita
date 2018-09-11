using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace Vita.Entities {

  // Note: DbException would be better name, but it is already used.
  /// <summary>A wrapper for runtime exception in the database. 
  /// This exception is used to 'unify' different types of data exceptions for different providers.</summary>
  /// <remarks>
  /// The original database exception is saved in the Exception.Inner property. 
  /// Using this exception helps making the business code to be more data-provider independent. 
  /// </remarks>
  public class DataAccessException : Exception {
    #region constants
    //subtypes
    public const string SubTypeConnectionFailed = "ConnectionFailed";
    public const string SubTypeDeadLock = "DeadLock";
    public const string SubTypeConcurrentUpdate = "ConcurrentUpdate";
    public const string SubTypeUniqueIndexViolation = "UniqueIndexViolation";
    public const string SubTypeIntegrityViolation = "IntegrityViolation";
    public const string SubTypeConstraintViolation = "ConstraintViolation";

    //keys for extra values in exc.Data dictionary
    public const string KeyEntityCommandName = "EntityCommandName";
    public const string KeyDbKeyName = "DbKeyName";
    public const string KeyDbColumnNames = "DbColumnNames"; //SQLite does not report key name, only key list
    public const string KeyEntityKeyName = "EntityKeyName";
    public const string KeyIndexAlias = "IndexAlias";
    public const string KeyEntityName = "EntityName";
    public const string KeyMemberNames = "MemberNames";
    public const string KeyTableName = "TableName";
    public const string KeyDbCommand = "DbCommand";
    public const string KeyLinqQuery = "LinqQuery";
    public const string KeyRowPrimaryKey = "RowPrimaryKey";

    #endregion

    public string SubType;
    public object DbCommand; 
    public string EntityCommandName;
    public int ProviderErrorNumber; 

    public DataAccessException(Exception sqlException, 
                               object dbCommand = null, string entityCommandName = null)   
      : base(sqlException.Message, sqlException) {
      DbCommand = dbCommand; 
      EntityCommandName = entityCommandName;
      if (!string.IsNullOrWhiteSpace(EntityCommandName))
        this.Data["EntityCommandName"] =  EntityCommandName;
    }

  }//class
}//ns
