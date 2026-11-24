namespace Cpu6502_10;

// computer.v: a processor, a ROM, a RAM, the bus that ties them, and six LEDs.
//
//   $0000-$7FFF   the RAM: 32,768 bytes
//   $8000         the six LEDs
//   $8001-$BFFF   nothing yet
//   $C000-$FFFF   the ROM: 16,384 bytes
public sealed class Computer
{
    private readonly Cpu _cpu = new();
    private readonly Ram _ram = new();
    private readonly Rom _rom;

    /// <summary>The last byte stored at $8000: its six low bits.</summary>
    public byte Leds { get; private set; }

    // The bus, visible from outside so that we can watch it.
    public ushort Address => _cpu.Address;
    public byte DataOut => _cpu.DataOut;
    public bool Write => _cpu.Write;
    public Cpu.CpuState State => _cpu.State;

    /// <summary>data_in: the answer of whoever the processor is talking to.</summary>
    public byte DataIn
    {
        get
        {
            ushort address = _cpu.Address;
            bool ramSelected = (address & 0x8000) == 0;          // $0000-$7FFF
            bool romSelected = (address & 0xC000) == 0xC000;     // $C000-$FFFF
            bool ledsSelected = address == 0x8000;

            if (ramSelected) return _ram.DataOut;
            if (romSelected) return _rom.Data;
            if (ledsSelected) return Leds;                       // LEDs can be read back
            return 0x00;                                         // nothing there
        }
    }

    public Computer(Rom rom) => _rom = rom;

    /// <summary>One rising edge of the clock.</summary>
    public void Tick(bool reset, bool step)
    {
        // Everything below sees the values from before the edge.
        ushort addressAtTick = _cpu.Address;
        bool writeAtTick = _cpu.Write;
        byte dataOutAtTick = _cpu.DataOut;
        byte dataInAtTick = DataIn;  // This is the value from the last tick

        bool ramSelected = (addressAtTick & 0x8000) == 0;
        bool ledsSelected = addressAtTick == 0x8000;

        _cpu.Tick(reset, step, dataInAtTick);   // dataIn sent to the CPU is from the last tick

        // Both memories see the low bits of every address, and both answer.
        _ram.Tick(addressAtTick, writeAtTick && ramSelected, dataOutAtTick);
        _rom.Tick(addressAtTick);

        // The LEDs: a register of six bits that sits on the bus like a cell of memory.
        if (writeAtTick && ledsSelected)
            Leds = (byte)(dataOutAtTick & 0x3F);
    }
}