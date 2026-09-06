using Microsoft.Identity.Client;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
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

string configText = File.ReadAllText("appsettings.json");
var settings = JsonSerializer.Deserialize<AppSettings>(configText);

if (settings is null)
{
    Console.Error.WriteLine("Could not read appsettings.json");
    return 1;
}

//The app is registered for personal Microsoft accounts, so it must use
//the consumers authority. "common" would not work against a personal OneDrive.
var app = PublicClientApplicationBuilder
    .Create(settings.ClientId)
    .WithAuthority(AadAuthorityAudience.PersonalMicrosoftAccount)
    .Build();

//This only covers the signed-in user's own OneDrive. Files.ReadWrite.All would also include everything shared with them
string[] scopes = { "Files.ReadWrite" };
Task ShowDeviceCode(DeviceCodeResult codeResult)
{
    Console.WriteLine(codeResult.Message);
    return Task.CompletedTask;
}

var result = await app.AcquireTokenWithDeviceCode(scopes, ShowDeviceCode).ExecuteAsync();

var http = new HttpClient();
http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);

var response = await http.GetAsync("https://graph.microsoft.com/v1.0/me/drive");

if (!response.IsSuccessStatusCode)
{
    Console.Error.WriteLine($"Could not reach OneDrive: {response.StatusCode}");
    return 1;
}

Console.WriteLine("Signed in.");

//Graph would create missing folders automatically during upload, 
//but the requirement asks for an explicit check, and it makes the failure clearer.
var folderCheck = await http.GetAsync($"https://graph.microsoft.com/v1.0/me/drive/root:/{settings.FolderName}");

if (folderCheck.StatusCode == HttpStatusCode.OK)
{
    Console.WriteLine("Folder exists.");
}
else
{
    Console.WriteLine("Folder does not exist.");
    var folderBody = new Dictionary<string, object>
    {
        ["name"] = settings.FolderName,
        ["folder"] = new { }
    };

    string json = JsonSerializer.Serialize(folderBody);

    var createResponse = await http.PostAsync("https://graph.microsoft.com/v1.0/me/drive/root/children",
        new StringContent(json, Encoding.UTF8, "application/json"));

    if (!createResponse.IsSuccessStatusCode)
    {
        Console.Error.WriteLine($"Could not create folder: {createResponse.StatusCode}");
        return 1;
    }

    Console.WriteLine("Folder created.");
}

string uploadUrl = $"https://graph.microsoft.com/v1.0/me/drive/root:/{settings.FolderName}/{file.Name}:/content";

HttpResponseMessage uploadResponse;
using (var fileStream = File.OpenRead(filePath))
{
    uploadResponse = await http.PutAsync(uploadUrl, new StreamContent(fileStream));

    if (!uploadResponse.IsSuccessStatusCode)
    {
        Console.Error.WriteLine($"Upload failed: {uploadResponse.StatusCode}");
        return 1;
    }
}

var downloadResponse = await http.GetAsync(uploadUrl);

if (!downloadResponse.IsSuccessStatusCode)
{
    Console.Error.WriteLine($"Download failed: {downloadResponse.StatusCode}");
    return 1;
}
string downloadedPath = Path.Combine(Path.GetTempPath(), file.Name);

//The block closes the file before the hash is calculated
using (var outFile = File.Create(downloadedPath))
{
    await downloadResponse.Content.CopyToAsync(outFile);
}
static string Sha256(string path)
{
    using var stream = File.OpenRead(path);
    using var sha = SHA256.Create();
    return Convert.ToHexString(sha.ComputeHash(stream));
}

string originalHash = Sha256(filePath);
string downloadedHash = Sha256(downloadedPath);
Console.WriteLine($"Original:   {originalHash}");
Console.WriteLine($"Downloaded: {downloadedHash}");

if (originalHash == downloadedHash)
{
    Console.WriteLine("MATCH - transfer verified.");
    return 0;
}

Console.Error.WriteLine("MISMATCH - transfer failed.");

return 1;