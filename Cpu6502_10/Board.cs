namespace Cpu6502_10;

// board.v: our computer on the Tang Nano 20K. A timer says when the processor
// moves, button S1 starts the program again, and the six LEDs are lit when
// their pins are LOW.
public sealed class Board
{
    /// <summary>Ticks of the 27 MHz clock for one cycle of the processor.
    /// 540,000 ticks make 50 cycles a second. The fastest the bus allows is 2.</summary>
    public const int DefaultPeriod = 540_000;

    private readonly StepTimer _timer;
    private readonly Computer _computer;

    // A button changes whenever a finger decides. One flip-flop makes it
    // change on a tick, like everything else in the circuit.
    private bool _reset;

    public Board(Rom rom, int period = DefaultPeriod)
    {
        _timer = new StepTimer(period);
        _computer = new Computer(rom);
    }

    /// <summary>The six LED pins. An LED lights up when its pin is LOW,
    /// so these are the inverted bits of the computer's LED register.</summary>
    public byte Led => (byte)(~_computer.Leds & 0x3F);

    /// <summary>One rising edge of the 27 MHz clock, with button S1 as it is now.</summary>
    public void Tick(bool btn1)
    {
        // Everything sees the values from before the edge: the computer gets
        // the reset and the step that were there, and the flip-flop takes the button.
        bool step = _timer.Pulse;
        _computer.Tick(_reset, step);
        _reset = btn1;
        _timer.Tick();
    }
}