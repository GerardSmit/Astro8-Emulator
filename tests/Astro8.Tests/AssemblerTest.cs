﻿using Astro8.Devices;
using Astro8.Instructions;
using Moq;
using Xunit.Abstractions;

namespace Astro8.Tests;

public class AssemblerTest
{
    private readonly ITestOutputHelper _output;

    public AssemblerTest(ITestOutputHelper output)
    {
        _output = output;
    }

    private Cpu<Handler> Create(YabalBuilder builder)
    {
        _output.WriteLine("Instructions:");
        _output.WriteLine(builder.ToString());

        var mock = Mock.Of<Handler>();
        var cpu = CpuBuilder.Create(mock)
            .WithMemory()
            .WithProgram(builder)
            .Create();

        return cpu;
    }

    [Fact]
    public void Push()
    {
        var builder = new YabalBuilder();

        builder.Nop();
        var value = builder.CreatePointer("Counter");
        var function = builder.CreateLabel("Function");
        var skipFunction = builder.CreateLabel("Main");
        var stackVariable = builder.GetStackVariable(0);

        builder.Jump(skipFunction);

        // Functions
        builder.Mark(function);
        builder.LoadA(value);
        builder.SetB(1);
        builder.Add();
        builder.StoreA(value);
        builder.Ret();

        // Program
        builder.Mark(skipFunction);

        builder.SetA(1);
        builder.StoreA(stackVariable);

        builder.Call(function);
        builder.Call(function);

        // Run
        var cpu = Create(builder);
        cpu.Run();

        Assert.Equal(2, cpu.Memory[value.Address]);
        Assert.Equal(1, cpu.Memory[stackVariable.Address]);
    }

    [Theory]
    [InlineData("+", 4)]
    [InlineData("-", 0)]
    [InlineData("*", 4)]
    [InlineData("/", 1)]
    public void Binary(string type, int expected)
    {
        var code = $"""
            var a = 2;
            var b = 2

            a = a {type} b;
            """;

        var builder = new YabalBuilder();
        builder.CompileCode(code);

        var cpu = Create(builder);
        cpu.Run();

        var address = builder.GetVariable("a").Pointer.Address;
        Assert.Equal(expected, cpu.Memory[address]);
    }

    [Theory]
    [InlineData("+=", 4)]
    [InlineData("-=", 0)]
    [InlineData("*=", 4)]
    [InlineData("/=", 1)]
    public void BinaryEqual(string type, int expected)
    {
        var code = $"""
            var a = 2;

            a {type} 2;
            """;

        var builder = new YabalBuilder();
        builder.CompileCode(code);

        var cpu = Create(builder);
        cpu.Run();

        var address = builder.GetVariable("a").Pointer.Address;
        Assert.Equal(expected, cpu.Memory[address]);
    }

    [Fact]
    public void Function()
    {
        const string code = """
            var a = 0

            void functionA(int amount) {
                a += amount
                functionB()
            }

            void functionB() {
                var value = 1

                a += value
            }

            functionA(2)
            """;

        var builder = new YabalBuilder();
        builder.CompileCode(code);

        var cpu = Create(builder);
        cpu.Run();

        var address = builder.GetVariable("a").Pointer.Address;
        Assert.Equal(3, cpu.Memory[address]);
    }

    [Fact]
    public void InlineAsm()
    {
        const string code = """
            var result = 0

            void increment(int amount) {
                asm {
                    AIN @result
                    BIN @amount
                    ADD
                    STA @result
                }
            }

            increment(1)
            """;

        var builder = new YabalBuilder();
        builder.CompileCode(code);

        var cpu = Create(builder);
        cpu.Run();

        var address = builder.GetVariable("result").Pointer.Address;
        Assert.Equal(1, cpu.Memory[address]);
    }

    [Fact]
    public void Array()
    {
        const string code = """
            int[] create_memory(int address) {
                return asm {
                    AIN @address
                }
            }

            var index = 1
            var value = 2
            var memory = create_memory(4095)

            memory[index] = value
            """;

        var builder = new YabalBuilder();
        builder.CompileCode(code);

        var cpu = Create(builder);
        cpu.Run();

        Assert.Equal(4095, cpu.Memory[builder.GetVariable("memory").Pointer.Address]);
        Assert.Equal(2, cpu.Memory[4096]);
    }

