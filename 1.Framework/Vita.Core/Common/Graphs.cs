using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Vita.Common.Graphs {
  //Implements Trajan algorithm of searching for SCC components (http://en.wikipedia.org/wiki/Tarjan's_strongly_connected_components_algorithm)
  // We are interested in a topologically sorted (http://en.wikipedia.org/wiki/Topological_sorting) list of 
  // Strongly-Connected Componentes (SCC) in a directed graph.
  // This algorithm is used for sorting entities for update, to satisfy referential constraints

  public class Vertex {
    public object Tag;
    public List<Vertex> LinkedTo = new List<Vertex>();
    internal int Index, LowIndex;
    public bool InStack;
    public int SccIndex;
    public bool NonTrivialGroup; 

    public void AddLink(params Vertex[] toVertexes) {
      foreach (var v in toVertexes)
        LinkedTo.Add(v);
    }
    public override string ToString() {
      return Tag + ", Scc=" + SccIndex;
    }
  }

  public class Graph {
    public readonly List<Vertex> Vertexes = new List<Vertex>();
    public int SccCount; //# of SCC group

    public Vertex Add(object tag) {
      var v = new Vertex() { Tag = tag };
      Vertexes.Add(v);
      return v;
    }
    public Vertex FindOrAdd(object tag) {
      var vf = Vertexes.FirstOrDefault(v => v.Tag == tag);
      if (vf == null)
        vf = Add(tag);
      return vf; 
    }

    Stack<Vertex> _vertexStack;
    int _currentIndex;

    /// <summary>Builds SCC for a graph. Sets SCC index for each vertex. </summary>
    /// <returns>The top index of SCC.</returns>
    public int BuildScc() {
      Reset();
      foreach (var v in Vertexes)
        if (v.Index == 0)
          StrongConnect(v);
      // The Trajan algorithm assigns SCC index in REVERSE topological order
      // So the root of SCC tree has max index. We reverse values of SCC indexes to have more direct order, and then sort
      foreach (var v in Vertexes)
        v.SccIndex = SccCount - v.SccIndex + 1;
      Vertexes.Sort((x, y) => x.SccIndex.CompareTo(y.SccIndex));
      return SccCount;
    }

    private void Reset() {
      _vertexStack = new Stack<Vertex>();
      SccCount = 0;
      _currentIndex = 0;
      foreach (var v in Vertexes) {
        v.LowIndex = 0;
        v.Index = 0;
        v.SccIndex = 0;
      }
    }//method

    private void StrongConnect(Vertex v) {
      v.LowIndex = v.Index = ++_currentIndex;
      v.InStack = true;
      _vertexStack.Push(v);
      foreach (var w in v.LinkedTo) {
        if (w.Index == 0) {
          StrongConnect(w);
          v.LowIndex = Math.Min(v.LowIndex, w.LowIndex);
        } else if (w.InStack)
          v.LowIndex = Math.Min(v.LowIndex, w.Index);
      }//foreach
      // If v is a root node, pop the stack and generate the SCC
      if (v.LowIndex == v.Index) {
        //Pop all vertexes until v - this is a new SCC group
        SccCount++;
        var nonTrivialGroup = _vertexStack.Peek() != v;
        while (true) {
          var w = _vertexStack.Pop();
          w.InStack = false;
          w.SccIndex = SccCount;
          w.NonTrivialGroup = nonTrivialGroup;
          if (w == v)  break; //from while
        }
      } //if v.LowIndex...   
    }//method

  }//class
}//ns
