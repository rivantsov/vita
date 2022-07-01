using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.CodeDom.Compiler;
using System.Reflection;
using System.IO;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Emit;

namespace Vita.Tools.DbFirst {

  public class CompileResult {
    public bool Success;
    public IList<string> Messages;
    public Assembly Assembly; 
  }

  public static class CompilerHelper {
    public static CompileResult CompileSources(Type driverType, params string[] sources) {
      var trees = new List<SyntaxTree>();
      foreach(var src in sources) {
        var tree = CSharpSyntaxTree.ParseText(src);
        trees.Add(tree); 
      }
      //create refs 
      var mRefs = GetAssemblyReferences(driverType); 

      //compile
      var compilation = CSharpCompilation.Create("temp", trees, mRefs);
      var output = new MemoryStream();
      var emitResult = compilation.Emit(output);
      if (!emitResult.Success)
        return new CompileResult() { Success = false, Messages = emitResult.GetMessages() };
      output.Position = 0;
      var bytes = output.ToArray();
      var asm = Assembly.Load(bytes);
      return new CompileResult() { Success = true, Assembly = asm, Messages = emitResult.GetMessages() };
    }

    private static IList<string> GetMessages(this EmitResult emit) {
      
      var result = emit.Diagnostics.Select(
        d => d.GetMessage() + " Line: " + d.Location.GetLineSpan().StartLinePosition.Line).ToList();
      return result; 
    }

    private static IList<MetadataReference> GetAssemblyReferences(Type driverType) {
      // could not make these refs through Type object (like Console), type.assembly.loc returns some other system assembly
      var netstandard = Assembly.Load("netstandard, Version=2.0.3.0").Location;
      var runtime = Assembly.Load("System.Runtime, Version=4.0.0.0").Location;
      var collections = Assembly.Load("System.Collections, Version=4.0.0.0").Location;
      var console = typeof(Console).Assembly.Location;
      var corlib = typeof(object).Assembly.Location;
      var systemLinq = typeof(Enumerable).Assembly.Location;
      var linqExpr = typeof(System.Linq.Expressions.Expression).Assembly.Location;
      var dataCommon = typeof(System.Data.DbType).Assembly.Location;

      //VITA assemblies
      var driverLoc = driverType.Assembly.Location;
      //var driverAsmPath = System.IO.Path.GetFileName(driverLoc);
      var vitaPath = typeof(Entities.EntityApp).Assembly.Location;

      var refs = new[] {
        MetadataReference.CreateFromFile(netstandard),
        MetadataReference.CreateFromFile(corlib),
        MetadataReference.CreateFromFile(console),
        MetadataReference.CreateFromFile(runtime),
        MetadataReference.CreateFromFile(collections),
        MetadataReference.CreateFromFile(systemLinq),
        MetadataReference.CreateFromFile(linqExpr),
        MetadataReference.CreateFromFile(dataCommon),
        MetadataReference.CreateFromFile(driverLoc),
        MetadataReference.CreateFromFile(vitaPath),
      };
      return refs; 
    } //method
  }
}
