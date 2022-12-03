
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

using Vita.Entities.Utilities;
using Vita.Data.Model;
using Vita.Entities.Model;
using Vita.Data.Linq.Translation.Expressions;
using Vita.Data.Driver;
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
          var entQuery = constExpr.Value as EntityQuery;
          if(entQuery == null)
            break;
          var iLock = entQuery as ILockTarget;
          var lockType = iLock == null ? LockType.None : iLock.LockType;
          return CreateTable(entQuery.ElementType, context, lockType);
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
    public virtual TableExpression RegisterTable(TableExpression tableExpression, TranslationContext context) {
      // 1. Find the table in current scope
      var foundTableExpression = (from t in context.EnumerateScopeTables()
                                  where t.IsEqualTo(tableExpression)
                                  select t).FirstOrDefault();
      if(foundTableExpression != null)
        return foundTableExpression;
      // 2. Find it in all scopes, and promote it to current scope.
      foundTableExpression = PromoteTable(tableExpression, context);
      if(foundTableExpression != null)
        return foundTableExpression;
      // 3. Add it
      if(!tableExpression.TableInfo.IsNullTable())
        context.CurrentSelect.Tables.Add(tableExpression);
      return tableExpression;
    }

    /// <summary>
    /// Promotes a table to a common parent between its current scope and our current scope
    /// </summary>
    /// <param name="tableExpression"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    protected virtual TableExpression PromoteTable(TableExpression tableExpression, TranslationContext context) {
      int currentIndex = 0;
      SelectExpression oldSelect = null;
      SelectExpression commonScope = null;
      TableExpression foundTable = null;
      do {
        // take a select
        oldSelect = context.SelectExpressions[currentIndex];

        // look for a common scope
        if(oldSelect != context.CurrentSelect) {
          commonScope = FindCommonScope(oldSelect, context.CurrentSelect);
          if(commonScope != null)
            // if a common scope exists, look for an equivalent table in that select
            for(int tableIndex = 0; tableIndex < oldSelect.Tables.Count && foundTable == null; tableIndex++) {
              if(oldSelect.Tables[tableIndex].IsEqualTo(tableExpression)) {
                // found a matching table!
                foundTable = oldSelect.Tables[tableIndex];
              }
            }
        }
        ++currentIndex;
      }
      while(currentIndex < context.SelectExpressions.Count && foundTable == null);

      if(foundTable != null) {
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
    protected virtual SelectExpression FindCommonScope(SelectExpression a, SelectExpression b) {
      for(var aScope = a; aScope != null; aScope = aScope.Parent) {
        for(var bScope = b; bScope != null; bScope = bScope.Parent) {
          if(aScope == bScope)
            return aScope;
        }
      }
      return null;
    }

    public ColumnExpression RegisterColumnByMemberName(TableExpression table, string memberName, TranslationContext context) {
      var colInfo = table.TableInfo.GetColumnByMemberName(memberName);
      return RegisterColumn(table, colInfo.ColumnName, context);
    }

    public ColumnExpression RegisterColumn(TableExpression table, string columnName, TranslationContext context) {
      var oldCol = context.CurrentSelect.Columns.FirstOrDefault(c => c.Table == table && c.ColumnInfo.ColumnName == columnName);
      if(oldCol != null)
        return oldCol; 
      table = RegisterTable(table, context);
      var col = CreateColumn(table, columnName, context);
      context.CurrentSelect.Columns.Add(col);
      return col;
    }

    protected virtual ColumnExpression GetRegisteredColumn(TableExpression table, string columnName,
                                                            TranslationContext context) {
      return
            (from queryColumn in context.EnumerateScopeColumns()
             where queryColumn.Table.IsEqualTo(table) && queryColumn.Name == columnName
             // where queryColumn.Table == table && queryColumn.Name == name // - RI: this does not work
             select queryColumn).SingleOrDefault();
    }


    public ColumnExpression CreateColumn(TableExpression table, string columnName, TranslationContext context) {
      var col = table.TableInfo.GetColumn(columnName);
      return new ColumnExpression(table, col);
    }
    public ColumnExpression CreateColumnForMember(TableExpression table, EntityMemberInfo member, TranslationContext context) {
      var col = table.TableInfo.Columns.FirstOrDefault(c => c.Member == member);
      return new ColumnExpression(table, col);
    }

    public virtual TableExpression CreateTable(Type tableType, TranslationContext context, LockType lockType = LockType.None) {
      var tableInfo = _dbModel.GetTable(tableType);
      var tableExpr = new TableExpression(tableInfo, lockType);
      return tableExpr;
    }

    public virtual SqlFunctionExpression CreateSqlFunction(SqlFunctionType functionType, params Expression[] operands) {
      return CreateSqlFunction(functionType, true, operands);
    }

    public virtual SqlFunctionExpression CreateSqlFunction(SqlFunctionType functionType, bool ignoreCase, params Expression[] operands) {
      Type[] opTypes = null;
      if(operands != null && operands.Length > 0)
        opTypes = operands.Select(op => op.Type).ToArray();
      var outType = _dbModel.Driver.SqlDialect.GetSqlFunctionResultType(functionType, opTypes);
      return new SqlFunctionExpression(functionType, outType, ignoreCase, operands);
    }

    public virtual AggregateExpression CreateAggregate(AggregateType aggregateType, params Expression[] operands) {
      Type[] opTypes = null;
      if(operands != null && operands.Length > 0)
        opTypes = operands.Select(op => op.Type).ToArray();
      var outType = _dbModel.Driver.SqlDialect.GetAggregateResultType(aggregateType, opTypes);
      return new AggregateExpression(aggregateType, outType, operands);
    }

    public virtual TableExpression RegisterAssociation(TableExpression tableExpression, EntityMemberInfo refMember, TranslationContext context) {
      IList<EntityMemberInfo> otherKeys;
      TableJoinType joinType;
      string joinID;
      var theseKeys = GetAssociationMembers(tableExpression, refMember, out otherKeys, out joinType, out joinID);
      // if the memberInfo has no corresponding association, we get a null, that we propagate
      if(theseKeys == null)
        return null;

      var otherType = refMember.DataType;
      var otherTableInfo = context.DbModel.GetTable(otherType);
      var otherTableExpression = new TableExpression(otherTableInfo, LockType.None, joinID);
      otherTableExpression = RegisterTable(otherTableExpression, context);
      Expression joinExpression = null;

      var createdColumns = new List<ColumnExpression>();
      for(int keyIndex = 0; keyIndex < theseKeys.Count; keyIndex++) {
        // joinedKey is registered, even if unused by final select (required columns will be filtered anyway)
        var otherMember = otherKeys[keyIndex];
        Expression otherKey = RegisterColumnByMemberName(otherTableExpression, otherMember.MemberName, context); 
        // foreign key is created, we will store it later if this assocation is registered too
        var thisMember = theseKeys[keyIndex];
        Expression thisKey = CreateColumnForMember(tableExpression, thisMember, context);
        createdColumns.Add((ColumnExpression)thisKey);

        // the other key is set as left operand, this must be this way
        // since some vendors (SQL Server) don't support the opposite
        var referenceExpression = MakeEqual(otherKey, thisKey);

        // if we already have a join expression, then we have a double condition here, so "AND" it
        if(joinExpression != null)
          joinExpression = Expression.And(joinExpression, referenceExpression);
        else
          joinExpression = referenceExpression;
      }
      // we complete the table here, now that we have all join information
      otherTableExpression.Join(joinType, tableExpression, joinExpression);

      foreach(var createdColumn in createdColumns)
        context.CurrentSelect.Columns.Add(createdColumn);
      return otherTableExpression;
    }

    private IList<EntityMemberInfo> GetAssociationMembers(TableExpression thisTableExpression, EntityMemberInfo member,
                            out IList<EntityMemberInfo> otherKeyMembers, out TableJoinType joinType, out string joinID) {
      switch(member.Kind) {
        case EntityMemberKind.EntityRef:
          // by default, join is inner
          joinType = TableJoinType.Inner;
          joinID = member.MemberName;
          var otherType = member.ReferenceInfo.ToKey.Entity.ClassInfo.Type;
          otherKeyMembers = member.ReferenceInfo.ToKey.KeyMembersExpanded.Select(km => km.Member).ToList();
          var thisKey = member.ReferenceInfo.FromKey.KeyMembersExpanded.Select(km => km.Member).ToList();
          if(member.Flags.IsSet(EntityMemberFlags.Nullable))
            joinType |= TableJoinType.LeftOuter;
          return thisKey;
        case EntityMemberKind.Transient:
          if(!member.Flags.IsSet(EntityMemberFlags.FromOneToOneRef))
            break;
          
          joinType = TableJoinType.LeftOuter;
          joinID = member.MemberName;
          var targetEnt = _dbModel.EntityModel.GetEntityInfo(member.DataType, throwIfNotFound: true);
          otherKeyMembers = targetEnt.PrimaryKey.KeyMembersExpanded.Select(km => km.Member).ToList();
          var thisPkMembers = member.Entity.PrimaryKey.KeyMembersExpanded.Select(km => km.Member).ToList();
          return thisPkMembers;
      }
      Util.Throw("Cannot create JOIN expression for property {0}, property not supported in LINQ.", member);
      //just to satisfy 
      otherKeyMembers = null;
      joinType = TableJoinType.Default;
      joinID = null;
      return null;
    }


    /// <summary>
    /// Registers a where clause in the current context scope
    /// </summary>
    /// <param name="whereExpression"></param>
    /// <param name="context"></param>
    public virtual void RegisterWhere(Expression whereExpression, TranslationContext context) {
      var where = CheckBoolBitExpression(whereExpression);
      var currSelect = context.CurrentSelect;
      currSelect.Where = currSelect.Where == null ? where : Expression.And(currSelect.Where, where);
    }

    /// <summary>
    /// Some methods, like Single(), Count(), etc. can get an extra parameter, specifying a restriction.
    /// This method checks if the parameter is specified, and adds it to the WHERE clauses
    /// </summary>
    /// <param name="table"></param>
    /// <param name="parameters"></param>
    /// <param name="extraParameterIndex"></param>
    /// <param name="context"></param>
    private void CheckWhere(Expression table, IList<Expression> parameters, int extraParameterIndex, TranslationContext context) {
      if(extraParameterIndex >= 0 && extraParameterIndex < parameters.Count) // a lambda can be specified here, this is a restriction
        RegisterWhere(Analyze(parameters[extraParameterIndex], table, context), context);
    }

    public virtual IList<ColumnExpression> RegisterAllColumns(TableExpression tableExpression, TranslationContext context) {
      var result = new List<ColumnExpression>();
      foreach(var colInfo in tableExpression.TableInfo.Columns) {
        if(colInfo.Member == null)
          continue;
        var colExpr = RegisterColumn(tableExpression, colInfo.ColumnName, context);
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
    public virtual int RegisterOutputValue(Expression expression, TranslationContext context) {
      var scope = context.CurrentSelect;
      //check if expression is already registered
      var operands = scope.Operands; 
      var index = operands.IndexOf(expression);
      if(index >= 0)
        return index;
      operands.Add(expression);
      context.CurrentSelect = (SelectExpression)scope.Mutate(operands);
      return operands.Count - 1;
    }

    // RI: new stuff, optimized entity reader
    protected virtual Expression GetEntityReaderAsExpression(TableExpression tableExpression,
                                                      ParameterExpression dataRecordParameter, ParameterExpression sessionParameter,
                                                      TranslationContext context) {
      // Note: we have to create EntityRecordReader each time, because column indexes in output can change from query to query
      var reader = CreateEntityReader(tableExpression, context);
      var readerConst = Expression.Constant(reader);
      var callReadEntity = Expression.Call(readerConst, EntityRecordReader.ReadMethodInfo, dataRecordParameter, sessionParameter);
      var convExpr = Expression.Convert(callReadEntity, tableExpression.Type);
      return convExpr;
    }

    protected virtual EntityRecordReader CreateEntityReader(TableExpression tableExpression,
                                                      TranslationContext context) { 
      // Note: we have to create EntityRecordReader each time, because column indexes in output can change from query to query
      var reader = new EntityRecordReader(tableExpression.TableInfo);
      var allColExprs = RegisterAllColumns(tableExpression, context);
      foreach(var col in allColExprs) {
        var colIndex = RegisterOutputValue(col, context);
        reader.AddColumn(col.ColumnInfo, colIndex);
      }
      return reader; 
    }

    protected virtual Expression GetOutputValueReader(Expression expression, Type expectedType, ParameterExpression dataRecordParameter,
                                                      ParameterExpression sessionParameter, TranslationContext context) {
      expectedType = expectedType ?? expression.Type;
      int valueIndex = RegisterOutputValue(expression, context);
      DbValueConverter conv = null;
      if(expression is ColumnExpression colExpr) {
        //With column everything is simple
        conv = colExpr.ColumnInfo.Converter;
      } else {
        //Otherwise get converter from type registry;
        // Why we need converters for non-column values. 
        // Example: Count(*) function. In this case expectedType is always 'int', but some providers (MySql) return Int64.
        // In this case expression.Type is Int64 (it is SqlFunction expression), while expectedType is int32. We need a converter.
        // Note - even if expression.Type is the same as expectedType, we still might need a converter. 
        // Example : 'Max(decimalCol)' in SQLite. SQLite stores decimals as doubles (we do), so SQL will return double; 
        // we need an extra converter to cast to Decimal expected by c# code. We must go through TypeRegistry anyway, 
        // to verify how type is supported.
        conv = _dbModel.Driver.TypeRegistry.GetDbValueConverter(expression.Type, expectedType);
      }
      var reader = ColumnReaderHelper.GetColumnValueReader(dataRecordParameter, valueIndex, expectedType, conv.ColumnToProperty);
      return reader;
    }
  }

}