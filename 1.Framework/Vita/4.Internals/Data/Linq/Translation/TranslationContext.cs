
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

using Vita.Entities;
using Vita.Data.Model;
using Vita.Data.Linq.Translation.Expressions;
using Vita.Entities.Model;
using Vita.Data.Linq;
using Vita.Entities.Runtime;

namespace Vita.Data.Linq.Translation {

  internal class TranslationContext {

    public DbModel DbModel;
    public EntityCommand Command;
    public QueryOptions QueryOptions { get { return Command.Info.Options; } } 

    public Stack<MethodInfo> CallStack { get; private set; }

    /// <summary>Values coming from the code executing the query - parameters. Top-level parameters for the entire query.</summary>
    public readonly IList<ExternalValueExpression> ExternalValues;

    // Build context: values here are related to current context, and can change with it
    private int _currentScopeIndex;
    public SelectExpression CurrentSelect {
      get { return SelectExpressions[_currentScopeIndex]; }
      set { SelectExpressions[_currentScopeIndex] = value; }
    }
    public IList<SelectExpression> SelectExpressions { get; private set; }
    public IList<MetaTableExpression> MetaTables { get; private set; }
    public IDictionary<string, Expression> LambdaParameters { get; private set; }

    public TranslationContext(DbModel dbModel, EntityCommand command) {
      DbModel = dbModel;
      Command = command;
      CallStack = new Stack<MethodInfo>();
      SelectExpressions = new List<SelectExpression>();
      _currentScopeIndex = SelectExpressions.Count;
      SelectExpressions.Add(new SelectExpression(command.Info));
      ExternalValues = new List<ExternalValueExpression>();
      MetaTables = new List<MetaTableExpression>();
      LambdaParameters = new Dictionary<string, Expression>();
    }

    public TranslationContext(TranslationContext source) {
      this.DbModel = source.DbModel;
      this.Command = source.Command;
      this.CallStack = source.CallStack;
      this.ExternalValues = source.ExternalValues;
      this.MetaTables = source.MetaTables;
      this.SelectExpressions = source.SelectExpressions;
      this.LambdaParameters = source.LambdaParameters;
      this._currentScopeIndex = source._currentScopeIndex;
    }



    /// <summary>
    /// Helper to enumerate all registered tables
    /// </summary>
    /// <returns></returns>
    public IEnumerable<TableExpression> EnumerateAllTables() {
      foreach (var scopePiece in SelectExpressions) {
        foreach (var table in scopePiece.Tables)
          yield return table;
      }
    }

    /// <summary>
    /// Helper to enumerate all registered columns
    /// </summary>
    /// <returns></returns>
    public IEnumerable<TableExpression> EnumerateScopeTables() {
      for (SelectExpression currentSelect = CurrentSelect; currentSelect != null; currentSelect = currentSelect.Parent) {
        foreach (var table in currentSelect.Tables)
          yield return table;
      }
    }

    /// <summary>
    /// Helper to enumerate all registered columns
    /// </summary>
    /// <returns></returns>
    public IEnumerable<ColumnExpression> EnumerateScopeColumns() {
      for (SelectExpression currentSelect = CurrentSelect; currentSelect != null; currentSelect = currentSelect.Parent) {
        foreach (var column in currentSelect.Columns)
          yield return column;
      }
    }


    /// <summary>Creates a new rewriterContext where parameters have a local scope.</summary>
    public TranslationContext NewQuote() {
      var rewriterContext = new TranslationContext(this);
      // scope dependent Parts
      rewriterContext.LambdaParameters = new Dictionary<string, Expression>(LambdaParameters);
      return rewriterContext;
    }

    /// <summary>Creates a new rewriterContext with a new query scope. </summary>
    public TranslationContext NewSelect() {
      var rewriterContext = new TranslationContext(this);
      // scope dependent Parts
      rewriterContext._currentScopeIndex = SelectExpressions.Count;
      SelectExpressions.Add(new SelectExpression(CurrentSelect, CurrentSelect.QueryInfo));
      return rewriterContext;
    }

    /// <summary>Creates a new rewriterContext with a new query scope with the same parent of the CurrentSelect. </summary>
    public TranslationContext NewSisterSelect() {
      var rewriterContext = new TranslationContext(this);
      rewriterContext._currentScopeIndex = SelectExpressions.Count;
      SelectExpressions.Add(new SelectExpression(CurrentSelect.Parent, CurrentSelect.QueryInfo));
      return rewriterContext;
    }

    public void NewParentSelect() {
      SelectExpression currentSelect = this.CurrentSelect;
      SelectExpression newParentSelect = new SelectExpression(currentSelect.Parent, CurrentSelect.QueryInfo);
      currentSelect.Parent = newParentSelect;
      this._currentScopeIndex = SelectExpressions.Count;
      SelectExpressions.Add(newParentSelect);
    }


    public bool IsExternalInExpressionChain { get; set; }
  }
}
