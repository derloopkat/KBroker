using System;
using System.Threading;

namespace KBroker
{
    public class OneCancelsTheOther : Operation
    {
        private bool FailedToSellGreedyTakeprofit = false;
        public OneCancelsTheOther()
        {
            TakeProfit = new Order();
            StopLoss = new Order();
        }

        private bool WaitForBetterPrice(Broker broker, decimal currentPrice, decimal? lastPrice)
        {
            if (TakeProfit.PlainGreed)
                return lastPrice.HasValue && currentPrice >= lastPrice && !FailedToSellGreedyTakeprofit;
            else if (TakeProfit.BeGreedy)
                return !broker.PriceLostTooMuchMargin(currentPrice, TakeProfit.Price.Value, UseMarketPrice);
            else
                return false;
        }

        private void EditOrderPrice(Order order, Broker broker)
        {
            order.Price = order.Trigger.NewPrice;
            if (order.HasId)
            {
                var response = broker.EditOrder(order);
                Display.AddOrderResponse(order, response); 
            }

            if (!order.Error)
            {
                order.Trigger.Price = null;
            }
        }

        public override void Execute(Broker broker)
        {
            try
            {
                dynamic response = null;
                Price lastPrice = broker.LastPriceId == 0 ? null : broker.Prices[broker.LastPriceId];
                Price price = broker.GetCurrentPrice();
                FailedToSellGreedyTakeprofit = price.Close < TakeProfit.Price && broker.MaxPrice.Close > 0;
                broker.PercentageDone = Display.PrintProgress(
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
                else if (StopLoss.Trigger.Price.HasValue && price.Close >= StopLoss.Trigger.Price)
                {
                    EditOrderPrice(StopLoss, broker);
                    if(!StopLoss.Error)
                        Display.PrintSuccess($"Increased stoploss price to ${StopLoss.Price}.");
                }
                else if (TakeProfit.Trigger.Price.HasValue && price.Close <= TakeProfit.Trigger.Price)
                {
                    EditOrderPrice(TakeProfit, broker);
                    if(!TakeProfit.Error)
                        Display.PrintSuccess($"Reduced takeprofit price to ${TakeProfit.Price}.");
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
