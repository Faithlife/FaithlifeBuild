# DotNetClassicTool.TryCreate method

Accesses a classic NuGet packaged tool using the standard Build project.

```csharp
public static DotNetClassicTool? TryCreate(string packageName, string? toolName = null)
```

| parameter | description |
| --- | --- |
| packageName | The name of the NuGet package. |
| toolName | The name of the tool executable, as found in the `tools` folder of the NuGet package. Defaults to the package name. |

## Return Value

Null if the tool is not installed.

## Remarks

The version of the tool is determined by the matching `PackageReference` in `tools/Build/Build.csproj`.

## See Also

* class [DotNetClassicTool](../DotNetClassicTool.md)
* namespace [Faithlife.Build](../../Faithlife.Build.md)

<!-- DO NOT EDIT: generated by xmldocmd for Faithlife.Build.dll -->
