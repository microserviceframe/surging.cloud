using Microsoft.Extensions.Logging;
using Surging.Core.CPlatform.Address;
using Surging.Core.CPlatform.Exceptions;
using Surging.Core.CPlatform.Routing;
using Surging.Core.CPlatform.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Surging.Core.CPlatform.Runtime.Client.HealthChecks.Implementation
{
    /// <summary>
    /// 默认健康检查服务(每10秒会检查一次服务状态，在构造函数中添加服务管理事件) 
    /// </summary>
    public class DefaultHealthCheckService : IHealthCheckService, IDisposable
    {
        private readonly ConcurrentDictionary<Tuple<string, int>, MonitorEntry> _dictionary =
            new ConcurrentDictionary<Tuple<string, int>, MonitorEntry>();
        private readonly IServiceRouteManager _serviceRouteManager;
        private readonly int _timeout = AppConfig.ServerOptions.HealthCheckTimeout;
        private readonly Timer _timer;
        private EventHandler<HealthCheckEventArgs> _removed;

        private EventHandler<HealthCheckEventArgs> _changed;
        private static ILogger<DefaultHealthCheckService> _logger;
        public event EventHandler<HealthCheckEventArgs> Removed
        {
            add { _removed += value; }
            remove { _removed -= value; }
        }

        public event EventHandler<HealthCheckEventArgs> Changed
        {
            add { _changed += value; }
            remove { _changed -= value; }
        }

        /// <summary>
        /// 默认心跳检查服务(每10秒会检查一次服务状态，在构造函数中添加服务管理事件) 
        /// </summary>
        /// <param name="serviceRouteManager"></param>
        public DefaultHealthCheckService(IServiceRouteManager serviceRouteManager)
        {
            _logger = ServiceLocator.GetService<ILogger<DefaultHealthCheckService>>();
            var timeSpan = TimeSpan.FromSeconds(AppConfig.ServerOptions.HealthCheckWatchIntervalInSeconds);

            _serviceRouteManager = serviceRouteManager;
            //建立计时器
            _timer = new Timer(async s =>
            {
                //检查服务是否可用
                await Check(_dictionary.ToArray().Select(i => i.Value), _timeout);
                //移除不可用的服务地址
                RemoveUnhealthyAddress(_dictionary.ToArray().Select(i => i.Value).Where(m => m.UnhealthyTimes >= AppConfig.ServerOptions.AllowServerUnhealthyTimes));
            }, null, timeSpan, timeSpan);

            //去除监控。
            serviceRouteManager.Removed += (s, e) =>
            {
                Remove(e.Route.Address);
            };
            //重新监控。
            serviceRouteManager.Created += async (s, e) =>
            {
                var keys = e.Route.Address.Select(address =>
                {
                    var ipAddress = address as IpAddressModel;
                    return new Tuple<string, int>(ipAddress.Ip, ipAddress.Port);
                });
                await Check(_dictionary.Where(i => keys.Contains(i.Key)).Select(i => i.Value), _timeout);
            };
            //重新监控。
            serviceRouteManager.Changed += async (s, e) =>
            {
                var keys = e.Route.Address.Select(address => {
                    var ipAddress = address as IpAddressModel;
                    return new Tuple<string, int>(ipAddress.Ip, ipAddress.Port);
                });
                await Check(_dictionary.Where(i => keys.Contains(i.Key)).Select(i => i.Value), _timeout);
            };
        }


        #region Implementation of IHealthCheckService

        /// <summary>
        /// 监控一个地址。
        /// </summary>
        /// <param name="address">地址模型。</param>
        /// <returns>一个任务。</returns>
        //public async Task Monitor(AddressModel address)
        //{
        //    var ipAddress = address as IpAddressModel;
        //    var entry = _dictionary.GetOrAdd(new Tuple<string, int>(ipAddress.Ip, ipAddress.Port), k => { var entry = new MonitorEntry(address, Check(ipAddress.Ip, _timeout).Result); return entry; });
        //    OnChanged(new HealthCheckEventArgs(address, entry.Health));
        //}

        /// <summary>
        /// 判断一个地址是否健康。
        /// </summary>
        /// <param name="address">地址模型。</param>
        /// <returns>健康返回true，否则返回false。</returns>
        public async Task<bool> IsHealth(AddressModel address)
        {
            var ipAddress = address as IpAddressModel;
            var entry = _dictionary.AddOrUpdate(new Tuple<string, int>(ipAddress.Ip, ipAddress.Port), k => { var entry = new MonitorEntry(address, Check(ipAddress.Ip, _timeout).Result); return entry; }, (k, v) => { Check(v, _timeout).Wait(); return v; });
            OnChanged(new HealthCheckEventArgs(address, entry.Health));
            return entry.Health;
            //var ipAddress = address as IpAddressModel;
            //MonitorEntry entry;
            //var isHealth = !_dictionary.TryGetValue(new Tuple<string, int>(ipAddress.Ip, ipAddress.Port), out entry) ? await Check(ipAddress.Ip, _timeout) : entry.Health;
            //OnChanged(new HealthCheckEventArgs(address, isHealth));
            //return isHealth;
        }

        /// <summary>
        /// 标记一个地址为失败的。
        /// </summary>
        /// <param name="address">地址模型。</param>
        /// <returns>一个任务。</returns>
        public Task MarkFailure(AddressModel address)
        {
            return Task.Run(() =>
            {
                var ipAddress = address as IpAddressModel;
                var entry = _dictionary.GetOrAdd(new Tuple<string, int>(ipAddress.Ip, ipAddress.Port), k => new MonitorEntry(address, false));
                entry.Health = false;
            });
        }

        protected void OnRemoved(params HealthCheckEventArgs[] args)
        {
            if (_removed == null)
                return;

            foreach (var arg in args)
                _removed(this, arg);
        }

        protected void OnChanged(params HealthCheckEventArgs[] args)
        {
            if (_changed == null)
                return;

            foreach (var arg in args)
                _changed(this, arg);
        }

        #endregion Implementation of IHealthCheckService

        #region Implementation of IDisposable

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public void Dispose()
        {
            _timer.Dispose();
        }

        #endregion Implementation of IDisposable

        #region Private Method

        private void Remove(IEnumerable<AddressModel> addressModels)
        {
            foreach (var addressModel in addressModels)
            {
                MonitorEntry value;
                var ipAddress = addressModel as IpAddressModel;
                _dictionary.TryRemove(new Tuple<string, int>(ipAddress.Ip, ipAddress.Port), out value);
            }
        }

        private void RemoveUnhealthyAddress(IEnumerable<MonitorEntry> monitorEntry)
        {
            if (monitorEntry.Any())
            {
                var addresses = monitorEntry.Select(p =>
                {
                    var ipEndPoint = p.EndPoint as IPEndPoint;
                    return new IpAddressModel(ipEndPoint.Address.ToString(), ipEndPoint.Port);
                }).ToList();
                _serviceRouteManager.RemveAddressAsync(addresses).Wait();
                addresses.ForEach(p => {
                    var ipAddress = p as IpAddressModel;
                    _dictionary.TryRemove(new Tuple<string, int>(ipAddress.Ip, ipAddress.Port), out MonitorEntry value);
                });
                OnRemoved(addresses.Select(p => new HealthCheckEventArgs(p)).ToArray());
            }
        }


        private void RemoveUnhealthyAddress(MonitorEntry monitorEntry)
        {
            var ipEndPoint = monitorEntry.EndPoint as IPEndPoint;
            var address = new IpAddressModel(ipEndPoint.Address.ToString(), ipEndPoint.Port);
            _serviceRouteManager.RemveAddressAsync(new List<AddressModel>() { address }).Wait();
            _dictionary.TryRemove(new Tuple<string, int>(address.Ip, address.Port), out MonitorEntry value);
            OnRemoved(new HealthCheckEventArgs(address));
        }

        private static async Task<bool> Check(string ip, int timeout)
        {
            bool isHealth = false;
            try
            {
                var ping = new Ping();
                var pingReply = ping.Send(ip, timeout);
                isHealth = pingReply.Status == IPStatus.Success;
                return isHealth;
            }
            catch
            {
                _logger.LogDebug($"地址为{ip}的主机当前无法连接");
            }
            return isHealth;

        }

        private static async Task<bool> Check(IPAddress ip, int timeout)
        {
            bool isHealth = false;
            try
            {
                var ping = new Ping();
                var pingReply = ping.Send(ip, timeout);
                isHealth = pingReply.Status == IPStatus.Success;
                return isHealth;
            }
            catch
            {
                _logger.LogDebug($"地址为{ip}的主机当前无法连接");
            }
            return isHealth;

        }

        private static async Task Check(MonitorEntry entry, int timeout)
        {
            var ipEndPoint = entry.EndPoint as IPEndPoint;
            var isHealth = await Check(ipEndPoint.Address, timeout);
            if (isHealth)
            {
                using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) { SendTimeout = timeout })
                {
                    try
                    {
                        await socket.ConnectAsync(entry.EndPoint);
                        entry.UnhealthyTimes = 0;
                        entry.Health = true;
                    }
                    catch
                    {
                        entry.UnhealthyTimes++;
                        entry.Health = false;
                    }
                }
            }
            else 
            {
                entry.UnhealthyTimes++;
                entry.Health = false;
            }
            
        }

        private static async Task Check(IEnumerable<MonitorEntry> entrys, int timeout)
        {
            foreach (var entry in entrys)
            {
                var ipEndPoint = entry.EndPoint as IPEndPoint;
                var isHealth = await Check(ipEndPoint.Address, timeout);
                if (isHealth)
                {
                    using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) { SendTimeout = timeout })
                    {
                        try
                        {
                            await socket.ConnectAsync(entry.EndPoint);
                            entry.UnhealthyTimes = 0;
                            entry.Health = true;
                        }
                        catch
                        {
                            entry.UnhealthyTimes++;
                            entry.Health = false;
                        }
                    }
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
            public MonitorEntry(AddressModel addressModel, bool health)
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