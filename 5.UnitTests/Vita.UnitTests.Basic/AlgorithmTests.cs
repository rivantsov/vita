using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Vita.Common.Graphs;


namespace Vita.UnitTests.Basic {

  [TestClass]
  public class AlgorithmTests {

    #region SCC test
    [TestMethod]
    public void TestGraphSccAlgorithm() {
      //This test uses a simple graph from the wikipedia page about SCC: http://en.wikipedia.org/wiki/Strongly_connected_components
      // The copy of this image is in SccTestGraph.jpg file in this test project.
      var expected = "a, Scc=1; b, Scc=1; e, Scc=1; c, Scc=2; d, Scc=2; h, Scc=2; f, Scc=3; g, Scc=3"; //expected SCC indexes
      var gr = new Graph();
      SetupSampleGraph(gr);
      gr.BuildScc();
      // additionally sort by tags, so that result string matches
      var sortedVertexes = gr.Vertexes.OrderBy(v => v.SccIndex).ThenBy(v => (string)v.Tag).ToList();
      var strOut = string.Join("; ", sortedVertexes);
      Assert.AreEqual(expected, strOut, "SCC computation did not return expected result."); 
    }

    // Builds a sample graph from http://en.wikipedia.org/wiki/Strongly_connected_components
    private static void SetupSampleGraph(Graph gr) {
      var a = gr.Add("a");
      var b = gr.Add("b");
      var c = gr.Add("c");
      var d = gr.Add("d");
      var e = gr.Add("e");
      var f = gr.Add("f");
      var g = gr.Add("g");
      var h = gr.Add("h");
      //links
      a.AddLink(b);
      b.AddLink(e, f, c);
      c.AddLink(d, g);
      d.AddLink(c, h);
      e.AddLink(a, f);
      f.AddLink(g);
      g.AddLink(f);
      h.AddLink(g, d);
    }
    #endregion



  }//class
}//ns
