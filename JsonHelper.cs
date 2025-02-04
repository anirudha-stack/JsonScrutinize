using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace JsonScrutinize
{
    /// <summary>
    /// Represents a mismatch between a JSON property and the expected type.
    /// </summary>
    public class KeyUnderCheck
    {
        public string KeyName { get; set; }
        public Type ExpectedType { get; set; }
        public Type ActualType { get; set; }
    }

    public class KeyTypeScrutinizeResult
    {
        public List<string> MismatchedKeys { get; set; }
        public string StatusCode { get; set; }
        public string Description { get; set; }
    }

    public class KeyNullScrutinizeResult
    {
        public List<string> NullKeys { get; set; }
        public string StatusCode { get; set; }
        public string Description { get; set; }
    }
    public class KeyMissingScrutinizeResult
    {
        public List<string> MissingKeys { get; set; }
        public string StatusCode { get; set; }
        public string Description { get; set; }
    }

    public class JsonHelper
    {
        #region Constants

        private const string JsonEmptyErrorCode = "E6001";
        private const string TypeNullErrorCode = "E6002";
        private const string SomethingWentWrongErrorCode = "E6005";
        private const string SuccessCode = "S200";
        private const string MismatchedKeysFoundCode = "S201";
        private const string NullKeysFoundCode = "S202";
        private const string MissingKeysFoundCode = "S203";

        #endregion


        #region TypeScrutinize

        /// <summary>
        /// Checks the provided JSON against the comparison type for type mismatches or regex mismatches.
        /// Returns a list of strings where each string is either a property path with a mismatch or a success/error code.
        /// </summary>
        /// <param name="jsonToCheck">The JSON string to validate.</param>
        /// <param name="comparisonType">The type against which the JSON is validated.</param>
        /// <returns>A Task that resolves to a list of mismatch keys or an error/success code.</returns>
        public Task<KeyTypeScrutinizeResult> KeyTypeScrutinize(string jsonToCheck, Type comparisonType)
        {
            KeyTypeScrutinizeResult response = new KeyTypeScrutinizeResult();


            if (string.IsNullOrWhiteSpace(jsonToCheck))
            {
               response = new KeyTypeScrutinizeResult();
                response.StatusCode = JsonEmptyErrorCode;
                response.Description = "JSON cannot be empty or null";
                return Task.FromResult(response);
               
            }
            if (comparisonType == null)
            {
                response = new KeyTypeScrutinizeResult();
                response.StatusCode = JsonEmptyErrorCode;
                response.Description = "Type to compare cannot be null";
                return Task.FromResult(response);
              

            }

            try
            {
                JObject jsonObject;
                try
                {
                    jsonObject = JObject.Parse(jsonToCheck);
                }
                catch (JsonReaderException ex)
                {
                    response = new KeyTypeScrutinizeResult();
                    response.StatusCode = SomethingWentWrongErrorCode;
                    response.Description = ex.Message;
                    return Task.FromResult(response);
                }

                List<KeyUnderCheck> mismatches = GetMismatchedKeysInNestedObjects(jsonObject, comparisonType);
                var mismatchKeys = mismatches.Select(m => m.KeyName).ToList();

                if (mismatchKeys.Any())
                {
                    response.StatusCode = MismatchedKeysFoundCode;
                    response.Description = $"Found {mismatches.Count()} mismatched keys in the provided JSON";
                    response.MismatchedKeys = mismatchKeys;
                    return Task.FromResult(response);

                  
                }
                else
                {
                    response = new KeyTypeScrutinizeResult();
                    response.StatusCode = SuccessCode;
                    response.Description = $"No mismatched keys in the provided JSON";
                    
                    return Task.FromResult(response);
                }
            }
            catch (Exception ex)
            {
                response = new KeyTypeScrutinizeResult();
                response.StatusCode = SomethingWentWrongErrorCode;
                response.Description = ex.Message;
                return Task.FromResult(response);
            }
        }

        /// <summary>
        /// Recursively checks the JSON object against the provided class type and collects any type or regex mismatches.
        /// </summary>
        /// <param name="jsonObject">The JSON object to check.</param>
        /// <param name="classType">The expected type for comparison.</param>
        /// <param name="currentPath">The JSON path (used for nested properties).</param>
        /// <returns>A list of mismatched keys and their expected/actual types.</returns>
        public static List<KeyUnderCheck> GetMismatchedKeysInNestedObjects(JObject jsonObject, Type classType, string currentPath = "")
        {
            var mismatches = new List<KeyUnderCheck>();

            foreach (var property in jsonObject.Properties())
            {
                string propertyPath = string.IsNullOrEmpty(currentPath)
                    ? property.Name
                    : $"{currentPath}.{property.Name}";

                Type expectedPropType = GetPropertyType(classType, property.Name);
                bool regexCheckPerformed = false;

                // Check for regex and required attribute if available on the property.
                if (classType != null)
                {
                    var propertyInfo = classType.GetProperty(property.Name);
                    if (propertyInfo != null)
                    {
                        var regexAttr = propertyInfo.GetCustomAttribute<RegularExpressionAttribute>();
                        if (regexAttr != null)
                        {
                            var propertyValue = property.Value?.ToString();
                            if (!string.IsNullOrEmpty(propertyValue))
                            {
                                if (!Regex.IsMatch(propertyValue, regexAttr.Pattern) || propertyValue == "{}")
                                {
                                    mismatches.Add(new KeyUnderCheck
                                    {
                                        KeyName = propertyPath,
                                        ExpectedType = expectedPropType,
                                        ActualType = GetJTokenType(property.Value.Type)
                                    });
                                    regexCheckPerformed = true;
                                }
                            }
                            else if (propertyInfo.GetCustomAttribute<RequiredAttribute>() != null)
                            {
                                mismatches.Add(new KeyUnderCheck
                                {
                                    KeyName = propertyPath,
                                    ExpectedType = expectedPropType,
                                    ActualType = GetJTokenType(property.Value.Type)
                                });
                                regexCheckPerformed = true;
                            }
                        }
                    }
                }

                // Process based on the JSON token type.
                switch (property.Value.Type)
                {
                    case JTokenType.Object:
                        mismatches.AddRange(GetMismatchedKeysInNestedObjects((JObject)property.Value, expectedPropType, propertyPath));
                        break;

                    case JTokenType.Array:
                        var array = (JArray)property.Value;
                        if (IsTypeICollection(expectedPropType) || IsTypeIEnumerable(expectedPropType))
                        {
                            Type elementType = expectedPropType?.IsArray == true
                                ? expectedPropType.GetElementType()
                                : expectedPropType?.GetGenericArguments().FirstOrDefault() ?? typeof(object);

                            for (int i = 0; i < array.Count; i++)
                            {
                                string itemPath = $"{propertyPath}[{i}]";
                                if (array[i].Type == JTokenType.Object)
                                {
                                    mismatches.AddRange(GetMismatchedKeysInNestedObjects((JObject)array[i], elementType, itemPath));
                                }
                                else if (array[i].Type != JTokenType.Null)
                                {
                                    var itemActualType = GetJTokenType(array[i].Type);
                                    if (elementType != itemActualType && !regexCheckPerformed)
                                    {
                                        mismatches.Add(new KeyUnderCheck
                                        {
                                            KeyName = itemPath,
                                            ExpectedType = elementType,
                                            ActualType = itemActualType
                                        });
                                    }
                                }
                            }
                        }
                        break;

                    case JTokenType.Null:
                        {
                            var actualType = GetJTokenType(property.Value.Type);
                            if (expectedPropType != actualType && !regexCheckPerformed)
                            {
                                mismatches.Add(new KeyUnderCheck
                                {
                                    KeyName = propertyPath,
                                    ExpectedType = expectedPropType,
                                    ActualType = actualType
                                });
                            }
                        }
                        break;

                    default:
                        {
                            var actualType = GetJTokenType(property.Value.Type);
                            if (expectedPropType != actualType && !regexCheckPerformed)
                            {
                                mismatches.Add(new KeyUnderCheck
                                {
                                    KeyName = propertyPath,
                                    ExpectedType = expectedPropType,
                                    ActualType = actualType
                                });
                            }
                        }
                        break;
                }
            }

            return mismatches;
        }

        #endregion

        #region NullValue and MissingKey Scrutinize
        /// <summary>
        /// Inspects the provided object by serializing it to JSON and checking for:
        /// 1. Keys whose values are null or undefined.
        /// 2. Missing required keys (based on [Required] attributes of the expected type).
        /// 
        /// Returns a Task that resolves to a list of error messages prefixed with a specific error code if issues are found,
        /// or returns a list containing a success code if no issues are found.
        /// 
        /// Note: The parameters 'guid', 'source', and 'ipaddress' are currently unused and reserved
        /// for future enhancements.
        /// </summary>
        /// <param name="jsonToCheck">The JSON String to be validated.</param>
        /// <param name="comparisonType">
        /// The expected type whose properties (especially those marked as [Required]) will be used
        /// to verify the JSON structure.
        /// </param>
        /// <param name="optionalProperties">
        /// List of property names to ignore when checking for null values.
        /// </param>
        /// <returns>
        /// A Task that resolves to a list of error strings if any null keys are found; otherwise, a list with the success code.
        /// </returns>
        public Task<KeyNullScrutinizeResult> KeyNullScrutinize(string jsonToCheck, Type comparisonType, params string[] optionalProperties)
        {
            KeyNullScrutinizeResult response = new KeyNullScrutinizeResult();


            if (string.IsNullOrWhiteSpace(jsonToCheck))
            {
                response = new KeyNullScrutinizeResult();
                response.StatusCode = JsonEmptyErrorCode;
                response.Description = "JSON cannot be empty or null";
                return Task.FromResult(response);

            }
            if (comparisonType == null)
            {
                response = new KeyNullScrutinizeResult();
                response.StatusCode = JsonEmptyErrorCode;
                response.Description = "Type to compare cannot be null";
                return Task.FromResult(response);


            }

            try
            {


                // Parse the JSON string into a JObject for easier traversal.
                JObject jsonObject;
                try
                {
                    jsonObject = JObject.Parse(jsonToCheck);
                }
                catch (JsonReaderException ex)
                {
                    response = new KeyNullScrutinizeResult();
                    response.StatusCode = SomethingWentWrongErrorCode;
                    response.Description = ex.Message;
                    return Task.FromResult(response);
                }

                // Inspect the JSON for keys with null or undefined values.
                List<string> nullKeysList = GetNullKeysInNestedObjects(jsonObject, optionalProperties);

                if (nullKeysList.Any())
                {
                    // If any null keys are found, insert an error code at the beginning.

                    response.StatusCode = NullKeysFoundCode;
                    response.Description = $"Found {nullKeysList.Count()} NULL keys in the provided JSON";
                    response.NullKeys = nullKeysList;
                    return Task.FromResult(response);

                 

                }
                else
                {
                    // No issues found; return the success code.

                    response.StatusCode = SuccessCode;
                    response.Description = $"NULL keys not in the provided JSON";
                   
                    return Task.FromResult(response);
                }
            }
            catch (JsonReaderException ex)
            {
                response = new KeyNullScrutinizeResult();
                response.StatusCode = JsonEmptyErrorCode;
                response.Description = ex.Message;
                return Task.FromResult(response);
            }
            catch (Exception ex)
            {
                response = new KeyNullScrutinizeResult();
                response.StatusCode = SomethingWentWrongErrorCode;
                response.Description = ex.Message;
                return Task.FromResult(response);
                
            }
        }

        /// <summary>
        /// Inspects the provided object by serializing it to JSON and checking for missing required keys,
        /// based on the [Required] attributes in the standard type.
        /// 
        /// Returns a Task that resolves to a list of error messages prefixed with a specific error code if missing keys are found;
        /// otherwise, returns a list with the success code.
        /// 
        /// Note: The parameters 'guid', 'source', and 'ipaddress' are currently unused and reserved for future enhancements.
        /// </summary>
        /// <param name="objectToCheck">The object to be validated.</param>
        /// <param name="standardObjectType">
        /// The expected type whose properties (especially those marked as [Required]) will be used
        /// to verify the JSON structure.
        /// </param>
        /// <param name="optionalProperties">
        /// List of property names to ignore when checking for missing keys.
        /// </param>
        /// <returns>
        /// A Task that resolves to a list of error strings if any missing required keys are found;
        /// otherwise, a list containing the success code.
        /// </returns>
        public Task<KeyMissingScrutinizeResult> KeyMissingScrutinize(string jsonToCheck, Type comparisonType, params string[] optionalProperties)
        {
            KeyMissingScrutinizeResult response = new KeyMissingScrutinizeResult();


            if (string.IsNullOrWhiteSpace(jsonToCheck))
            {
                response = new KeyMissingScrutinizeResult();
                response.StatusCode = JsonEmptyErrorCode;
                response.Description = "JSON cannot be empty or null";
                return Task.FromResult(response);

            }
            if (comparisonType == null)
            {
                response = new KeyMissingScrutinizeResult();
                response.StatusCode = JsonEmptyErrorCode;
                response.Description = "Type to compare cannot be null";
                return Task.FromResult(response);


            }
            try
            {
                JObject jsonObject;
                try
                {
                    jsonObject = JObject.Parse(jsonToCheck);
                }
                catch (JsonReaderException ex)
                {
                    response = new KeyMissingScrutinizeResult();
                    response.StatusCode = SomethingWentWrongErrorCode;
                    response.Description = ex.Message;
                    return Task.FromResult(response);
                }

                // Check for missing required keys based on the standard type.
                List<string> missingKeys = GetMissingKeysInNestedObjects(jsonObject, comparisonType);
                if (missingKeys.Any())
                {
                    response.StatusCode = MissingKeysFoundCode;
                    response.Description = $"Found {missingKeys.Count()} Missing keys in the provided JSON";
                    response.MissingKeys = missingKeys;
                    return Task.FromResult(response);

                }
                else
                {
                    // No missing keys found; return the success code.
                    
                    response.StatusCode = SuccessCode;
                    response.Description = $"Missing keys not in the provided JSON";

                    return Task.FromResult(response);
                }
            }
            catch (JsonReaderException ex)
            {
                response = new KeyMissingScrutinizeResult();
                response.StatusCode = JsonEmptyErrorCode;
                response.Description = ex.Message;
                return Task.FromResult(response);
            }
            catch (Exception ex)
            {
                response = new KeyMissingScrutinizeResult();
                response.StatusCode = SomethingWentWrongErrorCode;
                response.Description = ex.Message;
                return Task.FromResult(response);

            }
        }





        /// <summary>
        /// Recursively collects missing required keys from a JSON object by comparing it against
        /// the expected type's properties. A key is considered missing if:
        ///   - The property is marked with [Required] on the expected type, and
        ///   - The JSON object does not contain that property.
        /// 
        /// This method processes nested objects and arrays, building the property path along the way.
        /// </summary>
        /// <param name="jsonObject">The JSON object to inspect.</param>
        /// <param name="standardObjectType">
        /// The expected type whose properties (with [Required] attributes) will be checked.
        /// </param>
        /// <param name="parentProperty">
        /// The current property path prefix for nested objects (optional).
        /// </param>
        /// <param name="arrayIndex">
        /// If processing an element in an array, this is its index; otherwise, -1.
        /// </param>
        /// <returns>A list of missing key paths.</returns>
        public List<string> GetMissingKeysInNestedObjects(JObject jsonObject, Type standardObjectType, string parentProperty = null, int arrayIndex = -1)
        {
            // Initialize an empty list to accumulate missing keys.
            List<string> missingKeys = new List<string>();

            // If either the JSON object or the expected type is null, there is nothing to check.
            if (jsonObject == null || standardObjectType == null)
            {
                return missingKeys;
            }

            // Build the property path prefix (e.g., "Parent." if a parent property exists).
            string propertyPrefix = string.IsNullOrEmpty(parentProperty) ? string.Empty : $"{parentProperty}.";

            // Retrieve all public properties from the expected type using reflection.
            PropertyInfo[] standardProperties = standardObjectType.GetProperties();

            // Iterate through each expected property.
            foreach (PropertyInfo standardProperty in standardProperties)
            {
                // Check if the property is marked as [Required].
                bool isRequired = Attribute.IsDefined(standardProperty, typeof(RequiredAttribute));

                // If the JSON object does not contain a property that is required, record it.
                if (!jsonObject.ContainsKey(standardProperty.Name) && isRequired)
                {
                    // If processing an array element, append the array index to the key path.
                    string indexSuffix = arrayIndex != -1 ? $"[{arrayIndex}]" : string.Empty;
                    missingKeys.Add($"{propertyPrefix}{standardProperty.Name}{indexSuffix}");
                }
                // If the JSON object contains the property, further inspect its value.
                else if (jsonObject.ContainsKey(standardProperty.Name))
                {
                    JToken jsonValue = jsonObject[standardProperty.Name];

                    // If the property value is an array...
                    if (jsonValue != null && jsonValue.Type == JTokenType.Array)
                    {
                        // If the array is empty and the property is required, mark it as missing.
                        if (!jsonValue.HasValues && isRequired)
                        {
                            string indexSuffix = arrayIndex != -1 ? $"[{arrayIndex}]" : string.Empty;
                            missingKeys.Add($"{propertyPrefix}{standardProperty.Name}{indexSuffix}");
                        }
                        else
                        {
                            // Process each element in the array.
                            int index = 0;
                            foreach (JObject arrayElement in jsonValue.Children<JObject>())
                            {
                                // Determine the type of elements within the array.
                                Type nestedObjectType = GetElementType(standardProperty.PropertyType);
                                // Recursively check the nested JSON object.
                                missingKeys.AddRange(GetMissingKeysInNestedObjects(arrayElement, nestedObjectType, $"{propertyPrefix}{standardProperty.Name}", index));
                                index++;
                            }
                        }
                    }
                    // If the property value is a nested object, process it recursively.
                    else if (jsonValue != null && jsonValue.Type == JTokenType.Object)
                    {
                        Type nestedObjectType = standardProperty.PropertyType;
                        missingKeys.AddRange(GetMissingKeysInNestedObjects((JObject)jsonValue, nestedObjectType, $"{propertyPrefix}{standardProperty.Name}"));
                    }
                }
            }

            return missingKeys;
        }

        /// <summary>
        /// Recursively traverses a JSON object to identify any keys whose values are null or undefined.
        /// Keys listed in the optionalProperties array are excluded from the results.
        /// 
        /// For nested objects and arrays, the full property path is constructed.
        /// </summary>
        /// <param name="jsonObject">The JSON object to inspect.</param>
        /// <param name="optionalProperties">
        /// An array of property names that should be ignored when a null or undefined value is encountered.
        /// </param>
        /// <returns>A list of property paths that have null or undefined values.</returns>
        public List<string> GetNullKeysInNestedObjects(JObject jsonObject, params string[] optionalProperties)
        {
            List<string> nullKeys = new List<string>();

            // Iterate through every property in the current JSON object.
            foreach (var property in jsonObject.Properties())
            {
                // Check if the property value is either null or undefined.
                if (property.Value.Type == JTokenType.Null || property.Value.Type == JTokenType.Undefined)
                {
                    // Only add the property if it is not in the list of optional properties.
                    if (!optionalProperties.Contains(property.Name))
                    {
                        nullKeys.Add(property.Name);
                    }
                }
                // If the property value is a nested object, process it recursively.
                else if (property.Value.Type == JTokenType.Object)
                {
                    var nestedNullKeys = GetNullKeysInNestedObjects((JObject)property.Value, optionalProperties);
                    foreach (var nestedKey in nestedNullKeys)
                    {
                        // Build the full property path (e.g., "Parent.Child").
                        if (!optionalProperties.Contains(property.Name))
                        {
                            nullKeys.Add($"{property.Name}.{nestedKey}");
                        }
                    }
                }
                // If the property value is an array, iterate through each element.
                else if (property.Value.Type == JTokenType.Array)
                {
                    var array = (JArray)property.Value;
                    for (int i = 0; i < array.Count; i++)
                    {
                        // If an array element is a JSON object, process it recursively.
                        if (array[i].Type == JTokenType.Object)
                        {
                            var nestedNullKeys = GetNullKeysInNestedObjects((JObject)array[i], optionalProperties);
                            foreach (var nestedKey in nestedNullKeys)
                            {
                                if (!optionalProperties.Contains(property.Name))
                                {
                                    nullKeys.Add($"{property.Name}[{i}].{nestedKey}");
                                }
                            }
                        }
                        // If the array element itself is null, record its path.
                        else if (array[i].Type == JTokenType.Null)
                        {
                            if (!optionalProperties.Contains(property.Name))
                            {
                                nullKeys.Add($"{property.Name}[{i}]");
                            }
                        }
                    }
                }
            }

            return nullKeys;
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Gets the list of property names for the specified type.
        /// </summary>
        public static List<string> GetPropertyNames(Type type)
        {
            if (type == null)
                return new List<string>();

            return type.GetProperties().Select(prop => prop.Name).ToList();
        }

        /// <summary>
        /// Checks whether the provided type implements ICollection&lt;T&gt;.
        /// </summary>
        public static bool IsTypeICollection(Type type)
        {
            if (type == null)
                return false;

            return type.GetInterfaces().Any(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICollection<>));
        }

        /// <summary>
        /// Checks whether the provided type implements IEnumerable.
        /// </summary>
        public static bool IsTypeIEnumerable(Type type)
        {
            return type != null && typeof(IEnumerable).IsAssignableFrom(type);
        }

        /// <summary>
        /// Maps a Newtonsoft.Json.Linq.JTokenType to a corresponding System.Type.
        /// </summary>
        public static Type GetJTokenType(JTokenType jTokenType)
        {
            return jTokenType switch
            {
                JTokenType.None => typeof(object),
                JTokenType.Object => typeof(JObject),
                JTokenType.Array => typeof(JArray),
                JTokenType.Constructor => typeof(JConstructor),
                JTokenType.Property => typeof(JProperty),
                JTokenType.Comment => typeof(string),
                JTokenType.Integer => typeof(long),
                JTokenType.Float => typeof(double),
                JTokenType.String => typeof(string),
                JTokenType.Boolean => typeof(bool),
                JTokenType.Null => typeof(object),
                JTokenType.Undefined => typeof(object),
                JTokenType.Date => typeof(DateTime),
                JTokenType.Raw => typeof(string),
                JTokenType.Bytes => typeof(byte[]),
                JTokenType.Guid => typeof(Guid),
                JTokenType.Uri => typeof(Uri),
                JTokenType.TimeSpan => typeof(TimeSpan),
                _ => typeof(object),
            };
        }

        /// <summary>
        /// Retrieves the expected type of a property from the specified class type.
        /// If the property is not found directly, attempts to locate it among array or class properties.
        /// </summary>
        public static Type GetPropertyType(Type classType, string propertyName)
        {
            if (classType == null)
                return null;

            // Handle array types by checking the element type.
            if (classType.IsArray)
            {
                Type elementType = classType.GetElementType();
                return elementType?.GetProperty(propertyName)?.PropertyType;
            }

            // Direct property lookup.
            var propertyInfo = classType.GetProperty(propertyName);
            if (propertyInfo != null)
                return propertyInfo.PropertyType;

            // Attempt to find a matching array property.
            var arrayPropertyInfo = classType.GetProperties()
                .FirstOrDefault(p => p.PropertyType.IsArray &&
                                     p.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase));
            if (arrayPropertyInfo != null)
                return arrayPropertyInfo.PropertyType.GetElementType();

            // Attempt to find a matching class property.
            var objectPropertyInfo = classType.GetProperties()
                .FirstOrDefault(p => p.PropertyType.IsClass &&
                                     p.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase));
            if (objectPropertyInfo != null)
                return objectPropertyInfo.PropertyType;

            // Fallback: assume object.
            return typeof(object);
        }

        /// <summary>
        /// Returns the element type for collections (arrays or generic collections).
        /// </summary>
        public static Type GetElementType(Type type)
        {
            if (type == null)
                return null;

            if (type.IsArray)
                return type.GetElementType();

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                return type.GetGenericArguments()[0];

            if (type.IsGenericType && type.GetGenericArguments().Length > 0)
                return type.GetGenericArguments()[0];

            return type;
        }

        #endregion
    }
}
