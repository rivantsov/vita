using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Entities;
using Vita.Modules.WebClient;

namespace Vita.Samples.BookStore.SampleData.Import {

  public class GoogleBooksImport {
    EntityApp _app;
    GoogleBooksApiClient _client; 
    IEntitySession _session;
    Dictionary<string, IBook> _bookCache;
    Dictionary<string, IPublisher> _publishersCache;
    Dictionary<string, IAuthor> _authorsCache;

    public void ImportBooks(EntityApp app, int count = 250) {
      _app = app;
      _session = _app.OpenSystemSession();
      //Preload caches
      _bookCache = _session.GetEntities<IBook>(take: 1000).ToDictionary(b => b.Title, StringComparer.OrdinalIgnoreCase);
      _publishersCache = _session.GetEntities<IPublisher>(take: 200).ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
      _authorsCache = _session.GetEntities<IAuthor>(take: 1000).ToDictionary(a => a.FirstName + a.LastName, StringComparer.OrdinalIgnoreCase);
      _client = new GoogleBooksApiClient(_session.Context);
      var batchSize = count / 5;
      ImportBooksInCategory(BookCategory.Programming, "c#", batchSize);
      ImportBooksInCategory(BookCategory.Programming, "Linux", batchSize);
      ImportBooksInCategory(BookCategory.Fiction, "Comics", batchSize);
      ImportBooksInCategory(BookCategory.Fiction, "Science fiction", batchSize);
      ImportBooksInCategory(BookCategory.Kids, "Fairy tales", batchSize);

    }//method

    private void ImportBooksInCategory(BookCategory category, string keyword, int count) {
      var skip = 0; 
      var currentCount = 0;
      while(currentCount < count) {
        var volumeSet = _client.GetVolumes(keyword, skip);
        skip += volumeSet.Items.Count;
        foreach (var volume in volumeSet.Items) {
          var vinfo = volume.VolumeInfo;
          if (string.IsNullOrWhiteSpace(vinfo.Publisher)) //some books don't have publisher, just skip these
            continue; 
          var title = Trim(vinfo.Title, 120);
          if (_bookCache.ContainsKey(title))
            continue;
          currentCount++;
          var ipub = GetCreatePublisher(vinfo.Publisher);
          var pubDate = ParsePublishedDate(vinfo.PublishedDate);
          var image = LoadImageFromUrl(vinfo.ImageLinks.Thumbnail);
          var price = GetPrice(volume.SaleInfo);
          var ibook = _session.NewBook(BookEdition.Paperback, category, title, vinfo.SubTitle, ipub, pubDate, price, coverImage: image);
          ibook.Abstract = vinfo.Description; 
          _bookCache.Add(vinfo.Title, ibook);
          //parse authors
          if (vinfo.Authors != null)
            foreach (var author in vinfo.Authors) {
              var iauth = GetCreateAuthor(author);
              if (iauth != null)
                ibook.Authors.Add(iauth);
            }
        }//foreach volume
      }
      try {
        _session.SaveChanges();
      } catch (Exception ex) {
        System.Diagnostics.Debug.WriteLine("Exception: " + ex.ToLogString());
        throw; 
      }
    }

    private string Trim(string value, int maxLen) {
      value = value.Trim();     
      if (value == null || value.Length <= maxLen)
        return value;
      return value.Substring(0, maxLen);
    }

    private Decimal GetPrice(SaleInfo info) {
      if (info == null)
        return 0.01m; 
      if (info.ListPrice != null)
        return info.ListPrice.Amount;
      if (info.RetailPrice != null)
        return info.RetailPrice.Amount;
      return 0.01m; 
    }

    private DateTime? ParsePublishedDate(string value) {
      DateTime date;
      if (DateTime.TryParse(value, out date))
        return date;
      return null; 
    }

    private IPublisher GetCreatePublisher(string publisher) {
      publisher  = Trim(publisher, 50);
      IPublisher ipub;
      if (_publishersCache.TryGetValue(publisher, out ipub))
        return ipub; 
      ipub = _session.NewPublisher(publisher);
      _publishersCache.Add(publisher, ipub); 
      return ipub; 
    }

    private IAuthor GetCreateAuthor(string author) {
      IAuthor iauth;
      var names = author.Split(' ');
      var last = names[names.Length - 1];
      var first = (names.Length > 1) ? names[0] : null;
      if (_authorsCache.TryGetValue(first + last, out iauth))
        return iauth;
      iauth = _session.NewAuthor(first, last);
      _authorsCache.Add(first + last, iauth);
      return iauth; 
    }


    private IImage LoadImageFromUrl(string url, ImageType type = ImageType.BookCover, string mediaType = "image/jpeg") {
      if (string.IsNullOrWhiteSpace(url))
        return null; 
      var name = Guid.NewGuid().ToString();
      var bytes = _client.GetImage(url);
      var image = _session.NewImage(name, type, mediaType, bytes);
      return image;
    }


  }//class
}
