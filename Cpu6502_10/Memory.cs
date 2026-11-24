using System.Globalization;

namespace Cpu6502_10;

// ram.v: 32,768 bytes. The byte read comes out one tick after its address.
public sealed class Ram
{
    private readonly byte[] _cells = new byte[32768];   // in the chip, cells start at zero

    /// <summary>data_out: the byte that was there, one tick later.</summary>
    public byte DataOut { get; private set; }

    public void Tick(ushort address, bool write, byte dataIn)
    {
        int a = address & 0x7FFF;
        byte old = _cells[a];            // non-blocking: the read sees the old cell
        if (write) _cells[a] = dataIn;

        // DataOut is available on both read and write ops. On a read, that's the value at the requested address.
        // On a write, it's the value that was there before the write.
        DataOut = old;
    }
}

// rom.v: 16,384 bytes filled from a file. The byte comes out one tick after its address.
public sealed class Rom
{
    private readonly byte[] _cells = new byte[16384];

    public byte Data { get; private set; }

    public int Length { get; private set; }

    public Rom(byte[] contents)
    {
        Array.Copy(contents, _cells, Math.Min(contents.Length, _cells.Length));
        Length = Math.Min(contents.Length, _cells.Length);
    }

    /// <summary>Reads a file with one byte per line in hex, like $readmemh
    /// (comments after "//" are ignored).</summary>
    public static Rom FromHexFile(string path)
    {
        var bytes = new List<byte>();
        foreach (var raw in File.ReadLines(path))
        {
            int comment = raw.IndexOf("//", StringComparison.Ordinal);
            string line = (comment >= 0 ? raw[..comment] : raw).Trim();
            if (line.Length == 0) continue;
            foreach (var token in line.Split(' ', '\t', StringSplitOptions.RemoveEmptyEntries))
                bytes.Add(byte.Parse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
        }
        return new Rom(bytes.ToArray());
    }

    public void Tick(ushort address)
    {
        Data = _cells[address & 0x3FFF];
    }
}

// timer.v: once every Period ticks, say "now".
public sealed class StepTimer
{
    private readonly int _period;
    private int _ticks;

    public StepTimer(int period = 27_000_000) => _period = period;

    /// <summary>pulse: true during the last tick of each round.</summary>
    public bool Pulse => _ticks == _period - 1;

    public void Tick()
    {
        _ticks = _ticks == _period - 1 ? 0 : _ticks + 1;
    }
}