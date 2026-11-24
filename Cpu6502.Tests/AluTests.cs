using System.Text;

namespace Cpu6502_10.Tests;

// Port of alu_tb.v (chapter 9): a few operations that we can read, then
// every operation on every pair of bytes, with the carry at 0 and at 1.
[TestClass]
public class AluTests
{
    // -----------------------------------------------------------------
    // What the test expects. It is worked out with ordinary numbers and
    // none of the tricks of the circuit: here a subtraction is a
    // subtraction, and a shift to the left is a multiplication by two.
    // -----------------------------------------------------------------
    private readonly record struct Expected(
        byte Result,
        bool HasCarry, bool Carry,
        bool HasOverflow, bool Overflow,
        bool Negative, bool Zero);

    private static Expected Expect(AluOperation operation, int a, int b, int carryIn)
    {
        int signedA = a > 127 ? a - 256 : a;
        int signedB = b > 127 ? b - 256 : b;

        int value = 0;                 // the true answer, which may not fit in a byte
        bool hasCarry = false, carry = false;
        bool hasOverflow = false, overflow = false;

        switch (operation)
        {
            case AluOperation.Pass:
                value = a;
                break;

            case AluOperation.Add:
                {
                    value = a + b + carryIn;
                    int signedValue = signedA + signedB + carryIn;
                    carry = value > 255;
                    overflow = signedValue > 127 || signedValue < -128;
                    hasCarry = hasOverflow = true;
                    break;
                }
            case AluOperation.Subtract:
                {
                    // With carry_in at 0, one more is taken away: a borrow.
                    value = a - b - (1 - carryIn);
                    int signedValue = signedA - signedB - (1 - carryIn);
                    carry = value >= 0;                  // 1: no need to borrow
                    overflow = signedValue > 127 || signedValue < -128;
                    hasCarry = hasOverflow = true;
                    break;
                }
            case AluOperation.Compare:
                value = a - b;
                carry = a >= b;
                hasCarry = true;
                break;

            case AluOperation.And: value = a & b; break;
            case AluOperation.Or: value = a | b; break;
            case AluOperation.Xor: value = a ^ b; break;

            case AluOperation.ShiftLeft:
                value = a * 2;
                carry = value > 255;
                hasCarry = true;
                break;
            case AluOperation.RotateLeft:
                value = a * 2 + carryIn;
                carry = value > 255;
                hasCarry = true;
                break;
            case AluOperation.ShiftRight:
                value = a / 2;
                carry = a % 2 == 1;                  // what the division leaves
                hasCarry = true;
                break;
            case AluOperation.RotateRight:
                value = a / 2 + 128 * carryIn;
                carry = a % 2 == 1;
                hasCarry = true;
                break;

            case AluOperation.Increment: value = a + 1; break;
            case AluOperation.Decrement: value = a - 1; break;
        }

        // A byte keeps what is left of the true answer once every whole 256
        // is gone. Adding 256 first makes sure we never divide a negative number.
        byte result = (byte)((value + 256) % 256);
        return new Expected(result, hasCarry, carry, hasOverflow, overflow,
                            result > 127, result == 0);
    }

    private static bool Matches(AluResult got, Expected want) =>
        got.Result == want.Result
        && got.Negative == want.Negative
        && got.Zero == want.Zero
        && (!want.HasCarry || got.CarryOut == want.Carry)
        && (!want.HasOverflow || got.Overflow == want.Overflow);

