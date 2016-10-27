using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

using Vita.Common;
using Vita.Entities;
using Vita.Entities.Authorization;
using Vita.Entities.Runtime;
using Vita.Entities.Services;
using Vita.Modules.Logging;
using Vita.Modules.EncryptedData;

// See here for detailed discussion of password reset feature: 
// http://www.troyhunt.com/2012/05/everything-you-ever-wanted-to-know.html

namespace Vita.Modules.Login {

  public partial class LoginModule{

    /// <summary>Imports secret questions from defaults embedded as resource into this assembly.</summary>
    /// <param name="session">Entity session.</param>
    /// <returns>Number of questions imported.</returns>
    public static int ImportDefaultSecretQuestions(IEntitySession session) {
      using(var qStream = typeof(LoginModule).Assembly.GetManifestResourceStream("Vita.Modules.Login.DefaultSecretQuestions.txt")) {
        return LoginModule.ImportSecretQuestions(session, qStream); // "RejuvenlySecurityQuestions.txt");
      }
    }

    public static int ImportSecretQuestions(IEntitySession session, string filePath) {
      Util.Check(File.Exists(filePath), "File {0} not found.", filePath);
      using(var fs = File.OpenRead(filePath)) {
        return ImportSecretQuestions(session, fs); 
      }
    }

    // Use it to import from resource - look at ImportDefaultSecretQuestions
    public static int ImportSecretQuestions(IEntitySession session, Stream stream) {
      var reader = new StreamReader(stream);
      var text = reader.ReadToEnd(); // File.ReadAllLines(filePath);
      var lines = text.Split(new [] {'\r', '\n'}, StringSplitOptions.RemoveEmptyEntries); 
      // Postgres blows up here, trying with LINQ
      //var oldList = session.GetEntities<ISecretQuestion>();
      var oldList = session.EntitySet<ISecretQuestion>().ToList();
      var count = 0;
      foreach(var line in lines) {
        if(string.IsNullOrWhiteSpace(line) || line.StartsWith("//"))
          continue;
        //check if there's existing
        var trimLine = line.Trim(); 
        var oldLine = oldList.FirstOrDefault(l => l.Question == trimLine);
        if(oldLine != null)
          continue;
        var q = session.NewEntity<ISecretQuestion>();
        q.Question = trimLine;
        q.Number = count++;
        q.Flags = SecretQuestionFlags.None;
      }
      return count; 
    }

  }//module

}//ns
