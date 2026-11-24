using Cpu6502_10;

namespace Cpu6502_10.Tests;

// MSTest port of TestBench: every cycle on the bus compared with what a 6502
// does with the program, and the LEDs must count. Mirrors computer_tb.v.
[TestClass]
public sealed class ComputerTests
{
    private const int Period = 2;
    private const long TickLimit = 100_000;

    // Expected bus, from the cycle tables of "64doc": 2 cycles for LDA #0, then
    // the eleven cycles of one lap of the loop, which come back forever.
    private static readonly (ushort addr, bool write, byte value, string note)[] validations =
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

    private Computer _computer = null!;
    private StepTimer _timer = null!;

    private bool _reset;
    private int _cycle, _laps;
    private bool _after1stStep;
    private byte _count;
    private byte _expectedLeds;
    private long _ticks;

    [TestInitialize]
    public void Setup()
    {
        _computer = new Computer(new Rom(TestProgram.Data));
        _timer = new StepTimer(Period);
    }

    [TestMethod]
    public void CountProgram_MatchesExpectedBusCycles_For600Laps()
    {
        // Twenty-four cycles to look at, then 600 laps: the count goes past 255 twice.
        while (_laps < 600) Tick();
    }

    [TestMethod]
    public void ResetDuringStaWrite_RestartsProgramCorrectly()
    {
        while (_laps < 600) Tick();

        // A reset at the worst moment: the processor has just begun to store a byte.
        // Run until the cycle where STA's address is complete (place 5), as the Verilog does.
        while (
            (_cycle >= 3 &&
             (_cycle <= 13 ? _cycle : 3 + (_cycle - 3) % 11) == 5
            ) == false)
        {
            Tick();
        }

        _reset = true;
        _after1stStep = false; _cycle = 0; _laps = 0; _count = 0;
        for (int i = 0; i < 3; i++) Tick();
        _reset = false;
        while (_laps < 1) Tick();
    }

    // One clock tick, as the test bench does it: look at the bus before the
    // edge (end of a cycle), then let the edge happen.
    private void Tick()
    {
        bool step = _timer.Pulse;

        if (step && !_reset)
        {
            if (_after1stStep)
            {
                _cycle++; // cycle only increases on step == true, and not on the first step
                int place = _cycle <= 13 ? _cycle : 3 + (_cycle - 3) % 11;
                var exp = validations[place];
                byte theByte = _computer.Write ? _computer.DataOut : _computer.DataIn;
                byte wanted = exp.write ? _count : exp.value;

                Assert.AreEqual(exp.addr, _computer.Address, $"cycle {_cycle}: address mismatch ({exp.note})");
                Assert.AreEqual(exp.write, _computer.Write, $"cycle {_cycle}: read/write mismatch ({exp.note})");
                Assert.AreEqual(wanted, theByte, $"cycle {_cycle}: data mismatch ({exp.note})");

                if (_computer.Write)
                {
                    _laps++;
                    _count++;
                }
            }
            _after1stStep = true;   // the very first step only asks for the first byte
        }

        // At the edge, the test bench does what the computer should do with the LEDs.
        if (_computer.Write && _computer.Address == 0x8000)
            _expectedLeds = (byte)(_computer.DataOut & 0x3F);

        _computer.Tick(_reset, step);
        _timer.Tick();
        _ticks++;

        // (Verilog checks at the negedge: after the edge has happened.)
        Assert.AreEqual(_expectedLeds, _computer.Leds, $"tick {_ticks}: LEDs mismatch");

        if (_ticks > TickLimit * 10) throw new InvalidOperationException("still waiting after too many ticks");
    }
}
