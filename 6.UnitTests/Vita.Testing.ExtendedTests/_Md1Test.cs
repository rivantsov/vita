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

    [TestMethod]
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
      var editorParam = new ViewParam() { Name = "Editor", Type = typeof(IUser) };
      var mBkEditor = partBooks.GetCreateMember("Editor");
      var editorFilter = new ViewFilter() { Param = editorParam, Member = mBkEditor };
      viewBooks.AvailableFilters.Add(editorFilter);

      // retrieve an editor and create query on books bu this editor
      var session = app.OpenSession();
      var editor0 = session.EntitySet<IUser>().FirstOrDefault(u => u.Type == UserType.BookEditor);
      
      // create ViewQuery with param
      var edBksQuery = new ViewQuery() { View = viewBooks, Skip = 1, Take = 3 };
      edBksQuery.OrderBy.Add(new OrderBySpec() { Member = mTitle });
      var prmValue = new ViewParamValue() { Param = editorParam, Value = editor0 };
      edBksQuery.ParamValues.Add(prmValue);
      // add output members
      var membersBk = partBooks.GetMembers("Id", "Title", "PublishedOn", "Category");
      var membersPub = partPub.GetMembers("Name");
      edBksQuery.OutMembers.AddRange(membersBk);
      edBksQuery.OutMembers.AddRange(membersPub);

      var editorBooks = session.ExecuteViewQuery(edBksQuery);
    }


    private EntityInfo GetEntity<T>() {
      return Startup.BooksApp.Model.GetEntityInfo(typeof(T));
    }
  }
}
