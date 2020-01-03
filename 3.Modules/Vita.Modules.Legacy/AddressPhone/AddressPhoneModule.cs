using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Modules.Party; 

namespace Vita.Modules.AddressPhone {

  public class AddressPhoneModule : AddressPhoneModule<IAddress, IPhone, IOrganization> {
    public static readonly Version CurrentVersion = new Version("1.0.0.0");

    //Length codes used in Size(lenCode) attribute. Using these codes you can override default sizes during app setup time
    public const string AddressLenCode = "Address";
    public const string SuiteNoLenCode = "AptNo";
    public const string PhoneLenCode = "Phone";
    public const string PhoneExtLenCode = "PhoneExt";
    public const string CityLenCode = "City";
    public const string StateLenCode = "State";
    public const string PostalCodeLenCode = "PostalCode";
    public const string CountryLenCode = "Country";
    public const string ContactTypeLenCode = "ContactType";

    public AddressPhoneModule(EntityArea area, string name = "AddressPhone")  : base(area, name) {}
  }

  public class AddressPhoneModule<TAddress, TPhone, TOrganization> : EntityModule
    where TAddress : class, IAddress
    where TPhone : class, IPhone
    where TOrganization: class, IOrganization
  {
    public AddressPhoneModule(EntityArea area, string name = "AddressPhone"): base(area, name, version: AddressPhoneModule.CurrentVersion) {
      RegisterEntities(typeof(TAddress), typeof(TPhone), typeof(IOrgContact), typeof(IAddressPartyLink) );
    }
  }
}
