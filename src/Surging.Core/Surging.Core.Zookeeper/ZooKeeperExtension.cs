using org.apache.zookeeper;
using System.Threading.Tasks;
using static org.apache.zookeeper.KeeperException;

namespace Surging.Core.Zookeeper
{
    public static class ZooKeeperExtension
    {
        public static async Task<bool> Exists(this ZooKeeper zooKeeper,string nodePath) 
        {
            try
            {
                if (await zooKeeper.existsAsync(nodePath) != null)
                {
                    return true;
                }
                return false;
            }
            catch (NoNodeException ex) 
            {
                return false;
            }
            
        }

        public static async Task<bool> Exists(this ZooKeeper zooKeeper, string nodePath, Watcher watcher)
        {
            try
            {
                if (await zooKeeper.existsAsync(nodePath, watcher) != null)
                {
                    return true;
                }
                return false;
            }
            catch (NoNodeException ex)
            {
                return false;
            }

        }

        public static async Task<bool> Exists(this ZooKeeper zooKeeper, string nodePath, bool isWatcher)
        {
            try
            {
                if (await zooKeeper.existsAsync(nodePath, isWatcher) != null)
                {
                    return true;
                }
                return false;
            }
            catch (NoNodeException ex)
            {
                return false;
            }

        }
    }
}
