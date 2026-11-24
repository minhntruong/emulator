namespace Cpu6502_10;

// alu_operations.vh: the operations of the ALU, by name.
public enum AluOperation
{
    Pass = 0,         // a, unchanged
    Add = 1,          // a + b + carry_in
    Subtract = 2,     // a - b, and one more taken away if carry_in is 0
    Compare = 3,      // a - b, whatever carry_in says
    And = 4,
    Or = 5,
    Xor = 6,
    ShiftLeft = 7,    // a, one place left; a 0 comes in
    RotateLeft = 8,   // the same, but carry_in comes in
    ShiftRight = 9,
    RotateRight = 10,
    Increment = 11,   // a + 1
    Decrement = 12,   // a - 1
}

public readonly record struct AluResult(
    byte Result,
    bool CarryOut,
    bool Overflow,
    bool Negative,
    bool Zero);

// alu.v: gates only. No clock, no memory: same inputs, same outputs.
public static class Alu
{
    public static AluResult Execute(AluOperation operation, byte a, byte b, bool carryIn)
    {
        // One adder does five jobs: ADD, SUBTRACT, COMPARE, INCREMENT, DECREMENT.
        bool subtracting = operation == AluOperation.Subtract || operation == AluOperation.Compare;

        int second = operation == AluOperation.Add ? b
                   : subtracting ? (byte)~b
                   : operation == AluOperation.Decrement ? 255
                   : 0;

        int firstCarry = operation switch
        {
            AluOperation.Add => carryIn ? 1 : 0,
            AluOperation.Subtract => carryIn ? 1 : 0,
            AluOperation.Compare => 1,
            AluOperation.Increment => 1,
            _ => 0,
        };

        // Nine bits: the eight of the sum, and the carry that comes out.
        int sum = (a + second + firstCarry) & 0x1FF;
        int cin = carryIn ? 1 : 0;

        byte result = operation switch
        {
            AluOperation.Pass => a,
            AluOperation.And => (byte)(a & b),
            AluOperation.Or => (byte)(a | b),
            AluOperation.Xor => (byte)(a ^ b),
            AluOperation.ShiftLeft => (byte)(a << 1),
            AluOperation.RotateLeft => (byte)((a << 1) | cin),
            AluOperation.ShiftRight => (byte)(a >> 1),
            AluOperation.RotateRight => (byte)((cin << 7) | (a >> 1)),
            _ => (byte)(sum & 0xFF),
        };

        bool carryOut = operation switch
        {
            AluOperation.ShiftLeft or AluOperation.RotateLeft => (a & 0x80) != 0,
            AluOperation.ShiftRight or AluOperation.RotateRight => (a & 1) != 0,
            _ => (sum & 0x100) != 0,
        };

        // Overflow: two numbers of the same sign gave a sum of the other sign.
        bool overflow = ((a & 0x80) == (second & 0x80)) && ((sum & 0x80) != (a & 0x80));

        return new AluResult(result, carryOut, overflow, (result & 0x80) != 0, result == 0);
    }
}