using Microsoft.Identity.Client;
using System.Text.Json;
using UploadFileToOneDrive;

if (args.Length == 0)
{
    Console.Error.WriteLine("Please provide a file path.");
    return 1;
}

string filePath = args[0];

if (!File.Exists(filePath))
{
    Console.Error.WriteLine($"File has not been found: {filePath}");
    return 1;
}

var file = new FileInfo(filePath);

Console.WriteLine($"This file has been found {file.FullName} with this size {file.Length} bytes");

Console.WriteLine("Ready to upload.");

string configText = File.ReadAllText("appsettings.json");
var settings = JsonSerializer.Deserialize<AppSettings>(configText);

if (settings is null)
{
    Console.Error.WriteLine("Could not read appsettings.json");
    return 1;
}

var app = PublicClientApplicationBuilder
    .Create(settings.ClientId)
    .WithAuthority(AadAuthorityAudience.PersonalMicrosoftAccount)
    .Build();

string[] scopes = { "Files.ReadWrite" };
Task ShowDeviceCode(DeviceCodeResult codeResult)
{
    Console.WriteLine(codeResult.Message);
    return Task.CompletedTask;
}

var result = await app.AcquireTokenWithDeviceCode(scopes, ShowDeviceCode).ExecuteAsync();

return 0;