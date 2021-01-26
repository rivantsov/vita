using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vita.Entities;
using Vita.Entities.Utilities;

namespace BookStore {
  public static class SearchExtensions {

    public static SearchResults<IBook> SearchBooks(this IEntitySession session, BookSearch searchParams) {
      // Warning about substring match (LIKE): Be careful using it in real apps, against big tables
      // Match by fragment results in LIKE operator which NEVER works on real volumes.
      // For MS SQL, it is OK to do LIKE with pattern that does not start with % (so it is StartsWith(smth) operator).
      //  AND column must be indexed - so server will use index. For match inside the string, LIKE is useless on big tables.
      // In our case, Title is indexed and we use StartsWith, so it's OK
      // An interesting article about speeding up string-match search in MS SQL:
      //  http://aboutsqlserver.com/2015/01/20/optimizing-substring-search-performance-in-sql-server/
      var categories = ConvertHelper.ParseEnumArray<BookCategory>(searchParams.Categories);
      var where = session.NewPredicate<IBook>()
        .AndIfNotEmpty(searchParams.Title, b => b.Title.StartsWith(searchParams.Title))
        .AndIfNotEmpty(searchParams.MaxPrice, b => b.Price <= (Decimal)searchParams.MaxPrice.Value)
        .AndIfNotEmpty(searchParams.Publisher, b => b.Publisher.Name.StartsWith(searchParams.Publisher))
        .AndIfNotEmpty(searchParams.PublishedAfter, b => b.PublishedOn.Value >= searchParams.PublishedAfter.Value)
        .AndIfNotEmpty(searchParams.PublishedBefore, b => b.PublishedOn.Value <= searchParams.PublishedBefore.Value)
        .AndIf(categories != null && categories.Length > 0, b => categories.Contains(b.Category));
      // A bit more complex clause for Author - it is many2many, results in subquery
      if (!string.IsNullOrEmpty(searchParams.AuthorLastName)) {
        var qAuthBookIds = session.EntitySet<IBookAuthor>()
          .Where(ba => ba.Author.LastName.StartsWith(searchParams.AuthorLastName))
          .Select(ba => ba.Book.Id);
        where = where.And(b => qAuthBookIds.Contains(b.Id));
      }
      // Alternative method for author name - results in inefficient query (with subquery for every row)
      //      if(!string.IsNullOrEmpty(authorLastName))
      //         where = where.And(b => b.Authors.Any(a => a.LastName == authorLastName));

      //Use VITA-defined helper method ExecuteSearch - to build query from where predicate, get total count,
      // add clauses for OrderBy, Take, Skip, run query and convert to list of model objects with TotalCount

      var results = session.ExecuteSearch(where, searchParams, include: b => b.Publisher,  nameMapping: _orderByMapping);
      return results;
    }

    // Mapping names in BookSearch.OrderBy; the following mapping allows us to use 'pubname', which will be translated 
    // into "order by book.Publisher.Name"
    // This mapping facility allows us use more friendly names in UI code when forming search query, without thinking 
    // about exact relations between entities and property names
    static Dictionary<string, string> _orderByMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
        {"pubname" , "Publisher.Name"}
    };



  }
}
