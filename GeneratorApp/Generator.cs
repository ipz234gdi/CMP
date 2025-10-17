using System;
using System.IO;

class Program
{
    static void Main()
    {
        var rnd = new Random();
        int n = 25;
        string outPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "data.dat"));

        try
        {
            FileMode mode = File.Exists(outPath) ? FileMode.Open : FileMode.CreateNew;

            using var fs = new FileStream(outPath, mode, FileAccess.Write, FileShare.ReadWrite);
            using var bw = new BinaryWriter(fs);

            fs.Seek(0, SeekOrigin.Begin);
            for (int i = 0; i < n; i++)
            {
                int v = rnd.Next(10, 101);
                bw.Write(v);
            }

            bw.Flush();
            fs.Flush(true);

            Console.WriteLine($"Згенеровано {n} чисел у {outPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Помилка при генерації: " + ex.Message);
        }
    }
}
