using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace BlazorChat.Client.Components.Forms
{
    // https://github.com/dotnet/runtime/issues/64472
    // TL;DR: Json Nodes suck, because they're strongly typed internally (which does not function well with Json, which is not strongly typed)
    // This makes it impossible to reliably get a value representation via Node.GetValue<>(). Therefore we work with .ToJsonString().
    // 
    // That is not very efficient, but it works

    /// <summary>
    /// Represents a constraint which can be used to validate a json node
    /// </summary>
    public interface ISchemaConstraint
    {
        void Init(JsonElement element);
        /// <summary>
        /// Validate <paramref name="value"/> Json Node. Appends an error to <paramref name="errorHints"/> if validation fails.
        /// </summary>
        /// <returns>Returns true if validation succeeds, false otherwise</returns>
        bool IsValid(JsonNode? value, List<string> errorHints);
    }

    /// <summary>
    /// Helper class managing schema constraint mappings for parsing. Extend it by pushing / overwriting values in ConstraintTypes member
    /// </summary>
    public class SchemaConstraintParser
    {
        public readonly Dictionary<string, Type> ConstraintTypes = new Dictionary<string, Type>()
        {
            {EnumConstraint.Id, typeof(EnumConstraint) },
            {TypeConstraint.Id, typeof(TypeConstraint) },
            {StringMaxLengthConstraint.Id, typeof(StringMaxLengthConstraint) },
            {StringMinLengthConstraint.Id, typeof(StringMinLengthConstraint) },
            {StringPatternConstraint.Id, typeof(StringPatternConstraint) },
            {NumberMultipleOfConstraint.Id, typeof(NumberMultipleOfConstraint) },
            {NumberMinimumConstraint.Id, typeof(NumberMinimumConstraint) },
            {NumberExclusiveMinimumConstraint.Id, typeof(NumberExclusiveMinimumConstraint) },
            {NumberMaximumConstraint.Id, typeof(NumberMaximumConstraint) },
            {NumberExclusiveMaximumConstraint.Id, typeof(NumberExclusiveMaximumConstraint) },
            {ObjectRequiredConstraint.Id, typeof(ObjectRequiredConstraint) },
            {ObjectMinPropertiesConstraint.Id, typeof(ObjectMinPropertiesConstraint) },
            {ObjectMaxPropertiesConstraint.Id, typeof(ObjectMaxPropertiesConstraint) },
            {ObjectDependentRequiredConstraint.Id, typeof(ObjectDependentRequiredConstraint) },
        };

        public bool TryParse(JsonProperty property, out ISchemaConstraint? value)
        {
            if (ConstraintTypes.TryGetValue(property.Name, out Type? type))
            {
                var constructor = type.GetConstructor(BindingFlags.Instance | BindingFlags.Public, Array.Empty<Type>());
                if (constructor != null)
                {
                    value = constructor.Invoke(Array.Empty<object?>()) as ISchemaConstraint;
                    if (value != null)
                    {
                        try
                        {
                            value.Init(property.Value);
                            return true;
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"Failed to init [{property.Name}], Value: {property.Value}: {e.Message}");
                        }
                    }
                }
            }
            value = null;
            return false;
        }
    }
    /// <summary>
    /// https://json-schema.org/understanding-json-schema/reference/generic.html#enumerated-values
    /// </summary>
    public class EnumConstraint : ISchemaConstraint
    {
        internal const string Id = "enum";
        public void Init(JsonElement value) { Values = value.EnumerateArray().Select(x => x.GetRawText()).ToArray(); }
        public string[] Values { get; set; } = Array.Empty<string>();
        public bool IsValid(JsonNode? value, List<string> errorHints)
        {
            string json = value?.ToString() ?? "null";
            foreach (var item in Values)
            {
                if (item == json)
                {
                    return true;
                }
            }
            errorHints.Add($"[{Id}] Value \"{json}\" may only be one of [{string.Join(", ", Values)}]");
            return false;
        }
    }


    public class TypeConstraint : ISchemaConstraint
    {
        public const string Id = "type";
        private static readonly Dictionary<string, ESchemaValueKind> s_strToValueKind = new Dictionary<string, ESchemaValueKind>()
        {
            {"null", ESchemaValueKind.Null },
            {"boolean", ESchemaValueKind.Boolean },
            {"integer", ESchemaValueKind.Integer},
            {"number", ESchemaValueKind.Number },
            {"string", ESchemaValueKind.String},
            {"object", ESchemaValueKind.Object},
            {"array", ESchemaValueKind.Array},
        };

        public void Init(JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.String)
            {
                ValidTypes.Add(s_strToValueKind[value.ToString()]);
            }
            else if (value.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in value.EnumerateArray())
                {
                    ValidTypes.Add(s_strToValueKind[element.ToString()]);
                }
            }
        }
        public HashSet<ESchemaValueKind> ValidTypes { get; set; } = new HashSet<ESchemaValueKind>();

        public bool IsValid(JsonNode? node, List<string> errorHints)
        {
            HashSet<ESchemaValueKind> representingTypes = new HashSet<ESchemaValueKind>();
            if (node == null)
            {
                representingTypes.Add(ESchemaValueKind.Null);
            }
            if (node is JsonValue value)
            {
                if (value.TryGetValue<double>(out double number))
                {
                    representingTypes.Add(ESchemaValueKind.Number);
                    if (number == (double)(long)number)
                    {
                        representingTypes.Add(ESchemaValueKind.Integer);
                    }
                }
                if (value.TryGetValue(out string? str))
                {
                    if (string.IsNullOrEmpty(str))
                    {
                        representingTypes.Add(ESchemaValueKind.Null);
                    }
                    else
                    {
                        representingTypes.Add(ESchemaValueKind.String);
                    }
                }
                if (value.TryGetValue(out bool _))
                {
                    representingTypes.Add(ESchemaValueKind.Boolean);
                }
            }
            else if (node is JsonObject)
            {
                representingTypes.Add(ESchemaValueKind.Object);
            }
            else if (node is JsonArray)
            {
                representingTypes.Add(ESchemaValueKind.Array);
            }
            bool match = representingTypes.Intersect(ValidTypes).Any();
            if (!match)
            {
                errorHints.Add($"[{Id}] Value \"{node?.ToJsonString() ?? "null"}\" must be of type {JsonSerializer.Serialize(ValidTypes.Select(e => e.ToString()))}");
            }
            return match;
        }
    }

    public class StringMaxLengthConstraint : ISchemaConstraint
    {
        public const string Id = "maxLength";
        public void Init(JsonElement value) { MaxLength = value.GetInt32(); }
        public int MaxLength { get; set; }
        public bool IsValid(JsonNode? value, List<string> errorHints)
        {
            string str = value?.GetValue<string>() ?? string.Empty;
            if (str.Length > MaxLength)
            {
                errorHints.Add($"[{Id}] Value.Length {str.Length} is longer than maximum allowed {MaxLength}");
                return false;
            }
            return true;
        }
    }
    public class StringMinLengthConstraint : ISchemaConstraint
    {
        public const string Id = "minLength";
        public void Init(JsonElement value) { MinLength = value.GetInt32(); }
        public int MinLength { get; set; }
        public bool IsValid(JsonNode? value, List<string> errorHints)
        {
            string str = value?.GetValue<string>() ?? string.Empty;
            if (str.Length < MinLength)
            {
                errorHints.Add($"[{Id}] Value.Length {str?.Length ?? 0} is less than minimum required {MinLength}");
                return false;
            }
            return true;
        }
    }
    public class StringPatternConstraint : ISchemaConstraint
    {
        public const string Id = "pattern";
        public void Init(JsonElement value)
        {
            //Console.WriteLine($"Initialize regex with \"{value.GetString()}\"")
            Pattern = new Regex(value.GetString() ?? "^$");
        }
        public Regex Pattern { get; set; } = new Regex("^$");
        public bool IsValid(JsonNode? value, List<string> errorHints)
        {
            string str = value?.GetValue<string?>() ?? string.Empty;
            var match = Pattern.Match(str);
            if (!match.Success || match.Value.Length != str.Length)
            {
                errorHints.Add($"[{Id}] Value does not match pattern \"{Pattern}\"");
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// https://json-schema.org/understanding-json-schema/reference/numeric.html
    /// </summary>
    public class NumberMultipleOfConstraint : ISchemaConstraint
    {
        public const string Id = "multipleOf";
        public void Init(JsonElement value) { Base = (decimal)value.GetDouble(); }
        public decimal Base;

        public bool IsValid(JsonNode? value, List<string> errorHints)
        {
            decimal num = (decimal)(value?.GetValue<double>() ?? 0);
            if (num <= 0 || num % Base != 0)
            {
                errorHints.Add($"[{Id}] Value {num} needs to be bigger than 0 and a mulitple of {Base}");
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// https://json-schema.org/understanding-json-schema/reference/numeric.html
    /// </summary>
    public class NumberMaximumConstraint : ISchemaConstraint
    {
        internal const string Id = "maximum";
        public void Init(JsonElement value) { Max = value.GetDouble(); }
        public double Max;

        public bool IsValid(JsonNode? value, List<string> errorHints)
        {
            double num = value?.GetValue<double>() ?? 0;
            if (num > Max)
            {
                errorHints.Add($"[{Id}] Value needs to smaller or equal {Max}");
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// https://json-schema.org/understanding-json-schema/reference/numeric.html
    /// </summary>
    public class NumberMinimumConstraint : ISchemaConstraint
    {
        internal const string Id = "minimum";
        public void Init(JsonElement value) { Min = value.GetDouble(); }
        public double Min;

        public bool IsValid(JsonNode? value, List<string> errorHints)
        {
            double num = value?.GetValue<double>() ?? 0;
            if (num < Min)
            {
                errorHints.Add($"[{Id}] Value needs to greater or equal {Min}");
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// https://json-schema.org/understanding-json-schema/reference/numeric.html
    /// </summary>
    public class NumberExclusiveMaximumConstraint : ISchemaConstraint
    {
        internal const string Id = "exclusiveMaximum";
        public void Init(JsonElement value) { Max = value.GetDouble(); }
        public double Max;

        public bool IsValid(JsonNode? value, List<string> errorHints)
        {
            double num = value?.GetValue<double>() ?? 0;
            if (num >= Max)
            {
                errorHints.Add($"[{Id}] Value needs to smaller than {Max}");
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// https://json-schema.org/understanding-json-schema/reference/numeric.html
    /// </summary>
    public class NumberExclusiveMinimumConstraint : ISchemaConstraint
    {
        internal const string Id = "exclusiveMinimum";
        public void Init(JsonElement value) { Min = value.GetDouble(); }
        public double Min;

        public bool IsValid(JsonNode? value, List<string> errorHints)
        {
            double num = value?.GetValue<double>() ?? 0;
            if (num <= Min)
            {
                errorHints.Add($"[{Id}]Value needs to greater than {Min}");
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// https://json-schema.org/understanding-json-schema/reference/object.html#required-properties
    /// </summary>
    public class ObjectRequiredConstraint : ISchemaConstraint
    {
        internal const string Id = "required";
        public void Init(JsonElement value) { Values = value.EnumerateArray().Select(x => x.ToString()).ToArray(); }
        public string[] Values { get; set; } = Array.Empty<string>();
        public bool IsValid(JsonNode? value, List<string> errorHints)
        {
            JsonObject? element = value as JsonObject;
            if (element == null)
            {
                errorHints.Add($"[{Id}] Expected object");
                return false;
            }
            List<string> notfound = new List<string>();
            foreach (var property in Values)
            {
                if (!element.ContainsKey(property))
                {
                    notfound.Add(property);
                }
            }
            if (notfound.Count > 0)
            {
                errorHints.Add($"[{Id}] Missing properties {JsonSerializer.Serialize(notfound)}");
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// https://json-schema.org/understanding-json-schema/reference/object.html#required-properties
    /// </summary>
    public class ObjectMinPropertiesConstraint : ISchemaConstraint
    {
        internal const string Id = "minProperties";
        public void Init(JsonElement element)
        {
            Min = element.GetInt32();
        }
        int Min { get; set; }

        public bool IsValid(JsonNode? value, List<string> errorHints)
        {
            JsonObject? element = value as JsonObject;
            if (element == null)
            {
                errorHints.Add($"[{Id}] Expected object");
                return false;
            }
            int count = element.Count;
            if (count < Min)
            {
                errorHints.Add($"[{Id}] Minimum property count {Min} not met");
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// https://json-schema.org/understanding-json-schema/reference/object.html#required-properties
    /// </summary>
    public class ObjectMaxPropertiesConstraint : ISchemaConstraint
    {
        internal const string Id = "maxProperties";
        public void Init(JsonElement element)
        {
            Max = element.GetInt32();
        }
        int Max { get; set; }

        public bool IsValid(JsonNode? value, List<string> errorHints)
        {
            JsonObject? element = value as JsonObject;
            if (element == null)
            {
                errorHints.Add($"[{Id}] Expected object");
                return false;
            }
            int count = element.Count;
            if (count > Max)
            {
                errorHints.Add($"[{Id}] Maximum property count {Max} not met");
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class ObjectDependentRequiredConstraint : ISchemaConstraint
    {
        internal const string Id = "dependentRequired";
        public void Init(JsonElement element)
        {
            foreach (var property in element.EnumerateObject())
            {
                var dependencies = property.Value.EnumerateArray().Select(x => x.GetString()).ToArray() as string[];
                Dependencies.Add(property.Name, dependencies);
            }
        }
        public Dictionary<string, string[]> Dependencies = new Dictionary<string, string[]>();

        public bool IsValid(JsonNode? value, List<string> errorHints)
        {
            JsonObject? element = value as JsonObject;
            if (element == null)
            {
                errorHints.Add($"[{Id}] Expected object");
                return false;
            }
            bool success = true;
            foreach (var propertyname in Dependencies.Keys)
            {
                var dependencies = Dependencies[propertyname];
                if (element.ContainsKey(propertyname))
                {
                    foreach (var dependency in dependencies)
                    {
                        if (!element.ContainsKey(propertyname))
                        {
                            errorHints.Add($"[{Id}] Property {propertyname} requires properties {JsonSerializer.Serialize(dependencies)} to be set");
                            success = false;
                        }
                    }
                }
            }
            return success;
        }
    }
}