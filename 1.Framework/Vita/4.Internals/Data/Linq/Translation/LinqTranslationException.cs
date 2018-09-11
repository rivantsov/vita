using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities.Runtime;

namespace Vita.Data.Linq.Translation {
  public class LinqTranslationException : Exception {
    public EntityCommand Command; 

    public LinqTranslationException(string message, EntityCommand command, Exception inner = null) : base(message, inner) {
      Command = command; 
      if (Command != null)
        this.Data["LinqCommand"] = Command.ToString(); 
    }
  }
}
