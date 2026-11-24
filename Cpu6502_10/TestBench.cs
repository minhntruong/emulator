namespace Cpu6502_10;

// Test bench: every cycle on the bus compared with what a 6502 does with the
// program, and the LEDs must count. Mirrors computer_tb.v.
public sealed class TestBench
{
    private const int Period = 2;
    private const long TickLimit = 100_000;

    // Expected bus, from the cycle tables of "64doc": 2 cycles for LDA #0, then
    // the eleven cycles of one lap of the loop, which come back forever.
    private static readonly (ushort addr, bool write, byte value, string note)[] Table =
    {
        (0, false, 0, ""),                                   // index 0 unused: places are 1-based
        (0xC000, false, 0xA9, "LDA #0"),
        (0xC001, false, 0x00, "  the value"),
        (0xC002, false, 0x8D, "STA $8000"),
        (0xC003, false, 0x00, "  the address, low byte"),
        (0xC004, false, 0x80, "  the address, high byte"),
        (0x8000, true,  0x00, "  A, on its way to the LEDs"),   // value = the count
        (0xC005, false, 0x18, "CLC"),
        (0xC006, false, 0x69, "  the next byte, not used"),
        (0xC006, false, 0x69, "ADC #1"),
        (0xC007, false, 0x01, "  the value"),
        (0xC008, false, 0x4C, "JMP $C002"),
        (0xC009, false, 0x02, "  the address, low byte"),
        (0xC00A, false, 0xC0, "  the address, high byte"),
    };

    private readonly Computer _computer;
    private readonly StepTimer _timer;

    private bool _reset;
    private int _cycle, _laps, _checkedCycles, _errors, _shown = 24;
    private bool _asked;
    private byte _count;
    private byte _expectedLeds;
    private long _ticks;

    public TestBench(Rom rom)
    {
        _computer = new Computer(rom);
        _timer = new StepTimer(Period);
    }

    /// <summary>Runs the test bench to completion and returns the process exit code.</summary>
    public int Run()
    {
        // Twenty-four cycles to look at, then 600 laps: the count goes past 255 twice.
        while (_laps < 600) Tick();

        // A reset at the worst moment: the processor has just begun to store a byte.
        Console.WriteLine("reset");
        // Run until the cycle where STA's address is complete (place 5), as the Verilog does.
        while (
            (_cycle >= 3 &&
             (_cycle <= 13 ? _cycle : 3 + (_cycle - 3) % 11) == 5
            ) == false)
        {
            Tick();
        }

        _reset = true;
        _asked = false; _cycle = 0; _laps = 0; _count = 0; _shown = 6;
        for (int i = 0; i < 3; i++) Tick();
        _reset = false;
        while (_laps < 1) Tick();

        if (_errors == 0)
        {
            Console.WriteLine($"PASS: computer, {_checkedCycles} cycles as a 6502 does them, and LEDs that count");
            return 0;
        }
        Console.WriteLine($"FAIL: computer, {_errors} error(s)");
        return 1;
    }

    private void Error(string msg)
    {
        _errors++;
        if (_errors <= 10) Console.WriteLine("wrong: " + msg);
    }

    // One clock tick, as the test bench does it: look at the bus before the
    // edge (end of a cycle), then let the edge happen.
    private void Tick()
    {
        bool step = _timer.Pulse;

        if (step && !_reset)
        {
            if (_asked)
            {
                _cycle++;
                int place = _cycle <= 13 ? _cycle : 3 + (_cycle - 3) % 11;
                var exp = Table[place];
                byte theByte = _computer.Write ? _computer.DataOut : _computer.DataIn;
                byte wanted = exp.write ? _count : exp.value;

                if (_cycle <= _shown)
                    Console.WriteLine($"cycle {_cycle,2}: {_computer.State,-8} {_computer.Address:x4} " +
                                      $"{(_computer.Write ? "write" : "read "),-5} {theByte:x2}   {exp.note}");

                _checkedCycles++;
                if (_computer.Address != exp.addr || _computer.Write != exp.write || theByte != wanted)
                    Error($"cycle {_cycle} is {_computer.Address:x4} {(_computer.Write ? "write" : "read")} {theByte:x2}, " +
                          $"expected {exp.addr:x4} {(exp.write ? "write" : "read")} {wanted:x2}");

                if (_computer.Write)
                {
                    _laps++;
                    if (_laps is 3 or 4 or 64 or 65 or 256 or 257 or 258 or 600)
                        Console.WriteLine($"lap {_laps,3}: the program stores {_computer.DataOut,3}, " +
                                          $"the LEDs show {Convert.ToString(_computer.Leds, 2).PadLeft(6, '0')}");
                    _count++;
                }
            }
            _asked = true;   // the very first step only asks for the first byte
        }

        // At the edge, the test bench does what the computer should do with the LEDs.
        if (_computer.Write && _computer.Address == 0x8000)
            _expectedLeds = (byte)(_computer.DataOut & 0x3F);

        _computer.Tick(_reset, step);
        _timer.Tick();
        _ticks++;

        // (Verilog checks at the negedge: after the edge has happened.)
        if (_computer.Leds != _expectedLeds)
            Error($"the LEDs show {_computer.Leds:b6}, expected {_expectedLeds:b6}");

        if (_ticks > TickLimit * 10) throw new InvalidOperationException("still waiting after too many ticks");
    }
}
