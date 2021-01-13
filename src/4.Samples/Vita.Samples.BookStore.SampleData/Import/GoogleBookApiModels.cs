using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Samples.BookStore.SampleData.Import {

  // Data models for Google Books API. We do not list all properties that returned json contains (there are a lot more), only those we use in our Books sample
  // Note that node names in json data in Google Books API are camelCase (ex: volumeInfo), while we define classes with PascalCase convention (standard for c#).
  // The matching is maintained automatically by setting a flag ClientOptions.CamelCaseNames - this sets the NodeNameContractResolver to flip names to camelCase. 
  public class VolumeSet {
    public string Kind;
    public List<Volume> Items;
    public int TotalItems;
  }

  [DebuggerDisplay("{VolumeInfo.Title}")]
  public class Volume {
    public VolumeInfo VolumeInfo;
    public SaleInfo SaleInfo;
  }

  public class VolumeInfo {
    public string Title;
    public string SubTitle;
    public List<string> Authors;
    public string PublishedDate;
    public string Publisher;
    public string Description;
    public List<string> Categories;
    public string MainCategory;
    public VolumeImageLinks ImageLinks;
  }//VolumeInfo

  public class VolumeImageLinks {
    public string Thumbnail;
    public string Medium;
  }

  public class SaleInfo {
    public Price ListPrice;
    public Price RetailPrice;
  }
  public class Price {
    public Decimal Amount;
    public string CurrencyCode; 
  }

  //  ============================================== BadRequest response data models ========================================================
  //See sample error in data_samples.txt
  public class GoogleBadRequestResponse {
    public GoogleApiError Error;
    public override string ToString() {
      return Error + string.Empty;
    }
  }
  
  public class GoogleApiError {
    public string Code;
    public string Message;
    public List<ErrorDetail> Errors;
    public override string ToString() {
      var errs = string.Join(";", Errors);
      return string.Format("{0}: {1}  \r\n {2}", Code, Message, Errors);
    }
  }

  public class ErrorDetail {
    public string Domain;
    public string Reason;
    public string Message;
    public string LocationType;
    public string Location;
    public override string ToString() {
      return string.Format("{0}: {1}  ({2}/{3}", Reason, Message, LocationType, Location);
    }
  }

}//ns


