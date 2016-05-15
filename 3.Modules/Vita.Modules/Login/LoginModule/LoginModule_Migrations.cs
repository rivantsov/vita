using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities; 
using Vita.Data.Upgrades;
using Vita.Modules.TextTemplates;


namespace Vita.Modules.Login {
  public partial class LoginModule {

    public override void RegisterMigrations(DbMigrationSet migrations) {
      // In LoginModule v 1.1, email template naming conventions changed; so this migration action renames existing templates
      var templateRenamingMap = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase) {
        {"MultiFactorEmailSubject" , LoginMessageTemplates.MultiFactorEmailSubject},
        {"MultiFactorEmailBody" , LoginMessageTemplates.MultiFactorEmailBody},
        {"MultiFactorSmsBody" , LoginMessageTemplates.MultiFactorSmsBody},
        {"OneTimePasswordSubject", LoginMessageTemplates.OneTimePasswordSubject},
        {"OneTimePasswordBody",LoginMessageTemplates.OneTimePasswordBody},
        {"PasswordResetCompleteEmailSubject", LoginMessageTemplates.PasswordResetCompleteEmailSubject},
        {"PasswordResetCompleteEmailBody", LoginMessageTemplates.PasswordResetCompleteEmailBody},
        {"PasswordResetPinEmailSubject", LoginMessageTemplates.PasswordResetPinEmailSubject},
        {"PasswordResetPinEmailBody", LoginMessageTemplates.PasswordResetPinEmailBody},
        {"PasswordResetPinSmsBody", LoginMessageTemplates.PasswordResetPinSmsBody},
        {"VerifyEmailSubject", LoginMessageTemplates.VerifyEmailSubject},
        {"VerifyEmailBody", LoginMessageTemplates.VerifyEmailBody},
        {"VerifyPhoneSmsBody", LoginMessageTemplates.VerifySmsBody},
      };

      migrations.AddPostUpgradeAction("1.1.0.0", "UpdateTemplateNames", "Updates Login text templates to new names", (session) => {
        var templateModule = session.Context.App.GetModule<TemplateModule>();
        if (templateModule == null)
            return; 
        var templates = session.GetEntities<ITextTemplate>(take: 100);
        foreach (var t in templates) {
          string newName;
          if (templateRenamingMap.TryGetValue(t.Name.Trim(), out newName))
            t.Name = newName; 
        }//foreach
        session.SaveChanges(); 
      });
    }

  }//class
}//ns
