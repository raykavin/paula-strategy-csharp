using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using System;
using System.Diagnostics;
using System.Linq;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.FullAccess, AddIndicators = true)]
    public class BollingerReversionBot : Robot
    {
        #region Parâmetros - Configuração da Estratégia

        [Parameter("Período Bollinger", Group = "Estratégia", DefaultValue = 20, MinValue = 1)]
        public int BollingerPeriod { get; set; }

        [Parameter("Tipo", Group = "Estratégia", DefaultValue = MovingAverageType.Exponential)]
        public MovingAverageType BollingerMAType { get; set; }

        [Parameter("Pips dist. media", Group = "Estratégia", DefaultValue = MovingAverageType.Exponential)]
        public int PriceBollingerMiddleMADistanceMin { get; set; }

        [Parameter("Desvio Padrão", Group = "Estratégia", DefaultValue = 2.0, MinValue = 0.1)]
        public double StandardDeviation { get; set; }

        [Parameter("Período ATR", Group = "Estratégia", DefaultValue = 14, MinValue = 1)]
        public int AtrPeriod { get; set; }

        [Parameter("Largura Mínima Bandas (Pips)", Group = "Estratégia", DefaultValue = 20, MinValue = 5)]
        public double MinBandWidthPips { get; set; }

        #endregion

        #region Parâmetros - Gestão de Risco

        [Parameter("Volume Inicial (Lots)", Group = "Gestão de Risco", DefaultValue = 0.01, MinValue = 0.01)]
        public double InitialVolume { get; set; }

        [Parameter("Volume Máximo (Lots)", Group = "Gestão de Risco", DefaultValue = 1.0, MinValue = 0.01)]
        public double MaxVolume { get; set; }

        [Parameter("Usar Risco %", Group = "Gestão de Risco", DefaultValue = false)]
        public bool UseRiskPercent { get; set; }

        [Parameter("Risco % do Saldo", Group = "Gestão de Risco", DefaultValue = 1.0, MinValue = 0.1, MaxValue = 10)]
        public double RiskPercent { get; set; }

        [Parameter("Spread Máximo (Pips)", Group = "Gestão de Risco", DefaultValue = 3.0, MinValue = 0)]
        public double MaxSpreadPips { get; set; }

        #endregion

        #region Parâmetros - Take Profit e Stop Loss

        [Parameter("Usar ATR para TP/SL", Group = "TP/SL", DefaultValue = true)]
        public bool UseAtrForTpSl { get; set; }

        [Parameter("Múltiplo ATR para TP", Group = "TP/SL", DefaultValue = 2.0, MinValue = 0.1)]
        public double TpAtrMultiplier { get; set; }

        [Parameter("Múltiplo ATR para SL", Group = "TP/SL", DefaultValue = 1.5, MinValue = 0.1)]
        public double SlAtrMultiplier { get; set; }

        [Parameter("Take Profit (Pips)", Group = "TP/SL", DefaultValue = 50, MinValue = 1)]
        public double TakeProfitPips { get; set; }

        [Parameter("Stop Loss (Pips)", Group = "TP/SL", DefaultValue = 30, MinValue = 1)]
        public double StopLossPips { get; set; }

        #endregion

        #region Parâmetros - Trailing Stop

        [Parameter("Ativar Trailing Stop", Group = "Trailing Stop", DefaultValue = false)]
        public bool EnableTrailingStop { get; set; }

        [Parameter("Trigger Trailing (Pips)", Group = "Trailing Stop", DefaultValue = 20, MinValue = 1)]
        public double TrailingTriggerPips { get; set; }

        [Parameter("Step Trailing (Pips)", Group = "Trailing Stop", DefaultValue = 5, MinValue = 1)]
        public double TrailingStepPips { get; set; }

        #endregion

        #region Parâmetros - BreakEven

        [Parameter("Ativar BreakEven", Group = "BreakEven", DefaultValue = false)]
        public bool EnableBreakEven { get; set; }

        [Parameter("Trigger BreakEven (Pips)", Group = "BreakEven", DefaultValue = 15, MinValue = 1)]
        public double BreakEvenTriggerPips { get; set; }

        [Parameter("Lucro Travado (Pips)", Group = "BreakEven", DefaultValue = 2, MinValue = 0)]
        public double BreakEvenProfitPips { get; set; }

        #endregion

        #region Parâmetros - Limites Diários

        [Parameter("Lucro Máximo Diário ($)", Group = "Limites Diários", DefaultValue = 1000, MinValue = 0)]
        public double MaxDailyProfit { get; set; }

        [Parameter("Perda Máxima Diária ($)", Group = "Limites Diários", DefaultValue = 500, MinValue = 0)]
        public double MaxDailyLoss { get; set; }

        [Parameter("Lucro Máximo por Trade ($)", Group = "Limites Diários", DefaultValue = 200, MinValue = 0)]
        public double MaxProfitPerTrade { get; set; }

        [Parameter("Perda Máxima por Trade ($)", Group = "Limites Diários", DefaultValue = 100, MinValue = 0)]
        public double MaxLossPerTrade { get; set; }

        [Parameter("Máximo de Trades por Dia", Group = "Limites Diários", DefaultValue = 10, MinValue = 1)]
        public int MaxTradesPerDay { get; set; }

        #endregion

        #region Parâmetros - Horário de Operação

        [Parameter("Horário Inicial (HH:mm)", Group = "Horário", DefaultValue = "09:00")]
        public string StartTime { get; set; }

        [Parameter("Horário Final (HH:mm)", Group = "Horário", DefaultValue = "17:00")]
        public string EndTime { get; set; }

        [Parameter("Operar Segunda", Group = "Dias da Semana", DefaultValue = true)]
        public bool TradeMonday { get; set; }

        [Parameter("Operar Terça", Group = "Dias da Semana", DefaultValue = true)]
        public bool TradeTuesday { get; set; }

        [Parameter("Operar Quarta", Group = "Dias da Semana", DefaultValue = true)]
        public bool TradeWednesday { get; set; }

        [Parameter("Operar Quinta", Group = "Dias da Semana", DefaultValue = true)]
        public bool TradeThursday { get; set; }

        [Parameter("Operar Sexta", Group = "Dias da Semana", DefaultValue = true)]
        public bool TradeFriday { get; set; }

        #endregion

        #region Variáveis Privadas

        private BollingerBands _bollingerBands;
        private AverageTrueRange _atr;
        private bool _wasAboveUpper;
        private bool _wasBelowLower;
        private DateTime _lastTradeDate;
        private int _tradesToday;
        private double _dailyProfit;
        private double _dailyLoss;

        #endregion

        #region Métodos do cBot

        protected override void OnStart()
        {
            base.OnStart();
            //VsDebug();


            _bollingerBands = Indicators.BollingerBands(Bars.ClosePrices, BollingerPeriod, StandardDeviation, BollingerMAType);
            _atr = Indicators.AverageTrueRange(AtrPeriod, MovingAverageType.Simple);

            _lastTradeDate = Server.Time.Date;
            _tradesToday = 0;
            _dailyProfit = 0;
            _dailyLoss = 0;

            Positions.Closed += OnPositionClosed;

            Print($"BollingerReversionBot iniciado - {Symbol.Name}");
            Print($"Horário de operação: {StartTime} às {EndTime}");
            Print($"Largura mínima das bandas: {MinBandWidthPips} pips");
        }

        protected override void OnBar()
        {
            // Verifica novo dia
            if (Server.Time.Date != _lastTradeDate)
            {
                _lastTradeDate = Server.Time.Date;
                _tradesToday = 0;
                _dailyProfit = 0;
                _dailyLoss = 0;
            }

            // Verifica condições de operação
            if (!CanTrade())
                return;

            // Gerencia proteções (Trailing Stop e BreakEven)
            ManagePositions();

            // Verifica sinais de entrada
            CheckEntrySignals();
        }

        protected override void OnTick()
        {
            // Gerencia proteções em tempo real
            ManagePositions();

            // Verifica limites de lucro/perda por trade
            CheckTradeLimits();
        }

        private void OnPositionClosed(PositionClosedEventArgs args)
        {
            if (args.Position.SymbolName != Symbol.Name)
                return;

            var profit = args.Position.NetProfit;

            if (profit > 0)
                _dailyProfit += profit;
            else
                _dailyLoss += Math.Abs(profit);

            Print($"Posição fechada - P&L: {profit:C2} | Diário: +{_dailyProfit:C2} -{_dailyLoss:C2}");
        }

        #endregion

        #region Lógica de Entrada

        private void CheckEntrySignals()
        {
            if (HasOpenPosition())
                return;

            // Verifica largura das bandas
            if (!IsBandWidthValid())
            {
                return;
            }

            var currentClose = Bars.ClosePrices.Last(1);
            var previousClose = Bars.ClosePrices.Last(2);
            var upperBand = _bollingerBands.Top.Last(1);
            var lowerBand = _bollingerBands.Bottom.Last(1);
            var middleBand = _bollingerBands.Main.Last(1); // Média do Bollinger

            var previousUpper = _bollingerBands.Top.Last(1);
            var previousLower = _bollingerBands.Bottom.Last(2);

            // Define a distância mínima da média (em pips)
            double distanceFromMiddle = Math.Abs(currentClose - middleBand) / Symbol.PipSize;

            // Não entra se estiver muito perto da média
            if (distanceFromMiddle < PriceBollingerMiddleMADistanceMin)
                return;

            // Verifica sinal de VENDA
            if (previousClose > previousUpper)
            {
                _wasAboveUpper = true;
                _wasBelowLower = false;
            }
            else if (_wasAboveUpper && currentClose <= upperBand && currentClose >= lowerBand)
            {
                ExecuteSellOrder();
                _wasAboveUpper = false;
            }

            // Verifica sinal de COMPRA
            if (previousClose < previousLower)
            {
                _wasBelowLower = true;
                _wasAboveUpper = false;
            }
            else if (_wasBelowLower && currentClose >= lowerBand && currentClose <= upperBand)
            {
                ExecuteBuyOrder();
                _wasBelowLower = false;
            }
        }


        private bool IsBandWidthValid()
        {
            var upperBand = _bollingerBands.Top.Last(1);
            var lowerBand = _bollingerBands.Bottom.Last(1);
            var bandWidth = (upperBand - lowerBand) / Symbol.PipSize;

            if (bandWidth < MinBandWidthPips)
            {
                Print($"Bandas muito estreitas: {bandWidth:F1} pips (mínimo: {MinBandWidthPips} pips)");
                return false;
            }

            return true;
        }

        private void ExecuteBuyOrder()
        {
            var volumeLots = CalculateVolumeLots(TradeType.Buy);
            if (volumeLots <= 0)
            {
                Print("Volume calculado inválido para ordem de compra");
                return;
            }

            // Converte lotes para unidades
            var volumeInUnits = Symbol.NormalizeVolumeInUnits(volumeLots * Symbol.LotSize);

            var sl = CalculateStopLoss(TradeType.Buy);
            var tp = CalculateTakeProfit(TradeType.Buy);

            var result = ExecuteMarketOrder(TradeType.Buy, Symbol.Name, volumeInUnits, "BollingerReversion", sl, tp);

            if (result.IsSuccessful)
            {
                _tradesToday++;
                var bandWidth = (_bollingerBands.Top.Last(1) - _bollingerBands.Bottom.Last(1)) / Symbol.PipSize;
                Print($"Ordem de COMPRA executada - Volume: {volumeLots:F2} lots | SL: {sl:F1} pips | TP: {tp:F1} pips | Largura Bandas: {bandWidth:F1} pips");
            }
            else
            {
                Print($"Erro ao executar ordem de compra: {result.Error}");
                Print($"Volume tentado: {volumeLots:F2} lots ({volumeInUnits} unidades)");
            }
        }

        private void ExecuteSellOrder()
        {
            var volumeLots = CalculateVolumeLots(TradeType.Sell);
            if (volumeLots <= 0)
            {
                Print("Volume calculado inválido para ordem de venda");
                return;
            }

            // Converte lotes para unidades
            var volumeInUnits = Symbol.NormalizeVolumeInUnits(volumeLots * Symbol.LotSize);

            var sl = CalculateStopLoss(TradeType.Sell);
            var tp = CalculateTakeProfit(TradeType.Sell);

            var result = ExecuteMarketOrder(TradeType.Sell, Symbol.Name, volumeInUnits, "BollingerReversion", sl, tp);

            if (result.IsSuccessful)
            {
                _tradesToday++;
                var bandWidth = (_bollingerBands.Top.Last(1) - _bollingerBands.Bottom.Last(1)) / Symbol.PipSize;
                Print($"Ordem de VENDA executada - Volume: {volumeLots:F2} lots | SL: {sl:F1} pips | TP: {tp:F1} pips | Largura Bandas: {bandWidth:F1} pips");
            }
            else
            {
                Print($"Erro ao executar ordem de venda: {result.Error}");
                Print($"Volume tentado: {volumeLots:F2} lots ({volumeInUnits} unidades)");
            }
        }

        #endregion

        #region Cálculos

        private double CalculateVolumeLots(TradeType tradeType)
        {
            double volumeLots;

            if (UseRiskPercent)
            {
                var sl = CalculateStopLoss(tradeType);
                var riskAmount = Account.Balance * (RiskPercent / 100);

                // Calcula volume baseado no risco
                var volumeInUnits = riskAmount / (sl * Symbol.PipValue);
                volumeLots = volumeInUnits / Symbol.LotSize;
            }
            else
            {
                // Usa volume fixo em lotes
                volumeLots = InitialVolume;
            }

            // Limita ao volume máximo
            volumeLots = Math.Min(volumeLots, MaxVolume);

            // Garante que o volume está dentro dos limites
            var minLots = Symbol.VolumeInUnitsMin / Symbol.LotSize;
            var maxLots = Symbol.VolumeInUnitsMax / Symbol.LotSize;

            if (volumeLots < minLots)
                volumeLots = minLots;

            if (volumeLots > maxLots)
                volumeLots = maxLots;

            // Arredonda para o step correto
            var volumeStep = Symbol.VolumeInUnitsStep / Symbol.LotSize;
            volumeLots = Math.Round(volumeLots / volumeStep) * volumeStep;

            return volumeLots;
        }

        private double CalculateStopLoss(TradeType tradeType)
        {
            if (UseAtrForTpSl)
            {
                var atrValue = _atr.Result.Last(1);
                return atrValue * SlAtrMultiplier / Symbol.PipSize;
            }

            return StopLossPips;
        }

        private double CalculateTakeProfit(TradeType tradeType)
        {
            if (UseAtrForTpSl)
            {
                var atrValue = _atr.Result.Last(1);
                return atrValue * TpAtrMultiplier / Symbol.PipSize;
            }

            return TakeProfitPips;
        }

        #endregion

        #region Gestão de Posições

        private void ManagePositions()
        {
            foreach (var position in Positions.Where(p => p.SymbolName == Symbol.Name && p.Label == "BollingerReversion"))
            {
                if (EnableBreakEven)
                    ManageBreakEven(position);

                if (EnableTrailingStop)
                    ManageTrailingStop(position);
            }
        }

        private void ManageBreakEven(Position position)
        {
            if (position.StopLoss == position.EntryPrice)
                return;

            var pips = position.Pips;

            if (pips >= BreakEvenTriggerPips)
            {
                var newSl = position.TradeType == TradeType.Buy
                    ? position.EntryPrice + BreakEvenProfitPips * Symbol.PipSize
                    : position.EntryPrice - BreakEvenProfitPips * Symbol.PipSize;

                if ((position.TradeType == TradeType.Buy && newSl > position.StopLoss) ||
                    (position.TradeType == TradeType.Sell && newSl < position.StopLoss))
                {
                    ModifyPosition(position, newSl, position.TakeProfit, ProtectionType.Absolute);
                    Print($"BreakEven ativado para posição {position.Id}");
                }
            }
        }

        private void ManageTrailingStop(Position position)
        {
            var pips = position.Pips;

            if (pips >= TrailingTriggerPips)
            {
                var newSl = position.TradeType == TradeType.Buy
                    ? Symbol.Bid - TrailingStepPips * Symbol.PipSize
                    : Symbol.Ask + TrailingStepPips * Symbol.PipSize;

                if ((position.TradeType == TradeType.Buy && newSl > position.StopLoss) ||
                    (position.TradeType == TradeType.Sell && newSl < position.StopLoss))
                {
                    ModifyPosition(position, newSl, position.TakeProfit, ProtectionType.Absolute);
                }
            }
        }

        private void CheckTradeLimits()
        {
            foreach (var position in Positions.Where(p => p.SymbolName == Symbol.Name && p.Label == "BollingerReversion"))
            {
                var profit = position.NetProfit;

                // Fecha se atingir limite de lucro por trade
                if (MaxProfitPerTrade > 0 && profit >= MaxProfitPerTrade)
                {
                    ClosePosition(position);
                    Print($"Posição {position.Id} fechada - Limite de lucro por trade atingido: {profit:C2}");
                }

                // Fecha se atingir limite de perda por trade
                if (MaxLossPerTrade > 0 && profit <= -MaxLossPerTrade)
                {
                    ClosePosition(position);
                    Print($"Posição {position.Id} fechada - Limite de perda por trade atingido: {profit:C2}");
                }
            }
        }

        #endregion

        #region Validações

        private bool CanTrade()
        {
            // Verifica horário de operação
            if (!IsInTradingHours())
                return false;

            // Verifica dia da semana
            if (!IsTradingDay())
                return false;

            // Verifica spread
            var spread = Symbol.Spread / Symbol.PipSize;
            if (spread > MaxSpreadPips)
            {
                Print($"Spread muito alto: {spread:F1} pips");
                return false;
            }

            // Verifica limites diários
            if (MaxDailyProfit > 0 && _dailyProfit >= MaxDailyProfit)
            {
                Print("Lucro máximo diário atingido");
                return false;
            }

            if (MaxDailyLoss > 0 && _dailyLoss >= MaxDailyLoss)
            {
                Print("Perda máxima diária atingida");
                return false;
            }

            if (_tradesToday >= MaxTradesPerDay)
            {
                Print("Número máximo de trades diários atingido");
                return false;
            }

            return true;
        }

        private bool IsInTradingHours()
        {
            var currentTime = Server.Time.TimeOfDay;
            var start = TimeSpan.Parse(StartTime);
            var end = TimeSpan.Parse(EndTime);

            if (start <= end)
                return currentTime >= start && currentTime <= end;
            else
                return currentTime >= start || currentTime <= end;
        }

        private bool IsTradingDay()
        {
            switch (Server.Time.DayOfWeek)
            {
                case DayOfWeek.Monday:
                    return TradeMonday;
                case DayOfWeek.Tuesday:
                    return TradeTuesday;
                case DayOfWeek.Wednesday:
                    return TradeWednesday;
                case DayOfWeek.Thursday:
                    return TradeThursday;
                case DayOfWeek.Friday:
                    return TradeFriday;
                default:
                    return false;
            }
        }

        private bool HasOpenPosition()
        {
            return Positions.Any(p => p.SymbolName == Symbol.Name && p.Label == "BollingerReversion");
        }

        #endregion

        protected override void OnStop()
        {
            Print($"BollingerReversionBot parado");
            Print($"Resultado do dia: Lucro: {_dailyProfit:C2} | Perda: {_dailyLoss:C2}");
        }

        private static void VsDebug() => Debugger.Launch();
    }
}