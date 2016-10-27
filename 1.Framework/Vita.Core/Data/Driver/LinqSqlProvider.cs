using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;

using Vita.Common;
using Vita.Entities;
using Vita.Data.Model;
using Vita.Data.Linq;
using Vita.Data.Linq.Translation.SqlGen;
using Vita.Data.Linq.Translation.Expressions;
using Binary = Vita.Common.Binary;
using Vita.Entities.Linq;


namespace Vita.Data.Driver {
  //RI: this class comes from DbLinq code, should be refactored in the future. 

    public class LinqSqlProvider {
      protected DbModel DbModel; 
      protected DbDriver Driver;
      protected char SafeNameStartQuote = '"';
      protected char SafeNameEndQuote = '"';


      public LinqSqlProvider(DbModel dbModel) {
        DbModel = dbModel; 
        Driver = dbModel.Driver; 
      }

      /// <summary>  Previews and changes if necessary the entire (outermost) expression. </summary>
      /// <param name="e"></param>
      /// <remarks> 
      /// <para>
      /// Derived classes can override this method to manipulate the entire expression prior to SQL generation.
      ///   </para>
      /// </remarks>
      public virtual SelectExpression PreviewSelect(SelectExpression e) {
        //None of the servers support in one query COUNT and Limit (FETCH NEXT for MS SQL) 
        if (e.HasLimit() && e.HasOutAggregates())
          Util.Throw("Invalid LINQ expression: Server does not support COUNT(*) and LIMIT (MS SQL: FETCH NEXT) in one query.");
        return e;
      }
      public virtual SqlStatement ReviewSelectSql(SelectExpression select, SqlStatement sql) {
        return sql;
      }

      /// <summary>Checks parameter type and determines if parameter may be used as DB command parameter. 
      /// Sets parameter value SqlUse property to DbParameter or Literal. </summary>
      /// <param name="parameter">Input parameter expression to check.</param>
      public virtual void CheckQueryParameter(ExternalValueExpression parameter) {
        var type = parameter.Type;
        if (type.IsNullableValueType())
          type = type.GetUnderlyingType();
        // this includes Guid and enum
        if (type.IsDbPrimitive() || type == typeof(Binary) || type == typeof(TimeSpan)) {
          parameter.SqlUse = ExternalValueSqlUse.Parameter;
          return; 
        }
        // if it is a list of primitive types, check if provider support list parameters
        if (type.IsListOfDbPrimitive()) { 
          var canUseArrayParam = Driver.Supports(DbFeatures.ArrayParameters) 
                                   && !DbModel.Config.Options.IsSet(DbOptions.ForceArraysAsLiterals); 
          if (canUseArrayParam)
            parameter.SqlUse = ExternalValueSqlUse.Parameter;
          else 
            parameter.SqlUse = ExternalValueSqlUse.Literal;
          return;
        }
        // throw error
        var msg = "Sql provider does not support parameter/value type: {0}.";
        if (typeof(System.Collections.IList).IsAssignableFrom(parameter.Type))
          msg += " List/array values are supported only for primitive types.";
        Util.Throw(msg, parameter.Type);
      }
        /// <summary>
        /// Gets the new line string.
        /// </summary>
        /// <value>The new line.</value>
        public string NewLine
        {
            get { return Environment.NewLine; }
        }
        /// <summary>
        /// Converts a constant value to a literal representation
        /// </summary>
        /// <param name="literal"></param>
        /// <returns></returns>
        public virtual SqlStatement GetLiteral(object literal)
        {
            if (literal == null)
                return GetNullLiteral();
            var type = literal.GetType();
            if (type == typeof(Binary))
              return GetLiteralBinary((Binary)literal);
            if (type == typeof(string))
                return GetLiteral((string)literal);
            if (type == typeof(char))
                return GetLiteral(literal.ToString());
            if (type == typeof(bool))
                return GetLiteral((bool)literal);
            if (type == typeof(Guid))
              return GetLiteral((Guid)literal);
            if (type == typeof(DateTime))
                return GetLiteral((DateTime)literal);
            if (type.IsListOrArray())
                return GetLiteralList((IList)literal);
            if (literal.GetType().IsEnum)
              return ((int)literal).ToString() + "/*" + literal.ToString() + "*/"; 
            return Convert.ToString(literal, CultureInfo.InvariantCulture);
        }

        public virtual SqlStatement GetLiteralBinary(Binary value) {
          return GetLiteralBinary(value.ToArray());
        }
        public virtual SqlStatement GetLiteralBinary(Byte[] value) {
          return "0x" + HexUtil.ByteArrayToHex(value);
        }

        public virtual SqlStatement GetLiteral(Guid guid) {
          return "'" + guid.ToString() + "'";
        }

        public virtual SqlStatement GetLiteral(DateTime literal)
        {
          return "'" + literal.ToString("o").Substring(0, 23) + "'";
        }

