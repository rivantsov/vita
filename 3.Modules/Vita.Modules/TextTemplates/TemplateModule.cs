using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Entities;

namespace Vita.Modules.TextTemplates {
  public partial class TemplateModule : EntityModule, ITemplateTransformService {
    public static readonly Version CurrentVersion = new Version("1.1.0.0");
    TemplateModuleSettings _settings;

    public TemplateModule(EntityArea area, TemplateModuleSettings settings = null) : base(area, "TemplateModule", version: CurrentVersion) {
      _settings = settings ?? new TemplateModuleSettings();
      RegisterEntities(typeof(ITextTemplate));
      App.RegisterService<ITemplateTransformService>(this); 
    }

    #region ITemplateTransformService members
    public ITextTemplate GetTemplate(IEntitySession session, string templateName, string culture = "EN-US", Guid? ownerId = null) {
      var where = session.NewPredicate<ITextTemplate>()
        .And(t => t.Name == templateName)
        .AndIfNotEmpty(culture, t => t.Culture == culture)
        .AndIfNotEmpty(ownerId, t => t.OwnerId == ownerId.Value);
      var template = session.EntitySet<ITextTemplate>().Where(where).FirstOrDefault();
      return template;
    }

    public string Transform(ITextTemplate template, IDictionary<string, object> data) {
      var engine = _settings.GetEngine(template.Engine); //will throw if not found
      var result = engine.Transform(template.Template, template.Format, data);
      return result;
    }
    #endregion
  }
}
