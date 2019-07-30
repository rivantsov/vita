using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Vita.Entities;
using Vita.Entities.MetaD1;
using Vita.Entities.Model;
using Vita.Samples.BookStore;

namespace Vita.Testing.ExtendedTests {

  [TestClass]
  public class Md1Test {

    [TestInitialize]
    public void TestInit() {
      Startup.InitApp();
    }

    [TestCleanup]
    public void TearDown() {
      Startup.TearDown();
    }

    //[TestMethod]
    public void _TestMd1EntityViews() {
      var app = Startup.BooksApp;
      var entBol = GetEntity<IBookOrderLine>();
      var entBk = GetEntity<IBook>();
      var entPub = GetEntity<IPublisher>();

      // Create EntityView: books joined with publishers
      var viewBooks = new EntityView(entBk);
      var partBooks = viewBooks.GetRoot(); 
      var mBkPub = partBooks.GetCreateMember("Publisher");
      var partPub = viewBooks.AddJoin(entPub, mBkPub);
      var mTitle = partBooks.GetCreateMember("Title");

      // Add editor param
      var categoryParam = new ViewParam() { Name = "Category", Type = typeof(BookCategory) };
      var mBkEditor = partBooks.GetCreateMember("Category");
      var categoryFilter = new ViewFilter() { Param = categoryParam, Member = mBkEditor };
      viewBooks.AvailableFilters.Add(categoryFilter);

      // create ViewQuery with param
      var bksViewQuery = new ViewQuery() { View = viewBooks, Skip = 1, Take = 3 };
      bksViewQuery.Filters.Add(categoryFilter); 
      var prmValue = new ViewParamValue() { Param = categoryParam, Value = BookCategory.Programming };
      bksViewQuery.ParamValues.Add(prmValue);

      bksViewQuery.OrderBy.Add(new OrderBySpec() { Member = mTitle });
      // add output members
      var membersBk = partBooks.GetMembers("Id", "Title", "PublishedOn", "Category");
      var membersPub = partPub.GetMembers("Name");
      bksViewQuery.OutMembers.AddRange(membersBk);
      bksViewQuery.OutMembers.AddRange(membersPub);

      var session = app.OpenSession(); 
      var editorBooks = session.ExecuteViewQuery(bksViewQuery);
    }


    private EntityInfo GetEntity<T>() {
      return Startup.BooksApp.Model.GetEntityInfo(typeof(T));
    }
  }
}
