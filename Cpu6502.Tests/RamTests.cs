using System.Text;

namespace Cpu6502_10.Tests;

// Port of ram_tb.v (chapter 8): a byte we write must come back when we read
// it, one tick after its address; a cell we did not write must not change;
// and nothing must be written while write is 0.
[TestClass]
public class RamTests
{
    // The inputs of the RAM as the test bench holds them: address, write and
    // data_in stay where they were left until the test bench changes them.
    private sealed class Bench
    {
        public readonly Ram Ram = new();
        public ushort Address;
        public bool Write;
        public byte DataIn;

        /// <summary>One tick: the RAM takes whatever is on its inputs.</summary>
        public void Tick() => Ram.Tick(Address, Write, DataIn);

        /// <summary>Store one byte: the address, the byte and write are taken at the tick.</summary>
        public void Store(ushort where, byte what)
        {
            Address = where;
            DataIn = what;
            Write = true;
            Tick();
            Write = false;
        }

        /// <summary>Read one byte, one tick after its address.</summary>
        public byte Read(ushort where)
        {
            Address = where;
            Tick();
            return Ram.DataOut;
        }

        /// <summary>Read one byte and compare it with the value we expect.</summary>
        public void ReadAndCheck(ushort where, byte expected)
        {
            byte got = Read(where);
            Assert.AreEqual(expected, got,
                $"address {where} gave {got:x2}, expected {expected:x2}");
        }
    }

    [TestMethod]
    public void ACellNobodyHasWrittenReadsZero()
    {
        // The simulator does not know what such a cell holds (x). In the chip,
        // and in this model, it starts at zero.
        var bench = new Bench();
        bench.Tick();                                  // the first @(negedge clk)
        Assert.AreEqual(0, bench.Read(100));
    }

    [TestMethod]
    public void AByteWeWriteComesBackWhenWeReadIt()
    {
        var bench = new Bench();

        // Write 42 there and read it back.
        bench.Store(100, 0x42);
        bench.ReadAndCheck(100, 0x42);
    }

    [TestMethod]
    public void TheByteComesOutOneTickAfterItsAddress()
    {
        var bench = new Bench();
        bench.Store(100, 0x42);
        bench.Store(101, 0x07);
        bench.Read(101);                               // data_out shows 07

        // Put an address on the bus: until the tick, data_out does not move.
        bench.Address = 100;
        Assert.AreEqual(0x07, bench.Ram.DataOut, "data_out moved before the tick");

        bench.Tick();
        Assert.AreEqual(0x42, bench.Ram.DataOut);
    }

    [TestMethod]
    public void NothingIsWrittenWhileWriteIsZero()
    {
        var bench = new Bench();
        bench.Store(100, 0x42);

        // Put another byte on data_in, but leave write at 0: nothing happens.
        bench.DataIn = 0x99;
        bench.Tick();
        bench.ReadAndCheck(100, 0x42);
    }

    [TestMethod]
    public void WritingANeighborLeavesTheFirstCellAlone()
    {
        var bench = new Bench();
        bench.Store(100, 0x42);

        // Write a neighbor. The first cell keeps its byte.
        bench.Store(101, 0x07);
        bench.ReadAndCheck(101, 0x07);
        bench.ReadAndCheck(100, 0x42);
    }

    [TestMethod]
    public void WritingOverAByteReplacesIt()
    {
        var bench = new Bench();
        bench.Store(100, 0x42);

        bench.Store(100, 0xFF);
        bench.ReadAndCheck(100, 0xFF);
    }

    [TestMethod]
    public void TheWholeMemoryReadsBackWhatWasWritten()
    {
        const int Size = 32768;
        var bench = new Bench();

        // Each cell gets the low byte of its address XOR the high byte: two
        // cells whose addresses differ by one bit never hold the same byte.
        // Then all of them are read back, from the last to the first.
        for (int n = 0; n < Size; n++)
        {
            bench.Store((ushort)n, (byte)(n ^ (n >> 8)));   // only the eight low bits are kept
        }

        int checkedCells = 0, errors = 0;
        var report = new StringBuilder();
        for (int n = Size - 1; n >= 0; n--)
        {
            byte expected = (byte)(n ^ (n >> 8));
            byte got = bench.Read((ushort)n);
            checkedCells++;
            if (got != expected)
            {
                errors++;
                if (errors <= 10)
                    report.AppendLine($"wrong: address {n} gave {got:x2}, expected {expected:x2}");
            }
        }

        Assert.AreEqual(Size, checkedCells);
        Assert.AreEqual(0, errors, report.ToString());
    }
}