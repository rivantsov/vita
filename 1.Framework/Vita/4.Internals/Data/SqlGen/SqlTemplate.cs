using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

using Vita.Entities;
using Vita.Data.SqlGen;

namespace Vita.Data.SqlGen {

  public class SqlTemplate {
    public int Precedence;
    public IList<SqlFragment> Fragments; //also contains placeholders 
    public IList<SqlPlaceHolder> PlaceHolders; // registered place holders

    public SqlTemplate(IList<SqlFragment> fragments = null, IList<SqlPlaceHolder> placeHolders = null) {
      Fragments = fragments ?? new List<SqlFragment>();
      PlaceHolders = placeHolders ?? new List<SqlPlaceHolder>(); 
    }

    public SqlTemplate (string template, int precedence = SqlPrecedence.NoPrecedence) : this() {
      Util.CheckParam(template, nameof(template));
      Precedence = precedence; 
      Parse(template); 
    } //constructor

    public override string ToString() {
      return string.Join(string.Empty, Fragments);
    }

    public IList<IFlatSqlFragment> Flatten(ISqlPrecedenceHandler handler = null) {
      var flatList = new List<IFlatSqlFragment>();
      foreach(var fr in Fragments)
        fr.Flatten(flatList, handler);
      return flatList; 
    }

    public void WriteTo(IList<string> strings, IList<string> placeHolderArgs, ISqlPrecedenceHandler handler = null) {
      var flatList = Flatten(handler); 
      foreach (var fl in flatList)
        fl.AddFormatted(strings, placeHolderArgs);
    }

    private void Parse(string template) {
      var currPos = 0; 
      while(currPos < template.Length) {
        if (template[currPos] == '{') {
          var closeBrPos = template.IndexOf('}', currPos);
          Util.Check(closeBrPos > 0, "Invalid SQL template, unclosed open brace at {0}; template: '{1}'", currPos, template);
          var strIndex = template.Substring(currPos + 1, closeBrPos - currPos - 1);
          Util.Check(int.TryParse(strIndex, out int index), "Invalid SQL template, invalid place holder at {0}, template: {1}", currPos, template);
          var ph = GetCreatePlaceHolderAt(index, template); 
          Fragments.Add(ph);
          currPos = closeBrPos + 1; 
        } else {
          var brPos = template.IndexOf("{", currPos);
          if(brPos < 0)
            brPos = template.Length; //beyond the last one
          var text = template.Substring(currPos, brPos - currPos);
          Fragments.Add(new TextSqlFragment(text));
          currPos = brPos; 
        }
      }//while
    }

    private SqlPlaceHolder GetCreatePlaceHolderAt(int index, string template) {
      Util.Check(index >= 0 && index < 20, "Invalid SQL placeholder ({0}), must be between 0 and 20; template: '{1}'.", index, template);
      if (index < PlaceHolders.Count)
        return PlaceHolders[index];
      // create missing
      while (PlaceHolders.Count - 1 < index)
        PlaceHolders.Add(new SqlPlaceHolder() { Index = PlaceHolders.Count });
      return PlaceHolders[PlaceHolders.Count - 1]; // return last one
    }

    public SqlFragment Format(params SqlFragment[] args) {
      Util.Check(args.Length == PlaceHolders.Count, 
        "Invalid SQL template formatting call, args count mismatch ({0}), expected: {1}, template: {2}.", 
        args.Length, PlaceHolders.Count, this);
      // doing for-loop (instead of foreach/Linq) for efficiency
      var parts = new SqlFragment[Fragments.Count];
      for(int i = 0; i < Fragments.Count; i++) {
        var ph = Fragments[i] as SqlPlaceHolder;
        if (ph == null)
          parts[i] = Fragments[i];
        else
          parts[i] = args[ph.Index];
      }
      return new CompositeSqlFragment(this.Precedence, parts);
    }

    /*
    /// <summary>Prepares SQL statement for reuse. Merges all consequtive non-parameter fragments into a single text fragment.
    /// </summary>
    public void Compact() {
      var newList = new List<IFlatSqlFragment>();
      IList<IFlatSqlFragment> accumList = new List<IFlatSqlFragment>();
      foreach(var fr in Fragments) {
        if(fr.IsParameter) {
          switch(accumList.Count) {
            case 0:
              break;
            case 1:
              newList.Add(accumList[0]);
              accumList.Clear();
              break;
            default:
              var text = string.Join(string.Empty, accumList.Select(f => f.GetSql()));
              var newFragm = new TextSqlFragment(text);
              newList.Add(newFragm);
              accumList.Clear();
              break;
          } //switch
        } else
          accumList.Add(fr);
      } //foreach
    }
    */

  }
}
