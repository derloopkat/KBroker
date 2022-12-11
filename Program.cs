using System;
using System.Collections.Generic;
using System.Threading;

namespace KBroker
{
    class Program
    {
        public static DateTime FirstRun;

        static void Main(string[] args)
        {
            Program.FirstRun = DateTime.Now;
            Display.PrintCredits();
            var operation = Configuration.LoadOrders();
            var stopLossWasAddedByUser = operation.StopLoss.IsPlaced;

            /* Comment or uncomment the following code */
            /* This line is for running simulation with fake broker */
            var broker = new Simulator
            (
                currentPrice: 15.9m,
                priceTrend: SimulatedPriceTrend.MockedAscending,
                stopLossPrice: 13.727m
            );

            ///* Above line is running the application for real  */
            //var broker = new Broker();

            broker.WaitForStartPrice(broker.Pair, operation.StartPrice ?? 0);

            var response = operation.SetupOrders(broker);
            if (!operation.StopLoss.Error)
            {
                Display.PrintHeader(broker, operation);
            }
            else
                Display.Print(response);

            if (!stopLossWasAddedByUser) Display.AddOrderResponse(operation.StopLoss, response);

            if (broker is not Simulator && broker.GetSystemStatus() != Broker.SystemStatus.Online)
            {
                throw new SystemStatusException("Exchange unavailable or under maintenance.");
            }

            broker.Trade(operation);

            Console.ReadLine();

            //var broker = new Broker();
            //var order = new Order();
            //order.Pair = "AVAXUSD";
            //order.SideType = OrderSide.Sell;
            //order.OrderType = OrderType.Market;
            //order.Volume = 27.94624M;
            //order.Validate = true;
            //var response = broker.AddOrder(order);
            //Display.Print(response);

            //var operation = Configuration.LoadSettings();
            //var broker = new Simulator(Configuration.Pair, 87, SimulatedPriceTrend.MockedAscending);
            //var intervalMiliseconds = 10000;
            //operation.Setup(broker);
            //Display.PrintHeader(broker, operation);

            //while (!operation.TasksCompleted)
            //{
            //    operation.Execute(broker);
            //    Thread.Sleep(intervalMiliseconds);
            //}

            //var order = new Order("OUVRNM-FC6ZU-GRFAGD");
            //order.Pair = "AVAXUSD";
            //order.Price = 20;
            //var response = broker.EditOrder(order);
            //var response = broker.QueryOrder(order);
            //Display.Print(response);


        }
    }
}
