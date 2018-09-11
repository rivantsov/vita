using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities.Utilities;
using Vita.Data.Driver;
using Vita.Data.Linq.Translation.Expressions;
using Vita.Entities.Model;
using Vita.Entities;
using Vita.Data.Model;

namespace Vita.Data.Linq.Translation {

  partial class ExpressionTranslator {

    //RI: new stuff to handle 2 special cases: 1. Entity comparison 2. Comparison with NULL 
    protected virtual Expression CheckEqualOperator(BinaryExpression expression, TranslationContext context) {
      var left = expression.Left;
      var right = expression.Right;
      var hasNonNullable = IsNonNullableMember(left) || IsNonNullableMember(right);
      // Try getting PK expressions - not null if we have entity references; replace expression with comparison of PKs
      var leftKey = GetPrimaryKeyAccessor(left, context);
      var rightKey = GetPrimaryKeyAccessor(right, context);
      if(leftKey != null && rightKey != null) {
        var hasParameters = left.NodeType == ExpressionType.Parameter || right.NodeType == ExpressionType.Parameter;
        if(hasParameters && !hasNonNullable && expression.NodeType == ExpressionType.Equal)
          return CreateSqlFunction(SqlFunctionType.EqualNullables, leftKey, rightKey);
        else 
          return Expression.MakeBinary(expression.NodeType, leftKey, rightKey);
      }
      // check if it is null comparison
      var isNullFuncType = expression.NodeType == ExpressionType.Equal ? SqlFunctionType.IsNull : SqlFunctionType.IsNotNull;
      if (leftKey != null && right.IsConstNull())
        return CreateSqlFunction(isNullFuncType, leftKey);
      if (rightKey != null && left.IsConstNull())
        return CreateSqlFunction(isNullFuncType, rightKey);
      // null-checks of plain values (strings)
      if (left.IsConstNull())
        return CreateSqlFunction(isNullFuncType, right);
      if (right.IsConstNull())
        return CreateSqlFunction(isNullFuncType, left);

      // If it is Equal, and both are nullable, add matching NULLs; 
      //   a == b   -> (a = b) | (a is Null & b IS NULL)
      var forceIgnoreCase = context.Command.Info.Options.IsSet(QueryOptions.ForceIgnoreCase);
      if (forceIgnoreCase && left.Type == typeof(string) & right.Type == typeof(string)) {
        return CreateSqlFunction(SqlFunctionType.StringEqual, forceIgnoreCase, left, right);
      }
      if (expression.NodeType == ExpressionType.Equal && !hasNonNullable &&
            left.NodeType != ExpressionType.Constant && right.NodeType != ExpressionType.Constant
            && left.Type.IsNullable() && right.Type.IsNullable()) {
        return CreateSqlFunction(SqlFunctionType.EqualNullables, forceIgnoreCase, left, right);
      }
        
      return expression;
    }

    public bool IsNonNullableMember(Expression expr) {
      if (expr.NodeType != ExpressionType.MemberAccess)
        return false;
      var mexpr = (MemberExpression)expr;
      var entType = mexpr.Member.DeclaringType;
      if (!entType.IsInterface) 
        return false; 
      var entInfo = _dbModel.EntityModel.GetEntityInfo(entType);
      if (entInfo == null)
        return false;
      var memberInfo = (EntityMemberInfo) entInfo.GetMember(mexpr.Member.Name);
      if (memberInfo.Flags.IsSet(EntityMemberFlags.Nullable))
        return false;
      return true; 
    }

    private Expression GetPrimaryKeyAccessor(Expression expression, TranslationContext context) {
      if(!expression.Type.IsInterface)
        return null; 
      if (expression.Type.IsDbPrimitive())
        return null;
      if(expression.IsConstNull())
        return null; 
      var table = context.DbModel.GetTable(expression.Type, throwIfNotFound: false);
      if (table == null)
        return null;
      var pkMembers = table.PrimaryKey.EntityKey.KeyMembers;
      if (pkMembers.Count > 1)
        Util.Throw("Equal operator for entities with composite key is not supported. Expression: {0}.", expression);
      var pkProp = pkMembers[0].Member.ClrMemberInfo;
      if (pkProp == null) //theoretically it might happen
        Util.Throw("Using hidden members in LINQ is not supported. Expression: {0}.", expression);
      Expression pkRead = Expression.MakeMemberAccess(expression, pkProp);
      // Bug fix - one-to-one relation
      //   in case it is an interface/entity - repeat getting PK again
      if (pkRead.Type.IsInterface)
        pkRead = GetPrimaryKeyAccessor(pkRead, context); 
      return pkRead;
    }

