namespace Cpu6502_10.Tests;

// Port of board_tb.v (chapter 10): what goes onto the board, pins and all.
// The six pins of the LEDs must count, upside down (an LED is lit when its
// pin is LOW), and button S1 must start the count again.
[TestClass]
public class BoardTests
{
    // On the board one cycle of the processor lasts 540,000 ticks, and the
    // simulation would take a long time. Here the processor runs at full speed.
    private const int Period = 2;

    // A test that waits for a processor needs a time limit. If the changes it
    // is waiting for never come, it gives up after 20,000 ticks.
    private const int TickLimit = 20_000;

    // The part of the test bench that watches the pins: every change of the
    // LEDs must show the next number, 11 cycles after the last change.
    private sealed class Bench
    {
        public readonly Board Board = new(Rom.FromHexFile("rom.hex"), Period);

        public int Ticks;          // ticks of the clock since the beginning
        public int Changed;        // the tick of the last change
        public int Changes;
        public int Checked;
        public readonly List<string> Errors = new();

        public byte Before = 0b111111;   // all the pins HIGH: no LED is lit, the count is 0
        public byte Expected = 1;        // the number we expect at the next change
        public bool Timed;               // is there a change before this one to measure from?

        /// <summary>One tick, then a look at the pins (the test bench looks at the negedge).</summary>
        public void Tick(bool btn1 = false)
        {
            Board.Tick(btn1);
            Ticks++;
            Assert.IsLessThanOrEqualTo(TickLimit, Ticks, $"still waiting after {TickLimit} ticks");

            // Each time the pins change, they must show the next number.
            if (Board.Led != Before)
            {
                Changes++;
                byte number = (byte)(~Board.Led & 0x3F);   // lit means LOW: turn the pins over
                if (Changes <= 4)
                    Console.WriteLine($"tick {Ticks,4}: the pins show {Convert.ToString(Board.Led, 2).PadLeft(6, '0')}, " +
                                      $"the LEDs count {number}");

                Checked++;
                if (number != Expected)
                    Errors.Add($"the LEDs count {number}, expected {Expected}");

                // Eleven cycles for one lap of the loop.
                if (Timed && Ticks - Changed != 11 * Period)
                    Errors.Add($"{Ticks - Changed} ticks since the last change, expected {11 * Period}");

                Timed = true;
                Changed = Ticks;
                Before = Board.Led;
                Expected = (byte)((number + 1) & 0x3F);    // a number of six bits
            }
        }

        public void RunUntilChanges(int changes, bool btn1 = false)
        {
            while (Changes < changes) Tick(btn1);
        }

        public void AssertNoErrors() =>
            Assert.IsEmpty(Errors, string.Join(Environment.NewLine, Errors.Take(10)));
    }

    [TestMethod]
    public void NoLedIsLitAtTheStart()
    {
        // Nothing is stored yet: every pin is HIGH, so no LED is lit.
        var board = new Board(Rom.FromHexFile("rom.hex"), Period);
        Assert.AreEqual(0b111111, board.Led);
    }

    [TestMethod]
    public void PinsCountUpsideDown_ElevenCyclesApart()
    {
        var bench = new Bench();

        // Count past 63 and around.
        bench.RunUntilChanges(70);

        Console.WriteLine($"tick {bench.Ticks,4}: the pins show {Convert.ToString(bench.Board.Led, 2).PadLeft(6, '0')}, " +
                          $"the LEDs count {(byte)(~bench.Board.Led & 0x3F)}");
        Assert.AreEqual(70, bench.Checked);
        bench.AssertNoErrors();
    }

    [TestMethod]
    public void HoldingS1StopsEverything_ReleasingItStartsTheCountAgain()
    {
        var bench = new Bench();
        bench.RunUntilChanges(70);

        // Hold S1 for a while: everything stops.
        bench.Tick();                    // @(negedge clk)
        for (int i = 0; i < 50; i++)
        {
            bench.Tick(btn1: true);
        }
        Assert.AreEqual(bench.Before, bench.Board.Led, "the LEDs moved while S1 was held down");

        // Release it: the program starts again, and its first act is to put
        // zero on the LEDs.
        Console.WriteLine("S1 held for 50 ticks, then released");
        bench.Changes = 0;
        bench.Expected = 0;
        bench.Timed = false;
        bench.RunUntilChanges(3);
        bench.Tick();                    // @(negedge clk)

        bench.AssertNoErrors();
    }
}