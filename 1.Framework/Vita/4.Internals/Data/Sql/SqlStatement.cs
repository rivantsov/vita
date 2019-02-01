using System;
using System.Collections.Generic;
using System.Linq;

using Vita.Entities;
using Vita.Data.Linq;
using Vita.Data.Runtime;
using Vita.Entities.Runtime;

namespace Vita.Data.Sql {

  public enum SqlKind {
    LinqSelect,
    LinqInsert,
    LinqUpdate,
    LinqDelete,
    InsertOne,
    InsertMany,
    UpdateOne,
    DeleteOne,
    DeleteMany,
    Other,
  }


  public class SqlStatement {
    public readonly SqlKind Kind; 
    public List<IFlatSqlFragment> Fragments = new List<IFlatSqlFragment>();
    public readonly SqlPlaceHolderList PlaceHolders = new SqlPlaceHolderList();
    public readonly QueryOptions Options;
    internal bool IsCompacted;

    public DbExecutionType ExecutionType;
    public IDataCommandResultProcessor ResultProcessor;

    public SqlStatement(SqlKind kind, SqlFragment sql, SqlPlaceHolderList placeHolders, DbExecutionType executionType,
            SqlPrecedenceHandler precedenceHandler = null, QueryOptions options = QueryOptions.None) {
      Kind = kind;
      ExecutionType = executionType; 
      Options = options;
      Append(sql, placeHolders, precedenceHandler);
      if(placeHolders == null)
        DiscoverPlaceholders(); 
    }

    public void Append(SqlFragment sql, SqlPlaceHolderList placeHolders = null, SqlPrecedenceHandler precedenceHandler = null) {
      Fragments.Add(SqlTerms.NewLine);
      sql.Flatten(Fragments, precedenceHandler);
      if (placeHolders != null && placeHolders.Count > 0) {
        PlaceHolders.AddRange(placeHolders);
        ReIndexPlaceHolders();
      }
    }

    public void WriteSql(IList<string> strings, IList<string> placeHolderArgs) {
      foreach (var f in this.Fragments)
        f.AddFormatted(strings, placeHolderArgs);
    }

    public void DiscoverPlaceholders() {
      PlaceHolders.Clear(); 
      foreach (var fr in Fragments) {
        var ph = fr as SqlPlaceHolder;
        if (ph != null && !PlaceHolders.Contains(ph)) {
          ph.Index = PlaceHolders.Count; 
          PlaceHolders.Add(ph);
        }
      }
    }

    public void ReIndexPlaceHolders() {
      for (int i = 0; i < PlaceHolders.Count; i++)
        PlaceHolders[i].Index = i;
    }

    /// <summary>Compacts SQL statement - merges all consequtive non-placeholder fragments into a single text fragment.
    /// Compacting is done when SQL is first time re-used from Query cache. (not when it placed there, but first time it is retrieved for reuse.
    /// </summary>
    public void Compact() {
      // we do not use locking, the entire Fragments list is replaced, so no racing; only very rarely duplicate work
      if (IsCompacted)
        return; 
      var newList = new List<IFlatSqlFragment>();
      IList<IFlatSqlFragment> textFragments = new List<IFlatSqlFragment>();
      foreach (var fr in Fragments) {
        if (fr is SqlPlaceHolder) {
          AppendAsOne(newList, textFragments);
          textFragments.Clear();
          newList.Add(fr);
        } else 
          textFragments.Add(fr);
      } //foreach
      if (textFragments.Count > 0)
        AppendAsOne(newList, textFragments); 
      Fragments = newList;
      IsCompacted = true; //race condition is not important here; 
    }

    private void AppendAsOne(IList<IFlatSqlFragment> toList, IList<IFlatSqlFragment> tail) {
      switch (tail.Count) {
        case 0: return;
        case 1:
          toList.Add(tail[0]);
          return;
        default:
          var strings = new List<string>();
          foreach(var f in tail)
            f.AddFormatted(strings, null);
          var text = string.Join(string.Empty, strings);
          toList.Add(new TextSqlFragment(text));
          return;
      } //switch tail.Count
    }


    public void TrimEndingSemicolon() {
      var last = this.Fragments[this.Fragments.Count - 1]; 
      var lastText = last as TextSqlFragment;
      Util.Check(lastText?.Text == ";", "Failed to trim ending semicolon, last fragment ({0}) is not semicolon.", last);
      this.Fragments.RemoveAt(this.Fragments.Count - 1);
    }

    public override string ToString() {
      return string.Join(string.Empty, this.Fragments); 
    }
  }//class
}