    [Theory]
    [InlineData(">", 10, -1, 0, 0)]
    [InlineData("<", 10, -1, 0, 10)]
    [InlineData(">=", 10, -1, 0, -1)]
    [InlineData("<=", 10, -1, 0, 10)]

    [InlineData(">", 0, 1, 10, 0)]
    [InlineData("<", 0, 1, 10, 10)]
    [InlineData(">=", 0, 1, 10, 0)]
    [InlineData("<=", 0, 1, 10, 11)]

    [InlineData("==", 10, -1, 10, 9)]
    [InlineData("!=", 10, -1, 0, 0)]
    public void While(string type, int start, int increment, int end, int expected)
    {
        var code = $$"""
            var value = {{ start }}

            while (value {{ type }} {{ end }}) {
                value += {{ increment }}
            }
            """;

        _output.WriteLine("Code:");
        _output.WriteLine(code);
        _output.WriteLine("");

        var builder = new YabalBuilder();
        builder.CompileCode(code);

        var cpu = Create(builder);
        cpu.Run();

        Assert.Equal(expected, cpu.Memory[builder.GetVariable("value").Pointer.Address]);
    }

    [Fact]
    public void WhileVariable()
    {
        const string code = $$"""
            var run = true
            var value = 10;

            while (run) {
                value -= 1
                run = value > 0
            }
            """;

        var builder = new YabalBuilder();
        builder.CompileCode(code);

        var cpu = Create(builder);
        cpu.Run();

        Assert.Equal(0, cpu.Memory[builder.GetVariable("value").Pointer.Address]);
    }

    [Fact]
    public void ArraySetter()
    {
        const string code = $$"""
            int[] create_array(int address) {
                return asm {
                    AIN @address
                }
            }

            int get_value() {
                return 0
            }

            var array = create_array(4095)
            array[get_value()] = 1
            """;

        var builder = new YabalBuilder();
        builder.CompileCode(code);

        var cpu = Create(builder);
        cpu.Run();

        Assert.Equal(1, cpu.Memory[4095]);
    }

    [Theory]
    [InlineData(10, ">", 0, 1)]
    [InlineData(9, ">", 0, 1)]
    [InlineData(10, "<", 0, 0)]
    [InlineData(0, "<", 0, 0)]
    [InlineData("true", "&&", "true", 1)]
    [InlineData("true", "&&", "false", 0)]
    [InlineData("false", "&&", "false", 0)]
    [InlineData("false", "&&", "true", 0)]
    [InlineData("true", "||", "true", 1)]
    [InlineData("true", "||", "false", 1)]
    [InlineData("false", "||", "false", 0)]
    [InlineData("false", "||", "true", 1)]
    public void Compare(object left, string type, object right, int expected)
    {
        var code = $$"""
            var left = {{ left }}
            var right = {{ right }}
            var optimized = {{ left }} {{ type }} {{ right }}
            var fast = left {{ type }} {{ right }}
            var slow = left {{ type }} right
            """;

        _output.WriteLine("Code:");
        _output.WriteLine(code);
        _output.WriteLine("");

        var builder = new YabalBuilder();
        builder.CompileCode(code);

        var cpu = Create(builder);
        cpu.Run();

        Assert.Equal(expected, cpu.Memory[builder.GetVariable("optimized").Pointer.Address]);
        Assert.Equal(expected, cpu.Memory[builder.GetVariable("fast").Pointer.Address]);
        Assert.Equal(expected, cpu.Memory[builder.GetVariable("slow").Pointer.Address]);
    }

    [Fact]
    public void For()
    {
        const string code = $$"""
            var value = 0

            for (; value < 10; value++) {
                value += 1
            }
            """;

        var builder = new YabalBuilder();
        builder.CompileCode(code);

        var cpu = Create(builder);
        cpu.Run();

        Assert.Equal(10, cpu.Memory[builder.GetVariable("value").Pointer.Address]);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void If(int step)
    {
        var code = $$"""
            var result = 0
            var value = {{step}}

            if (value == 1) {
                result = 1
            } else if (value == 2) {
                result = 2
            } else {
                result = 3
            }
            """;

        _output.WriteLine("Code:");
        _output.WriteLine(code);
        _output.WriteLine("");

        var builder = new YabalBuilder();
        builder.CompileCode(code);

        var cpu = Create(builder);
        cpu.Run();

        Assert.Equal(step, cpu.Memory[builder.GetVariable("result").Pointer.Address]);
    }
}
