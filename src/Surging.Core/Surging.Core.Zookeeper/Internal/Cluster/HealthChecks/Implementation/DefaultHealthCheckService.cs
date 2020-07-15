﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Surging.Core.CPlatform.Address;
using Surging.Core.CPlatform.Utilities;

namespace Surging.Core.Zookeeper.Internal.Cluster.HealthChecks.Implementation
{
    public class DefaultHealthCheckService : IHealthCheckService
    {
        private readonly int _timeout = 30000;
        private readonly Timer _timer;
        private readonly ConcurrentDictionary<ValueTuple<string, int>, MonitorEntry> _dictionary =
    new ConcurrentDictionary<ValueTuple<string, int>, MonitorEntry>();

        #region Implementation of IHealthCheckService
        public DefaultHealthCheckService()
        {
            var timeSpan = TimeSpan.FromSeconds(60);

            _timer = new Timer(async s =>
            {
                await Check(_dictionary.ToArray().Select(i => i.Value), _timeout);
            }, null, timeSpan, timeSpan);
        }

        public async ValueTask<bool> IsHealth(AddressModel address)
        {
            var ipAddress = address as IpAddressModel;
            MonitorEntry entry;
            var isHealth = !_dictionary.TryGetValue(new ValueTuple<string, int>(ipAddress.Ip, ipAddress.Port), out entry) ? await Check(address, _timeout) : entry.Health;
            return isHealth;
        }

        public void Monitor(AddressModel address)
        {
            var ipAddress = address as IpAddressModel;
            _dictionary.GetOrAdd(new ValueTuple<string, int>(ipAddress.Ip, ipAddress.Port), k => new MonitorEntry(address));
        }

        #region Implementation of IDisposable

        public void Dispose()
        {
            _timer.Dispose();
        }
        #endregion

        #endregion Implementation of IDisposable

        #region Private Method

        private static async Task<bool> Check(AddressModel address, int timeout)
        {
            var ipEndpoint = address.CreateEndPoint() as IPEndPoint;
            return SocketCheck.TestConnection(ipEndpoint.Address, ipEndpoint.Port, timeout);
        }

        private static async Task Check(IEnumerable<MonitorEntry> entrys, int timeout)
        {
            foreach (var entry in entrys)
            {
                var ipEndpoint = entry.EndPoint as IPEndPoint;
                if (SocketCheck.TestConnection(ipEndpoint.Address, ipEndpoint.Port, timeout))
                {
                    entry.UnhealthyTimes = 0;
                    entry.Health = true;
                }
                else
                {
                    entry.UnhealthyTimes++;
                    entry.Health = false;
                }
            }
        }

        #endregion Private Method

        #region Help Class

        protected class MonitorEntry
        {
            public MonitorEntry(AddressModel addressModel, bool health = true)
            {
                EndPoint = addressModel.CreateEndPoint();
                Health = health;

            }

            public int UnhealthyTimes { get; set; }

            public EndPoint EndPoint { get; set; }
            public bool Health { get; set; }
        }

        #endregion Help Class
    }
}

