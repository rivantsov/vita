using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

using Vita.Entities;
using Vita.Data.SqlGen;

namespace Vita.Data.SqlGen {

  public class SqlTemplate {
    public int Precedence;
    public IList<SqlFragment> Fragments = new List<SqlFragment>(); //also contains placeholders 
    public SqlPlaceHolderList PlaceHolders = new SqlPlaceHolderList(); 

    public SqlTemplate(string template, int precedence = SqlPrecedence.NoPrecedence) {
      Precedence = precedence;
      Parse(template); 
    }

    public SqlTemplate (int precedence) {
      Precedence = precedence; 
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

    public void Parse(string template) {
      Util.CheckParam(template, nameof(template));
      var currPos = 0; 
      while(currPos < template.Length) {
        if (template[currPos] == '{') {
          var closeBrPos = template.IndexOf('}', currPos);
          Util.Check(closeBrPos > 0, "Invalid SQL template, unclosed open brace at {0}; template: '{1}'", currPos, template);
          var strIndex = template.Substring(currPos + 1, closeBrPos - currPos - 1);
          Util.Check(int.TryParse(strIndex, out int index), 
            "Invalid SQL template, invalid place holder at {0}, template: {1}", currPos, template);
          var ph = GetCreatePlaceHolder(index);
          this.Fragments.Add(ph);
          currPos = closeBrPos + 1; 
        } else {
          var brPos = template.IndexOf("{", currPos);
          if(brPos < 0)
            brPos = template.Length; //beyond the last one
          var text = template.Substring(currPos, brPos - currPos);
          this.Fragments.Add(new TextSqlFragment(text));
          currPos = brPos; 
        }
      }//while
    }

    private SqlPlaceHolder GetCreatePlaceHolder(int index) {
      while(index > this.PlaceHolders.Count - 1) {
        var ph = new SqlPlaceHolder(this.PlaceHolders.Count);
        this.PlaceHolders.Add(ph);
      }
      return PlaceHolders[index];
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

  }
}
