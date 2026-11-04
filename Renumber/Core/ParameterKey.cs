using Autodesk.Revit.DB;
using Newtonsoft.Json;

namespace Renumber.Core
{
    /// <summary>
    /// Identifies a parameter the way that survives a save: built-in parameters by their
    /// enum value, shared parameters by GUID, the rest (project and family parameters)
    /// by name. Names alone are not enough — "Mark" is localised, and two shared
    /// parameters can share a name.
    /// </summary>
    public sealed class ParameterKey
    {
        public string Name { get; set; }
        public int? BuiltIn { get; set; }
        public string Guid { get; set; }

        public static ParameterKey Mark => new ParameterKey
        {
            Name = "Mark",
            BuiltIn = (int)BuiltInParameter.ALL_MODEL_MARK,
        };

        [JsonIgnore]
        public string Id => BuiltIn != null ? "bip:" + BuiltIn
                          : Guid != null ? "guid:" + Guid
                          : "name:" + Name;

        [JsonIgnore]
        public bool IsMark => BuiltIn == (int)BuiltInParameter.ALL_MODEL_MARK;

        public static ParameterKey From(Parameter parameter)
        {
            var key = new ParameterKey { Name = parameter.Definition.Name };

            if (parameter.Definition is InternalDefinition definition
                && definition.BuiltInParameter != BuiltInParameter.INVALID)
                key.BuiltIn = (int)definition.BuiltInParameter;
            else if (parameter.IsShared)
                key.Guid = parameter.GUID.ToString();

            return key;
        }

        public Parameter Find(Element element)
        {
            if (BuiltIn != null)
                return element.get_Parameter((BuiltInParameter)BuiltIn.Value);
            if (Guid != null && System.Guid.TryParse(Guid, out System.Guid guid))
                return element.get_Parameter(guid);
            return element.LookupParameter(Name);
        }
    }
}