    // Converts expressions like 'book.Publisher.Id' to 'book.Publisher_id', to avoid unnecessary joins
    // Note that we cannot perform this optimization on original expression tree; the Publisher_id property is not available 
    //  on IBook interface, so we must first convert IBook to book table, and then create member access expr 'BookTable.Publisher_id'
    private Expression OptimizeChainedMemberAccess(MemberExpression memberExpression, TranslationContext context) {
      var bookPubIdExpr = memberExpression; //just rename it for clarity
      var bookPubExpr = bookPubIdExpr.Expression as MemberExpression;
      var bookExpr = bookPubExpr.Expression;
      if (bookPubExpr.Member.IsStaticMember())
        return null;
      // check that child expr src object (book) is a Table
      var bookTable = context.DbModel.GetTable(bookExpr.Type, throwIfNotFound: false);
      if (bookTable == null)
        return null;
      // check that child expr result (book.Publisher) has Table result type
      var pubTable = context.DbModel.GetTable(bookPubExpr.Type, throwIfNotFound: false);
      if (pubTable == null)
        return null;
      //check that expr is trying read PK member of the target entity (Id of Publisher)
      var idMemberName = bookPubIdExpr.Member.Name;
      var idMember = pubTable.PrimaryKey.EntityKey.ExpandedKeyMembers.FirstOrDefault(km => km.Member.MemberName == idMemberName);
      if (idMember == null)
        return null;
      //OK, it might be a case for optimization. Analyze bottom parameter (Book reference) 
      var analyzedBookExpr = Analyze(bookExpr, context);
      // Don't optimize it if it is based on input parameter; just return derived parameter expression.
      // note: we cannot return null here, it is too late - bookExpr had been already analyzed, it will mess it all
      if (analyzedBookExpr is ExternalValueExpression) {
        var bookPubPrm = DeriveMemberAccessParameter((ExternalValueExpression)analyzedBookExpr, bookPubExpr.Member, context);
        var bookPubIdPrm = DeriveMemberAccessParameter(bookPubPrm, bookPubIdExpr.Member, context);
        return bookPubIdPrm;
      }
      // Find FK member on src table (Publisher_id on book entity)
      var idMemberIndex = pubTable.PrimaryKey.EntityKey.ExpandedKeyMembers.IndexOf(idMember);
      var pubRefMember = bookPubExpr.Member;
      var pubRefMemberInfo = bookTable.Entity.Members.First(m => m.ClrMemberInfo == pubRefMember);
      var bookPubIdMemberInfo = pubRefMemberInfo.ReferenceInfo.FromKey.ExpandedKeyMembers[idMemberIndex].Member;
      var bookTableExpr = (TableExpression)analyzedBookExpr;
      ColumnExpression queryColumnExpression = RegisterColumnByMemberName(bookTableExpr, bookPubIdMemberInfo.MemberName, context);
      return queryColumnExpression;
    }

    //Checks operands of Join expression: if it returns Entity, replaces it with PK of entity
    private Expression CheckJoinKeySelector(Expression selector, TranslationContext context) {
      var tmp = selector;
      if (tmp.NodeType == ExpressionType.Quote)
        tmp = ((UnaryExpression)tmp).Operand;
      // It should be lambda
      var lambda = (LambdaExpression)tmp;
      var retType = lambda.Body.Type;
      if (retType.IsDbPrimitive())
        return selector;
      var newBody = GetPrimaryKeyAccessor(lambda.Body, context);
      if (newBody == null)
        return selector;
      //Build new quoted lambda
      var newSelector = Expression.Quote(Expression.Lambda(newBody, lambda.Parameters));
      return newSelector;
    }

    private IList<Expression> ConvertContainsWithObject(IList<Expression> parameters, TranslationContext context) {
      var arg1Type = parameters[1].Type;
      if (arg1Type.IsDbPrimitive())
        return parameters; 
      // we have List<Entity>.Contains - convert to using IDs
      var newParams = new List<Expression>();
      // for param 0 (entity sequence), add 'Select(e=>e.Id)'
      var prm0 = BuildSelectPrimaryKeys(parameters[0], context);
      //for parameter 1 (entity to select), add accessor to primary key
      var prm1 = GetPrimaryKeyAccessor(parameters[1], context);
      newParams.Add(prm0);
      newParams.Add(prm1); 
      return newParams; 
    }

    // Converts expression returning list of entities into expression returning list of primary keys.
    // ex: session.EntitySet<IBook>() -> session.EntitySet<IBook>().Select(b=>b.Id)
    // This is used for conversion of expressions involving entities - which SQL cannot process directly- into expressions over IDs
    // One example is entSet.Contains(ent) method - we convert it into entSet.Select(e => e.Id).Contains(ent.Id)
    private Expression BuildSelectPrimaryKeys(Expression expression, TranslationContext context) {
      var entType = expression.Type.GetGenericArguments()[0];
      // build lambda
      var entPrm = Expression.Parameter(entType, "p");
      var body = GetPrimaryKeyAccessor(entPrm, context);
      var pkType = body.Type;
      var lambda = Expression.Lambda(body, entPrm);
      var quotedLambda = Expression.Quote(lambda);
      //build Select method
      var genSelMethod = LinqExpressionHelper.QueryableSelectMethod.MakeGenericMethod(entType, pkType);
      // Special case - when entity set is list, ex: pub.Books property; If we want to put Queryable.Select over pub.Books, we must convert 
      // the argument. It has no effect on SQL generation, it purely to satisfy type checking restraints of expression construction.
      if (!typeof(IQueryable).IsAssignableFrom(expression.Type)) {
        var asQMethod = LinqExpressionHelper.QueryableAsQueryableMethod.MakeGenericMethod(entType);
        expression = Expression.Call(null, asQMethod, expression); 
      }
      var callSelect = Expression.Call(null, genSelMethod, expression, quotedLambda);
      return callSelect; 
    }

