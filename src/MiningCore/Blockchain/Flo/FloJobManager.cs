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
using System.Text;
using System.Threading.Tasks;
using Autofac;
using MiningCore.Api.Responses;
using MiningCore.Blockchain.Bitcoin;
using MiningCore.Blockchain.Bitcoin.DaemonResponses;
using MiningCore.Blockchain.Flo.Configuration;
using MiningCore.Blockchain.Flo.DaemonRequests;
using MiningCore.Blockchain.Flo.Historian;
using MiningCore.Blockchain.Flo.Oip;
using MiningCore.Configuration;
using MiningCore.Extensions;
using MiningCore.Notifications;
using MiningCore.Time;
using Newtonsoft.Json;
using NLog;
using ProtoBuf;

namespace MiningCore.Blockchain.Flo
{
    public class FloJobManager : BitcoinJobManager<FloJob, BlockTemplate>
    {
        public FloJobManager(
            IComponentContext ctx,
            NotificationService notificationService,
            IMasterClock clock,
            IExtraNonceProvider extraNonceProvider) :
            base(ctx, notificationService, clock, extraNonceProvider)
        {
        }

        protected FloPoolConfigExtra extraFloPoolConfig;
        protected bool historianEnabled;
        protected string historianAddress;
        protected string historianUrl;
        protected string floData;

        protected async Task<HistorianDataPoint> FetchHistorian()
        {
            using (var client = new HttpClient())
            {
                var responseString = await client.GetStringAsync(historianUrl);
                var dp = JsonConvert.DeserializeObject<Datapoint>(responseString);

                var apiUrl = $"http://127.0.0.1:4000/api/pools/{poolConfig.Id}";
                var apiResponse = await client.GetStringAsync(apiUrl);
                var pi = JsonConvert.DeserializeObject<GetPoolResponse>(apiResponse).Pool;

                var pbdp = new HistorianDataPoint
                {
                    Version = 1,
                    AutominerPoolHashrate = pi.PoolStats.PoolHashrate,
                    FloNetHashRate = pi.NetworkStats.NetworkHashrate,
                    LtcMarketPriceUSD = dp.cmcLtcUsd,
                    MiningRigRentalsLast10 = dp.mrrLast10,
                    MiningRigRentalsLast24Hr = dp.mrrLast24hr,
                    FloMarketPriceBTC = dp.weightedBtc,
                    FloMarketPriceUSD = dp.weightedUsd,
                    PubKey = Encoding.UTF8.GetBytes(historianAddress)
                };

                return pbdp;
            }
        }

        protected async Task<SignedMessage> FetchAndSignHistorian()
        {
            var hist = await FetchHistorian();

            var serializedMessage = new MemoryStream();
            Serializer.Serialize(serializedMessage, hist);


            var result = await daemon.ExecuteCmdAnyAsync<string>(BitcoinCommands.SignMessage,
                new SignMessage
                {
                    Address = historianAddress,
                    Message = Convert.ToBase64String(serializedMessage.ToArray())
                });

            if (result.Error != null)
            {
                logger.Warn(() =>
                    $"[{LogCat}] Unable to sign historian. Daemon responded with: {result.Error.Message} Code {result.Error.Code}");
                return null;
            }

            var signed = new SignedMessage
            {
                MessageType = SignedMessage.MessageTypes.Historian,
                PubKey = Encoding.UTF8.GetBytes(historianAddress),
                SerializedMessage = serializedMessage.ToArray(),
                Signature = Convert.FromBase64String(result.Response),
                SignatureType = SignedMessage.SignatureTypes.Flo
            };
            return signed;
        }

        #region Overrides

        protected override string LogCat => "Flo Job Manager";

        public override void Configure(PoolConfig poolConfig, ClusterConfig clusterConfig)
        {
            extraFloPoolConfig = poolConfig.Extra.SafeExtensionDataAs<FloPoolConfigExtra>();

            if (extraPoolConfig?.MaxActiveJobs.HasValue == true)
                maxActiveJobs = extraPoolConfig.MaxActiveJobs.Value;

            if (extraFloPoolConfig?.HistorianEnabled.HasValue == true &&
                extraFloPoolConfig?.HistorianEnabled.Value == true)
            {
                historianEnabled = true;
                historianUrl = extraFloPoolConfig.HistorianUrl;
                historianAddress = extraFloPoolConfig.HistorianAddress;
            }
            else
            {
                historianEnabled = false;
            }
            
            floData = !string.IsNullOrEmpty(extraFloPoolConfig?.FloData)
                ? extraFloPoolConfig.FloData
                : "Mined by MiningCore";

            base.Configure(poolConfig, clusterConfig);
        }

        protected override async Task<(bool IsNew, bool Force)> UpdateJob(bool forceUpdate, string via = null, string json = null)
        {
            logger.LogInvoke(LogCat);

            try
            {
                if (forceUpdate)
                    lastJobRebroadcast = clock.Now;

                var response = string.IsNullOrEmpty(json) ? await GetBlockTemplateAsync() : GetBlockTemplateFromJson(json);

                var jobFloData = floData;

                if (historianEnabled)
                {
                    try
                    {
                        var hdp = await FetchAndSignHistorian(); 
                        var ms = new MemoryStream();
                        Serializer.Serialize(ms, hdp);
                        jobFloData = "p64:" + Convert.ToBase64String(ms.ToArray());
                    }
                    catch (Exception e)
                    {
                        logger.Warn(e, $"[{LogCat}] Failure generating historian message.");
                        jobFloData = floData;
                    }
                }

                // may happen if daemon is currently not connected to peers
                if (response.Error != null)
                {
                    logger.Warn(() => $"[{LogCat}] Unable to update job. Daemon responded with: {response.Error.Message} Code {response.Error.Code}");
                    return (false, forceUpdate);
                }

                var blockTemplate = response.Response;

                var job = currentJob;
                var isNew = job == null ||
                    (blockTemplate != null &&
                    job.BlockTemplate?.PreviousBlockhash != blockTemplate.PreviousBlockhash &&
                    blockTemplate.Height > job.BlockTemplate?.Height);

                if (isNew || forceUpdate)
                {
                    job = new FloJob();

                    job.Init(blockTemplate, NextJobId(),
                        poolConfig, clusterConfig, clock, poolAddressDestination, networkType, isPoS,
                        ShareMultiplier, extraPoolPaymentProcessingConfig?.BlockrewardMultiplier ?? 1.0m,
                        coinbaseHasher, headerHasher, blockHasher, jobFloData);

                    lock (jobLock)
                    {
                        if (isNew)
                        {
                            if (via != null)
                                logger.Info(() => $"[{LogCat}] Detected new block {blockTemplate.Height} via {via}");
                            else
                                logger.Info(() => $"[{LogCat}] Detected new block {blockTemplate.Height}");

                            validJobs.Clear();

                            // update stats
                            BlockchainStats.LastNetworkBlockTime = clock.Now;
                            BlockchainStats.BlockHeight = blockTemplate.Height;
                            BlockchainStats.NetworkDifficulty = job.Difficulty;
                        }

                        else
                        {
                            // trim active jobs
                            while (validJobs.Count > maxActiveJobs - 1)
                                validJobs.RemoveAt(0);
                        }

                        validJobs.Add(job);
                    }

                    currentJob = job;
                }

                return (isNew, forceUpdate);
            }

            catch (Exception ex)
            {
                logger.Error(ex, () => $"[{LogCat}] Error during {nameof(UpdateJob)}");
            }

            return (false, forceUpdate);
        }

        #endregion // Overrides
    }
}
