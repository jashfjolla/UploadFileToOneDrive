# UploadFileToOneDrive

This project contains a small .NET console app that uploads a file to OneDrive, downloads it back, and checks with SHA-256 that nothing changed along the way.

## What it does

1. Checks the file path you passed in
2. Signs you in with your Microsoft account using the device code flow
3. Looks for the target folder in OneDrive and creates it if it isn't there
4. Uploads the file with the Microsoft Graph API
5. Downloads the same file to a temp folder
6. Hashes both copies with SHA-256
7. Tells you whether they match

## What you need

- .NET 10 SDK
- A personal Microsoft account with OneDrive
- An app registration (steps below)

## Setting up the app registration

The app needs its own registration so it can ask for permission to use your OneDrive.

1. Go to https://entra.microsoft.com -> App registrations -> New registration
2. Give it any name
3. For Supported account types, pick an option that includes personal Microsoft accounts
4. Leave Redirect URI empty -> the device code flow doesn't use one
5. Register, then copy the Application (client) ID from the Overview page
6. Go to Authentication -> Advanced settings and turn on Allow public client flows. Without this the sign-in fails.
7. Go to API permissions -> Add a permission -> Microsoft Graph -> Delegated permissions -> add Files.ReadWrite

There's no client secret. A console app is a public client and only uses the Client ID. The Client ID identifies the app and it isn't a password.

## Configuration

Put your values in "appsettings.json":

```json
{
  "ClientId": "your-client-id-here",
  "FolderName": "FjollasAssignment"
}
```

'FolderName' is the folder in the root of your OneDrive. It gets created if it doesn't exist.

## Running it

dotnet run -- "C:\path\to\your\file.txt"

The console prints a URL and a short code. Open the URL in a browser, type the code, sign in, and accept the permission prompt. The app carries on by itself after that.

## Exit codes

- 0 - the hashes matched, the transfer is verified
- 1 - something went wrong: no file path given, file not found, config couldn't be read, or the hashes didn't match

I used exit codes on purpose so this could run as a step in a pipeline, which checks the exit code instead of needing someone to read the output.

## Some decisions I made

- Why SHA-256 -> Comparing file names proves nothing, because a broken file keeps its name. Comparing sizes is barely better, because one changed byte doesn't change the length. Hashing the actual content catches anything: a missing letter, a truncated file...

- Keeping dependencies small. -> The only package I added is Microsoft.Identity.Client (MSAL), which handles the device code sign-in while being in the browser. Everything else is built into .NET for the Graph calls, "System.Text.Json" for the config and the folder request, "System.Security.Cryptography" for the hashing. 

- Only asking for what I need -> The app requests the "Files.ReadWrite" scope, which covers the signed-in user's own OneDrive. "Files.ReadWrite.All" would also include everything shared with them, which this app has no reason to touch.

## How I tested it

Tested manually against a real OneDrive account:

Test results:

This file has been found C:\Users\fjoll\Documents\FjollaTesting.txt with this size 17 bytes
To sign in, use a web browser to open the page https://www.microsoft.com/link and enter the code MPX7TFVD to auth
enticate.
Signed in.
Folder does not exist.
Folder created.
Original: 087068AC8189D6F0677FE8010B563E9340F599141115A8ABADF47F197CBB31E2
Downloaded: 087068AC8189D6F0677FE8010B563E9340F599141115A8ABADF47F197CBB31E2
MATCH - transfer verified.

The mismatch test was the important one. I put a breakpoint in after the download, edited the local file, and let it finish debugging and the hashes came out different.

I picked an image and an empty file on purpose. A text file can survive a bug that quietly changes the content, so the PNG is the better check. And zero bytes is the kind of thing that gets missed, because it's easy to write code that assumes there's always something to read.

## Things I'd improve with more time

- Bigger files - The simple upload I'm using is documented for files up to 4 MB. Anything larger needs an upload session.
- Signing in every time - The token lasts about an hour and I get a new one on every run, so you have to sign in each time.
- Config file problems - If "appsettings.json" is missing or malformed the app throws instead of printing something useful. It should be wrapped in a try/catch.
- Automated tests - All my testing was manual. Automating it would mean restructuring the code so the Graph calls could be replaced with fakes, since otherwise every test hits the real OneDrive.