# cstemplate Example 3: Code Generation

A simple code generation template, which outputs C# class definitions and a SQL Create Table script. 

This example shows how templates can be used to parse models and to scaffold a project by generating multiple files with different output types (C#, SQL).

## Template Source Code: 

sample03.template.cs

This is the main body of the template:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HoofMark.CSharpTemplating.Abstractions;

namespace samples.sample03;

/// <summary>
/// A template that generates C# class definitions and a database creation script,
/// based on table definitions provided in the template configuration file (sample03.template.json).
/// </summary>
public class Template03 : ITemplate
{
    public static void Run(ITemplateContext context)
    {
        // Read configuration values
        var ns = context.Config.Get("namespace") ?? "undefined";
        var tables = context.Config.Get<List<TableDefinition>>("tables");
        var outputFileRelativePath = context.Config.Get("outputFileRelativePath") ?? "default";

        if (tables == null || tables.Count == 0)
        {
            context.WriteFile("error.txt", "No tables defined in the configuration.");
            return;
        }

        // Generate class definitions for each table, and generate SQL script for table creation
        var sb = new StringBuilder();
        foreach (var table in tables)
        {
            // Generate C# class file for each table
            var outputFileName = $"{table.Name}.cs";
            var outputFilePath = System.IO.Path.Combine(outputFileRelativePath, outputFileName);
            var code = table.ClassDefinition(ns);
            context.WriteFile(outputFilePath, code);

            // Generate SQL for table creation
            sb.AppendLine(table.TableCreateSql());
        }
        context.WriteFile("sql/create_tables.sql", sb.ToString());
    }
}
```

This section of the template introduces local types, which are easily parsed from the JSON configuration file using `context.Config.Get<List<TableDefinition>>("tables")`.

```csharp
/// <summary>
/// Local type definitions representing table and column definitions used in the template configuration file (sample03.template.json).
/// </summary>
public class TableDefinition
{
    public string Name { get; set; } = string.Empty;
    public List<ColumnDefinition> Columns { get; set; } = [];
}

public class ColumnDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}
```

In this example, extension methods are used to define code generation templates. Note it is not a `cstemplate` requirement to use extension methods for code generation, but this approach does enable a more fluent coding style. However, in principle any C# language construct that builds or returns a string value can be used to generate template output.

A couple of options for code generation are illustrated by this example:
- using string interpolation with multi-line strings
- using StringBuilder()

```csharp
/// <summary>
/// Extension methods for generating C# class definitions and SQL table creation scripts based on the table and column definitions.
/// </summary>
public static class Extensions
{
    /// <summary>
    /// This template illustrates how to use string interpolation and multi-line strings in C# 
    /// to create a class definition based on the table's columns.
    /// </summary>
    /// <param name="table"></param>
    /// <param name="ns"></param>
    /// <returns>the generated class definition</returns>
    public static string ClassDefinition(this TableDefinition table, string ns) => 
    $$"""
    namespace {{ns}};

    public class {{table.Name}}
    {
        {{PropertyDefinitions(table.Columns)}}
    }
    """;

    public static string PropertyDefinitions(this IEnumerable<ColumnDefinition> columns) =>
    string.Join("\n    ", columns.Select(c => c.PropertyDefinition()));

    public static string PropertyDefinition(this ColumnDefinition column) =>
    $"public {column.Type} {column.Name} {{ get; set; }}";

    /// <summary>
    /// This template illustrates how to use a StringBuilder to generate SQL for table creation based on the table's columns.
    /// </summary>
    /// <param name="table"></param>
    /// <returns></returns>
    public static string TableCreateSql(this TableDefinition table)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"CREATE TABLE {table.Name} (");
        foreach (var column in table.Columns)
        {
            sb.AppendLine($"    {column.Name} {column.Type.MapCSharpTypeToSqlType()},");
        }
        sb.Remove(sb.Length - 3, 1); // Remove the last comma
        sb.AppendLine(");");
        return sb.ToString();
    }
     
    public static string MapCSharpTypeToSqlType(this string csharpType)
    {
        return csharpType switch
        {
            "int" => "INT",
            "string" => "VARCHAR(255) NOT NULL",
            "string?" => "VARCHAR(255)",
            "bool" => "BIT",
            "DateTime" => "DATETIME",
            "decimal" => "DECIMAL(10,2)",
            _ => throw new ArgumentException($"Unsupported C# type: {csharpType}")
        };
    }
}


```

## Template config

This defines a simple data model with table/column definitions. This simple model can easily be extended with concepts like primary and foreign keys, SQL datatypes, etc.

Rather than embedding the model in the template config, an alternative option would be to maintain the model in a separate `model.json` file, and record the (relative) path to that model definition file in the template config file. The template would then use `context.ReadFile(<relativePath>)` to retrieve the model. This is useful if several templates need to share a single model.

sample03.template.json
```json
{
  "namespace": "Sample03.Database",
  "tables": [
    {
        "name": "User",
        "columns": [
            { "name": "UserId", "type": "int" },
            { "name": "Username", "type": "string?" },
            { "name": "Email", "type": "string?" }
        ]
    },
    {
        "name": "Product",
        "columns": [
            { "name": "ProductId", "type": "int" },
            { "name": "ProductName", "type": "string?" },
            { "name": "Price", "type": "decimal" }
        ]
    }
  ],
  "outputFileRelativePath": "database"
}
```

## Template context

For simplicity in these examples, the template context file is placed in the same directory as the template itself. In general, the context file can be placed anywhere in the folder hierarchy; `cstemplate` will scan parent folders and will use the first context file it finds in the folder tree.

cstemplate.config.json
```json
{
  "outputRoot": "../output/sample03",
  "project":    "../samples.csproj",
  "nugetPackages": [],
  "references": []
}
```

- `output root` identifies the path to the root folder, relative to the location of cstemplate.config.json
- `project` is the parent *.csproj file that "owns" the *.template.cs file
- `nugetPackages` lists referenced NuGet packages; not used in this example
- `references` lists local assembly references; not used in this example

## Output:

```bash
[10:49:20 AM] Running template: c:\Users\hoofmark\source\repos\vscode-extensions\template-test\samples\sample03\sample03.template.cs

✓ Template 'Template03' completed successfully.
  Output root: c:\Users\hoofmark\source\repos\vscode-extensions\template-test\samples\output\sample03
  Generated 3 file(s):
    → c:\Users\hoofmark\source\repos\vscode-extensions\template-test\samples\output\sample03\database\User.cs
    → c:\Users\hoofmark\source\repos\vscode-extensions\template-test\samples\output\sample03\database\Product.cs
    → c:\Users\hoofmark\source\repos\vscode-extensions\template-test\samples\output\sample03\sql\create_tables.sql
```

The output file path is relative to the `outputRoot` path specified in `cstemplate.config.json`. 

Contents of output/sample03:

database/Product.cs
```csharp
namespace Sample03.Database;

public class Product
{
    public int ProductId { get; set; }
    public string? ProductName { get; set; }
    public decimal Price { get; set; }
}
```

database/User.cs
```csharp
namespace Sample03.Database;

public class User
{
    public int UserId { get; set; }
    public string? Username { get; set; }
    public string? Email { get; set; }
}
```

sql/create_tables.sql
```sql
-- SQL script for creating tables
CREATE TABLE User (
    UserId INT,
    Username VARCHAR(255),
    Email VARCHAR(255)
);

CREATE TABLE Product (
    ProductId INT,
    ProductName VARCHAR(255),
    Price DECIMAL(10,2)
);
```