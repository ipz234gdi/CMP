using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;

class Program
{
    const string fileName = "data.dat";
    const string mapName = "Lab1_Map";
    const string mutexName = "Lab1_Mutex";

    static string? FindDataFile(string name, int maxUp = 5)
    {
        string? dir = Environment.CurrentDirectory;
        for (int i = 0; i <= maxUp && dir != null; i++)
        {
            string candidate = Path.Combine(dir, name);
            if (File.Exists(candidate)) return Path.GetFullPath(candidate);
            dir = Path.GetDirectoryName(dir);
        }

        string baseDir = AppContext.BaseDirectory;
        string candidate2 = Path.Combine(baseDir, name);
        if (File.Exists(candidate2)) return Path.GetFullPath(candidate2);

        string workspaceRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));
        string candidateRoot = Path.Combine(workspaceRoot, name);
        if (File.Exists(candidateRoot)) return Path.GetFullPath(candidateRoot);

        string sibling = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "GeneratorApp", name));
        if (File.Exists(sibling)) return sibling;

        return null;
    }

    static void Main()
    {
        try
        {
            string? dataPath = FindDataFile(fileName);
            if (dataPath == null)
            {
                Console.WriteLine($"Файл {fileName} не знайдено.");
                return;
            }

            long fileSize = new FileInfo(dataPath).Length;
            int count = (int)(fileSize / sizeof(int));
            if (count <= 1)
            {
                Console.WriteLine("Замало елементів у файлі для сортування.");
                return;
            }

            using var fs = new FileStream(dataPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            using var mmf = MemoryMappedFile.CreateFromFile(fs, null, 0, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, false);
            using var accessor = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.ReadWrite);
            Mutex? localMutex = null;
            try { localMutex = new Mutex(false, mutexName); } catch {}

            Console.WriteLine("Натисніть ПРОБІЛ щоб почати покрокове сортування (bubble). ESC для виходу.");
            while (true)
            {
                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Escape) break;
                if (key.Key != ConsoleKey.Spacebar) continue;

                Console.WriteLine("Сортування почалося...");
                bool swapped;
                for (int i = 0; i < count - 1; i++)
                {
                    swapped = false;
                    for (int j = 0; j < count - 1 - i; j++)
                    {
                        bool locked = false;
                        try
                        {
                            locked = localMutex?.WaitOne(5000) ?? false;

                            long offA = (long)j * sizeof(int);
                            long offB = (long)(j + 1) * sizeof(int);
                            if (offB + sizeof(int) > accessor.Capacity) continue;

                            int a = accessor.ReadInt32(j * 4);
                            int b = accessor.ReadInt32((j + 1) * 4);
                            if (a > b)
                            {
                                accessor.Write(j * 4, b);
                                accessor.Write((j + 1) * 4, a);
                                accessor.Flush();
                                swapped = true;
                            }
                        }
                        finally
                        {
                            if (locked) localMutex?.ReleaseMutex();
                        }

                        Thread.Sleep(1000);
                    }
                    if (!swapped) break;
                }
                Console.WriteLine("Робота завершена");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Unhandled exception: " + ex);
        }
    }
}
