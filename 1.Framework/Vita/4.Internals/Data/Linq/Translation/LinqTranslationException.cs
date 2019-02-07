using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities.Runtime;

namespace Vita.Data.Linq.Translation {
  public class LinqTranslationException : Exception {
    public LinqCommand Command; 

    public LinqTranslationException(string message, LinqCommand command, Exception inner = null) : base(message, inner) {
      Command = command; 
      if (Command != null)
        this.Data["LinqExpression"] = Command.Expression.ToString(); 
    }
  }
}
