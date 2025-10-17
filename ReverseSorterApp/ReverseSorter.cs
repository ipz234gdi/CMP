using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;

class Program
{
    const string fileName = "data.dat";
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

        string sibling = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "GeneratorApp", name));
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
                Console.WriteLine($"Файл {fileName} не знайдено. Запустіть генератор.");
                return;
            }

            long fileSize = new FileInfo(dataPath).Length;
            int count = (int)(fileSize / sizeof(int));
            if (count <= 1)
            {
                Console.WriteLine("Недостатньо елементів для сортування.");
                return;
            }

            using var fs = new FileStream(dataPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            using var mmf = MemoryMappedFile.CreateFromFile(fs, null, 0, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, false);
            using var accessor = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.ReadWrite);

            Mutex? localMutex = null;
            try { localMutex = new Mutex(false, mutexName); } catch { localMutex = null; }

            Console.WriteLine("ReverseSorter: натисніть ПРОБІЛ щоб почати сортування (selection sort, спадний). ESC для виходу.");

            while (true)
            {
                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Escape) break;
                if (key.Key != ConsoleKey.Spacebar) continue;

                Console.WriteLine("Reverse sorting started...");
                for (int i = 0; i < count - 1; i++)
                {
                    int maxIdx = i;
                    // знайти максимальний елемент у діапазоні [i..count-1]
                    for (int j = i + 1; j < count; j++)
                    {
                        long offJ = (long)j * sizeof(int);
                        if (offJ + sizeof(int) > accessor.Capacity) break;
                        int valJ = accessor.ReadInt32(j * 4);
                        int valMax = accessor.ReadInt32(maxIdx * 4);
                        if (valJ > valMax) maxIdx = j;
                    }

                    if (maxIdx != i)
                    {
                        bool locked = false;
                        try
                        {
                            locked = localMutex?.WaitOne(5000) ?? false;

                            long offI = (long)i * sizeof(int);
                            long offMax = (long)maxIdx * sizeof(int);
                            if (offMax + sizeof(int) > accessor.Capacity || offI + sizeof(int) > accessor.Capacity)
                                continue;

                            int a = accessor.ReadInt32(i * 4);
                            int b = accessor.ReadInt32(maxIdx * 4);

                            // обміняти місцями (щоб сортувати спадно)
                            accessor.Write(i * 4, b);
                            accessor.Write(maxIdx * 4, a);
                            accessor.Flush();
                        }
                        finally
                        {
                            if (locked) localMutex?.ReleaseMutex();
                        }

                        // пауза для візуалізації у Viewer
                        Thread.Sleep(1000);
                    }
                }

                Console.WriteLine("ReverseSorter: Робота завершена");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Unhandled exception: " + ex);
        }
    }
}