    private Expression AnalyzeEntityListMember(TableExpression table, PropertyInfo property, TranslationContext context) {
        var propType = property.PropertyType;
        if (!propType.IsEntitySequence())
          return null; 
        var modelInfo = context.DbModel.EntityModel;
        var masterEntInfo = (EntityInfo) modelInfo.GetEntityInfo(table.Type);
        var entMember = masterEntInfo.GetMember(property.Name);
        Util.Check(entMember != null, "Failed to find member {0} on entity {1}.", property.Name, masterEntInfo.Name);
        Util.Check(entMember.Kind == EntityMemberKind.EntityList, "Internal LINQ error: expected List member ({0}.{1}", masterEntInfo.Name, property.Name);
        var listInfo = entMember.ChildListInfo;
        Expression whereExpr; 
        switch(listInfo.RelationType) {
          
          case EntityRelationType.ManyToOne:
            var childTable = CreateTable(listInfo.TargetEntity.EntityType, context);
            whereExpr = BuildListPropertyJoinExpression(childTable, table, listInfo.ParentRefMember, context);
            if (listInfo.Filter != null) {
              var filterExpr = new TableFilterExpression(childTable, listInfo.Filter);
              whereExpr = Expression.And(whereExpr, filterExpr); 
            }
            context.CurrentSelect.Where.Add(whereExpr); 
            return childTable;

          case EntityRelationType.ManyToMany:
            var linkTable = CreateTable(listInfo.LinkEntity.EntityType, context);
            whereExpr = BuildListPropertyJoinExpression(linkTable, table, listInfo.ParentRefMember, context);
            context.CurrentSelect.Where.Add(whereExpr); 
            var targetTable = RegisterAssociation(linkTable, listInfo.OtherEntityRefMember, context);
            return targetTable;
        }
        return null; //never happens
    }//method

    private Expression BuildListPropertyJoinExpression(TableExpression fromTable, TableExpression toTable, EntityMemberInfo refMember, TranslationContext context) {
      var fromKeyMembers = refMember.ReferenceInfo.FromKey.ExpandedKeyMembers;
      var toKeyMembers = refMember.ReferenceInfo.ToKey.ExpandedKeyMembers;
      var clauses = new List<Expression>();
      for (int i = 0; i < fromKeyMembers.Count; i++) {
        var mFrom = fromKeyMembers[i];
        var mTo = toKeyMembers[i];
        var colFrom = RegisterColumnByMemberName(fromTable, mFrom.Member.MemberName, context);
        var colTo = RegisterColumnByMemberName(toTable, mTo.Member.MemberName, context);
        var eqExpr = MakeEqual(colFrom, colTo);
        clauses.Add(eqExpr); 
      }
      //Build AND
      var result = clauses[0];
      for (int i = 1; i < clauses.Count; i++) 
        result = Expression.And(result, clauses[i]);
      return result; 
    }

    // Translates bit column reference into an expression comparing with 1:
    //  bitCol -->   Convert(bitCol, typeof(int)) == 1
    // The Convert call is artificial, it will be eliminated during SQL translation, and resulting SQL will be smth like ' WHERE BitCol = 1; '
    public Expression CheckBoolBitExpression(Expression expression) {
      if(expression.Type != typeof(bool))
        return expression;
      if(!_dbModel.Driver.Supports(DbFeatures.TreatBitAsInt))
        return expression;
      if(ReturnsBit(expression)) {
        // Some simplification for true/false constants: true -> (1=1), false -> 1 <> 0
        bool value;
        if(expression.IsBoolConst(out value)) {
          var op = value ? ExpressionType.Equal : ExpressionType.NotEqual;
          return Expression.MakeBinary(op, Expression.Constant(1), Expression.Constant(1));
        }
        //general case - convert to 'BitCol == 1'
        var bitExpr = Expression.Convert(expression, typeof(int));
        return Expression.Equal(bitExpr, Expression.Constant(1));
      }
      //otherwise return expression
      return expression;
    }

    private BinaryExpression MakeEqual(Expression left, Expression right) {
      if (left.Type.IsNullableValueType())
        left = Expression.Convert(left, left.Type.GetUnderlyingStorageType());
      if (right.Type.IsNullableValueType())
        right = Expression.Convert(right, right.Type.GetUnderlyingStorageType());
      return Expression.Equal(left, right);
    }

    public static bool ReturnsBit(Expression expression) {
      return expression is ColumnExpression || expression is ExternalValueExpression || expression.NodeType == ExpressionType.Constant;
    }

  }
}
