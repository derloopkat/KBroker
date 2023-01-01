using KBroker;

var broker = new Simulator
(
    currentPrice: (decimal)14.5,
    priceTrend: SimulatedPriceTrend.MockedAscending,
    stopLossPrice: 14
);

/*
{
  "timeout": 8000,
  "interval": 1,
  "version": 1.0,
  "operation": {
    "type": "OneCancelsTheOther",
    "pair": "FOOUSD",
    "startPrice": 15,
    "stoploss": {
      "id": "XXXXXX-XXXXX-123456",
      "volume": 1
    },
    "takeprofit": {
      "price": 16,
      "volume": 1,
      "greedy": true
    }
  }
}
*/





var broker = new Simulator
(
    currentPrice: 0.2067m,
    priceTrend: SimulatedPriceTrend.UseRealPrice,
    stopLossPrice: 0.2m
);

/* 
{
  "timeout": 8000,
  "interval": 4,
  "version": 1.0,
  "operation": {
    "type": "OneCancelsTheOther",
    "pair": "LRCUSD",
    "useMarketPrice": true,
    "stoploss": {
      "id": "XXXXXX-XXXXX-123456",
      "volume": 1
    },
    "takeprofit": {
      "price": 0.21,
      "volume": 1,
      "plainGreed": true
    }
  }
}
 */