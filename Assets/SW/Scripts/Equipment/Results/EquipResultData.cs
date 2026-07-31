public class EquipResultData
{
    public EquipResult Result { get; private set; }

    public bool IsSuccess =>
        Result == EquipResult.Success ||
        Result == EquipResult.Swapped;

    private EquipResultData(EquipResult result)
    {
        Result = result;
    }

    public static EquipResultData Success()
    {
        return new EquipResultData(EquipResult.Success);
    }

    public static EquipResultData Swapped()
    {
        return new EquipResultData(EquipResult.Swapped);
    }

    public static EquipResultData Failed(EquipResult result)
    {
        return new EquipResultData(result);
    }
}
