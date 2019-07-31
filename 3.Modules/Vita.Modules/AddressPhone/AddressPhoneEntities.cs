using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Modules.Party; 

namespace Vita.Modules.AddressPhone {

  [Flags]
  public enum AddressType {
    None = 0,
    Home = 1, 
    Legal = 1 << 1, 
    Mail = 1 << 2,
    Billing = 1 << 3,
    Shipping = 1 << 4,
  }

  public enum PhoneType {
    Unknown = 0,
    Home = 1, 
    Cell = 2,
    Business = 3,
    Other = 10,
  }
  
  [Entity, Index("City,StateProvince")]
  public interface IAddress {
    [PrimaryKey, Auto]
    Guid Id { get; set; }
    [Size(AddressPhoneModule.AddressLenCode, 100), Index]
    string Street1 { get; set;  }
    [Nullable]
    string Street2 { get; set; }

    [Size(AddressPhoneModule.SuiteNoLenCode, 10), Nullable]
    string SuiteNo { get; set; }

    [Size(AddressPhoneModule.CityLenCode, 50)]
    string City { get; set; }
    
    [Size(AddressPhoneModule.StateLenCode, 2)]
    string StateProvince { get; set; }

    [Size(AddressPhoneModule.PostalCodeLenCode, 12), Index]
    string PostalCode { get; set; } //zip

    [Size(AddressPhoneModule.CountryLenCode, 50), Nullable]
    string Country { get; set; } 
  }

  [Entity]
  public interface IPhone {

    [PrimaryKey, Auto]
    Guid Id { get; set; }

    IParty Party { get; set; }

    PhoneType Type { get; set; }

    [Size(AddressPhoneModule.PhoneLenCode, 12), Index]
    string Phone { get; set; }

    [Size(AddressPhoneModule.PhoneExtLenCode, 6), Nullable]
    string Extension { get; set; }
  }

  [Entity, PrimaryKey("Party,Address"), Index("Address,Type")]
  public interface IAddressPartyLink
  {
    [CascadeDelete]
    IParty Party { get; set; }
    [CascadeDelete]
    IAddress Address { get; set; }
    AddressType Type { get; set; }
  }

  [Entity]
  public interface IOrgContact  {
    [PrimaryKey, Auto]
    Guid Id { get; set; }

    IOrganization Org { get; set; }
    
    [Size(AddressPhoneModule.ContactTypeLenCode, 20)]
    string ContactType { get; set; } //sales, support, etc

    [Size(Sizes.Name), Nullable] //might be anonymous
    string FullName { get; set; }

    [Size(Sizes.Email), Index, Nullable]
    string Email { get; set; }

    [Nullable]
    IPhone Phone { get; set; }
    
  }
  

}
