# Monthly Restore Compile Fix

Fixed a C# compile error in `BbsGuideParser.cs`.

The previous build accidentally used invalid character literals in `CsvEscape()`:

```csharp
'\r'
'\n'
```

This build corrects them to valid carriage return and newline character checks.
