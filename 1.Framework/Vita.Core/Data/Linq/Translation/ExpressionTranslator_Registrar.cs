
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

using Vita.Common;
using Vita.Data.Model;
using Vita.Entities.Model;
using Vita.Data.Linq.Translation.Expressions;
using Vita.Data.Driver;
using Vita.Entities.Runtime;
using Vita.Entities;
using Vita.Entities.Locking;


namespace Vita.Data.Linq.Translation {
    
  partial class ExpressionTranslator {

    /// <summary>
    /// Registers the first table. Extracts the table type and registeres the piece
    /// </summary>
    /// <param name="fromExpression"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public virtual Expression ExtractFirstTable(Expression fromExpression, TranslationContext context) {
      switch(fromExpression.NodeType) {
        case ExpressionType.Call:
          var callExpression = (MethodCallExpression)fromExpression;
          var srcQueryExpr = callExpression.Arguments.Count > 0 ? callExpression.Arguments[0] : null;
          Util.Check(srcQueryExpr != null, "LINQ: failed to create source table for {0}. Expected a query expression as argument. ", fromExpression);
          switch(srcQueryExpr.NodeType) {
            case ExpressionType.Constant:
              // most common case - it is an EntitySet wrapped in constant
              return ExtractFirstTable(srcQueryExpr, context);
            default:
              // special case - source is expression like 'pub.Books' where pub is parameter inside lambda
              return Analyze(srcQueryExpr, context);
          }
        case ExpressionType.Constant:
          var constExpr = (ConstantExpression)fromExpression;
          var entQuery = constExpr.Value as Vita.Entities.Linq.EntityQuery;
          if (entQuery == null)
            break;  
          var iLock = entQuery as ILockTarget;
          var lockOptions = iLock == null ? LockOptions.None : iLock.LockOptions;
          return CreateTable(entQuery.ElementType, context, lockOptions);
      }
      Util.Throw("LINQ engine error (ExtractFirstTable): failed to translate expression: {0}", fromExpression);
      return null; //never happens
  }

    /// <summary>
    /// Returns an existing table or registers the current one
    /// </summary>
    /// <param name="tableExpression"></param>
    /// <param name="context"></param>
    /// <returns>A registered table or the current newly registered one</returns>
    public virtual TableExpression RegisterTable(TableExpression tableExpression, TranslationContext context)
    {
        // 1. Find the table in current scope
        var foundTableExpression = (from t in context.EnumerateScopeTables()
                                    where t.IsEqualTo(tableExpression)
                                    select t).FirstOrDefault();
        if (foundTableExpression != null)
            return foundTableExpression;
        // 2. Find it in all scopes, and promote it to current scope.
        foundTableExpression = PromoteTable(tableExpression, context);
        if (foundTableExpression != null)
            return foundTableExpression;
        // 3. Add it
        context.CurrentSelect.Tables.Add(tableExpression);
        return tableExpression;
    }

    /// <summary>
    /// Promotes a table to a common parent between its current scope and our current scope
    /// </summary>
    /// <param name="tableExpression"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    protected virtual TableExpression PromoteTable(TableExpression tableExpression, TranslationContext context)
    {
        int currentIndex = 0;
        SelectExpression oldSelect = null;
        SelectExpression commonScope = null;
        TableExpression foundTable = null;
        do
        {
            // take a select
            oldSelect = context.SelectExpressions[currentIndex];

            // look for a common scope
            if (oldSelect != context.CurrentSelect)
            {
                commonScope = FindCommonScope(oldSelect, context.CurrentSelect);
                if (commonScope != null)
                    // if a common scope exists, look for an equivalent table in that select
                    for (int tableIndex = 0; tableIndex < oldSelect.Tables.Count && foundTable == null; tableIndex++)
                    {
                        if (oldSelect.Tables[tableIndex].IsEqualTo(tableExpression))
                        {
                            // found a matching table!
                            foundTable = oldSelect.Tables[tableIndex];
                        }
                    }
            }
            ++currentIndex;
        }
        while (currentIndex < context.SelectExpressions.Count && foundTable == null);

        if (foundTable != null)
        {
            oldSelect.Tables.Remove(foundTable);
            commonScope.Tables.Add(foundTable);
        }
        return foundTable;
    }

