using org.apache.zookeeper;
using Rabbit.Zookeeper;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Surging.Core.Zookeeper.WatcherProvider
{
    internal class ChildrenMonitorWatcher : WatcherBase
    {
        private readonly IZookeeperClient _zookeeperClient;
        private readonly Action<string[], string[]> _action;
        private string[] _currentData = new string[0];

        public ChildrenMonitorWatcher(IZookeeperClient zookeeperClient, string path, Action<string[], string[]> action)
                : base(path)
        {
            _zookeeperClient = zookeeperClient;
            _action = action;
        }

        public ChildrenMonitorWatcher SetCurrentData(string[] currentData)
        {
            _currentData = currentData ?? new string[0];

            return this;
        }

        #region Overrides of WatcherBase

        protected override async Task ProcessImpl(WatchedEvent watchedEvent)
        {
            var path = Path;
 
            Func<ChildrenMonitorWatcher> getWatcher = () => new ChildrenMonitorWatcher(_zookeeperClient, path, _action);
            switch (watchedEvent.get_Type())
            {
                //创建之后开始监视下面的子节点情况。
                case Event.EventType.NodeCreated:
                    if (await _zookeeperClient.StrictExistsAsync(path)) 
                    {
                        await _zookeeperClient.ZooKeeper.getChildrenAsync(path, getWatcher());
                    }
                    break;

                //子节点修改则继续监控子节点信息并通知客户端数据变更。
                case Event.EventType.NodeChildrenChanged:
                    if (await _zookeeperClient.StrictExistsAsync(path))
                    {
                        var watcher = getWatcher();
                        var result = await _zookeeperClient.ZooKeeper.getChildrenAsync(path, watcher);
                        var childrens = result.Children.ToArray();
                        _action(_currentData, childrens);
                        watcher.SetCurrentData(childrens);
                    }
                    else 
                    {
                        _action(_currentData, new string[0]);
                    }

                    break;

                //删除之后开始监控自身节点，并通知客户端数据被清空。
                case Event.EventType.NodeDeleted:
                    {
                        var watcher = getWatcher();
                        if (await _zookeeperClient.StrictExistsAsync(path)) 
                        {
                            _action(_currentData, new string[0]);
                            watcher.SetCurrentData(new string[0]);
                        }
                       
                    }
                    break;
            }
        }
        #endregion Overrides of WatcherBase
    }
}
