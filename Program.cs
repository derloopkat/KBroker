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
            var trigger = Configuration.LoadSettings();
            var stopLossWasAddedByUser = trigger.StopLoss.IsPlaced;

            /* Comment or uncomment the following code */
            /* This line is for running simulation with fake broker */
            var broker = new Simulator
            (
                currentPrice: 14.12m,
                priceTrend: SimulatedPriceTrend.MockedDescending,
                stopLossPrice: 13.727m
            );

            ///* Above line is running the application for real  */
            //var broker = new Broker();

            int seconds(double value) => (int)(value * 1000);
            broker.WaitForStartPrice(broker.Pair, trigger.StartPrice ?? 0);

            var response = trigger.SetupOrders(broker);
            if (!trigger.StopLoss.Error)
            {
                Display.PrintHeader(broker, trigger);
            }
            else
                Display.Print(response);

            if (!stopLossWasAddedByUser) Display.AddOrderResponse(trigger.StopLoss, response);

            if (!(broker is Simulator) && broker.GetSystemStatus() != Broker.SystemStatus.Online)
            {
                throw new Exception("Exchange unavailable or under maintenance.", new Exception("SystemStatus"));
            }

            while (!trigger.TasksCompleted)
            {
                var done = broker.PercentageDone;
                var wait = done >= 93 && done <= 95 ? Math.Min(seconds(8), broker.IntervalMiliseconds)
                        : done >= 96 && done <= 104 ? Math.Min(seconds(2), broker.IntervalMiliseconds)
                        : done >= 105 && done <= 110 ? Math.Min(seconds(8), broker.IntervalMiliseconds)
                        : done >= 111 && done <= 120 ? Math.Min(seconds(16), broker.IntervalMiliseconds)
                        : broker.IntervalMiliseconds;
                //System.Diagnostics.Debug.Print(wait.ToString());
                trigger.Execute(broker);
                Thread.Sleep(wait);
            }

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

            //var trigger = Configuration.LoadSettings();
            //var broker = new Simulator(Configuration.Pair, 87, SimulatedPriceTrend.MockedAscending);
            //var intervalMiliseconds = 10000;
            //trigger.Setup(broker);
            //Display.PrintHeader(broker, trigger);

            //while (!trigger.TasksCompleted)
            //{
            //    trigger.Execute(broker);
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
