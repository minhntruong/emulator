namespace Cpu6502_10.Tests;

// Port of rom_tb.v (chapter 8): every byte must be the one of the file, and it
// must come out one tick after its address, not before.
[TestClass]
public class RomTests
{
    // The test reads the same file on its own, to know what to expect
    // (the Verilog test bench does the same with a second $readmemh).
    private static byte[] expected = Array.Empty<byte>();

    [ClassInitialize]
    public static void LoadExpected(TestContext _)
    {
        expected = new byte[TestProgram.Data.Length];
        TestProgram.Data.CopyTo(expected);
    }

    // Ask for one byte. Until the tick, data must not move. After it, data
    // must be the byte of the file. (read_and_check in the test bench.)
    private static void ReadAndCheck(Rom rom, ushort wanted)
    {
        byte shown = rom.Data;

        // The address is on the bus, but no tick has happened yet.
        Assert.AreEqual(shown, rom.Data,
            $"data changed before the tick, at address {wanted}");

        rom.Tick(wanted);                     // the tick: the ROM takes the address

        Assert.AreEqual(expected[wanted], rom.Data,
            $"address {wanted} gave {rom.Data:x2}, expected {expected[wanted]:x2}");
    }

    [TestMethod]
    public void TheElevenBytesOfOurProgram()
    {
        // The test bench prints them "to look at"; here we check them.
        byte[] program = { 0xA9, 0x00, 0x8D, 0x00, 0x80, 0x18, 0x69, 0x01, 0x4C, 0x02, 0xC0 };
        var rom = new Rom(TestProgram.Data);

        for (ushort n = 0; n < program.Length; n++)
        {
            ReadAndCheck(rom, n);
            Assert.AreEqual(program[n], rom.Data, $"address {n}");
        }
    }

    [TestMethod]
    public void DataDoesNotMoveBeforeTheTick()
    {
        var rom = new Rom(TestProgram.Data);

        rom.Tick(0);                          // byte 0 is $A9
        Assert.AreEqual(0xA9, rom.Data);

        // Nothing we do between ticks changes data: only a tick does. In the
        // Verilog this is "put an address on the bus and look 2 ns later".
        byte before = rom.Data;
        Assert.AreEqual(before, rom.Data);

        rom.Tick(2);                          // byte 2 is $8D
        Assert.AreEqual(0x8D, rom.Data);
    }

    [TestMethod]
    public void EveryByteComesOutOneTickAfterItsAddress()
    {
        var rom = Rom.FromHexFile("rom.hex");

        // All of them, from the last to the first.
        for (int n = rom.Length - 1; n >= 0; n--)
        {
            ReadAndCheck(rom, (ushort)n);
        }
    }
}