    /// <summary>
    /// Find the common ancestor between two ScopeExpressions
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    protected virtual SelectExpression FindCommonScope(SelectExpression a, SelectExpression b)
    {
        for (var aScope = a; aScope != null; aScope = aScope.Parent)
        {
            for (var bScope = b; bScope != null; bScope = bScope.Parent)
            {
                if (aScope == bScope)
                    return aScope;
            }
        }
        return null;
    }

    /// <summary>
    /// Registers a column
    /// This method requires the table to be already registered
    /// </summary>
    /// <param name="table"></param>
    /// <param name="name"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public ColumnExpression RegisterColumn(TableExpression table, string name, TranslationContext context)
    {
        var queryColumn = GetRegisteredColumn(table, name, context);
        if (queryColumn == null)
        {
            table = RegisterTable(table, context);
            queryColumn = CreateColumn(table, name, context);
            context.CurrentSelect.Columns.Add(queryColumn);
        }
        return queryColumn;
    }

    /// <summary>
    /// Returns a registered column, or null if not found
    /// This method requires the table to be already registered
    /// </summary>
    /// <param name="table"></param>
    /// <param name="name"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    protected virtual ColumnExpression GetRegisteredColumn(TableExpression table, string name,
                                                            TranslationContext context) {
      return
            (from queryColumn in context.EnumerateScopeColumns()
              where queryColumn.Table.IsEqualTo(table) && queryColumn.Name == name
              // where queryColumn.Table == table && queryColumn.Name == name // - RI: this does not work
              select queryColumn).SingleOrDefault();
    }

    public ColumnExpression RegisterColumn(TableExpression tableExpression, MemberInfo memberInfo, TranslationContext context) {
        return RegisterColumn(tableExpression, memberInfo.Name, context);
    }

    public ColumnExpression CreateColumn(TableExpression table, string memberName, TranslationContext context) {
        var col = table.TableInfo.GetColumnByMemberName(memberName);
        Util.Check(col != null, "Column for member [{0}] not found in table {1}. Member type is not supported by LINQ.", memberName, table.Name);
        return new ColumnExpression(table, col);
    }

    public virtual TableExpression CreateTable(Type tableType, TranslationContext context, LockOptions lockOptions = LockOptions.None) {
      var tableInfo = _dbModel.GetTable(tableType);
      var tableExpr = new TableExpression(tableType, tableInfo.FullName, tableInfo, lockOptions);
      return tableExpr; 
    }

    public virtual SqlFunctionExpression CreateSqlFunction(SqlFunctionType functionType, params Expression[] operands) {
      return CreateSqlFunction(functionType, true, operands);
    }
    public virtual SqlFunctionExpression CreateSqlFunction(SqlFunctionType functionType, bool ignoreCase, params Expression[] operands) {
      Type[] opTypes = null; 
      if (operands != null && operands.Length > 0)
        opTypes = operands.Select(op => op.Type).ToArray();
      var outType = _dbModel.LinqSqlProvider.GetSqlFunctionResultType(functionType, opTypes); 
      return new SqlFunctionExpression(functionType, outType, ignoreCase, operands);
    }

    public virtual TableExpression RegisterAssociation(TableExpression tableExpression, EntityMemberInfo refMember, TranslationContext context) {
        IList<MemberInfo> otherKeys;
        TableJoinType joinType;
        string joinID;
        var theseKeys = GetAssociationMembers(tableExpression, refMember, out otherKeys, out joinType, out joinID);
        // if the memberInfo has no corresponding association, we get a null, that we propagate
        if (theseKeys == null)
            return null;

        // the current table has the foreign key, the other table the referenced (usually primary) key
        if (theseKeys.Count != otherKeys.Count)
            Util.Throw("S0128: Association arguments (FK and ref'd PK) don't match");

        // we first create the table, with the JoinID, and we MUST complete the table later, with the Join() method
        var otherType = refMember.DataType; 
        var otherTableInfo = context.DbModel.GetTable(otherType);
        var otherTableExpression = CreateTable(otherType, context); // new TableExpression(otherType, otherTableInfo.FullName, otherTableInfo, joinID);
        otherTableExpression = RegisterTable(otherTableExpression, context); 
        Expression joinExpression = null;

        var createdColumns = new List<ColumnExpression>();
        for (int keyIndex = 0; keyIndex < theseKeys.Count; keyIndex++)
        {
            // joinedKey is registered, even if unused by final select (required columns will be filtered anyway)
            Expression otherKey = RegisterColumn(otherTableExpression, otherKeys[keyIndex], context);
            // foreign is created, we will store it later if this assocation is registered too
            Expression thisKey = CreateColumn(tableExpression, theseKeys[keyIndex].Name, context);
            createdColumns.Add((ColumnExpression)thisKey);

            // the other key is set as left operand, this must be this way
            // since some vendors (SQL Server) don't support the opposite
            var referenceExpression = MakeEqual(otherKey, thisKey);

            // if we already have a join expression, then we have a double condition here, so "AND" it
            if (joinExpression != null)
                joinExpression = Expression.And(joinExpression, referenceExpression);
            else
                joinExpression = referenceExpression;
        }
        // we complete the table here, now that we have all join information
        otherTableExpression.Join(joinType, tableExpression, joinExpression);

        // our table is created, with the expressions
        // now check if we didn't register exactly the same
        var existingTable = (from t in context.EnumerateScopeTables() where t.IsEqualTo(otherTableExpression) select t).SingleOrDefault();
        if (existingTable != null)
            return existingTable;
 
        context.CurrentSelect.Tables.Add(otherTableExpression);
        foreach (var createdColumn in createdColumns)
            context.CurrentSelect.Columns.Add(createdColumn);
        return otherTableExpression;
    }

  private IList<MemberInfo> GetAssociationMembers(TableExpression thisTableExpression, EntityMemberInfo member,
                          out IList<MemberInfo> otherKey, out TableJoinType joinType, out string joinID) {
    switch(member.Kind) {
      case MemberKind.EntityRef:
        // by default, join is inner
        joinType = TableJoinType.Inner;
        joinID = member.MemberName;
        var otherType = member.ReferenceInfo.ToKey.Entity.ClassInfo.Type;
        otherKey = member.ReferenceInfo.ToKey.ExpandedKeyMembers.Select(km => (MemberInfo) km.Member.ClrClassMemberInfo).ToList();
        var thisKey = member.ReferenceInfo.FromKey.ExpandedKeyMembers.Select(km => (MemberInfo)km.Member.ClrClassMemberInfo).ToList();
        if(member.Flags.IsSet(EntityMemberFlags.Nullable))
          joinType |= TableJoinType.LeftOuter;
        return thisKey;
      case MemberKind.Transient:
        if(!member.Flags.IsSet(EntityMemberFlags.FromOneToOneRef))
          break; 
        joinType = TableJoinType.LeftOuter;
        joinID = member.MemberName;
        var targetEnt = this._dbModel.EntityApp.Model.GetEntityInfo(member.DataType, throwIfNotFound: true);
        otherKey = targetEnt.PrimaryKey.ExpandedKeyMembers.Select(km => km.Member.ClrClassMemberInfo).ToList();
        var thisPk = member.Entity.PrimaryKey.ExpandedKeyMembers.Select(km => km.Member.ClrClassMemberInfo).ToList();
        return thisPk;
    }
    Util.Throw("Cannot create JOIN expression for property {0}, property not supported in LINQ.", member);
    otherKey = null;
    joinType = TableJoinType.Default;
    joinID = null;
    return null;
  }


    /// <summary>
    /// Registers a where clause in the current context scope
    /// </summary>
    /// <param name="whereExpression"></param>
    /// <param name="context"></param>
    public virtual void RegisterWhere(Expression whereExpression, TranslationContext context)
    {
        var where = CheckBoolBitExpression(whereExpression);
        context.CurrentSelect.Where.Add(where);
    }

    /// <summary>
    /// Registers all columns of a table.
    /// </summary>
    /// <param name="tableExpression"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public virtual IList<ColumnExpression> RegisterAllColumns(TableExpression tableExpression, TranslationContext context)
    {
      var result = new List<ColumnExpression>(); 
      foreach (var colInfo in tableExpression.TableInfo.Columns)
      {
        if (colInfo.Member == null)
          continue; 
        var colExpr = RegisterColumn(tableExpression, colInfo.Member.MemberName, context);
        result.Add(colExpr); 
      }
      return result; 
    }

    /// <summary>
    /// Registers an expression to be returned by main request.
    /// The strategy is to try to find it in the already registered parameters, and if not found, add it
    /// </summary>
    /// <param name="expression">The expression to be registered</param>
    /// <param name="context"></param>
    /// <returns>Expression index</returns>
    public virtual int RegisterOutputValue(Expression expression, TranslationContext context)
    {
        var scope = context.CurrentSelect;
        //check if expression is already registered
        var operands = scope.Operands.ToList();
        var index = operands.IndexOf(expression);
        if (index >= 0)
          return index; 
        operands.Add(expression);
        context.CurrentSelect = (SelectExpression)scope.Mutate(operands);
        return operands.Count - 1;
    }

    // RI: new stuff, optimized entity reader
    protected virtual Expression GetOutputTableReader(TableExpression tableExpression,
                                                      ParameterExpression dataRecordParameter, ParameterExpression sessionParameter,
                                                      TranslationContext context) {
      // Note: we have to create materializer each time, because column indexes in output can change from query to query
      var entMatzer = new EntityMaterializer(tableExpression.TableInfo);
      var allColExprs = RegisterAllColumns(tableExpression, context);
      foreach (var col in allColExprs) {
        var colIndex = RegisterOutputValue(col, context);
        entMatzer.AddColumn(col.ColumnInfo, colIndex);
      }
      var entMatzerConst = Expression.Constant(entMatzer);
      var callReadEntity = Expression.Call(entMatzerConst, EntityMaterializer.ReadMethodInfo, dataRecordParameter, sessionParameter);
      var convExpr = Expression.Convert(callReadEntity, tableExpression.Type);
      return convExpr; 
    }

    protected virtual Expression GetOutputValueReader(Expression expression, Type expectedType, ParameterExpression dataRecordParameter, 
                                                      ParameterExpression sessionParameter, TranslationContext context)
    {
      expectedType = expectedType ?? expression.Type;
      int valueIndex = RegisterOutputValue(expression, context);
      Func<object, object> conv = x => x;
      if(expression is ColumnExpression) {
        //With column everything is simple
        var colExpr = (ColumnExpression)expression;
        conv = colExpr.ColumnInfo.TypeInfo.ColumnToPropertyConverter;
      } else {
        //Otherwise get converter from type registry;
        // Why we need converters for non-column values. 
        // Example: Count(*) function. In this case expectedType is always 'int', but some providers (MySql) return Int64.
        // In this case expression.Type is Int64 (it is SqlFunction expression), while expectedType is int32. We need a converter.
        // Note - even if expression.Type is the same as expectedType, we still might need a converter. 
        // Example : 'Max(decimalCol)' in SQLite. SQLite stores decimals as doubles (we do), so SQL will return double; 
        // we need an extra converter to cast to Decimal expected by c# code. We must go through TypeRegistry anyway, 
        // to verify how type is supported.
        conv = _dbModel.Driver.TypeRegistry.GetLinqValueConverter(expression.Type, expectedType);
      }
      var reader = ColumnReaderHelper.GetColumnValueReader(dataRecordParameter, valueIndex, expectedType, conv);
      return reader; 
    }
}
  
}