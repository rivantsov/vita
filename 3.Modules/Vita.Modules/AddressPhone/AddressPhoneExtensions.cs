using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Modules.Party;

namespace Vita.Modules.AddressPhone {
  public static class AddressPhoneExtensions {
    
    public static IAddress NewAddress(this IEntitySession session, string street1, string street2, string aptNo, 
                                      string city, string stateProvince, string postalCode, string country = null) {
      return NewAddress<IAddress>(session, street1, street2, aptNo, city, stateProvince, postalCode, country);
    }

    public static TAddress NewAddress<TAddress>(this IEntitySession session, string street1, string street2, string aptNo, 
                                      string city, string stateProvince, string postalCode, string country = null) 
      where TAddress : class, IAddress
    {
      var addr = session.NewEntity<TAddress>();
      addr.Street1 = street1;
      addr.Street2 = street2;
      addr.SuiteNo = aptNo;
      addr.City = city;
      addr.StateProvince = stateProvince;
      addr.PostalCode = postalCode;
      addr.Country = country;
      return addr; 
    }

    public static IAddressPartyLink AddAddress<TAddress>(this IOrganization org, TAddress address, AddressType type) 
      where TAddress: class, IAddress 
    {
      return AddAddress<TAddress>(org.Party, address, type); 
    }

    public static IAddressPartyLink AddAddress<TAddress>(this IParty party, TAddress address, AddressType type)
      where TAddress : class, IAddress 
    {
      var session = EntityHelper.GetSession(party);
      var link = session.NewEntity<IAddressPartyLink>();
      link.Party = party;
      link.Address = address;
      link.Type = type;
      return link; 
    }

    public static IPhone AddPhone(this IParty party, PhoneType type, string phoneNo, string phoneExt = null) {
      return AddPhone<IPhone>(party, type, phoneNo, phoneExt);
    }

    public static TPhone AddPhone<TPhone>(this IParty party, PhoneType type, string phoneNo, string phoneExt = null)
                    where TPhone : class, IPhone {
      var session = EntityHelper.GetSession(party); 
      var ph = session.NewEntity<TPhone>();
      ph.Party = party;
      ph.Type = type;
      ph.Phone = phoneNo;
      ph.Extension = phoneExt;
      return ph; 
    }

    public static IOrgContact AddContact(this IOrganization org, string contactType, string fullName = null, string email = null, IPhone phone = null) {
      return AddContact<IOrganization, IPhone>(org, contactType, fullName, email, phone);
    }
    public static IOrgContact AddContact<TOrg, TPhone>(this TOrg org, string contactType, string fullName = null, string email = null, TPhone phone = null)
      where TOrg : class, IOrganization
      where TPhone : class, IPhone 
    {
      var session = EntityHelper.GetSession(org); 
      var cont = session.NewEntity<IOrgContact>();
      cont.Org = org;
      cont.ContactType = contactType;
      cont.FullName = fullName; 
      cont.Email = email;
      cont.Phone = phone;
      return cont; 
    }

    public static IList<IPhone> GetPhones(this IParty party) {
      return GetPhones<IPhone>(party); 
    }

    public static IList<TPhone> GetPhones<TPhone>(this IParty party) 
      where TPhone: class, IPhone 
    {
      var session = EntityHelper.GetSession(party);
      var phones = session.EntitySet<TPhone>().Where(p => p.Party == party).ToList();
      return phones; 
    }

    public static IList<IAddress> GetAddresses(this IParty party, AddressType? type = null) {
      return GetAddresses<IAddress>(party, type); 
    }

    public static IList<TAddress> GetAddresses<TAddress>(this IParty party, AddressType? type = null) 
      where TAddress: class, IAddress
    {
      var session = EntityHelper.GetSession(party);
      var links = session.EntitySet<IAddressPartyLink>();
      var query = links.Where(lnk => lnk.Party == party);
      if (type != null)
        query = query.Where(lnk => lnk.Type == type.Value);
      var list = query.Select(lnk => lnk.Address).ToList();
      return list.Select(a => (TAddress)a).ToList();
    }

    public static IList<IOrgContact> GetContacts(IOrganization org, string contactType = null) {
      return GetContacts<IOrganization>(org, contactType); 
    }

    public static IList<IOrgContact> GetContacts<TOrg>(TOrg org, string contactType = null)
      where TOrg : class, IOrganization
    {
      var session = EntityHelper.GetSession(org);
      var query = session.EntitySet<IOrgContact>().Where(oc => oc.Org == org);
      if(!string.IsNullOrWhiteSpace(contactType))
        query = query.Where(oc => oc.ContactType == contactType);
      var result = query.ToList();
      return result; 
    }

  }
}