    // -----------------------------------------------------------------
    // A few operations that we can read. Columns: operation, a, b,
    // carry_in, result, C, V. A flag of -1 is one the operation does not
    // define (the dash in the test bench). N and Z always come from the result.
    // -----------------------------------------------------------------
    [TestMethod]
    [DataRow(AluOperation.Add, 23, 19, 0, 42, 0, 0)]
    [DataRow(AluOperation.Add, 23, 19, 1, 43, 0, 0)]   // the carry coming in counts for one
    [DataRow(AluOperation.Add, 200, 100, 0, 44, 1, 0)]   // 300 needs a ninth bit: carry
    [DataRow(AluOperation.Add, 100, 50, 0, 150, 0, 1)]   // 150 is not a signed byte: overflow
    [DataRow(AluOperation.Add, 156, 156, 0, 56, 1, 1)]   // -100 + -100: a carry and an overflow
    [DataRow(AluOperation.Subtract, 42, 19, 1, 23, 1, 0)]
    [DataRow(AluOperation.Subtract, 42, 19, 0, 22, 1, 0)]   // carry_in at 0 takes one more away
    [DataRow(AluOperation.Subtract, 19, 42, 1, 233, 0, 0)]   // below zero: the carry comes out at 0
    [DataRow(AluOperation.Subtract, 100, 156, 1, 200, 0, 1)]   // 100 - -100 is 200: overflow
    [DataRow(AluOperation.Compare, 42, 19, 0, 23, 1, -1)]  // larger: carry at 1
    [DataRow(AluOperation.Compare, 42, 42, 0, 0, 1, -1)]  // equal: carry at 1, and zero
    [DataRow(AluOperation.Compare, 19, 42, 0, 233, 0, -1)]  // smaller: carry at 0
    [DataRow(AluOperation.And, 0b11001100, 0b10101010, 0, 0b10001000, -1, -1)]
    [DataRow(AluOperation.Or, 0b11001100, 0b10101010, 0, 0b11101110, -1, -1)]
    [DataRow(AluOperation.Xor, 0b11001100, 0b10101010, 0, 0b01100110, -1, -1)]
    [DataRow(AluOperation.ShiftLeft, 0b10010110, 0, 1, 0b00101100, 1, -1)]
    [DataRow(AluOperation.RotateLeft, 0b10010110, 0, 1, 0b00101101, 1, -1)]
    [DataRow(AluOperation.ShiftRight, 0b10010110, 0, 1, 0b01001011, 0, -1)]
    [DataRow(AluOperation.RotateRight, 0b10010110, 0, 1, 0b11001011, 0, -1)]
    [DataRow(AluOperation.Increment, 255, 0, 0, 0, -1, -1)]  // 255 + 1 wraps around to 0
    [DataRow(AluOperation.Decrement, 0, 0, 0, 255, -1, -1)]  // and 0 - 1 to 255
    [DataRow(AluOperation.Pass, 42, 0, 0, 42, -1, -1)]
    public void ReadableOperations(AluOperation operation, int a, int b, int carryIn,
                                   int result, int carry, int overflow)
    {
        var got = Alu.Execute(operation, (byte)a, (byte)b, carryIn == 1);
        string what = $"{operation}, a = {a}, b = {b}, carry_in = {carryIn}";

        Assert.AreEqual((byte)result, got.Result, what + ": result");
        Assert.AreEqual(result > 127, got.Negative, what + ": negative");
        Assert.AreEqual(result == 0, got.Zero, what + ": zero");
        if (carry >= 0) Assert.AreEqual(carry == 1, got.CarryOut, what + ": carry");
        if (overflow >= 0) Assert.AreEqual(overflow == 1, got.Overflow, what + ": overflow");
    }

    // -----------------------------------------------------------------
    // Now everything: for each of the 13 operations, 256 values for a,
    // 256 for b, and carry_in at 0 or 1 (131,072 cases each, 1,703,936
    // in all). One test per operation, so a failure names the operation.
    // -----------------------------------------------------------------
    [TestMethod]
    [DataRow(AluOperation.Pass)]
    [DataRow(AluOperation.Add)]
    [DataRow(AluOperation.Subtract)]
    [DataRow(AluOperation.Compare)]
    [DataRow(AluOperation.And)]
    [DataRow(AluOperation.Or)]
    [DataRow(AluOperation.Xor)]
    [DataRow(AluOperation.ShiftLeft)]
    [DataRow(AluOperation.RotateLeft)]
    [DataRow(AluOperation.ShiftRight)]
    [DataRow(AluOperation.RotateRight)]
    [DataRow(AluOperation.Increment)]
    [DataRow(AluOperation.Decrement)]
    public void EveryPairOfBytes(AluOperation operation)
    {
        int checkedCases = 0, errors = 0;
        var report = new StringBuilder();

        for (int a = 0; a < 256; a++)
        {
            for (int b = 0; b < 256; b++)
            {
                for (int carry = 0; carry < 2; carry++)
                {
                    var got = Alu.Execute(operation, (byte)a, (byte)b, carry == 1);
                    var want = Expect(operation, a, b, carry);
                    checkedCases++;

                    if (!Matches(got, want))
                    {
                        errors++;
                        // Report the first ten mistakes, not thousands of them.
                        // A dash stands for a flag that this operation does not define.
                        if (errors <= 10)
                        {
                            report.AppendLine($"wrong: {operation}, a = {a}, b = {b}, carry_in = {carry}");
                            report.AppendLine($"       gave     {got.Result,3}   C V N Z = " +
                                $"{Bit(got.CarryOut)} {Bit(got.Overflow)} {Bit(got.Negative)} {Bit(got.Zero)}");
                            report.AppendLine($"       expected {want.Result,3}   C V N Z = " +
                                $"{(want.HasCarry ? Bit(want.Carry) : "-")} " +
                                $"{(want.HasOverflow ? Bit(want.Overflow) : "-")} " +
                                $"{Bit(want.Negative)} {Bit(want.Zero)}");
                        }
                    }
                }
            }
        }

        Assert.AreEqual(131072, checkedCases);
        Assert.AreEqual(0, errors,
            $"{operation}: {errors} wrong answer(s) out of {checkedCases}\n{report}");
    }

    private static string Bit(bool value) => value ? "1" : "0";
}