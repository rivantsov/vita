using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Common;
using Vita.Entities; 

namespace Vita.Modules.Party {
  public static class PartyExtensions {
    // Providing Id explicitly allows to sync ID values with external entities - like loginId, userId, etc
    public static IParty NewParty(this IEntitySession session, PartyKind kind, string name, Guid? id = null) {
      var party = session.NewEntity<IParty>();
      party.Kind = kind;
      party.Name = name;
      if(id != null)
        party.Id = id.Value;
      return party; 
    }

    public static IPerson NewPerson(this IEntitySession session, string prefix, string first, string middle, string last, 
         string suffix = null, Gender gender = Gender.Unknown, DateTime? birthDate = null, string email = null, Guid? userId = null, Guid? id = null) {
      var fullName = last + ", " + first;
      var party = session.NewParty(PartyKind.Person, fullName, id);
      var person = session.NewEntity<IPerson>();
      person.Party = party;
      person.Prefix = prefix;
      person.FirstName = first;
      person.MiddleName = middle;
      person.LastName = last;
      person.Suffix = suffix;
      person.Gender = gender;
      person.BirthDate = birthDate;
      person.Email = email;
      person.UserId = userId; 
      return person; 
    }

    public static IOrganization NewOrg(this IEntitySession session, OrgType orgType, string legalName, 
                                       string extendedName = null, string legalId = null, string dba = null) {
      Util.Check(!string.IsNullOrWhiteSpace(legalName), "LegalName may not be empty");
      var party = session.NewParty(PartyKind.Org, legalName);
      var org = session.NewEntity<IOrganization>(); 
      org.Party = party;
      org.OrgType = orgType; 
      org.LegalName = legalName;
      org.ExtendedName = extendedName ?? legalName;
      org.DbaAlias = dba ?? legalName;
      org.LegalId = legalId;
      return org; 
    }

    public static string FullName(this IPerson person) {
      return person.FirstName + " " + person.LastName; 
    }
  }
}
