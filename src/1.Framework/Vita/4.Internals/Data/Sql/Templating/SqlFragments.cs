using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using Vita.Data.Linq;

namespace Vita.Data.Sql {

  public interface IFlatSqlFragment {
    void AddFormatted(IList<string> strings, IList<string> placeHolderArgs);
  }

  public abstract class SqlFragment {

    public int Precedence;

    public SqlFragment (int precedence = SqlPrecedence.NoPrecedence) {
      Precedence = precedence; 
    }

    public abstract void Flatten(IList<IFlatSqlFragment> flatList, ISqlPrecedenceHandler precedenceHandler);

    public static SqlFragment CreateList(TextSqlFragment delimiter, IList<SqlFragment> fragments) {
      switch(fragments.Count) {
        case 0:
          return null;
        case 1:
          return fragments[0];
      }
      var newList = new List<SqlFragment>();
      foreach(var fr in fragments) {
        if(newList.Count > 0)
          newList.Add(delimiter);
        newList.Add(fr);
      }
      return new CompositeSqlFragment(newList);
    }

    public string GetSql() {
      var flatList = new List<IFlatSqlFragment>();
      Flatten(flatList, null);
      return string.Join(string.Empty, flatList); 

    }
  } //class

  public class TextSqlFragment : SqlFragment, IFlatSqlFragment {
    public string Text;

    public TextSqlFragment(string value, int precedence = SqlPrecedence.NoPrecedence) : base(precedence)  {
      Text = value;
    }

    public override string ToString() {
      return Text;
    }

    public override void Flatten(IList<IFlatSqlFragment> flatList, ISqlPrecedenceHandler precedenceHandler) {
      flatList.Add(this);
    }

    public void AddFormatted(IList<string> strings, IList<string> placeHolderArgs) {
      strings.Add(Text);
    }
  }

  public class CompositeSqlFragment: SqlFragment {

    public IList<SqlFragment> Fragments = new List<SqlFragment>(); 
    public CompositeSqlFragment(int precedence, IList<SqlFragment> fragments): base(precedence) {
      Fragments = fragments;
      CheckInheritedPrecedence();
    }

    public CompositeSqlFragment(params SqlFragment[] fragments) {
      Fragments = fragments;
      CheckInheritedPrecedence(); 
    }
    private void CheckInheritedPrecedence() {
      if(Fragments.Count == 1 && this.Precedence == SqlPrecedence.NoPrecedence)
        this.Precedence = Fragments[0].Precedence;
    }

    public CompositeSqlFragment(IList<SqlFragment> fragments) {
      this.Fragments = fragments; 
    }//method

    public override void Flatten(IList<IFlatSqlFragment> flatList, ISqlPrecedenceHandler precedenceHandler) {
      if (precedenceHandler == null) {
        foreach(var fr in Fragments)
          fr?.Flatten(flatList, null);
        return; 
      }
      for(int i = 0; i < Fragments.Count; i++) {
        var fr = Fragments[i];
        if(fr == null)
          continue; 
        if (precedenceHandler.NeedsParenthesis(this, fr, isFirst: i==0)) {
          flatList.Add(SqlTerms.LeftParenthesis);
          fr.Flatten(flatList, precedenceHandler);
          flatList.Add(SqlTerms.RightParenthesis);
        } else 
          fr.Flatten(flatList, precedenceHandler);
      }
    }//method

    public override string ToString() {
      return string.Join(" ", Fragments);
    }

    public static CompositeSqlFragment Parenthesize(SqlFragment fragment) {
      return new CompositeSqlFragment(SqlTerms.LeftParenthesis, fragment, SqlTerms.RightParenthesis); 
    }
  }//class

   
}
