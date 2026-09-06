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

return 0;