internal sealed class EquipResultData
{
    public EquipResult Result { get; }

    public bool IsSuccess =>
        Result == EquipResult.Success ||
        Result == EquipResult.Swapped;

    private EquipResultData(EquipResult result)
    {
        Result = result;
    }

    internal static EquipResultData Success()
    {
        return new EquipResultData(EquipResult.Success);
    }

    internal static EquipResultData Swapped()
    {
        return new EquipResultData(EquipResult.Swapped);
    }

    internal static EquipResultData Failed(EquipResult result)
    {
        return new EquipResultData(result);
    }
}
