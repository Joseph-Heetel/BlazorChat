using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace BlazorChat.Client.Components.Forms
{
    /// <summary>
    /// Schema specific value types.
    /// </summary>
    /// <remarks>Unlike normal json types, a distinction is made between integers and floating point values</remarks>
    public enum ESchemaValueKind
    {
        Null,
        Boolean,
        Integer,
        Number,
        String,
        Object,
        Array,
    }

    /// <summary>
    /// Helper struct to combine success status and errorhint list into one return type.
    /// </summary>
    public struct ValidationResult
    {
        public bool IsSuccess { get; set; }
        public List<string> ErrorHints { get; }

        public ValidationResult()
        {
            IsSuccess = true;
            ErrorHints = new List<string>();
        }

        public void Merge(ValidationResult other)
        {
            IsSuccess &= other.IsSuccess;
            ErrorHints.AddRange(other.ErrorHints);
        }
    }

    /// <summary>
    /// Representing a schema describing and validating a json node
    /// </summary>
    public class JsonSchemaNode
    {
        [JsonIgnore]
        public bool IsOptional { get; set; }
        [JsonIgnore]
        public string[] Path { get; set; }
        [JsonIgnore]
        public string PathSection
        {
            get
            {
                if (Path != null && Path.Length > 0)
                {
                    return Path[Path.Length - 1];
                }
                return "";
            }
        }
        [JsonIgnore]
        public Dictionary<string, JsonSchemaNode> Properties { get; set; }
        [JsonPropertyName("title")]
        public string? Title { get; set; }
        [JsonPropertyName("description")]
        public string? Description { get; set; }
        [JsonIgnore]
        public Dictionary<string, ISchemaConstraint> Constraints { get; set; }

        public JsonSchemaNode()
        {
            Path = Array.Empty<string>();
            Title = null;
            Description = null;
            Properties = new Dictionary<string, JsonSchemaNode>();
            Constraints = new Dictionary<string, ISchemaConstraint>();
        }

        public void Init(JsonElement element, SchemaConstraintParser parser, bool optional, string[]? path = null)
        {
            IsOptional = optional;
            Path = path ?? Array.Empty<string>();
            foreach (var property in element.EnumerateObject())
            {
                if (parser.TryParse(property, out var constraint))
                {
                    Constraints.Add(property.Name, constraint!);
                }
            }
            if (element.TryGetProperty("properties", out JsonElement properties) && properties.ValueKind == JsonValueKind.Object)
            {
                HashSet<string> requiredProperties = new HashSet<string>();
                if (Constraints.TryGetValue(ObjectRequiredConstraint.Id, out var constraint))
                {
                    foreach (var required in (constraint as ObjectRequiredConstraint)!.Values)
                    {
                        requiredProperties.Add(required);
                    }
                }
                foreach (var property in properties.EnumerateObject())
                {
                    JsonSchemaNode? schema = property.Value.Deserialize<JsonSchemaNode>();
                    if (schema != null)
                    {
                        string[] childpath = Path.Append(property.Name).ToArray();
                        bool childoptional = !(requiredProperties.Contains(property.Name));
                        schema.Init(property.Value, parser, childoptional, childpath);
                        Properties.Add(property.Name, schema);
                    }
                }
            }
        }

        public ValidationResult ValidateSelf(JsonNode? node)
        {
            ValidationResult result = new ValidationResult();
            foreach (var constraint in Constraints.Values)
            {
                result.IsSuccess &= constraint.IsValid(node, result.ErrorHints);
            }
            return result;
        }

        public ValidationResult ValidateRecursively(JsonNode? node)
        {
            ValidationResult result = new ValidationResult();
            foreach (var constraint in Constraints.Values)
            {
                result.IsSuccess &= constraint.IsValid(node, result.ErrorHints);
            }
            JsonObject? obj = node as JsonObject;
            if (obj != null)
            {
                foreach (var propertyname in Properties.Keys)
                {
                    var childresult = Properties[propertyname].ValidateRecursively(obj[propertyname]);
                    result.Merge(childresult);
                }
            }
            return result;
        }

        /// <summary>
        /// Recursively generates json object nodes to be able to store a json structure described by this schema.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException">Schema has no entries in JsonSchemaNode.Properties</exception>
        public JsonNode Generate()
        {
            if (Properties.Count == 0)
            {
                throw new InvalidOperationException("Not an object");
            }
            JsonObject jsonobj = new JsonObject();

            foreach (var property in Properties)
            {
                if (property.Value.Properties.Count > 0 && !property.Value.IsOptional)
                {
                    jsonobj[property.Key] = property.Value.Generate();
                }
                else
                {
                    // Assigning with null pre-generates the entry
                    jsonobj[property.Key] = null;
                }
            }

            return jsonobj;
        }

        private static readonly HashSet<ESchemaValueKind> s_strings = new HashSet<ESchemaValueKind>() { ESchemaValueKind.String };
        private static readonly HashSet<ESchemaValueKind> s_nulls = new HashSet<ESchemaValueKind>() { ESchemaValueKind.Null };
        public IReadOnlySet<ESchemaValueKind> GetTypeConstraint()
        {

            if (Constraints.TryGetValue(TypeConstraint.Id, out var constraint))
            {
                return (constraint as TypeConstraint)!.ValidTypes;
            }
            if (Constraints.TryGetValue(EnumConstraint.Id, out constraint))
            {

                return s_strings;
            }
            return s_nulls;
        }
    }
}