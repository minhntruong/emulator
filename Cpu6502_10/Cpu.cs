namespace Cpu6502_10;

// cpu.v: LDA, STA, CLC, ADC and JMP. A state machine on the fast clock that
// moves only when step is true. One step is one cycle of the 6502: one byte
// read from memory, or one byte written to it.
public sealed class Cpu
{
    public enum Instruction { Nop, Lda, Sta, Clc, Adc, Jmp }
    public enum AddressingMode { Implied, Immediate, Absolute }
    public enum CpuState { Starting, Opcode, Operand, High, Store }

    /// <summary>Where the program begins: the first byte of the ROM.</summary>
    private readonly ushort _start;

    // Bus outputs
    public ushort Address { get; private set; }
    public byte DataOut { get; private set; }
    public bool Write { get; private set; }

    // Registers a program knows about
    public ushort PC { get; private set; }
    public byte A { get; private set; }
    public bool Carry { get; private set; }
    public bool Zero { get; private set; }
    public bool Overflow { get; private set; }
    public bool Negative { get; private set; }

    // Our own bookkeeping
    public CpuState State { get; private set; } = CpuState.Starting;
    private Instruction _instruction = Instruction.Nop;
    private AddressingMode _mode = AddressingMode.Implied;
    private byte _low;

    public Cpu(ushort start = 0xC000) => _start= start; // TODO: implement the 6502 reset/start-up sequence

    /// <summary>
    /// One rising edge of the clock. Everything is computed from the values
    /// before the edge, as flip-flops do: the old state is copied first and
    /// only the copies are read.
    /// </summary>
    public void Tick(bool reset, bool step, byte dataIn)
    {
        var stateAtTick = State;
        var curInstruction = _instruction;
        var curMode = _mode;
        var pcAtTick = PC;
        var aAtTick = A;
        var carryAtTick = Carry;
        var lowAtTick = _low;

        // ---- The decoder: at the step where an opcode arrives, look it up.
        if (step && stateAtTick == CpuState.Opcode)
        {
            switch (dataIn)
            {
                case 0x18: _instruction = Instruction.Clc; _mode = AddressingMode.Implied; break;
                case 0x4C: _instruction = Instruction.Jmp; _mode = AddressingMode.Absolute; break;
                case 0x69: _instruction = Instruction.Adc; _mode = AddressingMode.Immediate; break;
                case 0x8D: _instruction = Instruction.Sta; _mode = AddressingMode.Absolute; break;
                case 0xA9: _instruction = Instruction.Lda; _mode = AddressingMode.Immediate; break;
                default: _instruction = Instruction.Nop; _mode = AddressingMode.Implied; break;
            }
        }

        // ---- The sequencer: one step, one cycle.
        if (reset)
        {
            Write = false;
            State = CpuState.Starting;
        }
        else if (step)
        {
            switch (stateAtTick)
            {
                case CpuState.Starting:
                    // Ask for the first byte of the program.
                    Address = _start;
                    PC = _start;
                    State = CpuState.Opcode;
                    break;

                case CpuState.Opcode:
                    // Every instruction reads the byte that follows its opcode.
                    Address = (ushort)(pcAtTick + 1);
                    PC = (ushort)(pcAtTick + 1);
                    State = CpuState.Operand;
                    break;

                case CpuState.Operand:
                    switch (curMode)
                    {
                        case AddressingMode.Implied:
                            // Done. The byte that arrived is the next opcode: ask for it again.
                            Address = pcAtTick;
                            State = CpuState.Opcode;
                            break;
                        case AddressingMode.Immediate:
                            // The value arrived: done. The next opcode is the byte after it.
                            Address = (ushort)(pcAtTick + 1);
                            PC = (ushort)(pcAtTick + 1);
                            State = CpuState.Opcode;
                            break;
                        case AddressingMode.Absolute:
                            // The low byte arrived. Keep it, ask for the high byte.
                            _low = dataIn;
                            Address = (ushort)(pcAtTick + 1);
                            PC = (ushort)(pcAtTick + 1);
                            State = CpuState.High;
                            break;
                    }
                    break;

                case CpuState.High:
                    {
                        ushort full = (ushort)((dataIn << 8) | lowAtTick);
                        Address = full;
                        if (curInstruction == Instruction.Jmp)
                        {
                            PC = full;
                            State = CpuState.Opcode;
                        }
                        else
                        {
                            // STA: write A at that address.
                            DataOut = aAtTick;
                            Write = true;
                            State = CpuState.Store;
                        }
                        break;
                    }

                case CpuState.Store:
                    // The byte is on its way into memory. Back to the program.
                    Write = false;
                    Address = (ushort)(pcAtTick + 1);
                    PC = (ushort)(pcAtTick + 1);
                    State = CpuState.Opcode;
                    break;
            }
        }

        // ---- The data path: the ALU and what each instruction does with it.
        var aluOp = curInstruction == Instruction.Adc ? AluOperation.Add : AluOperation.Pass;
        byte aluA = curInstruction == Instruction.Lda ? dataIn : aAtTick;
        var r = Alu.Execute(aluOp, aluA, dataIn, carryAtTick);

        bool working = stateAtTick == CpuState.Operand
                    && (curMode == AddressingMode.Implied || curMode == AddressingMode.Immediate);

        if (step && !reset && working)
        {
            switch (curInstruction)
            {
                case Instruction.Lda:
                    A = r.Result;
                    Negative = r.Negative;
                    Zero = r.Zero;
                    break;
                case Instruction.Adc:
                    A = r.Result;
                    Negative = r.Negative;
                    Zero = r.Zero;
                    Carry = r.CarryOut;
                    Overflow = r.Overflow;
                    break;
                case Instruction.Clc:
                    Carry = false;
                    break;
            }
        }
    }
}