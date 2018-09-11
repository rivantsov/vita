using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Vita.Entities;
using Vita.Modules.Login;

namespace Vita.Testing.ExtendedTests {

  public class PinInfoMessage {
    public string Email;
    public string Pin; 
  }
  // fake/test login messaging service; pretends to send messages (emails) and simply accumulates the sent messages,
  //  allowing test code to retrieve them
  public class MockLoginMessagingService : ILoginMessagingService {
    public List<PinInfoMessage> PinMessages = new List<PinInfoMessage>();

    public Task SendMessage(OperationContext context, LoginMessageType messageType, 
            ILoginExtraFactor factor,  ILoginProcess process = null) {
      if (messageType == LoginMessageType.Pin)
        PinMessages.Add(new PinInfoMessage() { Pin = process.CurrentPin, Email = process.CurrentFactor.FactorValue });
      return Task.CompletedTask;
    }

  }//class
} //ns
