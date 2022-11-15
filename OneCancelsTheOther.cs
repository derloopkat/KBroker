using System;
using System.Threading;

namespace KBroker
{
    public class OneCancelsTheOther : Trigger
    {
        private int RepeatedLastPrice = 0;
        public OneCancelsTheOther()
        {
            TakeProfit = new Order();
            StopLoss = new Order();
        }

        private bool WaitForBetterPrice(Broker broker, decimal currentPrice, decimal? lastPrice)
        {
            RepeatedLastPrice += lastPrice == currentPrice ? 1 : - RepeatedLastPrice;
            if (TakeProfit.PlainGreed)
                return lastPrice.HasValue && currentPrice >= lastPrice && RepeatedLastPrice <= 3 ;
            else if (TakeProfit.BeGreedy)
                return !broker.PriceLostTooMuchGains(currentPrice, TakeProfit.Price.Value);
            else
                return false;
        }

        public override void Execute(Broker broker)
        {
            try
            {
                dynamic response = null;
                Price lastPrice = broker.LastPriceId == 0 ? null : broker.Prices[broker.LastPriceId]; ;
                Price price = broker.GetCurrentPrice();
                broker.PercentageDone = Display.PrintProgress(
                    StopLoss.Pair, 
                    TakeProfit.Price.Value,
                    StopLoss.Price.Value, 
                    price.Close, 
                    lastPrice?.Close ?? price.Close);

                if (price.Close >= TakeProfit.Price && !WaitForBetterPrice(broker, price.Close, lastPrice?.Close))
                {
                    if(broker.GetSystemStatus() != Broker.SystemStatus.Online)
                    {
                        throw new SystemStatusException("Exchange unavailable or under maintenance.");
                    }

                    if (!StopLoss.IsClosed)
                    {
                        response = broker.CancelOrder(StopLoss);
                        Display.CancelOrderResponse(StopLoss, response);
                        if (StopLoss.IsUnknown)
                        {
                            TasksCompleted = true;
                        }
                    }

                    if (StopLoss.IsClosed && !TakeProfit.IsPlaced)
                    {
                        response = broker.AddOrder(TakeProfit);
                        Display.AddOrderResponse(TakeProfit, response);
                    }

                    if (TakeProfit.IsPlaced)
                    {                        
                        while (!TakeProfit.IsCompleted)
                        {
                            response = broker.QueryOrder(TakeProfit);
                            Display.PrintSuccess("Take profit order not yet completed. Please wait...");
                            Thread.Sleep(4000);
                        }
                        Display.Print(response);
                        Display.PrintSuccess($"SUCCESS: Take profit executed. Received: ${TakeProfit.Cost}.");
                        TasksCompleted = true;
                    }
                }
                else if (price.Close < StopLoss.Price)
                {
                    response = broker.QueryOrder(StopLoss);
                    if (StopLoss.IsCompleted)
                    {
                        Display.Print(response);
                        Display.PrintSuccess($"Stoploss order was triggered by price. Received: ${StopLoss.Cost}.");
                        TasksCompleted = true;
                    }
                }
                else if (StopLoss.TriggerPrice.HasValue && price.Close >= StopLoss.TriggerPrice)
                {
                    StopLoss.Price = StopLoss.NewStoplossPrice;
                    response = broker.EditOrder(StopLoss);
                    if (!StopLoss.Error) 
                        StopLoss.TriggerPrice = null;
                    Display.AddOrderResponse(StopLoss, response);
                }
            }
            catch(SystemStatusException ex)
            {
                Display.PrintError(ex.Message, false);
                Logger.AddEntry(ex.Message);
                Thread.Sleep(20000);
            }
            catch (Exception ex)
            {
                Display.PrintError(ex.Message);
                Logger.AddEntry(ex.Message);
            }
        }
    }
}
