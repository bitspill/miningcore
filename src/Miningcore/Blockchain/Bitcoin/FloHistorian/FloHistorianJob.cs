/*
Copyright 2017 Coin Foundry (coinfoundry.org)
Authors: Oliver Weichhold (oliver@weichhold.com)

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and
associated documentation files (the "Software"), to deal in the Software without restriction,
including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so,
subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial
portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT
LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.IO;
using System.Net.Http;
using NBitcoin;
using System.Text;
using System.Threading.Tasks;
using Miningcore.DaemonInterface;
using Miningcore.Extensions;
using Miningcore.Mining;
using MiningCore.Blockchain.Bitcoin.DaemonRequests;
using MiningCore.Blockchain.Bitcoin.FloHistorian;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;
using NLog;
using ProtoBuf;

namespace Miningcore.Blockchain.Bitcoin.FloHistorian
{
  public class FloHistorianJob : BitcoinJob
  {
    private readonly BlockchainStats _stats;
    private readonly DaemonClient _daemon;
    private readonly ILogger _logger;
    private readonly PoolBase _pool;

    private bool _historianEnabled;
    private string _historianAddress;
    private string _historianUrl;

    public FloHistorianJob(ILogger logger, BlockchainStats stats, DaemonClient daemon, PoolBase pool)
    {
      _logger = logger;
      _stats = stats;
      _daemon = daemon;
      _pool = pool;
    }

    protected override void AppendCoinbaseFinal(BitcoinStream bs)
    {
      var configExtra = poolConfig.Extra?.SafeExtensionDataAs<FloHistorianPoolConfigExtra>();
      _historianEnabled = configExtra?.HistorianEnabled ?? false;

      if (_historianEnabled)
      {
        _historianAddress = configExtra?.HistorianAddress;
        _historianUrl = configExtra?.HistorianUrl;

        if (string.IsNullOrEmpty(_historianAddress) || string.IsNullOrEmpty(_historianUrl))
        {
          _historianEnabled = false;
        }
      }

      _logger.Debug(() =>
        $"[FloHistorianJob] Historian - Enabled: {_historianEnabled} Address: {_historianAddress} URL: {_historianUrl}");


      byte[] data;
      if (_historianEnabled)
      {
        var sm = FetchAndSignHistorian().Result;
        if (sm == null)
        {
          if (string.IsNullOrEmpty(coin.CoinbaseTxAppendData)) return;
          data = Encoding.ASCII.GetBytes(coin.CoinbaseTxAppendData);
        }
        else
          using (var stream = new MemoryStream())
          {
            Serializer.Serialize(stream, sm);
            var b64 = new Base64Encoder().EncodeData(stream.ToArray());
            data = Encoding.ASCII.GetBytes("p64:" + b64);
          }
      }
      else if (string.IsNullOrEmpty(coin.CoinbaseTxAppendData)) return;
      else
      {
        data = Encoding.ASCII.GetBytes(coin.CoinbaseTxAppendData);
      }

      // ReSharper disable once AccessToModifiedClosure
      _logger.Info(() => $"[FloHistorianJob] {Encoding.ASCII.GetString(data)}");

      bs.ReadWriteAsVarString(ref data);
    }

    private async Task<HistorianDataPoint> FetchHistorian()
    {
      using (var client = new HttpClient())
      {
        try
        {
          var responseString = client.GetStringAsync(_historianUrl);
          var dp = JsonConvert.DeserializeObject<DataPoint>(await responseString);

          var pbdp = new HistorianDataPoint
          {
            Version = 1,
            MiningRigRentalsLast10 = dp.mrrLast10,
            MiningRigRentalsLast24Hr = dp.mrrLast24hr,
            FloMarketPriceBTC = dp.weightedBtc,
            FloMarketPriceUSD = dp.weightedUsd,
            PubKey = Encoding.ASCII.GetBytes(_historianAddress),
            AutominerPoolHashrate = _pool.PoolStats.PoolHashrate,
            FloNetHashRate = _stats.NetworkHashrate,
            LtcMarketPriceUSD = dp.cmcLtcUsd,
            NiceHashLast = dp.nhLast,
            NiceHashLast24Hr = dp.nhLast24hr
          };

          return pbdp;
        }
        catch (Exception e)
        {
          _logger.Error(() => $"[FloHistorianJob] Error fetching historian data point. {e}");
          return null;
        }
      }
    }

    private async Task<SignedMessage> FetchAndSignHistorian()
    {
      var hist = await FetchHistorian();
      byte[] sm;
      string b64;

      if (hist == null)
        return null;

      using (var stream = new MemoryStream())
      {
        Serializer.Serialize(stream, hist);
        sm = stream.ToArray();
        b64 = new Base64Encoder().EncodeData(sm);
      }

      var result = await _daemon.ExecuteCmdAnyAsync<string>(_logger, BitcoinCommands.SignMessage,
        new SignMessage {Address = _historianAddress, Message = b64});

      if (result.Error != null)
      {
        _logger.Warn(() =>
          $"[FloHistorianJob] Unable to sign historian. Daemon responded with: {result.Error.Message} Code {result.Error.Code}");
        return null;
      }

      var signed = new SignedMessage
      {
        MessageType = SignedMessage.MessageTypes.Historian,
        PubKey = Encoding.ASCII.GetBytes(_historianAddress),
        SerializedMessage = sm,
        Signature = Convert.FromBase64String(result.Response),
        SignatureType = SignedMessage.SignatureTypes.Flo
      };
      return signed;
    }
  }
}