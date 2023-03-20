using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KBroker
{
    public static class Display
    {
        private static Dictionary<string, string> ErrorDetails = new Dictionary<string, string>
        {
            { "EGeneral:Invalid arguments", "The request payload is malformed, incorrect or ambiguous." },
            { "EGeneral:Invalid arguments:Index unavailable", "Index pricing is unavailable for stop/profit orders on this pair."},
            { "EService:Unavailable", "The matching engine or API is offline" },
            { "EService:Market in cancel_only mode", "Request can't be made at this time. (See SystemStatus endpoint.)" },
            { "EService:Market in post_only mode", "Request can't be made at this time. (See SystemStatus endpoint.)" },
            { "EService:Deadline elapsed", "The request timed out according to the default or specified deadline" },
            { "EAPI:Invalid key", "An invalid API-Key header was supplied (see documentation's Authentication section)" },
            { "EAPI:Invalid signature", "An invalid API-Sign header was supplied (see documentation's Authentication section)" },
            { "EAPI:Invalid nonce", "An invalid nonce was supplied. Nonce must be an always increasing, unsigned 64-bit integer, for each request that is made with a particular API key." },
            { "EGeneral:Permission denied", "API key doesn't have permission to make this request." },
            { "EOrder:Cannot open position", "User/tier is ineligible for margin trading" },
            { "EOrder:Margin allowance exceeded", "User has exceeded their margin allowance" },
            { "EOrder:Margin level too low", "Client has insufficient equity or collateral" },
            { "EOrder:Margin position size exceeded", "Client would exceed the maximum position size for this pair" },
            { "EOrder:Insufficient margin", "Exchange does not have available funds for this margin trade" },
            { "EOrder:Insufficient funds", "Client does not have the necessary funds" },
            { "EOrder:Order minimum not met", "Order size does not meet ordermin. (See AssetPairs endpoint.)" },
            { "EOrder:Orders limit exceeded", "The number of open orders in a given pair is exceeded. We have various safeguards in place to protect against system abuse." },
            { "EOrder:Rate limit exceeded", "The user's max ratecount is exceeded for a given pair"}
        };

        public static int? LastPercentageDone = null;

        public static void Print(string message, ConsoleColor color = ConsoleColor.White, bool append = false)
        {
            Console.ForegroundColor = color;
            if (append)
                Console.Write(message);
            else
            {
                Console.WriteLine(message);
            }
            Console.ResetColor();
        }

        public static void Print(string message, ConsoleColor foregroundColor, ConsoleColor backgroundColor, bool append = false)
        {
            Console.BackgroundColor = backgroundColor;
            Print(message, foregroundColor, append);
        }

        public static void PrintSuccess(string message)
        {
            Print(message, ConsoleColor.Green);
            Console.Write("\a");
        }

        public static void PrintCode(string message)
        {
            Print(message, ConsoleColor.Gray);
        }

        public static void Print(dynamic response)
        {
            if (response != null)
            {
                string responseText = Convert.ToString(response["result"]);
                var errorCount = response["error"].Count;
                for (int i = 0; i < errorCount; i++)
                {
                    PrintError($"ERROR. {response["error"][i].Value}", bell: false);
                }

                if (errorCount == 0 && responseText != "{}")
                    PrintCode(responseText);
                else if(errorCount > 1 || (!(responseText ?? "").Contains("Too many requests")))
                    Console.Write("\a");
            }
        }

        public static void PrintError(string errorCode, bool bell = true)
        {
            var message = ErrorDetails.ContainsKey(errorCode) ? $"{errorCode}. Details: {ErrorDetails[errorCode]}" : $"{errorCode}";
            var clearCharactersToTheRight = new string(' ', Console.WindowWidth - Math.Min(Console.WindowWidth, message.Length));
            Print(message + clearCharactersToTheRight, ConsoleColor.Red);
            if (bell) Console.WriteLine("\a");
        }

        public static void PrintErrors(string[] errors)
        {
            foreach (var error in errors)
            {
                PrintError(error);
            }
        }

        public static string GetCurrencySymbol(string pair)
        {
            return pair.EndsWith("USD") ? "$"
                : pair.EndsWith("EUR") ? "€"
                : pair.EndsWith("GBP") ? "£"
                : "$";
        }

        public static string GetCurrencySymbol()
        {
            return GetCurrencySymbol(Configuration.Pair);
        }

        public static void PrintWaitingForStartPrice(decimal currentPrice, decimal initialPrice, decimal startPrice)
        {
            const int uniqueFrames = 3;
            var frames = new List<string>() { "\u25A0  ", " \u25A0 ", "  \u25A0", " \u25A0 ", "\u25A0  " };  // "|/-\\|";
            var timeStamp = DateTime.Now.ToLongTimeString();
            var symbol = GetCurrencySymbol();
            var priceColor = currentPrice < initialPrice ? ConsoleColor.Red
                : currentPrice > initialPrice ? ConsoleColor.Green
                : ConsoleColor.Yellow;
            Print($"{timeStamp} ", ConsoleColor.White, true);
            Print($"{Configuration.Pair} ", ConsoleColor.Gray, true);
            Print($"Start: ", ConsoleColor.White, true);
            Print($"{symbol}{startPrice} ", ConsoleColor.Cyan, true);
            Print($"Current: ", ConsoleColor.White, true);
            Print($"{symbol}{currentPrice}    ", priceColor, true);
            for(int i=0; i < frames.Count; i++)
            {
                Print($"{new String('\b', uniqueFrames)}{frames.ElementAt(i)}", priceColor, true);
                Thread.Sleep(200);
            }
            Print("\r", ConsoleColor.Black, true);
        }

        public static int PrintProgress(decimal takeProfitPrice, decimal stopLossPrice, decimal currentPrice, decimal lastPrice)
        {
            var timeStamp = DateTime.Now.ToLongTimeString();
            int percentageDone;
            var progress = GetProgressChart(takeProfitPrice, stopLossPrice, currentPrice, out percentageDone);
            var currentIndex = progress.IndexOf("O");
            var chartColor = currentIndex < 3 ? ConsoleColor.Red
                : currentIndex < 5 ? ConsoleColor.Yellow
                : currentIndex <= 10 ? ConsoleColor.Green
                : ConsoleColor.Cyan;
            Print($"{timeStamp} ", ConsoleColor.White, true);
            Print($"{Configuration.Pair} Progress", ConsoleColor.Gray, true);
            Print($" {progress} ", chartColor, true);
            Print(percentageDone < 10 ? "   " : percentageDone < 100 ? "  " : " ", ConsoleColor.Black, true);
            Print($"Close: ", ConsoleColor.Gray, true);
            Print($"{GetCurrencySymbol(Configuration.Pair)}", ConsoleColor.Gray, true);
            PrintPrice(currentPrice, lastPrice);
            Print("");
            ShowProgressInTaskBar(percentageDone);
            return percentageDone;
        }

        private static void ShowProgressInTaskBar(int percentageDone)
        {
            if(percentageDone != LastPercentageDone)
            {
                Console.Title = $"KBroker {percentageDone}%";
                LastPercentageDone = percentageDone;
            }
        }

        private static void PrintPrice(decimal currentPrice, decimal lastPrice)
        {
            var currentPriceAsString = currentPrice.ToString();
            var lastPriceAsString = lastPrice.ToString();
            var priceColor = currentPrice < lastPrice ? ConsoleColor.Red
                : currentPrice > lastPrice ? ConsoleColor.Green
                : ConsoleColor.Gray;
            if (currentPriceAsString.Length != lastPriceAsString.Length || currentPriceAsString.IndexOf('.') != lastPriceAsString.IndexOf('.'))
            {
                Print($"{currentPrice}", priceColor, true);
            }
            else
            {
                PrintDetailedPrice(currentPrice, lastPrice, priceColor);
            }
        }

        private static void PrintDetailedPrice(decimal currentPrice, decimal lastPrice, ConsoleColor trendColor)
        {
            //TODO: prevent current price zero
            const int MaxColoredDigits = 30;
            const int MaxColoredDigitsRepresentPriceChangePercentDifference = 2;
            var digitPriceColor = ConsoleColor.Gray;

            var differencePercent = Math.Round(((currentPrice - lastPrice) * 100) / currentPrice, 2);
            var coloredDigits = Math.Min(MaxColoredDigits, Math.Abs(differencePercent * MaxColoredDigits / MaxColoredDigitsRepresentPriceChangePercentDifference));
            for (var i = 0; i < currentPrice.ToString().Length; i++)
            {
                if (currentPrice.ToString()[i] != lastPrice.ToString()[i])
                {
                    digitPriceColor = trendColor;
                    coloredDigits++;
                }
                Print(currentPrice.ToString()[i].ToString(), digitPriceColor, true);
            }
            Print(" ", ConsoleColor.Black, true);
            Console.OutputEncoding = Encoding.UTF8;
            Print(new string('\u25A0', (int)coloredDigits).ToString(), trendColor, true);
            //Print($" {differencePercent}%", trendColor, true);
        }

        public static void AddOrderResponse(Order order, dynamic response)
        {
            var orderType = order.OrderType.GetDescription();
            if (order.Error)
            {
                Print(response);
                if(!string.IsNullOrEmpty(orderType))
                    PrintError($"Unable to create new {orderType} order!");
            }
            else
            {
                var description = response["result"]?["descr"]?["order"]?.Value ?? "no details supplied";
                Print(response);
                PrintSuccess($"Added {orderType} order '{description}' with Id: {order.Id}");
            }
        }

        public static void CancelOrderResponse(Order order, dynamic response)
        {
            if (order.IsUnknown)
            {
                PrintError("Stoploss order was triggered by price or cancelled by user.");
            }
            else if (order.Error)
            {
                PrintError($"Unable to cancel StopLoss order {order.Id}");
                Print(response);
            }
            else
                PrintSuccess($"Cancelled StopLoss order {order.Id}");
        }

        public static void PrintCredits()
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("╔═════════════════════════════════════════╗");
            Console.WriteLine("║    KBroker client app by @derloopkat    ║");
            Console.WriteLine("╚═════════════════════════════════════════╝");
            Console.WriteLine();
            Console.ResetColor();
        }

        public static void PrintHeader(Broker broker, Operation operation)
        {
            var symbol = GetCurrencySymbol();
            var triggerBy = operation.UseMarketPrice ? "market" : "last";
            var stopLoss = operation.StopLoss;
            var takeProfit = operation.TakeProfit;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Pair:\t\t{Configuration.Pair}");
            if (stopLoss.Price.HasValue)
            {
                var edit = stopLoss.Trigger.Price.HasValue ? $"Change to {symbol}{stopLoss.Trigger.NewPrice} When {triggerBy} price is {symbol}{stopLoss.Trigger.Price}" : "";
                Console.WriteLine($"Stop loss:\t{symbol}{stopLoss.Price} {edit}");
            }

            if (operation is OneCancelsTheOther)
            {
                if (takeProfit != null)
                {
                    var greed = takeProfit.BeGreedy | takeProfit.PlainGreed ? "(greedy)" : "";
                    var edit = takeProfit.Trigger.Price.HasValue ? $"Change to {symbol}{takeProfit.Trigger.NewPrice} When {triggerBy} price is {symbol}{takeProfit.Trigger.Price}" : "\b";
                    Console.WriteLine($"Take profit:\t{symbol}{takeProfit.Price} {edit} {greed}");
                    Console.WriteLine($"Volume:\t\t{takeProfit.Volume}");
                }
            }
            else if(operation is TrailingStopLoss)
            {
                Console.WriteLine($"Trailing:\t{symbol}{String.Join($", {symbol}", stopLoss.TrailingLevels)}");
                Console.WriteLine($"Volume:\t\t{stopLoss.Volume}");
            }
            if (broker is Simulator)
            {
                Print($"{Environment.NewLine} *** This is a simulation *** ", ConsoleColor.White, ConsoleColor.Red);
            }
            Console.WriteLine();
            Console.ResetColor();
        }

        private static string GetProgressChart(decimal takeProfitPrice, decimal stopLossPrice, decimal currentPrice, out int percentageDone)
        {
            var canvas = "----------".ToCharArray();
            var range = takeProfitPrice - stopLossPrice;
            var done = currentPrice - stopLossPrice;
            percentageDone = (int)(done * 100 / range);
            var currentIndex = RestrictValueToRange(percentageDone / 10, new Range(0, canvas.Length - 1));
            canvas[currentIndex] = 'O';
            return $"|{new String(canvas)}| {percentageDone}%";
        }

        private static int RestrictValueToRange(int value, Range range)
        {
            return Math.Max(range.Start.Value, Math.Min(value, range.End.Value));
        }
    }
}
