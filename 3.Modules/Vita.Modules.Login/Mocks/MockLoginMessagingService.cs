using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Vita.Entities;

namespace Vita.Modules.Login.Mocks {

  public class SentMessageInfo {
    public string Email;
    public LoginMessageType MessageType; 
    public string Pin; 
  }

  // fake/test login messaging service; pretends to send messages (emails) and simply accumulates the sent messages,
  //  allowing test code to retrieve them
  public class MockLoginMessagingService : ILoginMessagingService {
    public List<SentMessageInfo> SentMessages = new List<SentMessageInfo>();

    public Task SendMessage(OperationContext context, LoginMessageType messageType, 
            ILoginExtraFactor factor,  ILoginProcess process = null) {
      SentMessages.Add(new SentMessageInfo() 
         {MessageType = messageType, Email = process.CurrentFactor.FactorValue, Pin = process?.CurrentPin});
      return Task.CompletedTask;
    }

  }//class
} //ns
