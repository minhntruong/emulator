using System;

namespace Cpu6502_10.Tests;

internal class TestProgram
{
    public static readonly byte[] Data =
    {
        0xA9, 0x00,        // LDA #0     :C000 / 2 cycles / cycles 1-2 (cycle 0 is ignored)
        0x8D, 0x00, 0x80,  // STA $8000  :C002 / 4 cycles / cycles 3-6
        0x18,              // CLC              / 2 cycles / cycles 7-8
        0x69, 0x01,        // ADC #1           / 2 cycles / cycles 9-10
        0x4C, 0x02, 0xC0   // JMP $C002        / 3 cycles / cycles 11-13
    };
}
