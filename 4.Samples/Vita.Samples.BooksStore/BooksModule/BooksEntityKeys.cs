using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Vita.Entities;

namespace Vita.Samples.BookStore {

  // This file contains companion types for Books entities. 
  // Companion types allow you to set some attributes (like DB keys and indexes) on companion types rather than
  // on entities themselves. You can put all database artifacts definitions in a separate c# file with companion types
  // and put the database admin in charge of this file, so he can fine-tune this stuff without clashing with other 
  // developers which work with entities in the middle tier.
  // You register companion types using the RegisterCompanionTypes method of EntityModelSetup class. 
  // There are 2 ways to link companion type to entity type
  //  1. Inheritance - companion type inherits from entity type; that's what we do here
  //  2. Using ForEntity attribute on companion type

  [ClusteredIndex("CreatedOn,Id"), Index("Title", IncludeMembers="PublishedOn,Category,Publisher", Filter="{PublishedOn} IS NOT NULL")]
  public interface IBookKeys : IBook {
  }

  public interface IPublisherKeys : IPublisher {
    [Unique(Alias = "PublisherName")] //Alias is used in identifying the key in UniqueIndexViolation exception
    new string Name { get; set; }
  }

  [ClusteredIndex("LastName:DESC,Id")] // DESC - just for test
  public interface IAuthorKeys : IAuthor {
  }

}
