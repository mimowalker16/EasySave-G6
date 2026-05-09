namespace CryptoSoft;

public static class Program
{
    private const string SingleInstanceMutexName = @"Global\EasySave_CryptoSoft_SingleInstance";
    private const string DefaultKey = "EasySave-CryptoSoft-Key";

    public static void Main(string[] args)
    {
        using var mutex = new Mutex(initiallyOwned: true, name: SingleInstanceMutexName, out bool isOwner);
        if (!isOwner)
        {
            Console.Error.WriteLine("[CryptoSoft] Another CryptoSoft instance is already running.");
            Environment.Exit(10);
            return;
        }

        try
        {
            if (args.Length < 1)
            {
                Console.Error.WriteLine("[CryptoSoft] Missing file path.");
                Console.Error.WriteLine("Usage: CryptoSoft.exe \"<file_path>\" [key]");
                Environment.Exit(2);
                return;
            }

            string filePath = args[0];
            string key = args.Length >= 2 && !string.IsNullOrWhiteSpace(args[1])
                ? args[1]
                : DefaultKey;

            var fileManager = new FileManager(filePath, key);
            int elapsedTimeMs = fileManager.TransformFile();
            if (elapsedTimeMs < 0)
            {
                Environment.Exit(3);
                return;
            }

            Console.WriteLine($"[CryptoSoft] EncryptionTimeMs={elapsedTimeMs}");
            Environment.Exit(0);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"[CryptoSoft] {e.Message}");
            Environment.Exit(99);
        }
    }
}
