using Cpu6502_10;

// Source: https://github.com/yvesrob/6502-gate-by-gate/tree/main/10-first-instructions
// Verilog to C# translated by Claude

// Tick vs Step vs CPU Cycle
// Cycle: unit of time of an CPU instruction (2-4)
// Tick:  units of implementation of a cycle/instruction. Multiple ticks are needed to complete a cycle
// Step:  Signals one of the tick to do special work

// Usage:
//   dotnet run                 run the test bench (computer_tb.v) on the built-in program
//   dotnet run -- rom.hex      the same, with a ROM image from a hex file
//   dotnet run -- --live       watch the six LEDs count in the console (Ctrl+C to stop)

// The program of count-by-hand.hex, at $C000:
//   LDA #0 / loop: STA $8000 / CLC / ADC #1 / JMP loop
byte[] countProgram = 
    { 
        0xA9, 0x00,        // LDA #0     :C000
        0x8D, 0x00, 0x80,  // STA $8000  :C002
        0x18,              // CLC
        0x69, 0x01,        // ADC #1
        0x4C, 0x02, 0xC0   // JMP $C002
    };

bool live = args.Contains("--live");
string? hexFile = args.FirstOrDefault(a => !a.StartsWith("--"));
Rom MakeRom() => hexFile is null ? new Rom(countProgram) : Rom.FromHexFile(hexFile);

if (live) return RunLive(MakeRom());
return new TestBench(MakeRom()).Run();

// ---------------------------------------------------------------------------
// Live: 27 MHz clock divided to 50 processor cycles per second, like board.v.
// ---------------------------------------------------------------------------
static int RunLive(Rom rom)
{
    var computer = new Computer(rom);
    var timer = new StepTimer(period: 2);   // fastest the bus allows; we pace in wall-clock time below
    long cycles = 0;
    var sw = System.Diagnostics.Stopwatch.StartNew();
    while (true)
    {
        bool step = timer.Pulse;

        computer.Tick(reset: false, step: step);
        timer.Tick();

        if (step) cycles++;

        // 50 cycles per second
        if (step && cycles % 5 == 0)
        {
            var bits = Convert.ToString(computer.Leds, 2).PadLeft(6, '0')
                              .Replace('1', '#').Replace('0', '.');
            Console.Write($"\rLEDs {bits}  ({computer.Leds,2})");
            long target = cycles * 1000 / 50;
            long wait = target - sw.ElapsedMilliseconds;
            if (wait > 0) Thread.Sleep((int)wait);
        }
    }
}