        public virtual SqlStatement GetLiteral(bool literal)
        {
            return Convert.ToString(literal, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Converts a standard operator to an expression
        /// </summary>
        /// <param name="operationType"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public virtual SqlStatement GetLiteral(ExpressionType operationType, IList<SqlStatement> args)
        {
            for(int i= 0; i < args.Count; i++)
              args[i] = EncloseParenth(args[i]);
            switch (operationType)  {
              case ExpressionType.Add:
                  return GetLiteralAdd(args[0], args[1]);
              case ExpressionType.AddChecked:
                  return GetLiteralAddChecked(args[0], args[1]);
              case ExpressionType.And:
                  return GetLiteralAnd(args[0], args[1]);
              case (ExpressionType) SqlFunctionType.AndBitwise:
                  return GetLiteralAndBitwise(args[0], args[1]);
              case ExpressionType.AndAlso:
                  return GetLiteralAndAlso(args[0], args[1]);
              case ExpressionType.ArrayLength:
                  return GetLiteralArrayLength(args[0], args[1]);
              case ExpressionType.ArrayIndex:
                  return GetLiteralArrayIndex(args[0], args[1]);
              case ExpressionType.Call:
                  return GetLiteralCall(args[0]);
              case ExpressionType.Coalesce:
                  return GetLiteralCoalesce(args[0], args[1]);
              case ExpressionType.Conditional:
                  return GetLiteralConditional(args[0], args[1], args[2]);
              //case ExpressionType.Constant:
              //break;
              case ExpressionType.Divide:
                  return GetLiteralDivide(args[0], args[1]);
              case ExpressionType.Equal:
                  return GetLiteralEqual(args[0], args[1]);
              case ExpressionType.ExclusiveOr:
                  return GetLiteralExclusiveOr(args[0], args[1]);
              case ExpressionType.GreaterThan:
                  return GetLiteralGreaterThan(args[0], args[1]);
              case ExpressionType.GreaterThanOrEqual:
                  return GetLiteralGreaterThanOrEqual(args[0], args[1]);
              //case ExpressionType.Invoke:
              //break;
              //case ExpressionType.Lambda:
              //break;
              case ExpressionType.LeftShift:
                  return GetLiteralLeftShift(args[0], args[1]);
              case ExpressionType.LessThan:
                  return GetLiteralLessThan(args[0], args[1]);
              case ExpressionType.LessThanOrEqual:
                  return GetLiteralLessThanOrEqual(args[0], args[1]);
              //case ExpressionType.ListInit:
              //break;
              //case ExpressionType.MemberAccess:
              //    break;
              //case ExpressionType.MemberInit:
              //    break;
              case ExpressionType.Modulo:
                  return GetLiteralModulo(args[0], args[1]);
              case ExpressionType.Multiply:
                  return GetLiteralMultiply(args[0], args[1]);
              case ExpressionType.MultiplyChecked:
                  return GetLiteralMultiplyChecked(args[0], args[1]);
              case ExpressionType.Negate:
                  return GetLiteralNegate(args[0]);
              case ExpressionType.UnaryPlus:
                  return GetLiteralUnaryPlus(args[0]);
              case ExpressionType.NegateChecked:
                  return GetLiteralNegateChecked(args[0]);
              //case ExpressionType.New:
              //    break;
              //case ExpressionType.NewArrayInit:
              //    break;
              //case ExpressionType.NewArrayBounds:
              //    break;
              case ExpressionType.Not:
                  return GetLiteralNot(args[0]);
              case ExpressionType.NotEqual:
                  return GetLiteralNotEqual(args[0], args[1]);
              case ExpressionType.Or:
                  return GetLiteralOr(args[0], args[1]);
              case (ExpressionType) SqlFunctionType.OrBitwise:
                  return GetLiteralOrBitwise(args[0], args[1]);
              case ExpressionType.OrElse:
                  return GetLiteralOrElse(args[0], args[1]);
              //case ExpressionType.Parameter:
              //    break;
              case ExpressionType.Power:
                  return GetLiteralPower(args[0], args[1]);
              //case ExpressionType.Quote:
              //    break;
              case ExpressionType.RightShift:
                  return GetLiteralRightShift(args[0], args[1]);
              case ExpressionType.Subtract:
                  return GetLiteralSubtract(args[0], args[1]);
              case ExpressionType.SubtractChecked:
                  return GetLiteralSubtractChecked(args[0], args[1]);
              //case ExpressionType.TypeAs:
              //    break;
              //case ExpressionType.TypeIs:
              //    break;
            }
            throw new ArgumentException("Unsupported operator for expression type: " +  operationType.ToString());
        }

        //Adds parenthesis if needed
        static char[] _operators = new char[] { '=', '<', '>', '+', '-', '*', '/' };
        protected virtual string EncloseParenth(SqlStatement s) {
          var str = s.ToString();
          //RI: I know, it is really sloppy, should be refactored with big engine refactoring
          if(str.StartsWith("(") && str.EndsWith(")"))
            return str;
          if(str.IndexOfAny(_operators) >= 0)
            return "(" + str + ")";
          return str;
        }

        public virtual SqlStatement GetSqlFunction(SqlFunctionType functionType, bool forceIgnoreCase, IList<SqlStatement> parameters)
        {
          var pLast = parameters.LastOrDefault();
            switch (functionType) // 
            {
            case SqlFunctionType.IsNull:
                return GetLiteralIsNull(parameters[0]);
            case SqlFunctionType.IsNotNull:
                return GetLiteralIsNotNull(parameters[0]);
            case SqlFunctionType.EqualNullables:
                return GetEqualNullables(parameters[0], parameters[1]);
            case SqlFunctionType.Concat:
                return GetLiteralStringConcat(parameters[0], parameters[1]);
            case SqlFunctionType.Count:
                return GetLiteralCount(parameters[0]);
            case SqlFunctionType.Exists:
                return GetLiteralExists(parameters[0]);
            case SqlFunctionType.Like:
                return GetLiteralLike(parameters[0], parameters[1], forceIgnoreCase);
            // RI: changed index to 1 (from 0) for Min, Max, Avg, Sum
            case SqlFunctionType.Min:
                return GetLiteralMin(pLast);
            case SqlFunctionType.Max:
                return GetLiteralMax(pLast);
            case SqlFunctionType.Sum:
                return GetLiteralSum(pLast);
            case SqlFunctionType.Average:
                return GetLiteralAverage(pLast);

            case SqlFunctionType.StringLength:
                return GetLiteralStringLength(parameters[0]);
            case SqlFunctionType.ToUpper:
                return GetLiteralStringToUpper(parameters[0]);
            case SqlFunctionType.ToLower:
                return GetLiteralStringToLower(parameters[0]);
            case SqlFunctionType.In:
                return GetLiteralIn(parameters[0], parameters[1]);
            case SqlFunctionType.InArray:
                return GetLiteralInArray(parameters[0], parameters[1]);
            case SqlFunctionType.StringEqual:
                return GetLiteralStringEqual(parameters[0], parameters[1], forceIgnoreCase);
            case SqlFunctionType.Substring:
                if (parameters.Count > 2)
                    return GetLiteralSubString(parameters[0], parameters[1], parameters[2]);
                else 
                    return GetLiteralSubString(parameters[0], parameters[1]);
            case SqlFunctionType.Trim:
            case SqlFunctionType.LTrim:
            case SqlFunctionType.RTrim:
                return GetLiteralTrim(parameters[0]);
            case SqlFunctionType.StringInsert:
                return GetLiteralStringInsert(parameters[0], parameters[1], parameters[2]);
            case SqlFunctionType.Replace:
                return GetLiteralStringReplace(parameters[0], parameters[1], parameters[2]);
            case SqlFunctionType.Remove:
                if (parameters.Count > 2)
                    return GetLiteralStringRemove(parameters[0], parameters[1], parameters[2]);
                return GetLiteralStringRemove(parameters[0], parameters[1]);
            case SqlFunctionType.IndexOf:
                if (parameters.Count == 2)
                    return GetLiteralStringIndexOf(parameters[0], parameters[1]);
                else if (parameters.Count == 3)
                    return GetLiteralStringIndexOf(parameters[0], parameters[1], parameters[2]);
                else if (parameters.Count == 4)
                    return GetLiteralStringIndexOf(parameters[0], parameters[1], parameters[2], parameters[3]);
                break;
            case SqlFunctionType.Year:
            case SqlFunctionType.Month:
            case SqlFunctionType.Day:
            case SqlFunctionType.Hour:
            case SqlFunctionType.Minute:
            case SqlFunctionType.Second:
            case SqlFunctionType.Millisecond:
            case SqlFunctionType.Date:
            case SqlFunctionType.Time:
            case SqlFunctionType.Week:
              return GetLiteralDateTimePart(parameters[0], functionType);
            case SqlFunctionType.DateDiffInMilliseconds:
                return GetLiteralDateDiff(parameters[0], parameters[1]);
            case SqlFunctionType.Abs:
                return GetLiteralMathAbs(parameters[0]);
            case SqlFunctionType.Exp:
                return GetLiteralMathExp(parameters[0]);
            case SqlFunctionType.Floor:
                return GetLiteralMathFloor(parameters[0]);
            case SqlFunctionType.Ln:
                return GetLiteralMathLn(parameters[0]);

            case SqlFunctionType.Log:
                if (parameters.Count == 1)
                    return GetLiteralMathLog(parameters[0]);
                else
                    return GetLiteralMathLog(parameters[0], parameters[1]);
            case SqlFunctionType.Pow:
                return GetLiteralMathPow(parameters[0], parameters[1]);
            case SqlFunctionType.Round:
                return GetLiteralMathRound(parameters[0]);
            case SqlFunctionType.Sign:
                return GetLiteralMathSign(parameters[0]);
            case SqlFunctionType.Sqrt:
                return GetLiteralMathSqrt(parameters[0]);
              case SqlFunctionType.AndBitwise:
                return GetLiteralAndBitwise(parameters[0], parameters[1]);
              case SqlFunctionType.OrBitwise:
                return GetLiteralOrBitwise(parameters[0], parameters[1]);
              case SqlFunctionType.XorBitwise:
                return GetLiteralExclusiveOrBitwise(parameters[0], parameters[1]);
              case SqlFunctionType.ConvertBoolToBit:
                return GetConvertBoolToBit(parameters[0]);
              case SqlFunctionType.NewGuid:
                return GetNewGuid();
            }//switch
            throw new ArgumentException(functionType.ToString());
        }

        protected virtual SqlStatement GetConvertBoolToBit(SqlStatement arg0) {
          return arg0; //default implementation; MS SQL overrides this
        }
        protected virtual SqlStatement GetNewGuid() {
          Util.Throw("LINQ provider: Server {0} does not provide implementation for NewGuid function.", this.Driver.ServerType);
          return null; //never happens
        }

        protected virtual SqlStatement GetLiteralExists(SqlStatement sqlStatement)
        {
            return SqlStatement.Format("EXISTS {0}", sqlStatement);
        }

        public virtual int SpecificVendorStringIndexStart
        {
            get {return 0;}
        }
        /// <summary>
        /// Gets the literal math SQRT.
        /// </summary>
        /// <param name="p">The p.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralMathSqrt(SqlStatement p)
        {
            return SqlStatement.Format("SQRT({0})", p);
        }

        /// <summary>
        /// Gets the literal math sign.
        /// </summary>
        /// <param name="p">The p.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralMathSign(SqlStatement p)
        {
            return SqlStatement.Format("SIGN({0})", p);
        }

        /// <summary>
        /// Gets the literal math round.
        /// </summary>
        /// <param name="p">The p.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralMathRound(SqlStatement p)
        {
            return SqlStatement.Format("ROUND({0})", p);
        }

        /// <summary>
        /// Gets the literal math pow.
        /// </summary>
        /// <param name="p">The p.</param>
        /// <param name="p_2">The P_2.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralMathPow(SqlStatement p, SqlStatement p_2)
        {
            return SqlStatement.Format("POW({0},{1})", p, p_2);
        }

        /// <summary>
        /// Gets the literal math log.
        /// </summary>
        /// <param name="p">The p.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralMathLog(SqlStatement p)
        {
            return SqlStatement.Format("LOG({0})", p);
        }

        /// <summary>
        /// Gets the literal math log.
        /// </summary>
        /// <param name="p">The p.</param>
        /// <param name="p_2">The P_2.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralMathLog(SqlStatement p, SqlStatement p_2)
        {
            return SqlStatement.Format("LOG({0},{1})", p, p_2);
        }

        /// <summary>
        /// Gets the literal math ln.
        /// </summary>
        /// <param name="p">The p.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralMathLn(SqlStatement p)
        {
            return SqlStatement.Format("LN({0})", p);
        }

        /// <summary>
        /// Gets the literal math floor.
        /// </summary>
        /// <param name="p">The p.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralMathFloor(SqlStatement p)
        {
            return SqlStatement.Format("FLOOR({0})", p);
        }

        /// <summary>
        /// Gets the literal math exp.
        /// </summary>
        /// <param name="p">The p.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralMathExp(SqlStatement p)
        {
            return SqlStatement.Format("EXP({0})", p);
        }

        /// <summary>
        /// Gets the literal math abs.
        /// </summary>
        /// <param name="p">The p.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralMathAbs(SqlStatement p)
        {
            return SqlStatement.Format("ABS({0})", p);
        }

        /// <summary>
        /// It should return a int with de difference in milliseconds between two dates.
        /// It is used in a lot of tasks, ie: operations of timespams ej: timespam.Minutes or timespam.TotalMinutes
        /// </summary>
        /// <remarks>
        /// In the implementation you should pay atention in overflows inside the database engine, since a difference of dates in milliseconds
        /// maybe deliver a very big integer int. Ie: sqlServer provider  has to do some tricks with castings for implementing such requeriments.
        /// </remarks>
        /// <param name="dateA"></param>
        /// <param name="dateB"></param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralDateDiff(SqlStatement dateA, SqlStatement dateB)
        {
            return SqlStatement.Format("DATEDIFF(MILLISECOND,{0},{1})", dateA, dateB);
        }


        /// <summary>
        /// Gets the literal date time part.
        /// </summary>
        /// <param name="dateExpression">The date expression.</param>
        /// <param name="operationType">Type of the operation.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralDateTimePart(SqlStatement dateExpression, SqlFunctionType operationType)
        {
            return SqlStatement.Format("EXTRACT({0} FROM {1})", operationType.ToString().ToUpper(), dateExpression);
        }

        /// <summary>
        /// Gets the literal string index of.
        /// </summary>
        /// <param name="baseString">The base string.</param>
        /// <param name="searchString">The search string.</param>
        /// <param name="startIndex">The start index.</param>
        /// <param name="count">The count.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralStringIndexOf(SqlStatement baseString, SqlStatement searchString, SqlStatement startIndex, SqlStatement count)
        {
            //trim left the string
            var substring = GetLiteralSubString(baseString, startIndex, count);

            var substringIndexOf = SqlStatement.Format("STRPOS({0},{1})", substring, searchString).ToString();
            // TODO: the start index MUST be handled above at code generation
            var indexOf = GetLiteralAdd(substringIndexOf, startIndex);

            return indexOf;
        }

        /// <summary>
        /// This function should return the first index of the string 'searchString' 
        /// in a string 'baseString' but starting in 'the startIndex' index . 
        /// This can be a problem since most of database engines doesn't have such overload of SUBSTR, 
        /// the base implementation do it in a pretty complex with the goal of be most generic syntax
        /// as possible using a set of primitives(SUBSTRING(X,X,X) and STRPOS(X,X),+ , *).
        /// This function is usually used in others methods of this sqlprovider.
        /// </summary>
        /// <remarks>
        /// In the impleementation you should pay atention that in some database engines the indexes of arrays or strings are shifted one unit.
        /// ie: in .NET stringExpression.Substring(2,2) should be translated as SUBSTRING (stringExpression, 3 , 2) since the first element in sqlserver in a SqlStatement has index=1
        /// </remarks>
        protected virtual SqlStatement GetLiteralStringIndexOf(SqlStatement baseString, SqlStatement searchString, SqlStatement startIndex)
        {
            var substring = GetLiteralSubString(baseString, startIndex);

            var substringIndexOf = SqlStatement.Format("STRPOS({0},{1})", substring, searchString);

            return GetLiteralMultiply(GetLiteralAdd(substringIndexOf, startIndex), substringIndexOf);
        }

        /// <summary>
        /// Gets the literal string index of.
        /// </summary>
        /// <param name="baseString">The base string.</param>
        /// <param name="searchString">The search string.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralStringIndexOf(SqlStatement baseString, SqlStatement searchString)
        {
            return SqlStatement.Format("STRPOS({0},{1})", baseString, searchString);
        }

        /// <summary>
        /// Gets the literal string remove.
        /// </summary>
        /// <param name="baseString">The base string.</param>
        /// <param name="startIndex">The start index.</param>
        /// <param name="count">The count.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralStringRemove(SqlStatement baseString, SqlStatement startIndex, SqlStatement count)
        {
            return GetLiteralStringConcat(
                    GetLiteralSubString(baseString, SqlStatement.Format(SpecificVendorStringIndexStart.ToString()), startIndex),
                    GetLiteralSubString(baseString, GetLiteralAdd(startIndex, count).ToString(), GetLiteralStringLength(baseString)));
        }

        /// <summary>
        /// Gets the literal string remove.
        /// </summary>
        /// <param name="baseString">The base string.</param>
        /// <param name="startIndex">The start index.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralStringRemove(SqlStatement baseString, SqlStatement startIndex)
        {
            return GetLiteralSubString(baseString, "1", startIndex);
        }

        /// <summary>
        /// Gets the literal string replace.
        /// </summary>
        /// <param name="stringExpresision">The string expresision.</param>
        /// <param name="searchString">The search string.</param>
        /// <param name="replacementstring">The replacementstring.</param>
        /// <returns></returns>
        protected SqlStatement GetLiteralStringReplace(SqlStatement stringExpresision, SqlStatement searchString, SqlStatement replacementstring)
        {
            return SqlStatement.Format("REPLACE({0},{1},{2})", stringExpresision, searchString, replacementstring);
        }

        /// <summary>
        /// Gets the literal string insert.
        /// </summary>
        /// <param name="stringExpression">The string expression.</param>
        /// <param name="position">The position.</param>
        /// <param name="insertString">The insert string.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralStringInsert(SqlStatement stringExpression, SqlStatement position, SqlStatement insertString)
        {

            return this.GetLiteralStringConcat(
                            this.GetLiteralStringConcat(
                                            GetLiteralSubString(stringExpression, "1", position),
                                            insertString),
                            this.GetLiteralSubString(stringExpression, GetLiteralAdd(position, "1")));
        }


        /// <summary>
        /// Returns an operation between two SELECT clauses (UNION, UNION ALL, etc.)
        /// </summary>
        /// <param name="selectOperator"></param>
        /// <param name="selectA"></param>
        /// <param name="selectB"></param>
        /// <returns></returns>
        public virtual SqlStatement GetLiteral(SelectOperatorType selectOperator, SqlStatement selectA, SqlStatement selectB)
        {
            switch (selectOperator)
            {
            case SelectOperatorType.Union:
                return GetLiteralUnion(selectA, selectB);
            case SelectOperatorType.UnionAll:
                return GetLiteralUnionAll(selectA, selectB);
            case SelectOperatorType.Intersection:
                return GetLiteralIntersect(selectA, selectB);
            case SelectOperatorType.Exception:
                return GetLiteralExcept(selectA, selectB);
            default:
                throw new ArgumentOutOfRangeException(selectOperator.ToString());
            }
        }

        /// <summary>
        /// Places the expression into parenthesis
        /// </summary>
        /// <param name="a"></param>
        /// <returns></returns>
        public virtual SqlStatement GetParenthesis(SqlStatement a)
        {
            return SqlStatement.Format("({0})", a);
        }

        /// <summary>
        /// Returns a column related to a table.
        /// Ensures about the right case
        /// </summary>
        /// <param name="table"></param>
        /// <param name="column"></param>
        /// <returns></returns>
        public virtual string GetColumn(string table, string column)
        {
            return string.Format("{0}.{1}", table, GetColumn(column));
        }

        public virtual string GetTableFilter(TableFilterExpression tableFilter) {
          var filter = tableFilter.Filter;
          var prefix = string.Empty;
          var alias = tableFilter.Table.Alias;
          if (!string.IsNullOrWhiteSpace(alias))
            prefix = GetTableAlias(alias) + ".";
          GetTableAlias(tableFilter.Table.Alias);
          foreach (var col in tableFilter.Table.TableInfo.Columns) {
            var name = "{" + col.Member.MemberName + "}";
            if (!filter.Contains(name))
              continue;
            var colRef = prefix + '"' + col.ColumnName + '"';
            filter = filter.Replace(name, colRef);
          }
          filter = "(" + filter + ")";
          return filter;
        }

        /// <summary>
        /// Returns a column related to a table.
        /// Ensures about the right case
        /// </summary>
        /// <param name="column"></param>
        /// <returns></returns>
        public string GetColumn(string column)
        {
            return GetSafeNamePart(column);
        }

        public virtual string GetTable(TableExpression tableExpr) {
          var result = GetSafeName(tableExpr.Name);
          if (!string.IsNullOrWhiteSpace(tableExpr.Alias))
            result += " " + GetTableAlias(tableExpr.Alias);
          return result; 
        }

        public virtual string GetSubQueryAsAlias(string subquery, string alias)
        {
            return string.Format("({0}) {1}", subquery, GetTableAlias(alias));
        }

        /// <summary>
        /// Joins a list of table selection to make a FROM clause
        /// </summary>
        /// <param name="tables"></param>
        /// <returns></returns>
        public virtual SqlStatement GetFromClause(SqlStatement[] tables)
        {
            if (tables.Length == 0)
                return SqlStatement.Empty;
            return SqlStatement.Format("FROM {0}", SqlStatement.Join(", ", tables));
        }

        /// <summary>
        /// Concatenates all join clauses
        /// </summary>
        /// <param name="joins"></param>
        /// <returns></returns>
        public virtual SqlStatement GetJoinClauses(SqlStatement[] joins)
        {
            if (joins.Length == 0)
                return SqlStatement.Empty;
            var space = " ";
            return space + SqlStatement.Join(NewLine + space, joins);
        }

        /// <summary>
        /// Returns an INNER JOIN syntax
        /// </summary>
        /// <param name="joinedTable"></param>
        /// <param name="joinExpression"></param>
        /// <returns></returns>
        public virtual SqlStatement GetInnerJoinClause(SqlStatement joinedTable, SqlStatement joinExpression)
        {
            return SqlStatement.Format("INNER JOIN {0} ON {1}", joinedTable, joinExpression);
        }

        /// <summary>
        /// Returns a LEFT JOIN syntax
        /// </summary>
        /// <param name="joinedTable"></param>
        /// <param name="joinExpression"></param>
        /// <returns></returns>
        public virtual SqlStatement GetLeftOuterJoinClause(SqlStatement joinedTable, SqlStatement joinExpression)
        {
            return SqlStatement.Format("LEFT JOIN {0} ON {1}", joinedTable, joinExpression);
        }

        /// <summary>
        /// Returns a RIGHT JOIN syntax
        /// </summary>
        /// <param name="joinedTable"></param>
        /// <param name="joinExpression"></param>
        /// <returns></returns>
        public virtual SqlStatement GetRightOuterJoinClause(SqlStatement joinedTable, SqlStatement joinExpression)
        {
            return SqlStatement.Format("RIGHT JOIN {0} ON {1}", joinedTable, joinExpression);
        }

        /// <summary>
        /// Joins a list of conditions to make a WHERE clause
        /// </summary>
        /// <param name="wheres"></param>
        /// <returns></returns>
        public virtual SqlStatement GetWhereClause(SqlStatement[] wheres)
        {
            if (wheres.Length == 0)
                return SqlStatement.Empty;
            return SqlStatement.Format("WHERE ({0})", SqlStatement.Join(") AND (", wheres));
        }

        /// <summary>
        /// Joins a list of conditions to make a HAVING clause
        /// </summary>
        /// <param name="havings"></param>
        /// <returns></returns>
        public virtual SqlStatement GetHavingClause(SqlStatement[] havings)
        {
            if (havings.Length == 0)
                return SqlStatement.Empty;
            return SqlStatement.Format("HAVING {0}", SqlStatement.Join(" AND ", havings));
        }

        /// <summary>
        /// Joins a list of operands to make a SELECT clause
        /// </summary>
        /// <param name="selects"></param>
        /// <returns></returns>
        public virtual SqlStatement GetSelectClause(SqlStatement[] selects)
        {
            if (selects.Length == 0)
                return SqlStatement.Empty;
            var result = SqlStatement.Format("SELECT {0}", SqlStatement.Join(", ", selects));
            return result; 
        }

        /// <summary>
        /// Joins a list of operands to make a SELECT clause
        /// </summary>
        /// <param name="selects"></param>
        /// <returns></returns>
        public virtual SqlStatement GetSelectDistinctClause(SqlStatement[] selects)
        {
            if (selects.Length == 0)
                return SqlStatement.Empty;
            return SqlStatement.Format("SELECT DISTINCT {0}", SqlStatement.Join(", ", selects));
        }

        /// <summary>
        /// Returns all table columns (*)
        /// </summary>
        /// <returns></returns>
        public virtual string GetColumns()
        {
            return "*";
        }

        public virtual string GetParameter(ExternalValueExpression parameter) {
          return "{" + (parameter.LinqParameter.Index + 2) + "}";
        }

        public virtual string GetParameterName(string nameBase)
        {
          return Driver.DynamicSqlParameterPrefix + nameBase;
        }

        /// <summary>
        /// Returns a valid alias syntax for the given table
        /// </summary>
        /// <param name="nameBase"></param>
        /// <returns></returns>
        public virtual string GetTableAlias(string nameBase)
        {
            return string.Format("{0}$", nameBase);
        }

        /// <summary>
        /// Gets the literal add.
        /// </summary>
        /// <param name="a">A.</param>
        /// <param name="b">The b.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralAdd(SqlStatement a, SqlStatement b)
        {
            return SqlStatement.Format("{0} + {1}", a, b);
        }

        /// <summary>
        /// Gets the literal add checked.
        /// </summary>
        /// <param name="a">A.</param>
        /// <param name="b">The b.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralAddChecked(SqlStatement a, SqlStatement b)
        {
            return GetLiteralAdd(a, b);
        }

        /// <summary>
        /// Gets the literal and.
        /// </summary>
        /// <param name="a">A.</param>
        /// <param name="b">The b.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralAnd(SqlStatement a, SqlStatement b)
        {
          var strA = a.ToString(); 
          if (a.Count > 1) strA = "(" + strA + ")";
          var strB = string.Empty + b;
          if (b.Count > 1) strB = "(" + strB + ")";
          return strA + " AND " + strB;
        }

        /// <summary>
        /// Gets the literal and.
        /// </summary>
        /// <param name="a">A.</param>
        /// <param name="b">The b.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralAndBitwise(SqlStatement a, SqlStatement b) {
          var strA = a.ToString();
          if (a.Count > 1) strA = "(" + strA + ")";
          var strB = string.Empty + b;
          if (b.Count > 1) strB = "(" + strB + ")";
          return strA + " & " + strB;
        }

        /// <summary>
        /// Gets the literal and also.
        /// </summary>
        /// <param name="a">A.</param>
        /// <param name="b">The b.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralAndAlso(SqlStatement a, SqlStatement b)
        {
            return GetLiteralAnd(a, b);
        }

        /// <summary>
        /// Gets the length of the literal array.
        /// </summary>
        /// <param name="a">A.</param>
        /// <param name="b">The b.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralArrayLength(SqlStatement a, SqlStatement b)
        {
          Util.Throw("Arrays not supported in SQL: {0}/{1}", a, b);
          return null;
        }

        /// <summary>
        /// Gets the index of the literal array.
        /// </summary>
        /// <param name="a">A.</param>
        /// <param name="b">The b.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralArrayIndex(SqlStatement a, SqlStatement b)
        {
          Util.Throw("Arrays not supported in SQL: {0}/{1}", a, b);
          return null; 
        }

        /// <summary>
        /// Gets the literal call.
        /// </summary>
        /// <param name="a">A.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralCall(SqlStatement a)
        {
          Util.Throw("Function not supported in SQL: {0}", a);
          return null; 
        }

        /// <summary>
        /// Gets the literal coalesce.
        /// </summary>
        /// <param name="a">A.</param>
        /// <param name="b">The b.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralCoalesce(SqlStatement a, SqlStatement b)
        {
            return SqlStatement.Format("COALESCE({0}, {1})", a, b);
        }

        /// <summary>
        /// Gets the literal conditional.
        /// </summary>
        /// <param name="a">A.</param>
        /// <param name="b">The b.</param>
        /// <param name="c">The c.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralConditional(SqlStatement a, SqlStatement b, SqlStatement c)
        {
          Util.Throw("Conditional expressions not supported in SQL. Expression: {0} ? {1} : {2}", a, b, c);
          return null; 
        }

        /// <summary>
        /// Gets the literal convert.
        /// </summary>
        /// <param name="a">A.</param>
        /// <param name="newType">The new type.</param>
        /// <returns></returns>
        public virtual SqlStatement GetLiteralConvert(SqlStatement a, Type newType)
        {
            return a;
        }

        /// <summary>
        /// Gets the literal divide.
        /// </summary>
        /// <param name="a">A.</param>
        /// <param name="b">The b.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralDivide(SqlStatement a, SqlStatement b)
        {
            return SqlStatement.Format("{0} / {1}", a, b);
        }

        /// <summary>
        /// Gets the literal equal.
        /// </summary>
        /// <param name="a">A.</param>
        /// <param name="b">The b.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralEqual(SqlStatement a, SqlStatement b)
        {
            return SqlStatement.Format("{0} = {1}", a, b);
        }

        /// <summary>
        /// Gets the literal exclusive or.
        /// </summary>
        /// <param name="a">A.</param>
        /// <param name="b">The b.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralExclusiveOr(SqlStatement a, SqlStatement b)
        {
            return SqlStatement.Format("({0}) XOR ({1})", a, b);
        }
        protected virtual SqlStatement GetLiteralExclusiveOrBitwise(SqlStatement a, SqlStatement b) {
          return SqlStatement.Format("({0}) ^ ({1})", a, b);
        }

        /// <summary>
        /// Gets the literal greater than.
        /// </summary>
        /// <param name="a">A.</param>
        /// <param name="b">The b.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralGreaterThan(SqlStatement a, SqlStatement b)
        {
            return SqlStatement.Format("{0} > {1}", a, b);
        }

        /// <summary>
        /// Gets the literal greater than or equal.
        /// </summary>
        /// <param name="a">A.</param>
        /// <param name="b">The b.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralGreaterThanOrEqual(SqlStatement a, SqlStatement b)
        {
            return SqlStatement.Format("{0} >= {1}", a, b);
        }

        /// <summary>
        /// Gets the literal left shift.
        /// </summary>
        /// <param name="a">A.</param>
        /// <param name="b">The b.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralLeftShift(SqlStatement a, SqlStatement b)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the literal less than.
        /// </summary>
        /// <param name="a">A.</param>
        /// <param name="b">The b.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralLessThan(SqlStatement a, SqlStatement b)
        {
            return SqlStatement.Format("{0} < {1}", a, b);
        }

        /// <summary>
        /// Gets the literal less than or equal.
        /// </summary>
        /// <param name="a">A.</param>
        /// <param name="b">The b.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralLessThanOrEqual(SqlStatement a, SqlStatement b)
        {
            return SqlStatement.Format("{0} <= {1}", a, b);
        }

        /// <summary>
        /// Gets the literal modulo.
        /// </summary>
        /// <param name="a">A.</param>
        /// <param name="b">The b.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralModulo(SqlStatement a, SqlStatement b)
        {
            return SqlStatement.Format("{0} % {1}", a, b);
        }

        /// <summary>
        /// Gets the literal multiply.
        /// </summary>
        /// <param name="a">A.</param>
        /// <param name="b">The b.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralMultiply(SqlStatement a, SqlStatement b)
        {
            return SqlStatement.Format("{0} * {1}", a, b);
        }

        /// <summary>
        /// Gets the literal multiply checked.
        /// </summary>
        /// <param name="a">A.</param>
        /// <param name="b">The b.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralMultiplyChecked(SqlStatement a, SqlStatement b)
        {
            return GetLiteralMultiply(a, b);
        }

        /// <summary>
        /// Gets the literal negate.
        /// </summary>
        /// <param name="a">A.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralNegate(SqlStatement a)
        {
            return SqlStatement.Format("-{0}", a);
        }

        /// <summary>
        /// Gets the literal unary plus.
        /// </summary>
        /// <param name="a">A.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralUnaryPlus(SqlStatement a)
        {
            return SqlStatement.Format("+{0}", a);
        }

        /// <summary>
        /// Gets the literal negate checked.
        /// </summary>
        /// <param name="a">A.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralNegateChecked(SqlStatement a)
        {
            return GetLiteralNegate(a);
        }

        /// <summary>
        /// Gets the literal not.
        /// </summary>
        /// <param name="a">A.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralNot(SqlStatement a)
        {
            return SqlStatement.Format("NOT {0}", a);
        }

        /// <summary>
        /// Gets the literal not equal.
        /// </summary>
        /// <param name="a">A.</param>
        /// <param name="b">The b.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralNotEqual(SqlStatement a, SqlStatement b)
        {
            return SqlStatement.Format("{0} <> {1}", a, b);
        }

        /// <summary>
        /// Gets the literal or.
        /// </summary>
        /// <param name="a">A.</param>
        /// <param name="b">The b.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralOr(SqlStatement a, SqlStatement b)
        {
          var strA = a.ToString();
          if (a.Count > 1) strA = "(" + strA + ")";
          var strB = string.Empty + b;
          if (b.Count > 1) strB = "(" + strB + ")";
          return strA + " OR " + strB;
        }
        protected virtual SqlStatement GetLiteralOrBitwise(SqlStatement a, SqlStatement b) {
          var strA = a.ToString();
          if (a.Count > 1) strA = "(" + strA + ")";
          var strB = string.Empty + b;
          if (b.Count > 1) strB = "(" + strB + ")";
          return strA + " | " + strB;
        }

        /// <summary>
        /// Gets the literal or else.
        /// </summary>
        /// <param name="a">A.</param>
        /// <param name="b">The b.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralOrElse(SqlStatement a, SqlStatement b)
        {
            return GetLiteralOr(a, b);
        }

        /// <summary>
        /// Gets the literal power.
        /// </summary>
        /// <param name="a">A.</param>
        /// <param name="b">The b.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralPower(SqlStatement a, SqlStatement b)
        {
            return SqlStatement.Format("POWER ({0}, {1})", a, b);
        }

        /// <summary>
        /// Gets the literal right shift.
        /// </summary>
        /// <param name="a">A.</param>
        /// <param name="b">The b.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralRightShift(SqlStatement a, SqlStatement b)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the literal subtract.
        /// </summary>
        /// <param name="a">A.</param>
        /// <param name="b">The b.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralSubtract(SqlStatement a, SqlStatement b)
        {
            return SqlStatement.Format("{0} - {1}", a, b);
        }

        /// <summary>
        /// Gets the literal subtract checked.
        /// </summary>
        /// <param name="a">A.</param>
        /// <param name="b">The b.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralSubtractChecked(SqlStatement a, SqlStatement b)
        {
            return GetLiteralSubtract(a, b);
        }

        /// <summary>
        /// Gets the literal is null.
        /// </summary>
        /// <param name="a">A.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralIsNull(SqlStatement a)
        {
            return SqlStatement.Format("{0} IS NULL", a);
        }

        /// <summary>
        /// Gets the literal is not null.
        /// </summary>
        /// <param name="a">A.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralIsNotNull(SqlStatement a)
        {
            return SqlStatement.Format("{0} IS NOT NULL", a);
        }

        protected virtual SqlStatement GetEqualNullables(SqlStatement a, SqlStatement b) {
          return SqlStatement.Format("({0} = {1} OR ({0} IS NULL) AND ({1} IS NULL))", a, b);
        }
        /// <summary>
        /// Gets the literal string concat.
        /// </summary>
        /// <param name="a">A.</param>
        /// <param name="b">The b.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralStringConcat(SqlStatement a, SqlStatement b)
        {
            return SqlStatement.Format("CONCAT({0}, {1})", a, b);
        }

        /// <summary>
        /// Gets the length of the literal string.
        /// </summary>
        /// <param name="a">A.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralStringLength(SqlStatement a)
        {
            return SqlStatement.Format("CHARACTER_LENGTH({0})", a);
        }

        /// <summary>
        /// Gets the literal string to upper.
        /// </summary>
        /// <param name="a">A.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralStringToUpper(SqlStatement a)
        {
            return SqlStatement.Format("UCASE({0})", a);
        }

        /// <summary>
        /// Gets the literal string to lower.
        /// </summary>
        /// <param name="a">A.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralStringToLower(SqlStatement a)
        {
            return SqlStatement.Format("LCASE({0})", a);
        }


        /// <summary>
        /// Gets the literal trim.
        /// </summary>
        /// <param name="a">A.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralTrim(SqlStatement a)
        {
            return SqlStatement.Format("TRIM({0})", a);
        }

        /// <summary>
        /// Gets the literal L trim.
        /// </summary>
        /// <param name="a">A.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralLeftTrim(SqlStatement a)
        {
            return SqlStatement.Format("LTRIM({0})", a);
        }

        /// <summary>
        /// Gets the literal R trim.
        /// </summary>
        /// <param name="a">A.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralRightTrim(SqlStatement a)
        {
            return SqlStatement.Format("RTRIM({0})", a);
        }

        /// <summary>
        /// Gets the literal sub string.
        /// </summary>
        /// <param name="baseString">The base string.</param>
        /// <param name="startIndex">The start index.</param>
        /// <param name="count">The count.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralSubString(SqlStatement baseString, SqlStatement startIndex, SqlStatement count)
        {
            //in standard sql base SqlStatement index is 1 instead 0
            return SqlStatement.Format("SUBSTR({0}, {1}, {2})", baseString, startIndex, count);
        }

        /// <summary>
        /// Gets the literal sub string.
        /// </summary>
        /// <param name="baseString">The base string.</param>
        /// <param name="startIndex">The start index.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralSubString(SqlStatement baseString, SqlStatement startIndex)
        {
            //in standard sql base SqlStatement index is 1 instead 0
            return SqlStatement.Format("SUBSTR({0}, {1})", baseString, startIndex);
        }

        protected virtual SqlStatement GetLiteralLike(SqlStatement column, SqlStatement pattern, bool forceIgnoreCase)  {
          //Note: trying to figure out here whether to use ESCAPE clause or not does not work - the pattern might be a SQL parameter
          // The pattern is already escaped in calling code (escape call is part of translated LINQ expression).
          return SqlStatement.Format("{0} LIKE {1} ESCAPE '{2}'", column, pattern, Driver.DefaultLikeEscapeChar.ToString());
        }
        // default implementation: ignores case by default; Postgres overrides it
        protected virtual SqlStatement GetLiteralStringEqual(SqlStatement x, SqlStatement y, bool forceIgnoreCase) {
          return SqlStatement.Format("{0} = {1}", x, y);
        }

        /// <summary>
        /// Gets the literal count.
        /// </summary>
        /// <param name="a">A.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralCount(SqlStatement a)
        {
            return SqlStatement.Format("COUNT({0})", a);
        }

        /// <summary>
        /// Gets the literal min.
        /// </summary>
        /// <param name="a">A.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralMin(SqlStatement a)
        {
            return SqlStatement.Format("MIN({0})", a);
        }

        /// <summary>
        /// Gets the literal max.
        /// </summary>
        /// <param name="a">A.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralMax(SqlStatement a)
        {
            return SqlStatement.Format("MAX({0})", a);
        }

        /// <summary>
        /// Gets the literal sum.
        /// </summary>
        /// <param name="a">A.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralSum(SqlStatement a)
        {
            return SqlStatement.Format("SUM({0})", a);
        }

        /// <summary>
        /// Gets the literal average.
        /// </summary>
        /// <param name="a">A.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralAverage(SqlStatement a)
        {
            return SqlStatement.Format("AVG({0})", a);
        }

        protected virtual SqlStatement GetLiteralIn(SqlStatement a, SqlStatement b)
        {
            return SqlStatement.Format("{0} IN {1}", a, b);
        }

        protected virtual SqlStatement GetLiteralInArray(SqlStatement a, SqlStatement b) {
          return GetLiteralIn(a, b); 
        }


        /// <summary>
        /// Gets the null literal.
        /// </summary>
        /// <returns></returns>
        protected virtual SqlStatement GetNullLiteral()
        {
            return "NULL";
        }

        /// <summary>
        /// Returns a LIMIT clause around a SELECT clause, with offset
        /// </summary>
        /// <param name="select">SELECT clause</param>
        /// <param name="limit">limit value (number of columns to be returned)</param>
        /// <param name="offset">first row to be returned (starting from 0)</param>
        /// <param name="offsetAndLimit">limit+offset</param>
        /// <returns></returns>
        public virtual SqlStatement GetLiteralLimit(SqlStatement select, SqlStatement limit, SqlStatement offset, SqlStatement offsetAndLimit)
        {
            // default SQL syntax: LIMIT limit OFFSET offset
          var clause = string.Empty;
          if(limit != null)
            clause += " LIMIT " + limit;
          if(offset != null)
            clause += " OFFSET " + offset;
          return select + clause; 
        }

        //We need to escape single quotes (usual thing);
        // and we need to escape braces { and } - because SQL is initially formed with place holders for parameters (ex: {2} for @P2)
        // Literal strings might contain substrings that look like place holders; so we need to escape the braces and replace 
        // them with { -> {0}, }-> {1} placeholders; these placeholders will be reverted them back to braces in final SQL formatting
        // (recommended by string.Format doc in msdn)
        static char[] _specialLiteralChars = new char[] { '\'', '{', '}' };
        protected virtual string GetLiteral(string str) {
          if (string.IsNullOrWhiteSpace(str))
            return "''";
          var result = str;
          if (result.IndexOfAny(_specialLiteralChars) >= 0)
            result = new string(EscapeBracesAndQuotes(result).ToArray());
          return "'" + result + "'";   
        }

        private IEnumerable<char> EscapeBracesAndQuotes(string str) {
          const char quote = '\'';
          foreach(var ch in str) {
            switch(ch) {
              case '{': 
                yield return '{'; 
                yield return '0'; 
                yield return '}';
                break; 
              case '}': 
                yield return '{'; 
                yield return '1'; 
                yield return '}';
                break; 
              case quote: 
                yield return quote; 
                yield return quote; 
                break; 
              default: 
                yield return ch;
                break; 
            }
          }          
        }

        /// <summary>
        /// Gets the literal array.
        /// </summary>
        /// <param name="list">The array.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralList(IList list)
        {
            // RI: HACK - using NULL for empty list 
            // Lists are used in IN clause; if list is empty, the query fails ' x IN ()' is invalid in MS SQL. But placing NULL works. 
            if (list.Count == 0)
              return "(NULL)";
            var listItems = new List<SqlStatement>();
            foreach (object o in list)
                listItems.Add(GetLiteral(o));
            return SqlStatement.Format("({0})", SqlStatement.Join(", ", listItems.ToArray()));
        }

        /// <summary>
        /// Returns an ORDER criterium
        /// </summary>
        /// <param name="expression"></param>
        /// <param name="descending"></param>
        /// <returns></returns>
        public virtual SqlStatement GetOrderByColumn(SqlStatement expression, bool descending)
        {
            if (!descending)
                return expression;
            return SqlStatement.Format("{0} DESC", expression);
        }

        /// <summary>
        /// Joins a list of conditions to make a ORDER BY clause
        /// </summary>
        /// <param name="orderBy"></param>
        /// <returns></returns>
        public virtual SqlStatement GetOrderByClause(SqlStatement[] orderBy)
        {
          if(orderBy == null || orderBy.Length == 0)
            return SqlStatement.Empty;
          return SqlStatement.Format("ORDER BY {0}", SqlStatement.Join(", ", orderBy));
        }

        public virtual SqlStatement GetFakeOrderByClause() {
          return "ORDER BY (SELECT 1)"; 
        }

        /// <summary>
        /// Joins a list of conditions to make a GROUP BY clause
        /// </summary>
        /// <param name="groupBy"></param>
        /// <returns></returns>
        public virtual SqlStatement GetGroupByClause(SqlStatement[] groupBy)
        {
            if (groupBy.Length == 0)
                return SqlStatement.Empty;
            return SqlStatement.Format("GROUP BY {0}", SqlStatement.Join(", ", groupBy));
        }

        /// <summary>
        /// Gets the literal union.
        /// </summary>
        /// <param name="selectA">The select A.</param>
        /// <param name="selectB">The select B.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralUnion(SqlStatement selectA, SqlStatement selectB)
        {
            return SqlStatement.Format("{0}{2}UNION{2}{1}", selectA, selectB, NewLine);
        }

        /// <summary>
        /// Gets the literal union all.
        /// </summary>
        /// <param name="selectA">The select A.</param>
        /// <param name="selectB">The select B.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralUnionAll(SqlStatement selectA, SqlStatement selectB)
        {
            return SqlStatement.Format("{0}{2}UNION ALL{2}{1}", selectA, selectB, NewLine);
        }

        /// <summary>
        /// Gets the literal intersect.
        /// </summary>
        /// <param name="selectA">The select A.</param>
        /// <param name="selectB">The select B.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralIntersect(SqlStatement selectA, SqlStatement selectB)
        {
            return SqlStatement.Format("{0}{2}INTERSECT{2}{1}", selectA, selectB, NewLine);
        }

        /// <summary>
        /// Gets the literal except.
        /// </summary>
        /// <param name="selectA">The select A.</param>
        /// <param name="selectB">The select B.</param>
        /// <returns></returns>
        protected virtual SqlStatement GetLiteralExcept(SqlStatement selectA, SqlStatement selectB)
        {
            return SqlStatement.Format("{0}{2}EXCEPT{2}{1}", selectA, selectB, NewLine);
        }

        /// <summary>
        /// given 'User', return '[User]' to prevent a SQL keyword conflict
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public virtual string GetSafeName(string name)
        {
            string[] nameParts = name.SplitNames('.');
            for (int index = 0; index < nameParts.Length; index++)
            {
                nameParts[index] = GetSafeNamePart(nameParts[index]);
            }
            return string.Join(".", nameParts);
        }

        /// <summary>
        /// Gets the safe name part.
        /// </summary>
        /// <param name="namePart">The name part.</param>
        /// <returns></returns>
        protected virtual string GetSafeNamePart(string namePart)
        {
            return IsMadeSafe(namePart) ? namePart : MakeNameSafe(namePart);
        }

        /// <summary>
        /// Determines whether [is made safe] [the specified name part].
        /// </summary>
        /// <param name="namePart">The name part.</param>
        /// <returns>
        ///     <c>true</c> if [is made safe] [the specified name part]; otherwise, <c>false</c>.
        /// </returns>
        protected virtual bool IsMadeSafe(string namePart)
        {
            var l = namePart.Length;
            if (l < 2)
                return false;
            return namePart[0] == SafeNameStartQuote && namePart[l - 1] == SafeNameEndQuote;
        }

        public virtual void SetDbParameterValue(IDbDataParameter parameter, Type type, object value) {
          parameter.Value = value;           
        }
        /// <summary>
        /// Makes the name safe.
        /// </summary>
        /// <param name="namePart">The name part.</param>
        /// <returns></returns>
        protected virtual string MakeNameSafe(string namePart)
        {
            return namePart.Enquote(SafeNameStartQuote, SafeNameEndQuote);
        }

        private static readonly Regex _fieldIdentifierEx = new Regex(@"\[(?<var>[\w.]+)\]",
                                                                     RegexOptions.Singleline |
                                                                     RegexOptions.ExplicitCapture |
                                                                     RegexOptions.Compiled);

        public virtual string GetSafeQuery(string sqlString)
        {
            if (sqlString == null)
                return null;
            return _fieldIdentifierEx.Replace(sqlString, delegate(Match e)
            {
                var field = e.Groups[1].Value;
                var safeField = GetSafeNamePart(field);
                return safeField;
            });
        }


        public virtual Type GetSqlFunctionResultType(SqlFunctionType functionType, Type[] operandTypes) {
          Type defaultType = null;
          //RI: changed to use op[1] (from 0) here - this is selector
          if(operandTypes != null && operandTypes.Length > 0)
            defaultType = operandTypes[operandTypes.Length - 1];
          // TODO: see if this is necessary at all, maybe just return output type
          switch(functionType) 
          {
            case SqlFunctionType.IsNull:
            case SqlFunctionType.IsNotNull:
            case SqlFunctionType.EqualNullables:
            case SqlFunctionType.StringEqual:
              return typeof(bool);
            case SqlFunctionType.Concat:
              return typeof(string);
            case SqlFunctionType.Count:
              return typeof(int);
            case SqlFunctionType.Exists:
              return typeof(bool);
            case SqlFunctionType.Like:
              return typeof(bool);
            case SqlFunctionType.Min:
            case SqlFunctionType.Max:
            case SqlFunctionType.Sum:
              return defaultType; // for such methods, the type is related to the operands type
            case SqlFunctionType.Average:
              if(defaultType == typeof(decimal) || defaultType == typeof(float))
                return defaultType;
              return typeof(double);
            case SqlFunctionType.StringLength:
              return typeof(int);
            case SqlFunctionType.ToUpper:
            case SqlFunctionType.ToLower:
              return typeof(string);
            case SqlFunctionType.In:
            case SqlFunctionType.InArray:
              return typeof(bool);
            case SqlFunctionType.Substring:
              return defaultType;
            case SqlFunctionType.Trim:
            case SqlFunctionType.LTrim:
            case SqlFunctionType.RTrim:
              return typeof(string);
            case SqlFunctionType.StringInsert:
              return typeof(string);
            case SqlFunctionType.Replace:
              return typeof(string);
            case SqlFunctionType.Remove:
              return typeof(string);
            case SqlFunctionType.IndexOf:
              return typeof(int);
            case SqlFunctionType.Year:
            case SqlFunctionType.Month:
            case SqlFunctionType.Day:
            case SqlFunctionType.Hour:
            case SqlFunctionType.Second:
            case SqlFunctionType.Minute:
            case SqlFunctionType.Millisecond:
            case SqlFunctionType.Week:
              return typeof(int);
            case SqlFunctionType.Now:
            case SqlFunctionType.Date:
              return typeof(DateTime);
            case SqlFunctionType.Time:
              return typeof(TimeSpan); 
            case SqlFunctionType.DateDiffInMilliseconds:
              return typeof(long);
            case SqlFunctionType.Abs:
            case SqlFunctionType.Exp:
            case SqlFunctionType.Floor:
            case SqlFunctionType.Ln:
            case SqlFunctionType.Log:
            case SqlFunctionType.Pow:
            case SqlFunctionType.Round:
            case SqlFunctionType.Sign:
            case SqlFunctionType.Sqrt:
              return defaultType;
            case SqlFunctionType.AndBitwise:
            case SqlFunctionType.OrBitwise:
            case SqlFunctionType.XorBitwise:
              return defaultType;
            case SqlFunctionType.ConvertBoolToBit:
              return typeof(bool); 

            default:
              Util.Throw("S0058: Unknown SpecialExpressionType value {0}", functionType);
              return null;
          }
        }

        public virtual bool IsSqlTier(Expression expression, Vita.Entities.Linq.LinqCommandKind kind) {
          var sqlExpr = expression as SqlExpression;
          if (sqlExpr != null) {
            switch(sqlExpr.SqlNodeType) {
              case SqlExpressionType.Select: case SqlExpressionType.Column: case SqlExpressionType.Table:
              case SqlExpressionType.ExternalValue: case SqlExpressionType.SqlFunction:
                return true; 
              case SqlExpressionType.Group: case SqlExpressionType.MetaTable: 
                return false;
              default: return true; 
             }
          }
          switch (expression.NodeType) {
            case ExpressionType.ArrayLength:
            case ExpressionType.ArrayIndex:
            case ExpressionType.Call:
            case ExpressionType.Convert:
            case ExpressionType.ConvertChecked:
            case ExpressionType.Invoke:
            case ExpressionType.Lambda:
            case ExpressionType.ListInit:
            case ExpressionType.MemberAccess:
            case ExpressionType.MemberInit:
            case ExpressionType.New:
            case ExpressionType.NewArrayInit:
            case ExpressionType.NewArrayBounds:
            case ExpressionType.Parameter:
            case ExpressionType.SubtractChecked:
            case ExpressionType.TypeAs:
            case ExpressionType.TypeIs:
              return false; 
            default:
              return true; 
          }
        }



    }//class
}
