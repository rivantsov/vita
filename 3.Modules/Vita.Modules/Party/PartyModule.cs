using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;

namespace Vita.Modules.Party {

  //Default module definition with default entity types
  public class PartyModule : PartyModule<IParty, IPerson, IOrganization> {
    public static readonly Version CurrentVersion = new Version("1.0.0.0");

    public PartyModule(EntityArea area, string name = "PartyModule", string description = null)
      : base(area, name, description) {
    }
  }//class

  //Customizable module
  public class PartyModule<TParty, TPerson, TOrg> : EntityModule 
    where TParty: IParty
    where TPerson : IPerson
    where TOrg : IOrganization  
  {
    public PartyModule(EntityArea area, string name = "PartyModule", string description = null)  : base(area, name, description, version: PartyModule.CurrentVersion) {
      RegisterEntities(typeof(TParty), typeof(TPerson), typeof(TOrg));
    }

  }//class

}
