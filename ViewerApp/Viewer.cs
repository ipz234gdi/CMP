using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Threading;
using System.Windows.Forms;

class Viewer : Form
{
    const string fileName = "data.dat";
    const string mapName = "Lab1_Map";
    const string mutexName = "Lab1_Mutex";

    MemoryMappedFile? mmf;
    MemoryMappedViewAccessor? accessor;
    Mutex? mutex;
    int count;
    FileStream? dataFs;
    System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();

    static string? FindDataFile(string name, int maxUp = 5)
    {
        string dir = Environment.CurrentDirectory;
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

        string sibling = Path.Combine(workspaceRoot, "GeneratorApp", name);
        sibling = Path.GetFullPath(sibling);
        if (File.Exists(sibling)) return sibling;

        return null;
    }

    public Viewer()
    {
        Text = "Lab1 Viewer";
        Width = 800; Height = 600;
        Font = new System.Drawing.Font("Consolas", 10);

        this.SetStyle(System.Windows.Forms.ControlStyles.OptimizedDoubleBuffer |
                      System.Windows.Forms.ControlStyles.AllPaintingInWmPaint |
                      System.Windows.Forms.ControlStyles.UserPaint, true);
        this.UpdateStyles();

        string dataPath = FindDataFile(fileName);
        if (dataPath == null)
        {
            MessageBox.Show($"Файл {fileName} не знайдено (шукали в робочих і батьківських папках).", "Помилка");
            Environment.Exit(1);
        }

        long fileSize = new FileInfo(dataPath).Length;
        count = (int)(fileSize / sizeof(int));

        dataFs = new FileStream(dataPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        mmf = MemoryMappedFile.CreateFromFile(dataFs, null, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, false);
        accessor = mmf.CreateViewAccessor(0, fileSize, MemoryMappedFileAccess.Read);

        try { mutex = new Mutex(false, mutexName); } catch { mutex = null;}

        timer.Interval = 500;
        timer.Tick += (s, e) => Invalidate();
        timer.Start();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.Clear(System.Drawing.Color.White);

        if (accessor == null) return;

        bool acquired = false;
        try
        {
            acquired = mutex?.WaitOne(500) ?? false;
            for (int i = 0; i < count; i++)
            {
                long offset = (long)i * sizeof(int);
                if (offset + sizeof(int) > accessor.Capacity) break;

                int val = accessor.ReadInt32(i * 4);
                if (val < 0) val = 0;
                if (val > 200) val = 200;
                int len = Math.Min(val, 200);
                string line = new string('*', len);
                g.DrawString(line, Font, System.Drawing.Brushes.Black, 10, 20 + i * 16);
            }
        }
        finally
        {
            if (acquired) mutex?.ReleaseMutex();
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        timer.Stop();
        accessor?.Dispose();
        mmf?.Dispose();
        dataFs?.Dispose();
        mutex?.Dispose();
    }

    [STAThread]
    static void Main()
    {
        try
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Application.Run(new Viewer());
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), "Unhandled exception", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
