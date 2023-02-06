using Yabal.Instructions;

namespace Yabal;

public class PointerWithOffset : Pointer
{
    private readonly Pointer _pointer;
    private readonly int _offset;

    public PointerWithOffset(Pointer pointer, int offset)
    {
        _pointer = pointer;
        _offset = offset;
    }

    public override string Name => $"{_pointer.Name}+{_offset}";

    public override bool IsSmall => _pointer.IsSmall;

    public override int Bank => _pointer.Bank;

    public override int Address => _pointer.Address + _offset;

    public override int Get(IReadOnlyDictionary<InstructionPointer, int> mappings)
    {
        var value = _pointer.Get(mappings);

        return value + _offset;
    }

    public override string ToString()
    {
        return $"{_pointer} + {_offset}";
    }
}
