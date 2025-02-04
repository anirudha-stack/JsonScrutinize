# JSONScrutinize

JSONScrutinize is a powerful .NET library designed to validate and scrutinize JSON strings against predefined C# class definitions. It provides deep validation of JSON structures, detecting mismatches in data types, missing or null keys, and enforcing validation rules using attributes like `[Required]` and `[RegularExpression]`.

## Features

- **Type Validation:** Compares JSON property types with their corresponding C# class property types and flags any mismatches.
- **Missing Keys Detection:** Identifies missing properties based on `[Required]` attributes.
- **Null Key Detection:** Flags properties with `null` values unless explicitly ignored.
- **Regex Validation:** Ensures properties adhere to specified formats using `[RegularExpression]` attributes.
- **Nested Objects & Arrays:** Recursively validates nested structures, ensuring type integrity at all levels.

## Installation

To install JSONScrutinize, add the required package via NuGet:

```sh
Install-Package Newtonsoft.Json
```

## Usage

### Standard Class Definition

Define a C# class with validation attributes:

```csharp
using System.ComponentModel.DataAnnotations;

public class StandardClass
{
    [Required]
    public string key1 { get; set; }
    
    [RegularExpression(@"^[0-9]{3}$")]
    public string key2 { get; set; }
    
    public string key3 { get; set; }
}
```

### JSON Input Example

```json
{
    "key1": "value1",
    "key2": "123",
    "key3": 123
}
```

In this case, `key3` should be a string, but an integer is provided, which will be flagged as an error.

### Running Validation

```csharp
using JsonScrutinize;

StandardClass standard = new StandardClass();
string json = "{\"key1\": \"value1\", \"key2\": \"123\", \"key3\": 123}";
Type typeToCheck = standard.GetType();

JsonScrutinize jsonScrutinize = new JsonScrutinize();
var result = await jsonScrutinize.KeyTypeScrutinize(json, typeToCheck);
```

### Expected Output

```json
{
    "StatusCode": "S201",
    "Description": "Found 1 mismatched keys in the provided JSON",
    "MismatchedKeys": [
        "key3"
    ]
}
```

This indicates that `key3` does not match the expected type.

## Nested Objects & Arrays

### Standard Class for Nested Objects

```csharp
public class ParentClass
{
    [Required]
    public string parentKey { get; set; }
    
    public ChildClass child { get; set; }
}

public class ChildClass
{
    [RegularExpression(@"^[A-Za-z]{3}$")]
    public string childKey { get; set; }
}
```

### JSON Input for Nested Objects

```json
{
    "parentKey": "ParentValue",
    "child": {
        "childKey": "AB12"
    }
}
```

### Expected Output

```json
{
    "StatusCode": "S201",
    "Description": "Found 1 mismatched keys in the provided JSON",
    "MismatchedKeys": [
        "child.childKey"
    ]
}
```

## Status Codes & Their Meanings

| Status Code | Meaning |
|------------|---------|
| `S200` | Success - No issues found |
| `S201` | Type mismatch found |
| `S202` | Null keys detected |
| `S203` | Missing keys detected |
| `E6001` | JSON input is null or empty |
| `E6002` | Type to compare is null |
| `E6005` | JSON parsing error or unexpected failure |

## Additional Validation Functions

### Missing Key Check

```csharp
var missingKeysResult = await jsonScrutinize.KeyMissingScrutinize(json, typeToCheck);
```

### Null Key Check (Ignoring Certain Keys)

```csharp
var nullKeysResult = await jsonScrutinize.KeyNullScrutinize(json, typeToCheck, "key3");
```

## Conclusion

JSONScrutinize offers a comprehensive way to validate JSON structures against standard class definitions in .NET applications. Whether enforcing data types, required fields, or specific formats, this library ensures data integrity effectively.

For contributions, issues, or improvements, feel free to submit to the GitHub repository.

**Happy Coding!**

