using org.apache.zookeeper;
using Rabbit.Zookeeper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Surging.Core.Zookeeper.WatcherProvider
{
    internal class NodeMonitorWatcher : WatcherBase
    {
        private readonly Action<byte[], byte[]> _action;
        private byte[] _currentData;


        public NodeMonitorWatcher(string path, Action<byte[], byte[]> action) : base (path)
        {
            _action = action;
            _currentData = new byte[0];
        }

        //public void SetCurrentData(byte[] currentData)
        //{
        //    _currentData = currentData;
        //}

        internal async Task HandleNodeDataChange(IZookeeperClient client, NodeDataChangeArgs args)
        {
            Watcher.Event.EventType eventType = args.Type;
            switch (eventType)
            {
                case Watcher.Event.EventType.NodeCreated:
                    _action(new byte[0], args.CurrentData.ToArray());
                    _currentData = args.CurrentData.ToArray();
                    break;

                case Watcher.Event.EventType.NodeDataChanged:
                    _action(_currentData, args.CurrentData.ToArray());
                    _currentData = args.CurrentData.ToArray();
                    break;
            }
       
        }

    }
}