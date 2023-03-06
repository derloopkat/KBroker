namespace KBroker
{
    public enum OrderType
    {
        Market,
        Limit,
        StopLoss,
        TakeProfit,
        StopLossLimit,
        TakeProfitLimit,
        SettlePosition,
        CancelOrder
    }

    public enum OrderSide
    {
        Sell,
        Buy
    }

    public enum OrderTriggerBy
    {
        Last,
        Index
    }

    public enum SimulatedPriceTrend
    {
        Descending = -1,
        Ascending = 1
    }

    public static class EnumExtensions
    {
        public static string GetDescription(this OrderType? orderType)
        {
            switch (orderType)
            {
                case OrderType.Market:
                    return "market";
                case OrderType.Limit:
                    return "limit";
                case OrderType.StopLoss:
                    return "stop-loss";
                case OrderType.TakeProfit:
                    return "take-profit";
                case OrderType.StopLossLimit:
                    return "stop-loss-limit";
                case OrderType.TakeProfitLimit:
                    return "take-profit-limit";
                case OrderType.SettlePosition:
                    return "settle-position";
                default:
                    return null;
            }
        }

        public static string GetDescription(this OrderSide? orderSide)
        {
            switch (orderSide)
            {
                case OrderSide.Buy:
                    return "buy";
                case OrderSide.Sell:
                    return "sell";
                default:
                    return null;
            }
        }

        public static string GetDescription(this OrderTriggerBy? orderTriggerBy)
        {
            switch (orderTriggerBy)
            {
                case OrderTriggerBy.Last:
                    return "last";
                case OrderTriggerBy.Index:
                    return "index";
                default:
                    return null;
            }
        }
    }
}
