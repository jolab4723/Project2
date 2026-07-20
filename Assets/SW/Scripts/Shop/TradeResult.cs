public enum TradeResult : byte
{
    Success = 0,
    InvalidItem = 1,
    NotEnoughGold = 2,
    NoSpace = 3,
    TransferFailed = 4,
    StockUpdateFailed = 5
}