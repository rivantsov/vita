using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Data.Linq;

// Async extension 
public interface IAsyncQueryProvider {
  Task<TResult> ExecuteAsync<TResult>(Expression expression);
}
