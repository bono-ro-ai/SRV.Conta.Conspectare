# Bono.Conspectare.Client

Typed HttpClient SDK for the Conspectare Document Pipeline API.

## Installation

```bash
dotnet add package Bono.Conspectare.Client --source https://nuget.pkg.github.com/bono-ro-ai/index.json
```

## Usage

```csharp
builder.Services.AddConspectareClient(opts =>
{
    opts.BaseUrl = "https://your-conspectare-instance.com";
    opts.ApiKey = "your-api-key";
});
```

Then inject `IConspectareClient` wherever needed.
