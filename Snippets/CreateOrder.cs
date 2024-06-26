﻿using KBroker;

/* This code attempts to sell crypto. But since the validate flag is on,
 * server would only validate the request and not going to run it.
 */
Configuration.LoadSecretKeys();
var broker = new Broker();
var order = new Order();
Configuration.Pair = "LRCUSD";
order.SideType = OrderSide.Sell;
order.OrderType = OrderType.StopLoss;
order.Volume = 100M;
order.Price = 0.01M;
order.TriggerBy = OrderTriggerBy.Index;
order.Validate = true;
var response = broker.AddOrder(order);
Display.Print(response);
