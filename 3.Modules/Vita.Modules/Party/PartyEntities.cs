using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;

namespace Vita.Modules.Party {

  public enum PartyKind {
    Person = 0,
    Org = 1,
  }

  // HL7 standard definition
  public enum Gender {
    Unknown = 0,
    Male,
    Female,
    Other,
  }

  public enum OrgType {
    Unknown = 0,
    LegalEntity = 1, 
    Government = 2, 
    Educational = 3, 
    Association = 4, //association, political party
    Department = 5, 
    Other = 100,
  }

  [Entity, Display("{Name}")]
  public interface IParty {
    [PrimaryKey, Auto]
    Guid Id { get; set; }
    PartyKind Kind { get; set; }
    [Size("Name"), Index]
    string Name { get; set; }
    [HashFor("Name"), Index]
    int NameHash { get; set; }
    [OneToOne, Nullable]
    IPerson Person { get; }
    [OneToOne, Nullable]
    IOrganization Organization { get; }
  }

  [Entity]
  [Display("{LastName}, {FirstName}")]
  public interface IPerson {
    [PrimaryKey]
    IParty Party { get; set; }

    [Nullable, Size(Sizes.PrefixSuffix)]
    string Prefix { get; set; } //ex: Mr, Mrs
    
    [Size(Sizes.Name)]
    string FirstName { get; set; }
    
    [Size(Sizes.Name), Nullable]
    string MiddleName { get; set; }
    
    [Size(Sizes.Name), Index]
    string LastName { get; set; }
    
    [Nullable, Size(Sizes.PrefixSuffix)]
    string Suffix { get; set; } //ex: Jr

    Gender Gender { get; set; }

    [DateOnly]
    DateTime? BirthDate { get; set;  }

    [Size(Sizes.Email), Nullable, Index]
    string Email { get; set; }

    Guid? UserId { get; set; }

    [Index, HashFor("LastName")]
    int LastNameHash { get; set; } //stable hash of last name, for faster searches
  }

  [Entity, Unique("LegalName,LegalAuthority")]
  [Display("{LegalName}")]
  public interface IOrganization {
    [PrimaryKey]
    IParty Party { get; set; }

    OrgType OrgType { get; set; }

    [Size(Sizes.Name)]
    string LegalName { get; set; }
    
    [Size(Sizes.Name), Nullable, Index]
    string DbaAlias { get; set; } //Doing Business As...; - restaurant name, with company being different name 
    
    [Size(Sizes.LongName)]
    string ExtendedName { get; set; }

    [Size(Sizes.Name), Index, Nullable]
    string LegalId { get; set; } // ex: employer federal ID

    // optional, identifies location/incorporation place, 
    // Useful to allow more than one company with the same name - maybe from different states
    // - considering the fact that LegalName+LegalAuthority should be unique.
    [Size(Sizes.Name), Nullable]
    string LegalAuthority { get; set; }

    //Parent/holding org; or parent org for department 
    [Nullable]
    IOrganization Parent { get; set; }
  }

}//ns
